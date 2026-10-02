using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private sealed class AuditBody
        {
            internal string name;
            internal Body2 body;
            internal double[] bounds;
        }

        private static double[] ExactBounds(Body2 body)
        {
            var box = new double[6];
            for (int axis = 0; axis < 3; axis++)
            {
                double x, y, z;
                ParametricContext.Require(body.GetExtremePoint(axis == 0 ? -1 : 0, axis == 1 ? -1 : 0, axis == 2 ? -1 : 0, out x, out y, out z), "BODY_EXTREME_POINT_UNAVAILABLE");
                box[axis] = new[] { x,y,z }[axis] * 1000;
                ParametricContext.Require(body.GetExtremePoint(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0, out x, out y, out z), "BODY_EXTREME_POINT_UNAVAILABLE");
                box[axis+3] = new[] { x,y,z }[axis] * 1000;
            }
            return box;
        }

        private static void PhysicalAudit(ISldWorks sw, List<object> records)
        {
            string folder = Path.Combine(ParametricContext.AttemptDirectory, "package");
            string root = Directory.GetFiles(folder, "16029_*.SLDASM").Single();
            var hashes = Directory.GetFiles(folder).ToDictionary(path => path, ParametricContext.Hash);
            var bodies = new List<AuditBody>();
            ModelDoc2 model = Open(sw, root, true);
            try
            {
                var assembly = (AssemblyDoc)model;
                assembly.ResolveAllLightWeightComponents(false);
                ParametricContext.Require(model.ForceRebuild3(false), "AUDIT_ROOT_REBUILD_FAILED");
                foreach (Component2 component in (Array)assembly.GetComponents(false))
                {
                    if (component.IsSuppressed() || component.IsHidden(true)) continue;
                    object info;
                    Array sourceBodies = component.GetBodies3(0, out info) as Array;
                    if (sourceBodies == null) continue;
                    foreach (Body2 source in sourceBodies)
                    {
                        Body2 body = source.Copy() as Body2;
                        ParametricContext.Require(body != null && body.ApplyTransform(component.GetTotalTransform(true)), "AUDIT_BODY_TRANSFORM_FAILED");
                        bodies.Add(new AuditBody { name = component.Name2, body = body, bounds = ExactBounds(body) });
                    }
                }
                Modeler modeler = sw.GetModeler() as Modeler;
                records.Add(new { inspection = "native_world_body_bounds", bodies = bodies.Select(body => new { component = body.name, boxMm = body.bounds }).ToArray() });
                var collisions = new List<object>();
                int checkedPairs = 0;
                for (int i = 0; i < bodies.Count; i++) for (int j = i + 1; j < bodies.Count; j++)
                {
                    var a = bodies[i]; var b = bodies[j];
                    if (a.name == b.name) continue;
                    if (Enumerable.Range(0,3).Any(axis => Math.Min(a.bounds[axis+3], b.bounds[axis+3]) - Math.Max(a.bounds[axis], b.bounds[axis]) < .001)) continue;
                    checkedPairs++;
                    object facesA, facesB, intersections;
                    bool overlaps = modeler.CheckInterferenceBetweenTwoBodies(a.body, b.body, false, out facesA, out facesB, out intersections);
                    Array intersectionsArray = intersections as Array;
                    if (!overlaps && (intersectionsArray == null || intersectionsArray.Length == 0)) continue;
                    double volume = 0;
                    var intersectionBounds = new List<double[]>();
                    if (intersectionsArray != null) foreach (Body2 intersection in intersectionsArray)
                    {
                        double[] mass = intersection.GetMassProperties(1.0) as double[];
                        if (mass != null && mass.Length > 3) volume += mass[3] * 1e9;
                        intersectionBounds.Add(ExactBounds(intersection));
                        Marshal.ReleaseComObject(intersection);
                    }
                    collisions.Add(new { componentA = a.name, componentB = b.name, volumeMm3 = volume, interference = overlaps, intersectionBoundsMm = intersectionBounds });
                }
                records.Add(new { inspection = "native_exact_solid_interference", bodyCount = bodies.Count, checkedPairs = checkedPairs, collisions = collisions });
                Console.WriteLine("PHYSICAL_INTERFERENCE_MEASURED pairs=" + checkedPairs + " contacts=" + collisions.Count);
            }
            finally
            {
                foreach (var body in bodies) Marshal.ReleaseComObject(body.body);
                sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model);
            }
            foreach (string path in hashes.Keys.Where(path=>Path.GetExtension(path).Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase)&&!Path.GetFileName(path).StartsWith("~$",StringComparison.Ordinal)))
            {
                model = Open(sw, path, true);
                Feature flat = null;
                try
                {
                    Feature feature = model.FirstFeature() as Feature;
                    while (feature != null) { if (feature.GetTypeName2() == "FlatPattern") { flat = feature; break; } feature = feature.GetNextFeature() as Feature; }
                    if (flat == null) continue;
                    bool priorSuppressed = flat.IsSuppressed();
                    bool changed = !priorSuppressed || flat.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    bool rebuilt = changed && model.ForceRebuild3(false);
                    var issues = new List<object>();
                    feature = model.FirstFeature() as Feature;
                    while (feature != null) { if (!feature.IsSuppressed() && feature.GetErrorCode() > 0) issues.Add(new { feature = feature.Name, code = feature.GetErrorCode() }); feature = feature.GetNextFeature() as Feature; }
                    double[] box = rebuilt ? BodyBox(model) : null;
                    records.Add(new { inspection = "native_flat_pattern", file = Path.GetFileName(path), unsuppressed = changed, rebuilt = rebuilt, flatBoxMm = box, issues = issues });
                    Console.WriteLine("FLAT_PATTERN_MEASURED " + Path.GetFileName(path) + " rebuilt=" + rebuilt + " issues=" + issues.Count);
                    if (priorSuppressed) { flat.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null); model.ForceRebuild3(false); }
                }
                finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            }
            foreach (var hash in hashes) ParametricContext.Require(ParametricContext.Hash(hash.Key) == hash.Value, "AUDIT_MODIFIED_PACKAGE_FILE");
        }
    }
}
