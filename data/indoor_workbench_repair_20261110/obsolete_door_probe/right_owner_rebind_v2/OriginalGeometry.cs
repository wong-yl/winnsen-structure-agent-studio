using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
namespace Winnsen.StructureAgent.Native888 { internal static partial class NativeParametricLocks {
        private static readonly SlotTemplate[] SlotTemplates =
        {
            T("E01", 0.788322, 61.7, -5.708124, -19.3, 62.2, -5.639994, -19.8),
            T("E02", 0.788322, 62.2, 5.639994, -19.8, 61.7, 5.708124, -19.3),
            T("E03", 0.8, 61.7, -5.708124, -19.3, 61.7, -5.708124, -18.5),
            T("E04", 0.8, 62.2, -5.639994, -19.8, 63, -5.639994, -19.8),
            T("E05", 0.8, 62.2, -4.497259, -41.604672, 63, -4.497259, -41.604672),
            T("E06", 0.8, 62.2, -2.5, -43.5, 63, -2.5, -43.5),
            T("E07", 0.8, 62.2, 2.5, -43.5, 63, 2.5, -43.5),
            T("E08", 0.8, 62.2, 4.497259, -41.604672, 63, 4.497259, -41.604672),
            T("E09", 0.8, 62.2, 5.639994, -19.8, 63, 5.639994, -19.8),
            T("E10", 0.8, 61.7, 5.708124, -19.3, 61.7, 5.708124, -18.5),
            T("E11", 2.043171, 61.7, -5.708124, -18.5, 63, -5.639994, -19.8),
            T("E12", 2.043171, 63, 5.639994, -19.8, 61.7, 5.708124, -18.5),
            T("E13", 3.036873, 62.2, -2.5, -43.5, 62.2, -4.497259, -41.604672),
            T("E14", 3.036873, 63, -2.5, -43.5, 63, -4.497259, -41.604672),
            T("E15", 3.036873, 62.2, 4.497259, -41.604672, 62.2, 2.5, -43.5),
            T("E16", 3.036873, 63, 4.497259, -41.604672, 63, 2.5, -43.5),
            T("E17", 5, 63, 2.5, -43.5, 63, -2.5, -43.5),
            T("E18", 5, 62.2, 2.5, -43.5, 62.2, -2.5, -43.5),
            T("E19", 21.834595, 63, -4.497259, -41.604672, 63, -5.639994, -19.8),
            T("E20", 21.834595, 62.2, -4.497259, -41.604672, 62.2, -5.639994, -19.8),
            T("E21", 21.834595, 62.2, 5.639994, -19.8, 62.2, 4.497259, -41.604672),
            T("E22", 21.834595, 63, 5.639994, -19.8, 63, 4.497259, -41.604672)
        };
        private static double[] ExpectedCircles { get { return ExpectedSlots.Select(value => value - 60.0).ToArray(); } }
        private static double[] ExpectedSlots { get { return ActiveRows; } }
        private const double CircleLengthMm = Math.PI * 5.0;
        private const double LengthToleranceMm = 0.0005;
        private const double CoordinateToleranceMm = 0.01;
        private const double RowToleranceMm = 0.01;
        private static GeometrySnapshot CaptureGeometry(ModelDoc2 model, string side)
        {
            var snapshot = new GeometrySnapshot { side = side };
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int guard = 0;
            while (feature != null && guard++ < 5000)
            {
                CaptureFeatureHealth(feature, snapshot, 0);
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            if (feature != null)
            {
                snapshot.traversal_complete = false;
                Release(feature);
            }

            PartDoc part = model as PartDoc;
            Array bodies = part == null ? null : Safe(delegate
            {
                return part.GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
            }, null);
            if (bodies == null) return snapshot;
            snapshot.body_count = bodies.Length;
            foreach (object bodyItem in bodies)
            {
                Body2 body = bodyItem as Body2;
                Array edges = body == null ? null : Safe(delegate { return body.GetEdges() as Array; }, null);
                if (edges != null)
                {
                    foreach (object edgeItem in edges)
                    {
                        Edge edge = edgeItem as Edge;
                        if (edge == null) continue;
                        var row = new EdgeRow { index = snapshot.edges.Count + 1 };
                        Curve curve = Safe(delegate { return edge.GetCurve() as Curve; }, null);
                        CurveParamData parameter = Safe(delegate
                        {
                            return edge.GetCurveParams3() as CurveParamData;
                        }, null);
                        if (parameter != null)
                        {
                            row.length_mm = curve == null ? 0.0 : Safe(delegate
                            {
                                return curve.GetLength3(parameter.UMinValue, parameter.UMaxValue);
                            }, 0.0) * 1000.0;
                            row.param_start_mm = ToPointMm(Safe(delegate { return parameter.StartPoint; }, null));
                        }
                        row.vertex_start_mm = VertexMm(Safe(delegate { return edge.GetStartVertex() as Vertex; }, null));
                        row.vertex_end_mm = VertexMm(Safe(delegate { return edge.GetEndVertex() as Vertex; }, null));
                        snapshot.edges.Add(row);
                        Release(parameter);
                        Release(curve);
                        Release(edge);
                    }
                }
                Release(body);
            }
            snapshot.edge_count = snapshot.edges.Count;
            AnalyzeGeometry(snapshot);
            return snapshot;
        }
        private static void CaptureFeatureHealth(Feature feature, GeometrySnapshot snapshot, int depth)
        {
            if (feature == null) return;
            if (depth > 30 || snapshot.feature_count > 20000)
            {
                snapshot.traversal_complete = false;
                return;
            }
            string type = Safe(delegate { return feature.GetTypeName2(); }, "");
            string name = Safe(delegate { return feature.Name; }, "");
            snapshot.feature_types.Add(type);
            if (string.Equals(type, "SheetMetal", StringComparison.OrdinalIgnoreCase)) snapshot.has_sheet_metal = true;
            if (string.Equals(type, "FlatPattern", StringComparison.OrdinalIgnoreCase)) snapshot.has_flat_pattern = true;
            if (string.Equals(type, "MirrorStock", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "MirrorPart", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "BrokenDerivedPartFolder", StringComparison.OrdinalIgnoreCase))
                snapshot.mirror_feature_count++;
            if (string.Equals(type, "BaseBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Imported", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ImportedBody", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ForeignBody", StringComparison.OrdinalIgnoreCase))
                snapshot.forbidden_import_feature_count++;
            bool warning = false;
            int error2 = -1;
            try { error2 = feature.GetErrorCode2(out warning); } catch { }
            int error = Safe(delegate { return feature.GetErrorCode(); }, -1);
            bool suppressed = Safe(delegate { return feature.IsSuppressed(); }, false);
            snapshot.feature_count++;
            if (!suppressed && (error > 0 || error2 > 0)) snapshot.error_feature_count++;
            if (!suppressed && warning) snapshot.warning_feature_count++;
            Feature child = Safe(delegate { return feature.GetFirstSubFeature() as Feature; }, null);
            int guard = 0;
            while (child != null && guard++ < 3000)
            {
                CaptureFeatureHealth(child, snapshot, depth + 1);
                Feature next = Safe(delegate { return child.GetNextSubFeature() as Feature; }, null);
                Release(child);
                child = next;
            }
            if (child != null)
            {
                snapshot.traversal_complete = false;
                Release(child);
            }
        }
        private static void AnalyzeGeometry(GeometrySnapshot snapshot)
        {
            List<double> candidates = new List<double>();
            foreach (EdgeRow edge in snapshot.edges)
            {
                foreach (SlotTemplate template in SlotTemplates.Where(row => !Close(row.length, 0.8, 1e-9)))
                    candidates.AddRange(DerivedRows(edge, template));
            }
            foreach (double row in DistinctRows(candidates))
            {
                SlotAnalysis analysis = AnalyzeSlot(snapshot.edges, row, snapshot.side);
                if (analysis.matched_edge_count > 0) snapshot.slot_analyses.Add(analysis);
            }
            snapshot.slot_rows_mm = snapshot.slot_analyses.Where(row => row.full_fingerprint)
                .Select(row => row.row_mm).OrderBy(value => value).ToArray();
            snapshot.slot_fragment_count = snapshot.slot_analyses.Count(row => !row.full_fingerprint);

            List<double> circleCandidates = new List<double>();
            foreach (EdgeRow edge in snapshot.edges)
            {
                if (!Close(edge.length_mm, CircleLengthMm, LengthToleranceMm) || edge.param_start_mm.Length < 3) continue;
                double x = Math.Abs(edge.param_start_mm[0]);
                double z = edge.param_start_mm[2];
                if ((Close(x, 42.5, CoordinateToleranceMm) && Close(z, -19.3, CoordinateToleranceMm)) ||
                    (Close(x, 47.5, CoordinateToleranceMm) && Close(z, -18.5, CoordinateToleranceMm)))
                    circleCandidates.Add(edge.param_start_mm[1]);
            }
            foreach (double row in DistinctRows(circleCandidates))
            {
                CircleAnalysis analysis = AnalyzeCircle(snapshot.edges, row, snapshot.side);
                if (analysis.matched_edge_count > 0) snapshot.circle_analyses.Add(analysis);
            }
            snapshot.circle_rows_mm = snapshot.circle_analyses.Where(row => row.full_diameter5_pair)
                .Select(row => row.row_mm).OrderBy(value => value).ToArray();
            snapshot.circle_fragment_count = snapshot.circle_analyses.Count(row => !row.full_diameter5_pair);
            snapshot.unique_slot_profile_signature_count = snapshot.slot_analyses
                .Where(row => row.full_fingerprint).Select(row => row.normalized_signature_sha256)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
        private static SlotAnalysis AnalyzeSlot(List<EdgeRow> edges, double row, string side)
        {
            var analysis = new SlotAnalysis { row_mm = Round(row, 9) };
            var used = new HashSet<int>();
            var signature = new List<string>();
            int expectedSign = side == "L" ? -1 : 1;
            foreach (SlotTemplate template in SlotTemplates)
            {
                List<EdgeRow> matches = edges.Where(candidate => EdgeMatches(candidate, template, row)).ToList();
                if (matches.Count != 1)
                {
                    analysis.missing_or_duplicate_template_ids.Add(template.id);
                    continue;
                }
                EdgeRow edge = matches[0];
                if (!used.Add(edge.index)) analysis.ambiguous_edge_indexes.Add(edge.index);
                analysis.matched_edge_indexes.Add(edge.index);
                bool orientation = edge.vertex_start_mm.Length >= 3 && edge.vertex_end_mm.Length >= 3 &&
                    Math.Sign(edge.vertex_start_mm[0]) == expectedSign &&
                    Math.Sign(edge.vertex_end_mm[0]) == expectedSign;
                if (!orientation) analysis.side_orientation_exact = false;
                signature.Add(NormalizedEdgeSignature(template.id, edge, row));
            }
            analysis.matched_edge_count = used.Count;
            analysis.full_fingerprint = used.Count == 22 &&
                analysis.missing_or_duplicate_template_ids.Count == 0 &&
                analysis.ambiguous_edge_indexes.Count == 0 && analysis.side_orientation_exact;
            if (analysis.full_fingerprint)
                analysis.normalized_signature_sha256 = Sha256Text(string.Join("\n", signature));
            return analysis;
        }
        private static CircleAnalysis AnalyzeCircle(List<EdgeRow> edges, double row, string side)
        {
            var analysis = new CircleAnalysis { row_mm = Round(row, 9) };
            int sign = side == "L" ? -1 : 1;
            EdgeRow c1 = edges.SingleOrDefault(edge => CircleMatches(edge, row, 42.5, -19.3));
            EdgeRow c2 = edges.SingleOrDefault(edge => CircleMatches(edge, row, 47.5, -18.5));
            foreach (EdgeRow edge in new[] { c1, c2 }.Where(value => value != null))
            {
                analysis.matched_edge_indexes.Add(edge.index);
                if (edge.param_start_mm.Length < 3 || Math.Sign(edge.param_start_mm[0]) != sign)
                    analysis.side_orientation_exact = false;
            }
            analysis.matched_edge_count = analysis.matched_edge_indexes.Distinct().Count();
            analysis.full_diameter5_pair = c1 != null && c2 != null &&
                analysis.matched_edge_count == 2 && analysis.side_orientation_exact;
            return analysis;
        }
        private static bool GeometryGate(GeometrySnapshot value, string side)
        {
            return BasicPartGate(value) && value.side == side &&
                RowsEqual(value.slot_rows_mm, ExpectedSlots, RowToleranceMm) &&
                RowsEqual(value.circle_rows_mm, ExpectedCircles, RowToleranceMm) &&
                value.slot_fragment_count == 0 && value.circle_fragment_count == 0 &&
                value.slot_analyses.Count(row => row.full_fingerprint) == ExpectedSlots.Length &&
                value.circle_analyses.Count(row => row.full_diameter5_pair) == ExpectedSlots.Length &&
                value.slot_analyses.Where(row => row.full_fingerprint).All(row => row.side_orientation_exact) &&
                value.circle_analyses.Where(row => row.full_diameter5_pair).All(row => row.side_orientation_exact) &&
                value.unique_slot_profile_signature_count == 1 &&
                RowsEqual(value.slot_rows_mm.Select(row => row - 60.0).ToArray(),
                    value.circle_rows_mm, RowToleranceMm);
        }
        private static bool BasicPartGate(GeometrySnapshot value)
        {
            return value != null && value.body_count == 1 && value.feature_count > 0 &&
                value.error_feature_count == 0 && value.warning_feature_count == 0 &&
                value.has_sheet_metal && value.has_flat_pattern && value.edge_count == value.edges.Count &&
                value.edge_count > 0 && value.traversal_complete &&
                value.forbidden_import_feature_count == 0;
        }
        private static bool MirrorGeometryGate(GeometrySnapshot value, string side)
        {
            return GeometryGate(value, side) && value.mirror_feature_count >= 1 &&
                value.forbidden_import_feature_count == 0;
        }
        private static bool EdgeMatches(EdgeRow edge, SlotTemplate template, double row)
        {
            if (!Close(edge.length_mm, template.length, LengthToleranceMm) ||
                edge.vertex_start_mm.Length < 3 || edge.vertex_end_mm.Length < 3) return false;
            double[] start = NormalizeVertex(edge.vertex_start_mm, row);
            double[] end = NormalizeVertex(edge.vertex_end_mm, row);
            return (PointMatches(start, template.a) && PointMatches(end, template.b)) ||
                (PointMatches(start, template.b) && PointMatches(end, template.a));
        }
        private static IEnumerable<double> DerivedRows(EdgeRow edge, SlotTemplate template)
        {
            var output = new List<double>();
            if (!Close(edge.length_mm, template.length, LengthToleranceMm) ||
                edge.vertex_start_mm.Length < 3 || edge.vertex_end_mm.Length < 3) return output;
            double[] start = NormalizeVertex(edge.vertex_start_mm, 0);
            double[] end = NormalizeVertex(edge.vertex_end_mm, 0);
            foreach (var pair in new[] { new[] { template.a, template.b }, new[] { template.b, template.a } })
            {
                if (!Close(start[0], pair[0][0], CoordinateToleranceMm) ||
                    !Close(start[2], pair[0][2], CoordinateToleranceMm) ||
                    !Close(end[0], pair[1][0], CoordinateToleranceMm) ||
                    !Close(end[2], pair[1][2], CoordinateToleranceMm)) continue;
                double r1 = start[1] - pair[0][1];
                double r2 = end[1] - pair[1][1];
                if (Close(r1, r2, CoordinateToleranceMm)) output.Add((r1 + r2) / 2.0);
            }
            return output;
        }
        private static bool CircleMatches(EdgeRow edge, double row, double absX, double z)
        {
            return Close(edge.length_mm, CircleLengthMm, LengthToleranceMm) &&
                edge.param_start_mm.Length >= 3 &&
                Close(Math.Abs(edge.param_start_mm[0]), absX, CoordinateToleranceMm) &&
                Close(edge.param_start_mm[1], row, CoordinateToleranceMm) &&
                Close(edge.param_start_mm[2], z, CoordinateToleranceMm);
        }
        private static string NormalizedEdgeSignature(string id, EdgeRow edge, double row)
        {
            string start = PointSignature(NormalizeVertex(edge.vertex_start_mm, row));
            string end = PointSignature(NormalizeVertex(edge.vertex_end_mm, row));
            if (string.CompareOrdinal(start, end) > 0) { string temp = start; start = end; end = temp; }
            return id + "|" + Round(edge.length_mm, 6).ToString("0.######", CultureInfo.InvariantCulture) +
                "|" + start + "|" + end;
        }
        private static string PointSignature(double[] point)
        {
            return string.Join(",", point.Select(value => Round(value, 6)
                .ToString("0.######", CultureInfo.InvariantCulture)));
        }
        private static double[] NormalizeVertex(double[] point, double row)
        {
            return new[] { Math.Abs(point[0]), point[1] - row, point[2] };
        }
        private static bool PointMatches(double[] actual, double[] expected)
        {
            return actual.Length >= 3 && expected.Length >= 3 &&
                Close(actual[0], expected[0], CoordinateToleranceMm) &&
                Close(actual[1], expected[1], CoordinateToleranceMm) &&
                Close(actual[2], expected[2], CoordinateToleranceMm);
        }
        private static List<double> DistinctRows(IEnumerable<double> values)
        {
            var output = new List<double>();
            foreach (double value in values.Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .OrderBy(value => value))
                if (output.Count == 0 || !Close(output[output.Count - 1], value, RowToleranceMm))
                    output.Add(Round(value, 9));
            return output;
        }
        private static double[] VertexMm(Vertex vertex)
        {
            try { return ToPointMm(vertex == null ? null : Safe(delegate { return vertex.GetPoint(); }, null)); }
            finally { Release(vertex); }
        }
        private static double[] ToPointMm(object raw)
        {
            double[] values = ToNumbers(raw);
            for (int index = 0; index < Math.Min(3, values.Length); index++) values[index] *= 1000.0;
            return values;
        }
        private static double[] ToNumbers(object raw)
        {
            double[] direct = raw as double[];
            if (direct != null) return (double[])direct.Clone();
            Array array = raw as Array;
            if (array == null) return new double[0];
            var values = new double[array.Length];
            for (int index = 0; index < array.Length; index++)
                values[index] = Convert.ToDouble(array.GetValue(index), CultureInfo.InvariantCulture);
            return values;
        }
        private static bool RowsEqual(double[] actual, double[] expected, double tolerance)
        {
            return actual != null && expected != null && actual.Length == expected.Length &&
                actual.Zip(expected, (left, right) => Close(left, right, tolerance)).All(value => value);
        }
        private static SlotTemplate T(string id, double length,
            double ax, double ay, double az, double bx, double by, double bz)
        {
            return new SlotTemplate { id = id, length = length,
                a = new[] { ax, ay, az }, b = new[] { bx, by, bz } };
        }
        private static double Round(double value, int digits)
        {
            return Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }
        private static bool Close(double left, double right, double tolerance)
        {
            return Math.Abs(left - right) <= tolerance;
        }
        private static string Sha256Text(string value)
        {
            using (SHA256 hash = SHA256.Create())
                return BytesToHex(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }
        private static string Sha256Bytes(byte[] value)
        {
            using (SHA256 hash = SHA256.Create())
                return BytesToHex(hash.ComputeHash(value ?? new byte[0]));
        }
        private static string BytesToHex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
        private static T Safe<T>(Func<T> action, T fallback)
        {
            try { return action(); }
            catch { return fallback; }
        }
        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                try { Marshal.FinalReleaseComObject(value); } catch { }
        }
        private static bool SelectRightPlane(ModelDoc2 model)
        {
            foreach (string name in new[] { "右视", "Right Plane", "Right" })
            {
                bool selected = Safe(delegate
                {
                    return model.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0);
                }, false);
                if (selected) return true;
            }
            return false;
        }
        public sealed class SlotTemplate
        {
            public string id = "";
            public double length;
            public double[] a = new double[0];
            public double[] b = new double[0];
        }
        public sealed class EdgeRow
        {
            public int index;
            public double length_mm;
            public double[] vertex_start_mm = new double[0];
            public double[] vertex_end_mm = new double[0];
            public double[] param_start_mm = new double[0];
        }
        public sealed class SlotAnalysis
        {
            public double row_mm;
            public int matched_edge_count;
            public bool side_orientation_exact = true;
            public bool full_fingerprint;
            public string normalized_signature_sha256 = "";
            public readonly List<int> matched_edge_indexes = new List<int>();
            public readonly List<int> ambiguous_edge_indexes = new List<int>();
            public readonly List<string> missing_or_duplicate_template_ids = new List<string>();
        }
        public sealed class CircleAnalysis
        {
            public double row_mm;
            public int matched_edge_count;
            public bool side_orientation_exact = true;
            public bool full_diameter5_pair;
            public readonly List<int> matched_edge_indexes = new List<int>();
        }
        public sealed class GeometrySnapshot
        {
            public string side = "";
            public int body_count;
            public int feature_count;
            public int error_feature_count;
            public int warning_feature_count;
            public bool has_sheet_metal;
            public bool has_flat_pattern;
            public bool traversal_complete = true;
            public int mirror_feature_count;
            public int forbidden_import_feature_count;
            public int edge_count;
            public double[] slot_rows_mm = new double[0];
            public double[] circle_rows_mm = new double[0];
            public int slot_fragment_count;
            public int circle_fragment_count;
            public int unique_slot_profile_signature_count;
            public bool read_only_reopen;
            public int open_errors;
            public int open_warnings;
            public int session_process_id;
            public string source_sha256 = "";
            public readonly List<string> feature_types = new List<string>();
            public readonly List<EdgeRow> edges = new List<EdgeRow>();
            public readonly List<SlotAnalysis> slot_analyses = new List<SlotAnalysis>();
            public readonly List<CircleAnalysis> circle_analyses = new List<CircleAnalysis>();
        }

        private static double[] ActiveRows;
        internal static GeometrySnapshot ResearchCapture(ModelDoc2 model, string side, double[] rows) { ActiveRows = rows; return CaptureGeometry(model, side); }
        internal static bool ResearchGeometryGate(GeometrySnapshot value, string side, double[] rows) { ActiveRows = rows; return GeometryGate(value, side); }
        internal static bool ResearchMirrorGeometryGate(GeometrySnapshot value, double[] rows) { ActiveRows = rows; return MirrorGeometryGate(value, "R"); }
        internal static bool ResearchSelectRightPlane(ModelDoc2 model) { return SelectRightPlane(model); }

} }
namespace Winnsen.StructureAgent.NativeDoorModule888x14 { internal static class NativeParametricDoor {
        internal static void VerifyReflectedVertices(ModelDoc2 left, ModelDoc2 right)
        {
            Func<ModelDoc2, List<double[]>> vertices = delegate(ModelDoc2 model)
            {
                var result = new List<double[]>();
                foreach (Body2 body in (Array)((PartDoc)model).GetBodies2(0, false))
                    foreach (Vertex vertex in (Array)body.GetVertices()) result.Add((double[])vertex.GetPoint());
                return result;
            };
            var original = vertices(left); var mirrored = vertices(right);
            Require(original.Count == mirrored.Count && original.Count > 0, "MIRROR_VERTEX_COUNT_MISMATCH", 21, "native mirror vertex counts differ");
            foreach (double[] point in original)
                Require(mirrored.Any(candidate => Math.Abs(candidate[0] + point[0]) < .000002 && Math.Abs(candidate[1] - point[1]) < .000002 && Math.Abs(candidate[2] - point[2]) < .000002), "MIRROR_VERTEX_POSITION_MISMATCH", 21, "right geometry is not an exact X reflection of the native left panel");
            Console.WriteLine("MIRROR_VERTICES_VERIFIED " + original.Count);
        }

        private static void Require(bool condition, string code, int exitCode, string message) { if (!condition) throw new InvalidOperationException(code + " " + message); }
} }
