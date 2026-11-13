using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Winnsen.StructureAgent.NativeDoorModule888x14;
using Winnsen.StructureAgent.Native888;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricWidth
    {
        internal static Action<ISldWorks> RepairMaintenanceStrip(ISldWorks sw, string path, List<object> records)
        {
            double length = ParametricContext.HeightMm - 92;
            PartSnapshot baseline = null;
            Func<ModelDoc2, string, PartSnapshot> inspect = delegate(ModelDoc2 part, string stage)
            {
                bool rebuilt = part.ForceRebuild3(false);
                PartSnapshot snapshot = CapturePartSnapshot(part);
                double[] box = NativeParametricAssembly.BodyBox(part);
                var dimensions = new List<object>();
                foreach (var target in new[] {
                    new { name = "D1@草图1", value = 68.0 }, new { name = "D2@草图1", value = length },
                    new { name = "D1@凸台-拉伸1", value = 16.0 }, new { name = "D1@抽壳1", value = .8 },
                    new { name = "厚度@钣金1", value = .8 }, new { name = "D1@钣金1", value = .1 },
                    new { name = "D6@边线-法兰1", value = 2.0 }, new { name = "D7@边线-法兰1", value = 10.0 },
                    new { name = "D1@草图10", value = 40.0 }, new { name = "D1@圆角1", value = 3.0 },
                    new { name = "D1@草图11", value = 15.0 }, new { name = "D2@草图11", value = 2.0 },
                    new { name = "D3@草图11", value = 114.0 }, new { name = "D1@草图12", value = 3.8 },
                    new { name = "D2@草图12", value = 50.0 }, new { name = "D3@草图12", value = 6.0 }
                }) {
                    Dimension dimension = part.Parameter(target.name) as Dimension;
                    ParametricContext.Require(dimension != null && dimension.DrivenState == 2 && Math.Abs(dimension.SystemValue * 1000 - target.value) < .001, "MAINTENANCE_STRIP_DIMENSION_DRIFT " + target.name);
                    dimensions.Add(new { name = target.name, valueMm = dimension.SystemValue * 1000 });
                }
                var endHoles = new List<double[]>(); var topOpening = new List<double[]>();
                foreach (Body2 body in (Array)((PartDoc)part).GetBodies2(0, false)) foreach (Face2 face in (Array)body.GetFaces()) {
                    Surface surface = face.GetSurface() as Surface; if (surface == null || !surface.IsCylinder()) continue;
                    Feature owner = face.GetFeature() as Feature;
                    if (owner != null && owner.Name == "切除-拉伸3") endHoles.Add((double[])surface.CylinderParams);
                    if (owner != null && owner.Name == "切除-拉伸2") topOpening.Add((double[])surface.CylinderParams);
                }
                records.Add(new { inspection = "maintenance_strip_geometry", stage = stage, path = path, snapshot = snapshot, boxMm = box, dimensions = dimensions, endHoleCylinders = endHoles, topOpeningCylinders = topOpening });
                double[] expected = { -34, -length / 2 - 9.2, -2, 34, length / 2, 16 };
                ParametricContext.Require(rebuilt && PartHealthGate(snapshot, true) && DerivedPartHealthGate(snapshot, baseline) && box.Zip(expected, (a, b) => Math.Abs(a - b) < .001).All(v => v), "MAINTENANCE_STRIP_GEOMETRY_FAILED");
                ParametricContext.Require(endHoles.Count == 4 && endHoles.All(c => Math.Abs(Math.Abs(c[3]) - 1) < .000001 && Math.Abs(c[6] * 1000 - 1.9) < .001 && Math.Abs(c[2] * 1000 - 6) < .001 && Math.Abs(Math.Abs(c[1] * 1000) - (length / 2 - 50)) < .001), "MAINTENANCE_STRIP_END_HOLES_FAILED");
                foreach (double sign in new[] { -1.0, 1.0 }) ParametricContext.Require(endHoles.Count(c => Math.Abs(c[1] * 1000 - sign * (length / 2 - 50)) < .001) == 2, "MAINTENANCE_STRIP_END_HOLE_COUNT_FAILED");
                ParametricContext.Require(topOpening.Count == 2 && topOpening.All(c => Math.Abs(Math.Abs(c[5]) - 1) < .000001 && Math.Abs(c[6] * 1000 - 1) < .001 && Math.Abs(c[1] * 1000 - (length / 2 - 114)) < .001) && new[] { -7.5, 7.5 }.All(x => topOpening.Any(c => Math.Abs(c[0] * 1000 - x) < .001)), "MAINTENANCE_STRIP_TOP_OPENING_FAILED");
                return snapshot;
            };
            ModelDoc2 model = NativeParametricAssembly.Open(sw, path, false);
            try {
                baseline = CapturePartSnapshot(model);
                Dimension height = model.Parameter("D2@草图1") as Dimension;
                ParametricContext.Require(PartHealthGate(baseline, true) && height != null && height.DrivenState == 2 && (Math.Abs(height.SystemValue * 1000 - 1825) < .001 || Math.Abs(height.SystemValue * 1000 - length) < .001), "MAINTENANCE_STRIP_SOURCE_DRIFT");
                height.SystemValue = length / 1000;
                inspect(model, "height_driven");
                Feature flat = model.FirstFeature() as Feature;
                while (flat != null && flat.GetTypeName2() != "FlatPattern") flat = flat.GetNextFeature() as Feature;
                ParametricContext.Require(flat != null && flat.IsSuppressed(), "MAINTENANCE_STRIP_FLAT_SOURCE_DRIFT");
                string flatName = flat.Name;
                bool opened = flat.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                bool rebuilt = model.ForceRebuild3(false);
                PartSnapshot unfolded = CapturePartSnapshot(model); double[] flatBox = NativeParametricAssembly.BodyBox(model);
                records.Add(new { inspection = "maintenance_strip_flat", opened = opened, rebuilt = rebuilt, snapshot = unfolded, boxMm = flatBox });
                double[] expectedFlat = { -55.2, -length / 2 - 25.2, 15.2, 55.2, length / 2 + 21.2, 16 };
                ParametricContext.Require(opened && rebuilt && PartHealthGate(unfolded, true) && flatBox.Zip(expectedFlat, (a, b) => Math.Abs(a - b) < .001).All(v => v), "MAINTENANCE_STRIP_FLAT_FAILED");
                flat = FindFeature(model, flatName);
                ParametricContext.Require(flat.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null) && model.ForceRebuild3(false), "MAINTENANCE_STRIP_FOLD_RESTORE_FAILED");
                inspect(model, "fold_restored"); NativeParametricAssembly.Save(model);
            } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            return delegate(ISldWorks reviewSw) {
                ModelDoc2 reopened = NativeParametricAssembly.Open(reviewSw, path, true);
                try { inspect(reopened, "reopened"); }
                finally { reviewSw.CloseDoc(reopened.GetTitle()); Marshal.ReleaseComObject(reopened); }
            };
        }

        // This is diagnostic-only evidence for a rejected derived phase.  It is never
        // used to relax a rebuild or geometry gate.
        private static object CaptureDerivedRebuildFailure(ModelDoc2 model)
        {
            var featureErrors = new List<object>();
            Feature feature = Safe(delegate { return model.FirstFeature() as Feature; }, null);
            int rootIndex = 0;
            while (feature != null && rootIndex++ < 5000)
            {
                CaptureDerivedRebuildFailureFeature(feature, "root[" + (rootIndex - 1).ToString(CultureInfo.InvariantCulture) + "]", 0, featureErrors);
                Feature next = Safe(delegate { return feature.GetNextFeature() as Feature; }, null);
                Release(feature);
                feature = next;
            }
            object referenceFiles = null, referenceParts = null, referenceFeatures = null,
                referenceTypes = null, referenceStatuses = null, referenceEntities = null,
                referenceComponents = null;
            string externalReferenceError = "";
            try
            {
                model.ListExternalFileReferences2(out referenceFiles, out referenceParts,
                    out referenceFeatures, out referenceTypes, out referenceStatuses,
                    out referenceEntities, out referenceComponents);
            }
            catch (Exception ex) { externalReferenceError = ex.GetType().FullName + " " + ex.Message; }
            return new
            {
                part = model.GetPathName(),
                snapshot = CapturePartSnapshot(model),
                featureErrors = featureErrors,
                externalReferenceError = externalReferenceError,
                externalReferenceFiles = referenceFiles,
                externalReferenceFeatures = referenceFeatures,
                externalReferenceStatuses = referenceStatuses,
                externalReferenceEntities = referenceEntities,
                externalReferenceComponents = referenceComponents
            };
        }

        private static void CaptureDerivedRebuildFailureFeature(Feature feature, string featurePath,
            int depth, List<object> featureErrors)
        {
            if (feature == null || depth > 30 || featureErrors.Count > 200) return;
            string type = Safe(delegate { return feature.GetTypeName2(); }, "");
            string name = Safe(delegate { return feature.Name; }, "");
            bool warning = false;
            int error2 = FeatureErrorCode2(feature, out warning);
            int error1 = Safe(delegate { return feature.GetErrorCode(); }, 0);
            bool suppressed = Safe(delegate { return feature.IsSuppressed(); }, false);
            if (!suppressed && (error1 > 0 || error2 > 0 || warning))
                featureErrors.Add(new
                {
                    featurePath = featurePath,
                    name = name,
                    type = type,
                    errorCode = error1,
                    errorCode2 = error2,
                    warning = warning,
                    sketch = CaptureDerivedFailureSketch(feature)
                });
            Feature child = Safe(delegate { return feature.GetFirstSubFeature() as Feature; }, null);
            int childIndex = 0;
            while (child != null && childIndex++ < 3000)
            {
                CaptureDerivedRebuildFailureFeature(child, featurePath + "/" + name + "[" + (childIndex - 1).ToString(CultureInfo.InvariantCulture) + "]", depth + 1, featureErrors);
                Feature next = Safe(delegate { return child.GetNextSubFeature() as Feature; }, null);
                Release(child);
                child = next;
            }
        }

        private static object CaptureDerivedFailureSketch(Feature feature)
        {
            Sketch sketch = Safe(delegate { return feature.GetSpecificFeature2() as Sketch; }, null);
            if (sketch == null) return null;
            var entities = new List<object>();
            Array segments = Safe(delegate { return sketch.GetSketchSegments() as Array; }, null);
            if (segments != null)
            {
                int count = 0;
                foreach (object segment in segments)
                {
                    if (count++ >= 24) break;
                    SketchArc arc = segment as SketchArc;
                    SketchLine line = segment as SketchLine;
                    if (arc != null)
                    {
                        SketchPoint center = Safe(delegate { return arc.GetCenterPoint2() as SketchPoint; }, null);
                        entities.Add(new
                        {
                            kind = "arc",
                            circle = Safe(delegate { return arc.IsCircle(); }, 0),
                            radiusMm = Safe(delegate { return arc.GetRadius() * 1000.0; }, double.NaN),
                            centerMm = center == null ? null : new[] { center.X * 1000.0, center.Y * 1000.0, center.Z * 1000.0 }
                        });
                    }
                    else if (line != null)
                    {
                        SketchPoint start = Safe(delegate { return line.IGetStartPoint2(); }, null);
                        SketchPoint end = Safe(delegate { return line.IGetEndPoint2(); }, null);
                        entities.Add(new
                        {
                            kind = "line",
                            startMm = start == null ? null : new[] { start.X * 1000.0, start.Y * 1000.0, start.Z * 1000.0 },
                            endMm = end == null ? null : new[] { end.X * 1000.0, end.Y * 1000.0, end.Z * 1000.0 }
                        });
                    }
                    else entities.Add(new { kind = Safe(delegate { return segment.GetType().Name; }, "unknown") });
                }
            }
            return new
            {
                segmentCount = segments == null ? 0 : segments.Length,
                transform = Safe(delegate { return sketch.ModelToSketchTransform.ArrayData as double[]; }, null),
                entities = entities
            };
        }

        private static void DescribeMasterCut(ModelDoc2 model)
        {
            Feature feature=FindFeature(model,"切除-拉伸60");if(feature==null)return;
            Feature sketchFeature=FindFeature(model,"草图137");Sketch sketch=sketchFeature.GetSpecificFeature2() as Sketch;
            var entities=new List<object>();foreach(object segment in (Array)sketch.GetSketchSegments()){
                SketchArc arc=segment as SketchArc;SketchLine line=segment as SketchLine;
                if(arc!=null){SketchPoint p=arc.GetCenterPoint2() as SketchPoint;entities.Add(new{circle=arc.IsCircle(),radius=arc.GetRadius(),center=new[]{p.X,p.Y,p.Z}});}
                if(line!=null)entities.Add(new{start=new[]{line.IGetStartPoint2().X,line.IGetStartPoint2().Y,line.IGetStartPoint2().Z},end=new[]{line.IGetEndPoint2().X,line.IGetEndPoint2().Y,line.IGetEndPoint2().Z}});
            }
            var bodies=new List<object>();foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))bodies.Add(new{name=body.Name,box=body.GetBodyBox()});
            Console.WriteLine("MASTER_CUT60_DIAGNOSTIC "+ParametricContext.Json.Serialize(new{sketchTransform=sketch.ModelToSketchTransform.ArrayData,entities=entities,bodies=bodies}));
        }
        internal static void ProbeOriginalMaster(ISldWorks sw,List<object> rows)
        {
            string root=Path.GetFullPath(Path.Combine(ParametricContext.AttemptDirectory,"..","..",".."));
            string source=Path.Combine(root,@"workers\analysis\desktop_reference\参数化模板素材_U盘原始_20260526\16029 寄存柜(标准组合式 1917×1000×550)\1.工程图\标准寄存柜 模型.SLDPRT");
            string copy=Path.Combine(ParametricContext.AttemptDirectory,"original_master_probe.SLDPRT");
            ParametricContext.AssertMutation(copy);File.Copy(source,copy,false);
            ModelDoc2 model=NativeParametricAssembly.Open(sw,copy,true);
            try{
                DescribeMasterCut(model);
                rows.Add(new{originalSource=source,sha256=ParametricContext.Hash(source),widthMm=((Dimension)model.Parameter("D1@草图1")).SystemValue*1000,cut137SpanMm=((Dimension)model.Parameter("D3@草图137")).SystemValue*1000});
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }
        internal static int RunParametric(string[] args) { return Main(args); }
        internal static object DescribeParametricPlans(double width)
        {
            ParametricContext.Require(width >= 700 && width <= 1200, "UNSUPPORTED_WIDTH");
            ParametricContext.WidthMm=width;
            var plans=AssemblyPlans("");
            return new {widthMm=width,dimensionParts=DimensionPlans(""),derivedParts=DerivedPlans(""),assemblyPlanCount=plans.Count,expectedEditRecordCount=plans.Count*2,assemblies=plans};
        }
    }

    internal static partial class NativeParametricAssembly
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            try
            {
                if(args.Length==2&&args[0]=="--describe-plans") { Console.WriteLine(ParametricContext.Json.Serialize(NativeParametricWidth.DescribeParametricPlans(double.Parse(args[1],CultureInfo.InvariantCulture)))); return 0; }
                if (args.Length != 2) throw new InvalidOperationException("Usage: NativeParametricAssembly.exe <plan.json> <phase>");
                string phase = args[1];
                bool doorPhase = phase.StartsWith("door-", StringComparison.Ordinal);
                double height = 0;
                if (doorPhase)
                    ParametricContext.Require(double.TryParse(phase.Substring(5).Replace('p', '.'), NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out height) && height > 10.5 && !double.IsInfinity(height), "PARAMETRIC_DOOR_HEIGHT_INVALID");
                else
                    ParametricContext.Require(new[] {
                        "dimensions", "derived", "base-hole", "assemblies", "clone", "probe", "probe-original-master",
                        "probe-frame", "probe-top", "probe-height-braces", "repair-frame-slots", "repair-height-braces",
                        "repair-depth-placements", "probe-envelope", "envelope", "repair-interfaces", "repair-door-placements",
                        "repair-boundaries", "probe-base", "probe-partition-mates", "probe-base-mates", "probe-shallow-shelf-links",
                        "probe-shallow-assembly-links", "repair-base-mates", "repair-partition-mates", "repair-base-foot", "locks",
                        "locks-verify", "root", "repair-engineer-feedback", "verify", "package", "verify-package", "verify-relocated", "physical-audit"
                    }.Contains(phase), "UNKNOWN_PARAMETRIC_PHASE");
                ParametricContext.Initialize(args[0], phase);
                string evidence = Path.Combine(ParametricContext.AttemptDirectory, "evidence", phase + ".json");
                if (new[] { "dimensions", "derived", "base-hole", "assemblies" }.Contains(phase))
                    return NativeParametricWidth.RunParametric(new[] { ParametricContext.CadDirectory, evidence, phase });
                if (doorPhase)
                {
                    var rowMap = ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "rowsByColumn");
                    ParametricContext.Require(new[] { "L", "R" }.Any(side => ((IEnumerable)rowMap[side]).Cast<Dictionary<string, object>>()
                        .Any(row => phase == "door-" + ParametricContext.Number(row, "doorHeightMm").ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p'))),
                        "PARAMETRIC_DOOR_HEIGHT_NOT_IN_CONTRACT");
                    NativeParametricDoor.Generate(height, evidence);
                    return 0;
                }
                var rows = new List<object>();
                ReadOnlyInspection = new[] { "verify-package", "verify-relocated", "physical-audit" }.Contains(phase);
                try {
                if (phase == "envelope") RunLegacyDoorSessions(rows);
                if (phase == "repair-height-braces") RunHeightBraceSessions(phase, rows);
                else if (phase == "locks" || phase == "locks-verify") NativeParametricLocks.RunIsolated(rows, phase == "locks-verify");
                else NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw)
                {
                    sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory);
                    if (phase == "clone") { Relink(sw, ParametricContext.CadDirectory, rows); string doors = Path.Combine(ParametricContext.AttemptDirectory, "doors"); if (Directory.Exists(doors)) Relink(sw, doors, rows); }
                    else if (phase == "probe") Probe(sw, rows);
                    else if (phase == "probe-original-master") NativeParametricWidth.ProbeOriginalMaster(sw,rows);
                    else if (phase == "probe-frame") Probe(sw,rows,new[]{"门框 左.sldprt","门框 右.sldprt","门框 竖隔板L.sldprt","门框 竖隔板R.SLDPRT","门框 横隔板.sldprt"});
                    else if (phase == "probe-top") Probe(sw,rows,new[]{"上盖壳体底板.sldprt"});
                    else if (phase == "probe-height-braces") {
                        Probe(sw,rows,new[]{"箱体竖隔板L.sldprt","箱体竖隔板加强件.sldprt","锁控维护条_源钣金.SLDPRT","箱体侧板加强筋1.sldprt"});
                        ModelDoc2 part=Open(sw,Find("箱体侧板加强筋1.sldprt"),true);
                        try{
                            ParametricContext.Require(part.ForceRebuild3(false),"HEIGHT_GRID_READBACK_REBUILD_FAILED");
                            NativeParametricWidth.CaptureShallowVerticalFeatures(part,rows,"height-grid-readback");
                        }
                        finally{sw.CloseDoc(part.GetTitle());Marshal.ReleaseComObject(part);}
                    }
                    else if (phase == "repair-frame-slots") RepairFrameSlots(sw,rows);
                    else if (phase == "repair-depth-placements") RepairDepthPlacements(sw,rows);
                    else if (phase == "probe-envelope") NativeParametricWidth.ProbeEnvelope(sw,rows);
                    else if (phase == "envelope") NativeParametricWidth.ProbeEnvelope(sw,rows,true);
                    else if (phase == "repair-interfaces") RepairInterfaces(sw, rows);
                    else if (phase == "repair-door-placements") RepairDoorPlacements(sw, rows);
                    else if (phase == "repair-boundaries") RepairBoundaries(sw, rows);
                    else if (phase == "probe-base") ProbeBaseHoles(sw, rows);
                    else if (phase == "probe-partition-mates") ProbePartitionMates(sw,rows);
                    else if (phase == "probe-base-mates") ProbePartitionMates(sw,rows,"底座焊接.SLDASM");
                    else if (phase == "probe-shallow-shelf-links") {
                        foreach(string name in new[]{"箱体横层板L焊接.SLDASM","箱体横层板R焊接.SLDASM"}){rows.Add(new{inspection="shallow_shelf_assembly",file=name});ProbePartitionMates(sw,rows,name);}
                        foreach(string name in new[]{"箱体横层板加强筋.SLDPRT","箱体横层板L.sldprt","箱体横层板R.SLDPRT"}){
                            ModelDoc2 part=Open(sw,Find(name),true);
                            try{InspectShallowNativeInterfaces(part,part.GetPathName(),rows);}finally{sw.CloseDoc(part.GetTitle());Marshal.ReleaseComObject(part);}
                        }
                    }
                    else if (phase == "probe-shallow-assembly-links") {
                        NativeParametricWidth.ProbeShallowAssemblyLinks(sw,rows);
                        foreach(string name in new[]{"箱体左侧板焊接.sldasm","箱体右侧板焊接.SLDASM","箱体竖隔板L焊接.SLDASM","箱体竖隔板R焊接.SLDASM"})ProbePartitionMates(sw,rows,name);
                    }
                    else if (phase == "repair-base-mates") RepairBaseMates(sw,rows);
                    else if (phase == "repair-partition-mates") RepairPartitionMates(sw,rows);
                    else if (phase == "repair-base-foot") RepairBaseFootHoles(sw, rows);
                    else if (phase == "repair-engineer-feedback") RepairEngineerFeedback(sw, rows);
                    else if (phase == "root") BuildRoot(sw, rows);
                    else if (phase == "verify") VerifyRoot(sw, rows);
                    else if (phase == "package") PackageRoot(sw, rows);
                    else if (phase == "verify-package") VerifyPackage(sw, rows);
                    else if (phase == "verify-relocated") VerifyRelocatedPackage(sw, rows);
                    else if (phase == "physical-audit") PhysicalAudit(sw, rows);
                    else throw new InvalidOperationException("UNKNOWN_PARAMETRIC_PHASE");
                }); }
                catch (Exception error) { ParametricContext.Write(evidence, new { phase = phase, success = false, error = error.ToString(), rows = rows }); throw; }
                finally { ReadOnlyInspection = false; }
                ParametricContext.Write(evidence, new { phase = phase, success = true, rows = rows });
                Console.WriteLine("PARAMETRIC_PHASE_COMPLETE " + phase);
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error.ToString()); return 1; }
        }

        private static bool ReadOnlyInspection;
        private static readonly List<object> DocumentStates = new List<object>();


        private static void RunLegacyDoorSessions(List<object> records)
        {
            if (ParametricContext.Plan.ContainsKey("preprunedTemplate"))
            {
                var template = new PreprunedTemplateAudit(records);
                bool previous = ReadOnlyInspection; ReadOnlyInspection = true;
                try { NativeParametricDoor.WithSession("envelope", delegate(ISldWorks sw) { template.Verify(sw); }, "prepruned-template-readonly67"); }
                finally { ReadOnlyInspection = previous; template.AfterSession(); }
                return;
            }
            var audit = new LegacyDoorPruneAudit(records);
            NativeParametricDoor.WithSession("envelope", delegate(ISldWorks sw) { audit.Prune(sw); }, "legacy-door-prune");
            audit.AfterSession("legacy-door-prune", false);
            NativeParametricDoor.WithSession("envelope", delegate(ISldWorks sw) { audit.Reopen(sw); }, "legacy-door-reopen");
            audit.AfterSession("legacy-door-reopen", true);
        }

        private static PreprunedTemplateBinding TemplateBinding;
        private static readonly List<object> SaveGuards = new List<object>();

        private static object[] TemplateValues(object value)
        {
            ParametricContext.Require(value is IEnumerable && !(value is string), "PREPRUNED_EXPECTED_ARRAY");
            return ((IEnumerable)value).Cast<object>().ToArray();
        }
        private static bool TemplateSamePath(string left, string right)
        { return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        private static void TemplateRelease(object value)
        { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        private static string TemplateMapPath(string path, string source, string target)
        {
            ParametricContext.Require(Path.IsPathRooted(path) && TemplateSamePath(Path.GetDirectoryName(path), source), "PREPRUNED_BASELINE_PATH_OUTSIDE_TEMPLATE " + path);
            return Path.Combine(target, Path.GetFileName(path));
        }
        private static string[] TemplateComponentDifferences(Dictionary<string, object> actual, Dictionary<string, object> expected, string source, string target)
        {
            var differences = new List<string>();
            foreach (string key in new[] { "name", "configuration", "suppressed", "fixedComponent" })
                if (!object.Equals(actual[key], expected[key])) differences.Add(key);
            if (Convert.ToInt32(actual["suppression"], CultureInfo.InvariantCulture) != Convert.ToInt32(expected["suppression"], CultureInfo.InvariantCulture)) differences.Add("suppression");
            if (!TemplateSamePath(Convert.ToString(actual["referencePath"]), TemplateMapPath(Convert.ToString(expected["referencePath"]), source, target))) differences.Add("referencePath");
            if (expected["loadedPath"] == null)
            {
                if (actual["loadedPath"] != null || actual["loadedReadOnly"] != null) differences.Add("loadedPath");
            }
            else if (!TemplateSamePath(Convert.ToString(actual["loadedPath"]), TemplateMapPath(Convert.ToString(expected["loadedPath"]), source, target)) || !object.Equals(actual["loadedReadOnly"], true)) differences.Add("loadedPath");
            double[] left = TemplateValues(actual["transform"]).Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
            double[] right = TemplateValues(expected["transform"]).Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
            if (left.Length != 16 || right.Length != 16 || left.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || right.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || !left.Zip(right, (a, b) => Math.Abs(a - b) <= 1e-10).All(v => v)) differences.Add("transform");
            return differences.ToArray();
        }
        private static PreprunedTemplateBinding GetTemplateBinding()
        {
            if (ParametricContext.Plan == null || !ParametricContext.Plan.ContainsKey("preprunedTemplate")) return null;
            if (TemplateBinding == null) TemplateBinding = new PreprunedTemplateBinding();
            return TemplateBinding;
        }
        internal static void AssertProtectedTemplateInputs()
        { PreprunedTemplateBinding binding = GetTemplateBinding(); if (binding != null) binding.AssertProtected(); }

        internal static bool GuardedSave3(ModelDoc2 model, int options, ref int errors, ref int warnings)
        {
            PreprunedTemplateBinding binding = GetTemplateBinding(); string path = binding == null ? null : model.GetPathName();
            if (binding != null) { ParametricContext.AssertMutation(path); ParametricContext.Require(!ReadOnlyInspection && !model.IsOpenedReadOnly() && !model.IsOpenedViewOnly(), "GUARDED_SAVE_REQUIRES_WRITABLE " + path); binding.AssertProtected(); }
            bool result = false; string failure = null;
            try { result = model.Save3(options, ref errors, ref warnings); return result; }
            catch (Exception error) { failure = error.ToString(); throw; }
            finally
            {
                if (binding != null)
                {
                    object protection = null; string protectionError = null;
                    try { protection = binding.AssertProtected(); }
                    catch (Exception error) { protectionError = error.ToString(); throw; }
                    finally { SaveGuards.Add(new { operation = "Save3", path = path, options = options, returned = result, errors = errors, warnings = warnings, exception = failure, protection = protection, protectionError = protectionError }); ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".save-guards.json"), SaveGuards); }
                }
            }
        }
        internal static bool GuardedSaveAs(ModelDoc2 model, string target, int version, int options, object exportData, ref int errors, ref int warnings)
        {
            PreprunedTemplateBinding binding = GetTemplateBinding(); string path = binding == null ? null : model.GetPathName();
            if (binding != null) { if (!string.IsNullOrEmpty(path)) ParametricContext.AssertMutation(path); ParametricContext.AssertMutation(target); ParametricContext.Require(!ReadOnlyInspection && !model.IsOpenedReadOnly() && !model.IsOpenedViewOnly(), "GUARDED_SAVEAS_REQUIRES_WRITABLE " + target); binding.AssertProtected(); }
            bool result = false; string failure = null; ModelDocExtension extension = model.Extension;
            try { result = extension.SaveAs(target, version, options, exportData, ref errors, ref warnings); return result; }
            catch (Exception error) { failure = error.ToString(); throw; }
            finally
            {
                try
                {
                    if (binding != null)
                    {
                        object protection = null; string protectionError = null;
                        try { protection = binding.AssertProtected(); }
                        catch (Exception error) { protectionError = error.ToString(); throw; }
                        finally { SaveGuards.Add(new { operation = "SaveAs", path = path, target = target, version = version, options = options, returned = result, errors = errors, warnings = warnings, exception = failure, protection = protection, protectionError = protectionError }); ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".save-guards.json"), SaveGuards); }
                    }
                }
                finally { TemplateRelease(extension); }
            }
        }

        internal static object GuardedAuxiliarySave(string operation, string target, Func<object> save)
        {
            PreprunedTemplateBinding binding = GetTemplateBinding();
            if (binding != null) { ParametricContext.AssertMutation(target); binding.AssertProtected(); }
            object result = null; string failure = null;
            try { result = save(); return result; }
            catch (Exception error) { failure = error.ToString(); throw; }
            finally
            {
                if (binding != null)
                {
                    object protection = null; string protectionError = null;
                    try { protection = binding.AssertProtected(); }
                    catch (Exception error) { protectionError = error.ToString(); throw; }
                    finally { SaveGuards.Add(new { operation = operation, target = target, rawResult = result, exception = failure, protection = protection, protectionError = protectionError }); ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".save-guards.json"), SaveGuards); }
                }
            }
        }

        private sealed class PreprunedTemplateBinding
        {
            internal readonly string EvidencePath, EvidenceHash, Source, RootFile, Target;
            internal readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, string> Protected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), Inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, object> Cabinet;
            internal readonly Dictionary<string, Dictionary<string, object>> Configurations = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, Dictionary<string, object>> References = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            internal PreprunedTemplateBinding()
            {
                var selection = ParametricContext.Object(ParametricContext.Plan, "preprunedTemplate");
                EvidencePath = ParametricContext.Text(selection, "evidencePath"); EvidenceHash = ParametricContext.Text(selection, "evidenceSha256");
                Bind(EvidencePath, EvidenceHash); var evidence = ParametricContext.Read(EvidencePath);
                ParametricContext.Require(ParametricContext.Text(evidence, "schema") == "winnsen.locker16029.prepruned_template.v1" && ParametricContext.Text(evidence, "decisionAuthor") == "Eli", "PREPRUNED_TEMPLATE_DECISION_BINDING");
                Source = ParametricContext.Text(evidence, "sourceDirectory"); RootFile = ParametricContext.Text(evidence, "rootRelativePath");
                ParametricContext.Require(Path.IsPathRooted(Source) && Path.GetFileName(RootFile) == RootFile && !string.IsNullOrEmpty(RootFile), "PREPRUNED_TEMPLATE_PATH_INVALID");
                Target = Path.Combine(ParametricContext.CadDirectory, RootFile);
                foreach (Dictionary<string, object> row in TemplateValues(evidence["files"]))
                {
                    string path = ParametricContext.Text(row, "path"), relative = ParametricContext.Text(row, "relativePath");
                    ParametricContext.Require(Path.GetFileName(relative) == relative && TemplateSamePath(path, Path.Combine(Source, relative)), "PREPRUNED_FILE_MAPPING_INVALID");
                    ParametricContext.Require(!Files.ContainsKey(path), "PREPRUNED_DUPLICATE_FILE"); Files.Add(path, ParametricContext.Text(row, "sha256"));
                }
                ParametricContext.Require(Files.Count == 75 && Files.ContainsKey(Path.Combine(Source, RootFile)) && Files[Path.Combine(Source, RootFile)] == ParametricContext.Text(evidence, "sourceRootSha256"), "PREPRUNED_TEMPLATE_INVENTORY_BINDING");
                foreach (string key in new[] { "cabinetBaseline", "protectedManifest", "acceptedAnalysis" }) Bind(ParametricContext.Text(evidence, key + "Path"), ParametricContext.Text(evidence, key + "Sha256"));
                string plan = ParametricContext.Text(evidence, "planPath"); Bind(plan, ParametricContext.Hash(plan)); Bind(ParametricContext.PlanPath, ParametricContext.PlanHash);
                var manifest = ParametricContext.Read(ParametricContext.Text(evidence, "protectedManifestPath"));
                foreach (Dictionary<string, object> row in TemplateValues(manifest["files"])) Protected.Add(ParametricContext.Text(row, "path"), ParametricContext.Text(row, "sha256"));
                ParametricContext.Require(Protected.Count == 166, "PREPRUNED_PROTECTED_166_REQUIRED");
                var baseline = ParametricContext.Read(ParametricContext.Text(evidence, "cabinetBaselinePath"));
                var events = TemplateValues(baseline["events"]).Cast<Dictionary<string, object>>().ToArray();
                var snapshots = events.Where(row => ParametricContext.Text(row, "inspection") == "cabinet_snapshot" && ParametricContext.Text(row, "stage") == "before_geometry").ToArray();
                ParametricContext.Require(snapshots.Length == 1, "PREPRUNED_SINGLE_67_CABINET_REQUIRED"); Cabinet = ParametricContext.Object(snapshots[0], "cabinet");
                ParametricContext.Require(Cabinet.Count == 80, "PREPRUNED_CABINET_80_REQUIRED");
                foreach (var row in events.Where(row => ParametricContext.Text(row, "inspection") == "configuration_identity"))
                {
                    string path = ParametricContext.Text(row, "path"); ParametricContext.Require(Files.ContainsKey(path) && Files[path] == ParametricContext.Text(row, "documentSha256"), "PREPRUNED_CONFIGURATION_FILE_BINDING");
                    Configurations.Add(Path.GetFileName(path), row);
                }
                foreach (var row in events.Where(row => ParametricContext.Text(row, "inspection") == "closed_dependency_document" && ParametricContext.Text(row, "stage") == "before_open"))
                {
                    string owner = ParametricContext.Text(row, "owner"); ParametricContext.Require(Files.ContainsKey(owner) && object.Equals(row["exact"], true) && row["error"] == null, "PREPRUNED_REFERENCE_BASELINE_INVALID"); References.Add(Path.GetFileName(owner), row);
                }
                ParametricContext.Require(Configurations.ContainsKey(RootFile) && References.Count == 75 && References.Values.Sum(row => row["raw"] == null ? 0 : TemplateValues(row["raw"]).Length / 2) == 140, "PREPRUNED_67_BASELINE_INCOMPLETE");
                var opened = events.Where(row => ParametricContext.Text(row, "inspection") == "open_result").ToArray();
                ParametricContext.Require(opened.Length == 1 && Convert.ToInt32(opened[0]["options"]) == 67 && Convert.ToInt32(opened[0]["errors"]) == 0 && (Convert.ToInt32(opened[0]["warnings"]) & 1) == 0 && object.Equals(opened[0]["actualReadOnly"], true), "PREPRUNED_67_OPEN_BASELINE_INVALID");
                AssertProtected();
            }
            void Bind(string path, string hash)
            {
                ParametricContext.Require(Path.IsPathRooted(path) && hash.Length == 64 && hash.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F'), "PREPRUNED_INPUT_BINDING_INVALID");
                ParametricContext.CheckPath(path, Path.GetDirectoryName(path)); ParametricContext.Require(ParametricContext.Hash(path) == hash, "PREPRUNED_INPUT_SHA_MISMATCH " + path); Inputs.Add(path, hash);
            }
            static string Digest(Dictionary<string, string> files)
            {
                string text = string.Join("\n", files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "|" + pair.Value).ToArray());
                using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "");
            }
            Dictionary<string, string> VerifyHashes(Dictionary<string, string> expected, bool protectedCad)
            {
                var actual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in expected)
                {
                    if (protectedCad) ParametricContext.Require(!pair.Key.StartsWith(ParametricContext.AttemptDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "PREPRUNED_PROTECTION_OVERLAPS_ATTEMPT");
                    ParametricContext.CheckPath(pair.Key, Path.GetDirectoryName(pair.Key)); string hash = ParametricContext.Hash(pair.Key); actual.Add(pair.Key, hash);
                    ParametricContext.Require(hash == pair.Value, "PREPRUNED_PROTECTED_INPUT_CHANGED " + pair.Key);
                }
                return actual;
            }
            internal object AssertProtected()
            {
                var inputs = VerifyHashes(Inputs, false); var files = VerifyHashes(Files, true); var protectedFiles = VerifyHashes(Protected, true);
                string[] paths = Directory.GetFiles(Source).Where(IsCad).ToArray();
                ParametricContext.Require(paths.Length == 75 && Directory.GetDirectories(Source).Length == 0 && paths.All(Files.ContainsKey), "PREPRUNED_SOURCE_INVENTORY_CHANGED");
                return new { templateFiles = files.Count, templateDigest = Digest(files), protectedFiles = protectedFiles.Count, protectedDigest = Digest(protectedFiles), bindingFiles = inputs.Count, bindingDigest = Digest(inputs), evidencePath = EvidencePath, evidenceSha256 = EvidenceHash, pass = true };
            }
            internal static bool IsCad(string path)
            { return !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal) && (Path.GetExtension(path).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase)); }
            internal Dictionary<string, string> Inventory()
            {
                string[] paths = Directory.GetFiles(ParametricContext.CadDirectory).Where(IsCad).ToArray();
                ParametricContext.Require(paths.Length == 75 && Directory.GetDirectories(ParametricContext.CadDirectory).Length == 0 && paths.All(path => Files.ContainsKey(Path.Combine(Source, Path.GetFileName(path)))), "PREPRUNED_ATTEMPT_FLAT_75_REQUIRED");
                foreach (string path in paths) ParametricContext.CheckPath(path, ParametricContext.CadDirectory);
                return paths.ToDictionary(path => path, ParametricContext.Hash, StringComparer.OrdinalIgnoreCase);
            }
            internal void ClosedReferences(ISldWorks sw, string stage, List<object> records)
            {
                ParametricContext.Require(sw.GetDocumentCount() == 0, "PREPRUNED_REFERENCE_CHECK_REQUIRES_CLOSED_DOCUMENTS"); Inventory(); int pairs = 0;
                foreach (var pair in References)
                {
                    string owner = Path.Combine(ParametricContext.CadDirectory, pair.Key); object raw = sw.GetDocumentDependencies2(owner, false, false, false); Array array = raw as Array;
                    ParametricContext.Require(raw == null || array != null && array.Rank == 1, "PREPRUNED_REFERENCE_RAW_SHAPE");
                    object[] actual = array == null ? new object[0] : array.Cast<object>().ToArray(), expected = pair.Value["raw"] == null ? new object[0] : TemplateValues(pair.Value["raw"]);
                    ParametricContext.Require(actual.Length % 2 == 0 && actual.All(value => value is string) && (raw == null) == (pair.Value["raw"] == null) && actual.Length == expected.Length && object.Equals(raw == null ? null : raw.GetType().FullName, pair.Value["rawType"]), "PREPRUNED_REFERENCE_SHAPE_CHANGED " + owner);
                    for (int index = 0; index < actual.Length; index++)
                    {
                        string wanted = index % 2 == 0 ? (string)expected[index] : TemplateMapPath((string)expected[index], Source, ParametricContext.CadDirectory);
                        ParametricContext.Require(index % 2 == 0 ? (string)actual[index] == wanted : TemplateSamePath((string)actual[index], wanted), "PREPRUNED_REFERENCE_MAPPING_CHANGED " + owner);
                        if (index % 2 == 1) { ParametricContext.CheckPath((string)actual[index], ParametricContext.CadDirectory); ParametricContext.Require(File.Exists((string)actual[index]), "PREPRUNED_REFERENCE_MISSING"); }
                    }
                    pairs += actual.Length / 2; records.Add(new { inspection = "prepruned_closed_reference", stage = stage, owner = owner, raw = raw, pass = true });
                }
                ParametricContext.Require(pairs == 140 && sw.GetDocumentCount() == 0, "PREPRUNED_CLOSED_75_140_REQUIRED");
                records.Add(new { inspection = "prepruned_closed_reference_gate", stage = stage, owners = References.Count, pairs = pairs, pass = true });
            }
        }

        private sealed class PreprunedTemplateAudit
        {
            readonly PreprunedTemplateBinding Binding;
            readonly List<object> Records;
            readonly Dictionary<string, string> Before;
            internal PreprunedTemplateAudit(List<object> records)
            {
                ParametricContext.Require(ParametricContext.Phase == "envelope", "PREPRUNED_REQUIRES_ENVELOPE");
                Records = records; Binding = GetTemplateBinding(); Before = Binding.Inventory();
                Record(new { inspection = "prepruned_template_binding", route = "prepruned-template-readonly67", decisionAuthor = "Eli", evidencePath = Binding.EvidencePath, evidenceSha256 = Binding.EvidenceHash, legacyLdrHistoryUnchanged = true, noSave = true, noDelete = true, noRebuild = true, noResolve = true, noSelect = true });
            }
            void Record(object row)
            { Records.Add(row); ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", "envelope.prepruned-template.json"), new { phase = "envelope", rows = Records }); }
            Dictionary<string, object> ConfigurationIdentity(ModelDoc2 model)
            {
                string file = Path.GetFileName(model.GetPathName()); ParametricContext.Require(Binding.Configurations.ContainsKey(file), "PREPRUNED_UNEXPECTED_LOADED_CONFIGURATION " + file);
                var expected = Binding.Configurations[file]; var configurations = new List<object>(); var names = TemplateValues(model.GetConfigurationNames()); Configuration active = null;
                try
                {
                    active = model.GetActiveConfiguration() as Configuration; ParametricContext.Require(active != null, "PREPRUNED_ACTIVE_CONFIGURATION_MISSING");
                    foreach (string name in names)
                    {
                        Configuration configuration = model.GetConfigurationByName(name) as Configuration;
                        try { ParametricContext.Require(configuration != null, "PREPRUNED_CONFIGURATION_MISSING " + name); configurations.Add(new { reportedName = name, name = configuration.Name, streamName = configuration.GetStreamName(), id = configuration.GetID() }); }
                        finally { TemplateRelease(configuration); }
                    }
                    var identity = new Dictionary<string, object> { { "configurationNames", names }, { "configurations", configurations }, { "active", new { name = active.Name, streamName = active.GetStreamName(), id = active.GetID() } } };
                    foreach (string key in identity.Keys) ParametricContext.Require(ParametricContext.Json.Serialize(identity[key]) == ParametricContext.Json.Serialize(expected[key]), "PREPRUNED_CONFIGURATION_IDENTITY_CHANGED " + file + " " + key);
                    Record(new { inspection = "prepruned_configuration_identity", path = model.GetPathName(), identity = identity, aliasesMapped = false }); return identity;
                }
                finally { TemplateRelease(active); }
            }
            Dictionary<string, object> ComponentState(Component2 component)
            {
                ModelDoc2 loaded = null; MathTransform transform = null;
                try
                {
                    loaded = component.GetModelDoc2() as ModelDoc2; transform = component.Transform2 as MathTransform;
                    string path = component.GetPathName(); ParametricContext.CheckPath(path, ParametricContext.CadDirectory);
                    ParametricContext.Require(File.Exists(path) && transform != null, "PREPRUNED_COMPONENT_PATH_OR_TRANSFORM");
                    if (loaded != null) ParametricContext.Require(TemplateSamePath(path, loaded.GetPathName()) && loaded.IsOpenedReadOnly() && !loaded.IsOpenedViewOnly(), "PREPRUNED_COMPONENT_LOADED_IDENTITY_OR_MODE " + component.Name2);
                    return new Dictionary<string, object> { { "name", component.Name2 }, { "referencePath", path }, { "configuration", component.ReferencedConfiguration }, { "suppression", component.GetSuppression2() }, { "suppressed", component.IsSuppressed() }, { "fixedComponent", component.IsFixed() }, { "loadedPath", loaded == null ? null : loaded.GetPathName() }, { "loadedReadOnly", loaded == null ? (object)null : loaded.IsOpenedReadOnly() }, { "transform", transform.ArrayData } };
                }
                finally { TemplateRelease(transform); TemplateRelease(loaded); }
            }
            internal void Verify(ISldWorks sw)
            {
                ParametricContext.Require(sw.GetDocumentCount() == 0, "PREPRUNED_SESSION_NOT_EMPTY"); Binding.ClosedReferences(sw, "before_readonly67", Records);
                sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory); ModelDoc2 root = null;
                try
                {
                    int errors = 0, warnings = 0; root = sw.OpenDoc6(Binding.Target, 2, 67, "", ref errors, ref warnings) as ModelDoc2;
                    Record(new { inspection = "prepruned_open67", path = Binding.Target, options = 67, errors = errors, warnings = warnings, warning1 = warnings & 1, actualReadOnly = root == null ? (bool?)null : root.IsOpenedReadOnly(), viewOnly = root == null ? (bool?)null : root.IsOpenedViewOnly() });
                    ParametricContext.Require(root is AssemblyDoc && errors == 0 && (warnings & 1) == 0, "PREPRUNED_READONLY67_OPEN_FAILED");
                    ParametricContext.Require(TemplateSamePath(root.GetPathName(), Binding.Target) && root.IsOpenedReadOnly() && !root.IsOpenedViewOnly(), "PREPRUNED_ROOT_IDENTITY_OR_MODE");
                    Array documents = sw.GetDocuments() as Array; var loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        ParametricContext.Require(documents != null && documents.Length <= 100, "PREPRUNED_DOCUMENT_SET_INVALID");
                        foreach (ModelDoc2 document in documents)
                        {
                            string path = document.GetPathName(); ParametricContext.CheckPath(path, ParametricContext.CadDirectory);
                            ParametricContext.Require(File.Exists(path) && document.IsOpenedReadOnly() && !document.IsOpenedViewOnly() && loadedFiles.Add(Path.GetFileName(path)), "PREPRUNED_LOADED_DOCUMENT_IDENTITY_OR_MODE " + path); ConfigurationIdentity(document);
                        }
                        ParametricContext.Require(loadedFiles.SetEquals(Binding.Configurations.Keys), "PREPRUNED_LOADED_DOCUMENT_SET_CHANGED");
                    }
                    finally { if (documents != null) foreach (object document in documents) TemplateRelease(document); }
                    var cabinet = new Dictionary<string, object>(StringComparer.Ordinal); Array components = ((AssemblyDoc)root).GetComponents(false) as Array;
                    try
                    {
                        ParametricContext.Require(components != null && components.Length == 80, "PREPRUNED_COMPONENT_EXACT_80");
                        foreach (Component2 component in components)
                        {
                            string name = component.Name2; ParametricContext.Require(!cabinet.ContainsKey(name) && Binding.Cabinet.ContainsKey(name), "PREPRUNED_COMPONENT_SET_CHANGED " + name);
                            var state = ComponentState(component); string[] differences = TemplateComponentDifferences(state, (Dictionary<string, object>)Binding.Cabinet[name], Binding.Source, ParametricContext.CadDirectory);
                            Record(new { inspection = "prepruned_component_comparison", name = name, actual = state, differences = differences, pass = differences.Length == 0 });
                            ParametricContext.Require(differences.Length == 0, "PREPRUNED_COMPONENT_CHANGED " + name + " " + string.Join(",", differences)); cabinet.Add(name, state);
                        }
                    }
                    finally { if (components != null) foreach (object component in components) TemplateRelease(component); }
                    var direct = new HashSet<string>(StringComparer.Ordinal); Array directComponents = ((AssemblyDoc)root).GetComponents(true) as Array;
                    try { ParametricContext.Require(directComponents != null && directComponents.Length == 19, "PREPRUNED_DIRECT_EXACT_19"); foreach (Component2 component in directComponents) ParametricContext.Require(direct.Add(component.Name2) && cabinet.ContainsKey(component.Name2), "PREPRUNED_DIRECT_IDENTITY_CHANGED"); }
                    finally { if (directComponents != null) foreach (object component in directComponents) TemplateRelease(component); }
                    ParametricContext.Require(direct.SetEquals(Binding.Cabinet.Keys.Where(name => !name.Contains("/"))), "PREPRUNED_DIRECT_SET_CHANGED");
                    string[] nested = { "上盖焊接-1/上盖 模型-1", "底座焊接-1/底座 模型-1" };
                    ParametricContext.Require(nested.All(cabinet.ContainsKey), "PREPRUNED_TWO_NESTED_IDENTITIES_MISSING");
                    int oldDoors = cabinet.Values.Cast<Dictionary<string, object>>().Count(row => ParametricContext.Text(row, "name").StartsWith("储物柜门", StringComparison.Ordinal) || Path.GetFileName(ParametricContext.Text(row, "referencePath")).StartsWith("储物柜门", StringComparison.Ordinal));
                    ParametricContext.Require(oldDoors == 0, "PREPRUNED_OLD_DOOR_OR_CHILD_REMAINS");
                    Configuration active = root.GetActiveConfiguration() as Configuration;
                    try
                    {
                        ParametricContext.Require(active != null, "PREPRUNED_ROOT_CONFIGURATION_MISSING");
                        int exploded = ((AssemblyDoc)root).GetExplodedViewCount2(active.Name), steps = active.GetNumberOfExplodeSteps(); object rawNames = ((AssemblyDoc)root).GetExplodedViewNames2(active.Name);
                        int names = rawNames == null ? 0 : TemplateValues(rawNames).Length;
                        Record(new { inspection = "prepruned_identity_and_absence", direct = direct.OrderBy(name => name, StringComparer.Ordinal).Select(name => cabinet[name]).ToArray(), nested = nested.Select(name => cabinet[name]).ToArray(), oldDoorsAndChildren = oldDoors, explodedViews = exploded, explodedNames = rawNames, explodeSteps = steps });
                        ParametricContext.Require(exploded == 0 && names == 0 && steps == 0, "PREPRUNED_EXPLODED_VIEW_REMAINS");
                    }
                    finally { TemplateRelease(active); }
                }
                finally { TemplateRelease(root); }
                ParametricContext.Require(sw.CloseAllDocuments(true) && sw.GetDocumentCount() == 0, "PREPRUNED_READONLY_CLOSE_FAILED"); Binding.ClosedReferences(sw, "after_readonly67_close", Records);
                Record(new { inspection = "prepruned_readonly67_verified", components = 80, direct = 19, nested = 2, noSave = true, pass = true });
            }
            internal void AfterSession()
            {
                var after = Binding.Inventory(); bool same = Before.Count == after.Count && Before.All(pair => after.ContainsKey(pair.Key) && after[pair.Key] == pair.Value);
                Record(new { inspection = "prepruned_closed_inventory", unchanged = same, protection = Binding.AssertProtected() });
                ParametricContext.Require(same, "PREPRUNED_READONLY_INSPECTION_CHANGED_CAD_BYTES");
            }
        }

        private sealed class LegacyDoorIdentity
        {
            internal readonly string Name, File, Configuration;
            internal readonly bool Remove;
            internal LegacyDoorIdentity(string name, string file, string configuration, bool remove)
            { Name = name; File = file; Configuration = configuration; Remove = remove; }
        }

        private sealed class LegacyDoorPruneAudit
        {
            const string RootFile = "标准寄存柜1917×1000×550(总装配).SLDASM";
            const int MaxComponents = 600, MaxDocuments = 100, MaxFeatures = 12000, MaxDepth = 20;
            readonly string Target;
            readonly List<object> Records;
            readonly Dictionary<string, LegacyDoorIdentity> Expected;
            readonly HashSet<string> Retain, Remove;
            readonly Dictionary<string, string> InitialInventory;
            Dictionary<string, string> SavedInventory;
            string CabinetBaseline, MatesBaseline, ReferencesBaseline;
            bool ReadOnly;

            internal LegacyDoorPruneAudit(List<object> records)
            {
                ParametricContext.Require(ParametricContext.Phase == "envelope", "LEGACY_DOOR_REQUIRES_ENVELOPE");
                Records = records; Target = Find(RootFile);
                var identities = new[] {
                    new LegacyDoorIdentity("标准寄存柜 模型-1", "标准寄存柜 模型.SLDPRT", "Default", false),
                    new LegacyDoorIdentity("箱体横层板L焊接-1", "箱体横层板L焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体横层板L焊接-2", "箱体横层板L焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体横层板R焊接-1", "箱体横层板R焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体横层板R焊接-2", "箱体横层板R焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体左侧板焊接-1", "箱体左侧板焊接.sldasm", "Default", false),
                    new LegacyDoorIdentity("箱体右侧板焊接-1", "箱体右侧板焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体竖隔板L焊接-1", "箱体竖隔板L焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体竖隔板R焊接-1", "箱体竖隔板R焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("上盖焊接-1", "上盖焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("底座焊接-1", "底座焊接.SLDASM", "Default", false),
                    new LegacyDoorIdentity("箱体竖隔板加强件-1", "箱体竖隔板加强件.sldprt", "Default", false),
                    new LegacyDoorIdentity("箱体竖隔板加强件-2", "箱体竖隔板加强件.sldprt", "Default", false),
                    new LegacyDoorIdentity("门框焊接-1", "门框焊接.sldasm", "Default", false),
                    new LegacyDoorIdentity("锁控维护条_源钣金-1", "锁控维护条_源钣金.SLDPRT", "Default", false),
                    new LegacyDoorIdentity("调整脚 M12X60(模型)-5", "调整脚 M12X60(模型).SLDPRT", "默认", false),
                    new LegacyDoorIdentity("调整脚 M12X60(模型)-6", "调整脚 M12X60(模型).SLDPRT", "默认", false),
                    new LegacyDoorIdentity("调整脚 M12X60(模型)-7", "调整脚 M12X60(模型).SLDPRT", "默认", false),
                    new LegacyDoorIdentity("调整脚 M12X60(模型)-8", "调整脚 M12X60(模型).SLDPRT", "默认", false),
                    new LegacyDoorIdentity("储物柜门装配_L2-2", "储物柜门装配_L2.SLDASM", "默认", true),
                    new LegacyDoorIdentity("储物柜门装配_L4-2", "储物柜门装配_L4.SLDASM", "默认", true),
                    new LegacyDoorIdentity("储物柜门装配_L6-2", "储物柜门装配_L6.SLDASM", "默认", true),
                    new LegacyDoorIdentity("储物柜门装配_R2-2", "储物柜门装配_R2.SLDASM", "默认", true),
                    new LegacyDoorIdentity("储物柜门装配_R4-2", "储物柜门装配_R4.SLDASM", "默认", true),
                    new LegacyDoorIdentity("储物柜门装配_R6-2", "储物柜门装配_R6.SLDASM", "默认", true)
                };
                Expected = identities.ToDictionary(i => i.Name, StringComparer.Ordinal);
                Retain = new HashSet<string>(identities.Where(i => !i.Remove).Select(i => i.Name), StringComparer.Ordinal);
                Remove = new HashSet<string>(identities.Where(i => i.Remove).Select(i => i.Name), StringComparer.Ordinal);
                ParametricContext.Require(Expected.Count == 25 && Retain.Count == 19 && Remove.Count == 6 && !Retain.Overlaps(Remove), "LEGACY_DOOR_SEED_SET_INVALID");
                InitialInventory = Inventory();
                Record(new { inspection = "legacy_door_binding", phase = ParametricContext.Phase, planPath = ParametricContext.PlanPath, planSha256 = ParametricContext.PlanHash,
                    root = Target, identities = identities.Select(i => new { name = i.Name, file = i.File, configuration = i.Configuration, remove = i.Remove }).ToArray(), inventory = InitialInventory });
            }
            static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
            static string Text(Dictionary<string, object> row, string key)
            { ParametricContext.Require(row.ContainsKey(key) && row[key] is string, "EXPECTED_STRING " + key); return (string)row[key]; }
            static object[] Values(object value)
            { ParametricContext.Require(value is IEnumerable && !(value is string), "EXPECTED_ARRAY"); return ((IEnumerable)value).Cast<object>().ToArray(); }
            static bool SamePath(string a, string b)
            { return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            static bool IsLocal(string path)
            {
                if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path)) return false;
                ParametricContext.CheckPath(path, ParametricContext.CadDirectory); return System.IO.File.Exists(path);
            }
            void Record(object row)
            {
                Records.Add(row);
                ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", "envelope.legacy-doors.json"), new { phase = "envelope", rows = Records });
            }
            void RootIdentity(ModelDoc2 root, string mode)
            {
                ParametricContext.Require(root != null && root is AssemblyDoc && SamePath(root.GetPathName(), Target) && IsLocal(root.GetPathName()), "LEGACY_ROOT_IDENTITY");
                bool readOnly = root.IsOpenedReadOnly(), viewOnly = root.IsOpenedViewOnly();
                if (mode == "ldr")
                {
                    Record(new { inspection = "legacy_root_identity", mode = mode, path = root.GetPathName(), readOnly = readOnly, viewOnly = viewOnly, configurationDeferred = true });
                    ParametricContext.Require(viewOnly, "LEGACY_ROOT_NOT_LDR"); return;
                }
                Configuration configuration = root.GetActiveConfiguration() as Configuration;
                try
                {
                    string actual = configuration == null ? null : configuration.Name;
                    Record(new { inspection = "legacy_root_identity", mode = mode, path = root.GetPathName(), readOnly = readOnly, viewOnly = viewOnly, expectedConfiguration = "Default", actualConfiguration = actual });
                    ParametricContext.Require(configuration != null && actual == "Default" && !viewOnly && readOnly == (mode == "readonly"), "LEGACY_ROOT_CONFIGURATION_OR_MODE");
                }
                finally { Release(configuration); }
            }
            void Documents(ISldWorks sw, string stage, string mode)
            {
                Array documents = sw.GetDocuments() as Array; int count = documents == null ? 0 : documents.Length, roots = 0;
                var states = new List<object>();
                ParametricContext.Require(count <= MaxDocuments, "LEGACY_DOCUMENT_LIMIT");
                try
                {
                    if (documents != null) foreach (ModelDoc2 document in documents)
                    {
                        string path = document.GetPathName();
                        ParametricContext.Require(IsLocal(path) && !Path.GetFileName(path).StartsWith("储物柜门", StringComparison.Ordinal), "LEGACY_LOADED_DOCUMENT_OUTSIDE_CABINET " + path);
                        bool root = SamePath(path, Target), readOnly = document.IsOpenedReadOnly(); if (root) roots++;
                        states.Add(new { path = path, root = root, readOnly = readOnly, viewOnly = document.IsOpenedViewOnly(), dirty = document.GetSaveFlag() });
                        ParametricContext.Require((root ? (mode == "ldr" || readOnly == (mode == "readonly")) : readOnly) &&
                            (mode != "readonly" || !document.IsOpenedViewOnly()), "LEGACY_DOCUMENT_MODE " + path);
                    }
                }
                finally { if (documents != null) foreach (object document in documents) Release(document); }
                Record(new { inspection = "legacy_documents", stage = stage, mode = mode, count = count, rootCount = roots, documents = states });
                ParametricContext.Require(mode == "empty" ? count == 0 : roots == 1, "LEGACY_DOCUMENT_BASELINE " + stage);
            }
            Dictionary<string, Component2> Direct(ModelDoc2 root, string stage, bool pruned)
            {
                Array components = ((AssemblyDoc)root).GetComponents(true) as Array;
                ParametricContext.Require(components != null && components.Length == (pruned ? 19 : 25), "LEGACY_DIRECT_EXACT_COUNT " + stage);
                var result = new Dictionary<string, Component2>(StringComparer.Ordinal); var states = new List<object>();
                try
                {
                    foreach (Component2 component in components)
                    {
                        string name = component.Name2;
                        ParametricContext.Require(!result.ContainsKey(name) && Expected.ContainsKey(name) && (!pruned || Retain.Contains(name)), "LEGACY_DIRECT_SET " + name);
                        var expected = Expected[name]; var state = ComponentRecord(component);
                        ParametricContext.Require(Path.GetFileName(Text(state, "referencePath")).Equals(expected.File, StringComparison.OrdinalIgnoreCase) &&
                            Text(state, "configuration") == expected.Configuration, "LEGACY_COMPONENT_FILE_OR_CONFIGURATION " + name);
                        if (expected.Remove) ParametricContext.Require(state["loadedPath"] == null && Convert.ToInt32(state["suppression"]) == 0, "LEGACY_DOOR_NOT_UNLOADED_SUPPRESSED " + name);
                        else ParametricContext.Require(IsLocal(Convert.ToString(state["loadedPath"])) && Convert.ToInt32(state["suppression"]) == 2, "LEGACY_RETAINED_NOT_RESOLVED_LOCAL " + name);
                        result.Add(name, component); states.Add(state);
                    }
                    ParametricContext.Require(new HashSet<string>(result.Keys, StringComparer.Ordinal).SetEquals(pruned ? Retain : new HashSet<string>(Expected.Keys, StringComparer.Ordinal)), "LEGACY_DIRECT_EXACT_SET " + stage);
                    Record(new { inspection = "legacy_direct", stage = stage, components = states }); return result;
                }
                catch { foreach (object component in components) Release(component); throw; }
            }
            static void ReleaseComponents(Dictionary<string, Component2> components)
            { if (components != null) foreach (Component2 component in components.Values) Release(component); }
            void SelectionCount(ModelDoc2 root, int expected)
            {
                SelectionMgr selection = root.SelectionManager as SelectionMgr;
                try { ParametricContext.Require(selection != null && selection.GetSelectedObjectCount2(-1) == expected, "LEGACY_SELECTION_COUNT"); }
                finally { Release(selection); }
            }
            Dictionary<string, string> Inventory()
            {
                string[] paths = Directory.GetFiles(ParametricContext.CadDirectory).Where(p => !Path.GetFileName(p).StartsWith("~$", StringComparison.Ordinal) &&
                    (Path.GetExtension(p).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(p).Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase))).ToArray();
                ParametricContext.Require(paths.Length == 75 && Directory.GetDirectories(ParametricContext.CadDirectory).Length == 0, "LEGACY_FLAT_75_INVENTORY");
                var inventory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in paths) { ParametricContext.CheckPath(path, ParametricContext.CadDirectory); inventory.Add(path, ParametricContext.Hash(path)); }
                return inventory;
            }
            void VerifyInventory(Dictionary<string, string> before, string stage, bool allowRoot)
            {
                var after = Inventory(); var changed = before.Where(p => !after.ContainsKey(p.Key) || after[p.Key] != p.Value).Select(p => p.Key).ToArray();
                bool pass = before.Count == after.Count && before.Keys.All(after.ContainsKey) && changed.All(p => allowRoot && SamePath(p, Target));
                Record(new { inspection = "legacy_inventory", stage = stage, rootSaveAllowed = allowRoot, changedPaths = changed, inventory = after, pass = pass });
                ParametricContext.Require(pass, "LEGACY_NON_TARGET_CAD_BYTES_CHANGED " + stage);
            }
            internal void AfterSession(string stage, bool reopened)
            {
                ParametricContext.Require(SavedInventory != null, "LEGACY_SAVE_BASELINE_MISSING");
                VerifyInventory(reopened ? SavedInventory : InitialInventory, stage + "-closed", !reopened);
                Record(new { inspection = "legacy_session_completed", stage = stage, root = Target, readonlyReopen = reopened });
            }
            double[] Numbers(object value)
            {
                if (value == null) return null;
                double[] result = Values(value).Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
                ParametricContext.Require(result.All(v => !double.IsNaN(v) && !double.IsInfinity(v)), "NONFINITE_GEOMETRY_PARAMETER");
                return result;
            }
            Dictionary<string, object> ComponentRecord(Component2 component)
            {
                ModelDoc2 loaded = null; MathTransform transform = null;
                try
                {
                    loaded = component.GetModelDoc2() as ModelDoc2;
                    string referencePath = component.GetPathName(), loadedPath = loaded == null ? null : loaded.GetPathName();
                    ParametricContext.Require(!string.IsNullOrEmpty(referencePath) && Path.IsPathRooted(referencePath), "COMPONENT_REFERENCE_PATH_MISSING " + component.Name2);
                    if (loaded != null) { ParametricContext.Require(IsLocal(loadedPath), "COMPONENT_DOCUMENT_OUTSIDE_COPY " + loadedPath); ParametricContext.Require(((SamePath(loadedPath, Target) && !ReadOnly) || loaded.IsOpenedReadOnly()) && (!ReadOnly || !loaded.IsOpenedViewOnly()), "COMPONENT_DOCUMENT_WRITABLE " + loadedPath); }
                    transform = component.Transform2 as MathTransform;
                    return new Dictionary<string, object> { { "name", component.Name2 }, { "referencePath", referencePath }, { "loadedPath", loadedPath },
                        { "loadedReadOnly", loaded == null ? (object)null : loaded.IsOpenedReadOnly() }, { "configuration", component.ReferencedConfiguration },
                        { "suppression", component.GetSuppression2() }, { "fixedComponent", component.IsFixed() }, { "transform", transform == null ? null : Numbers(transform.ArrayData) } };
                }
                finally { Release(transform); Release(loaded); }
            }
            string StableCabinet(ModelDoc2 root, string stage)
            {
                Record(new { inspection = "cabinet_begin", stage = stage });
                var records = new SortedDictionary<string, object>(StringComparer.Ordinal); Array components = ((AssemblyDoc)root).GetComponents(false) as Array;
                ParametricContext.Require(components != null && components.Length <= MaxComponents, "CABINET_TREE_UNAVAILABLE_OR_LIMIT");
                try
                {
                    foreach (Component2 component in components)
                    {
                        string name = component.Name2;
                        if (Remove.Contains(name)) continue;
                        ParametricContext.Require(!records.ContainsKey(name), "CABINET_DUPLICATE_COMPONENT " + name); records.Add(name, ComponentRecord(component));
                    }
                }
                finally { foreach (object component in components) Release(component); }
                Record(new { inspection = "cabinet_end", stage = stage, count = records.Count }); return ParametricContext.Json.Serialize(records);
            }
            string ContextReferences(ISldWorks sw, string stage)
            {
                Record(new { inspection = "context_begin", stage = stage }); var records = new List<object>();
                foreach (string name in new[] { "箱体左侧板.sldprt", "箱体竖隔板L.sldprt" })
                {
                    ModelDoc2 part = sw.GetOpenDocumentByName(Path.Combine(ParametricContext.CadDirectory, name)) as ModelDoc2;
                    try
                    {
                        ParametricContext.Require(part != null && IsLocal(part.GetPathName()) && part.IsOpenedReadOnly(), "CONTEXT_PART_NOT_LOCAL_READONLY " + name);
                        object files, parts, features, types, statuses, entities, components;
                        part.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
                        records.Add(new { part = name, loadedPath = part.GetPathName(), files = files, parts = parts, features = features, types = types, statuses = statuses, entities = entities, components = components });
                    }
                    finally { Release(part); }
                }
                Record(new { inspection = "context_end", stage = stage, parts = records.Count }); return ParametricContext.Json.Serialize(records);
            }
            object Dimensions(Feature feature)
            {
                var values = new SortedDictionary<string, object>(StringComparer.Ordinal); DisplayDimension display = feature.GetFirstDisplayDimension() as DisplayDimension;
                int count = 0;
                try
                {
                    while (display != null)
                    {
                        ParametricContext.Require(++count <= 64, "MATE_DIMENSION_LIMIT " + feature.Name); Dimension dimension = null; DisplayDimension next = null;
                        try
                        {
                            dimension = display.GetDimension2(0); ParametricContext.Require(dimension != null && !values.ContainsKey(dimension.FullName), "MATE_DIMENSION_UNAVAILABLE_OR_CYCLE " + feature.Name);
                            double value = dimension.SystemValue; ParametricContext.Require(!double.IsNaN(value) && !double.IsInfinity(value), "MATE_DIMENSION_NONFINITE");
                            values.Add(dimension.FullName, new { systemValue = value, drivenState = dimension.DrivenState, type = dimension.GetType() });
                            next = feature.GetNextDisplayDimension(display) as DisplayDimension;
                        }
                        finally { Release(dimension); Release(display); display = next; }
                    }
                }
                finally { Release(display); }
                return values;
            }
            object MateRecord(ModelDoc2 document, Feature feature, string stage)
            {
                object specific = feature.GetSpecificFeature2(); Mate2 mate = specific as Mate2;
                try
                {
                    ParametricContext.Require(mate != null, "MATE2_UNAVAILABLE " + document.GetPathName() + " " + feature.Name);
                    int count = mate.GetMateEntityCount(); ParametricContext.Require(count > 0 && count <= 16, "MATE_ENTITY_COUNT_INVALID " + feature.Name);
                    var entities = new List<object>();
                    for (int i = 0; i < count; i++)
                    {
                        MateEntity2 entity = mate.MateEntity(i); Component2 component = null; object reference = null;
                        try
                        {
                            ParametricContext.Require(entity != null, "MATE_ENTITY_UNAVAILABLE " + feature.Name + " " + i);
                            component = entity.ReferenceComponent; reference = entity.Reference;
                            int size = entity.GetEntityParamsSize(); double[] parameters = Numbers(entity.EntityParams) ?? new double[0];
                            ParametricContext.Require(size >= 0 && size <= 64 && parameters.Length == size, "MATE_ENTITY_PARAMETER_COVERAGE " + feature.Name + " " + i);
                            ParametricContext.Require(reference != null, "MATE_REFERENCE_UNAVAILABLE " + feature.Name + " " + i);
                            ModelDocExtension extension = document.Extension; byte[] persistent = null;
                            try { persistent = extension.GetPersistReference3(reference) as byte[]; }
                            finally { Release(extension); }
                            ParametricContext.Require(persistent != null && persistent.Length > 0 && persistent.Length <= 65536, "MATE_REFERENCE_ID_UNAVAILABLE " + feature.Name + " " + i);
                            entities.Add(new { index = i, referenceType = entity.ReferenceType, referenceType2 = entity.ReferenceType2, parameters = parameters,
                                referenceId = Convert.ToBase64String(persistent), component = component == null ? null : ComponentRecord(component) });
                        }
                        finally { Release(reference); Release(component); Release(entity); }
                    }
                    return new { name = feature.Name, type = feature.GetTypeName2(), error = feature.GetErrorCode(), suppressed = feature.IsSuppressed(),
                        mateType = mate.Type, alignment = mate.Alignment, flipped = mate.Flipped, entityCount = count, entities = entities, dimensions = Dimensions(feature) };
                }
                finally { Release(specific); }
            }
            void MateChildren(ModelDoc2 document, Feature first, SortedDictionary<string, object> records, string stage, int depth, ref int visited)
            {
                ParametricContext.Require(depth <= MaxDepth, "MATE_FEATURE_DEPTH_LIMIT"); Feature feature = first;
                try
                {
                    while (feature != null)
                    {
                        ParametricContext.Require(++visited <= MaxFeatures, "MATE_FEATURE_TRAVERSAL_LIMIT"); Feature next = null;
                        try
                        {
                            string type = feature.GetTypeName2();
                            if (type.StartsWith("Mate", StringComparison.Ordinal) && type != "MateGroup")
                            {
                                ParametricContext.Require(!records.ContainsKey(feature.Name), "MATE_FEATURE_DUPLICATE_OR_CYCLE " + feature.Name);
                                Record(new { inspection = "mate_read", stage = stage, assembly = document.GetPathName(), name = feature.Name, index = records.Count });
                                records.Add(feature.Name, MateRecord(document, feature, stage));
                            }
                            Feature child = feature.GetFirstSubFeature() as Feature;
                            if (child != null) MateChildren(document, child, records, stage, depth + 1, ref visited);
                            next = feature.GetNextSubFeature() as Feature;
                        }
                        finally { Release(feature); feature = next; }
                    }
                }
                finally { Release(feature); }
            }
            string Mates(ISldWorks sw, string stage)
            {
                var records = new SortedDictionary<string, object>(StringComparer.Ordinal); Array documents = sw.GetDocuments() as Array;
                ParametricContext.Require(documents != null && documents.Length <= MaxDocuments, "MATE_DOCUMENT_SET_INVALID");
                try
                {
                    foreach (ModelDoc2 document in documents)
                    {
                        if (!(document is AssemblyDoc)) continue;
                        string path = document.GetPathName(); ParametricContext.Require(IsLocal(path) && (SamePath(path, Target) || document.IsOpenedReadOnly()), "MATE_ASSEMBLY_MODE_OR_PATH " + path);
                        ParametricContext.Require(!records.ContainsKey(path), "DUPLICATE_LOADED_ASSEMBLY " + path);
                        Record(new { inspection = "mates_begin", stage = stage, assembly = path });
                        var mates = new SortedDictionary<string, object>(StringComparer.Ordinal); int visited = 0;
                        Feature feature = document.FirstFeature() as Feature;
                        try
                        {
                            while (feature != null)
                            {
                                ParametricContext.Require(++visited <= MaxFeatures, "ASSEMBLY_FEATURE_TRAVERSAL_LIMIT"); Feature next = null;
                                try
                                {
                                    if (feature.GetTypeName2() == "MateGroup") MateChildren(document, feature.GetFirstSubFeature() as Feature, mates, stage, 1, ref visited);
                                    next = feature.GetNextFeature() as Feature;
                                }
                                finally { Release(feature); feature = next; }
                            }
                        }
                        finally { Release(feature); }
                        records.Add(path, mates); Record(new { inspection = "mates_end", stage = stage, assembly = path, count = mates.Count, visitedFeatures = visited });
                    }
                }
                finally { foreach (object document in documents) Release(document); }
                Record(new { inspection = "all_mates_end", stage = stage, assemblyCount = records.Count }); return ParametricContext.Json.Serialize(records);
            }
            bool SameMates(ISldWorks sw, string before, string after, string stage)
            {
                var previous = ParametricContext.Json.Deserialize<Dictionary<string, object>>(before);
                var current = ParametricContext.Json.Deserialize<Dictionary<string, object>>(after);
                ParametricContext.Require(previous != null && current != null && previous.Count == current.Count && previous.Keys.All(current.ContainsKey), "MATE_ASSEMBLY_SET_CHANGED " + stage);
                int comparedEntities = 0;
                foreach (string path in previous.Keys)
                {
                    var oldMates = previous[path] as Dictionary<string, object>; var newMates = current[path] as Dictionary<string, object>;
                    ParametricContext.Require(oldMates != null && newMates != null && oldMates.Count == newMates.Count && oldMates.Keys.All(newMates.ContainsKey), "MATE_FEATURE_SET_CHANGED " + stage + " " + path);
                    ModelDoc2 document = null; ModelDocExtension extension = null;
                    try
                    {
                        document = sw.GetOpenDocumentByName(path) as ModelDoc2;
                        ParametricContext.Require(document != null && document is AssemblyDoc && SamePath(document.GetPathName(), path) && IsLocal(document.GetPathName()), "MATE_COMPARE_OWNER_UNAVAILABLE " + path);
                        if (SamePath(path, Target)) RootIdentity(document, ReadOnly ? "readonly" : "write");
                        else ParametricContext.Require(document.IsOpenedReadOnly(), "MATE_COMPARE_OWNER_NOT_READONLY " + path);
                        extension = document.Extension; ParametricContext.Require(extension != null, "MATE_COMPARE_EXTENSION_UNAVAILABLE " + path);
                        foreach (string name in oldMates.Keys)
                        {
                            var oldMate = oldMates[name] as Dictionary<string, object>; var newMate = newMates[name] as Dictionary<string, object>;
                            ParametricContext.Require(oldMate != null && newMate != null && oldMate.Count == newMate.Count && oldMate.Keys.All(newMate.ContainsKey), "MATE_FIELDS_CHANGED " + stage + " " + path + " " + name);
                            ParametricContext.Require(Text(oldMate, "name") == name && Text(newMate, "name") == name, "MATE_NAME_KEY_MISMATCH " + name);
                            foreach (string key in oldMate.Keys)
                                if (key != "entities") ParametricContext.Require(ParametricContext.Json.Serialize(oldMate[key]) == ParametricContext.Json.Serialize(newMate[key]), "MATE_METADATA_CHANGED " + stage + " " + path + " " + name + " " + key);
                            object[] oldEntities = Values(oldMate["entities"]), newEntities = Values(newMate["entities"]);
                            ParametricContext.Require(oldEntities.Length > 0 && oldEntities.Length <= 16 && oldEntities.Length == newEntities.Length && oldEntities.Length == Convert.ToInt32(oldMate["entityCount"]) && newEntities.Length == Convert.ToInt32(newMate["entityCount"]), "MATE_COMPARE_ENTITY_COUNT " + name);
                            for (int i = 0; i < oldEntities.Length; i++)
                            {
                                var oldEntity = oldEntities[i] as Dictionary<string, object>; var newEntity = newEntities[i] as Dictionary<string, object>;
                                ParametricContext.Require(oldEntity != null && newEntity != null && oldEntity.Count == newEntity.Count && oldEntity.Keys.All(newEntity.ContainsKey), "MATE_ENTITY_FIELDS_CHANGED " + stage + " " + path + " " + name + " " + i);
                                ParametricContext.Require(Convert.ToInt32(oldEntity["index"]) == i && Convert.ToInt32(newEntity["index"]) == i, "MATE_ENTITY_INDEX_MISMATCH " + name + " " + i);
                                foreach (string key in oldEntity.Keys)
                                    if (key != "referenceId") ParametricContext.Require(ParametricContext.Json.Serialize(oldEntity[key]) == ParametricContext.Json.Serialize(newEntity[key]), "MATE_ENTITY_METADATA_CHANGED " + stage + " " + path + " " + name + " " + i + " " + key);
                                string oldId = Text(oldEntity, "referenceId"), newId = Text(newEntity, "referenceId");
                                byte[] oldBytes = Convert.FromBase64String(oldId), newBytes = Convert.FromBase64String(newId);
                                ParametricContext.Require(oldBytes.Length > 0 && oldBytes.Length <= 65536 && newBytes.Length > 0 && newBytes.Length <= 65536, "MATE_COMPARE_REFERENCE_ID_INVALID " + name + " " + i);
                                int equality;
                                try { equality = extension.IsSamePersistentID(oldBytes, newBytes); }
                                catch (Exception error)
                                {
                                    Record(new { inspection = "mate_persistent_id_comparison", stage = stage, assembly = path, name = name, entityIndex = i,
                                        referenceIdBefore = oldId, referenceIdAfter = newId, result = (int?)null, pass = false, error = error.ToString() });
                                    throw;
                                }
                                Record(new { inspection = "mate_persistent_id_comparison", stage = stage, assembly = path, name = name, entityIndex = i,
                                    referenceIdBefore = oldId, referenceIdAfter = newId, result = equality, pass = equality == 1 });
                                ParametricContext.Require(equality == 1, "MATE_PERSISTENT_ID_NOT_SAME " + stage + " " + path + " " + name + " " + i + " " + equality);
                                comparedEntities++;
                            }
                        }
                    }
                    finally { Release(extension); Release(document); }
                }
                Record(new { inspection = "mate_semantic_comparison", stage = stage, assemblyCount = previous.Count, comparedEntities = comparedEntities, metadataEqual = true, persistentIdsSame = true });
                return true;
            }
            internal void Prune(ISldWorks sw)
            {
                ReadOnly = false; Documents(sw, "empty-prune", "empty");
                sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory); sw.Visible = true; sw.CommandInProgress = false;
                ModelDoc2 root = null;
                try
                {
                    int errors = 0, warnings = 0;
                    Record(new { inspection = "legacy_open_begin", path = Target, options = 4 });
                    root = sw.OpenDoc6(Target, 2, 4, "", ref errors, ref warnings) as ModelDoc2;
                    Record(new { inspection = "legacy_open", mode = "ldr", errors = errors, warnings = warnings, readOnly = root == null ? (bool?)null : root.IsOpenedReadOnly(), viewOnly = root == null ? (bool?)null : root.IsOpenedViewOnly() });
                    ParametricContext.Require(root != null && errors == 0 && (warnings & 1) == 0, "LEGACY_LDR_OPEN_FAILED");
                    RootIdentity(root, "ldr"); Documents(sw, "ldr", "ldr"); root.ClearSelection2(true);
                    foreach (string name in Retain.OrderBy(n => n, StringComparer.Ordinal))
                    {
                        ModelDocExtension extension = root.Extension; bool selected;
                        try { selected = extension.SelectByID2(name + "@" + Path.GetFileNameWithoutExtension(Target), "COMPONENT", 0, 0, 0, true, 0, null, 0); }
                        finally { Release(extension); }
                        Record(new { inspection = "legacy_select_cabinet", name = name, selected = selected });
                        ParametricContext.Require(selected, "LEGACY_CABINET_SELECTION_FAILED " + name);
                    }
                    SelectionCount(root, 19); bool opened = ((AssemblyDoc)root).SelectiveOpen(true, false);
                    Record(new { inspection = "legacy_selective_open", result = opened }); ParametricContext.Require(opened, "LEGACY_SELECTIVE_OPEN_FAILED");
                    RootIdentity(root, "write"); Documents(sw, "after-selective", "write"); sw.CommandInProgress = true;
                    Dictionary<string, Component2> direct = Direct(root, "before-prune", false); ReleaseComponents(direct);
                    string cabinet = StableCabinet(root, "before-prune"), mates = Mates(sw, "before-prune"), references = ContextReferences(sw, "before-prune");
                    Record(new { inspection = "legacy_before_prune", cabinet = cabinet, mates = mates, references = references });
                    RootIdentity(root, "write"); Documents(sw, "immediately-before-prune", "write"); ParametricContext.AssertMutation(Target);
                    direct = Direct(root, "immediately-before-prune", false);
                    try
                    {
                        root.ClearSelection2(true);
                        foreach (string name in Remove.OrderBy(n => n, StringComparer.Ordinal))
                        {
                            ModelDoc2 loaded = direct[name].GetModelDoc2() as ModelDoc2;
                            try { ParametricContext.Require(loaded == null && direct[name].GetSuppression2() == 0, "LEGACY_DELETE_REQUIRES_UNLOADED_SUPPRESSED " + name); }
                            finally { Release(loaded); }
                            ParametricContext.Require(direct[name].Select4(true, null, false), "LEGACY_DELETE_SELECTION_FAILED " + name);
                        }
                        SelectionCount(root, 6); ModelDocExtension extension = root.Extension; bool deleted;
                        try { deleted = extension.DeleteSelection2(0); } finally { Release(extension); }
                        root.ClearSelection2(true); Record(new { inspection = "legacy_delete", selected = 6, deleted = deleted, names = Remove.OrderBy(n => n, StringComparer.Ordinal).ToArray() });
                        ParametricContext.Require(deleted, "LEGACY_DELETE_FAILED");
                    }
                    finally { ReleaseComponents(direct); }
                    RootIdentity(root, "write"); Documents(sw, "after-prune", "write"); direct = Direct(root, "after-prune", true); ReleaseComponents(direct);
                    string afterCabinet = StableCabinet(root, "after-prune"), afterMates = Mates(sw, "after-prune"), afterReferences = ContextReferences(sw, "after-prune");
                    Record(new { inspection = "legacy_prune_comparison", cabinetEqual = cabinet == afterCabinet, matesEqual = mates == afterMates, referencesEqual = references == afterReferences,
                        cabinet = afterCabinet, mates = afterMates, references = afterReferences });
                    ParametricContext.Require(cabinet == afterCabinet && mates == afterMates && references == afterReferences, "LEGACY_NON_TARGET_PRUNE_CHANGE");
                    VerifyInventory(InitialInventory, "before-rebuild", false); RootIdentity(root, "write"); Documents(sw, "before-rebuild", "write"); ParametricContext.AssertMutation(Target);
                    bool rebuilt = root.ForceRebuild3(false);
                    RootIdentity(root, "write"); Documents(sw, "rebuilt", "write"); direct = Direct(root, "rebuilt", true); ReleaseComponents(direct);
                    afterCabinet = StableCabinet(root, "rebuilt"); afterMates = Mates(sw, "rebuilt"); afterReferences = ContextReferences(sw, "rebuilt");
                    bool sameMates = SameMates(sw, mates, afterMates, "rebuilt");
                    Record(new { inspection = "legacy_rebuild_comparison", rebuilt = rebuilt, cabinetEqual = cabinet == afterCabinet, matesEqual = sameMates, referencesEqual = references == afterReferences,
                        rawMateJsonEqual = mates == afterMates, cabinet = afterCabinet, mates = afterMates, references = afterReferences });
                    ParametricContext.Require(rebuilt && cabinet == afterCabinet && sameMates && references == afterReferences, "LEGACY_NON_TARGET_REBUILD_CHANGE");
                    VerifyInventory(InitialInventory, "immediately-before-save", false); RootIdentity(root, "write"); Documents(sw, "immediately-before-save", "write"); ParametricContext.AssertMutation(Target);
                    int saveErrors = 0, saveWarnings = 0; bool saved = GuardedSave3(root, 1, ref saveErrors, ref saveWarnings);
                    Record(new { inspection = "legacy_save", saved = saved, errors = saveErrors, warnings = saveWarnings, path = root.GetPathName(), dirty = root.GetSaveFlag() });
                    ParametricContext.Require(saved && saveErrors == 0 && saveWarnings == 0 && !root.GetSaveFlag(), "LEGACY_ROOT_SAVE_FAILED");
                    RootIdentity(root, "write"); Documents(sw, "saved", "write"); direct = Direct(root, "saved", true); ReleaseComponents(direct);
                    CabinetBaseline = StableCabinet(root, "saved"); MatesBaseline = Mates(sw, "saved"); ReferencesBaseline = ContextReferences(sw, "saved");
                    sameMates = SameMates(sw, mates, MatesBaseline, "saved");
                    Record(new { inspection = "legacy_save_comparison", cabinetEqual = cabinet == CabinetBaseline, matesEqual = sameMates, referencesEqual = references == ReferencesBaseline,
                        rawMateJsonEqual = mates == MatesBaseline, cabinet = CabinetBaseline, mates = MatesBaseline, references = ReferencesBaseline });
                    ParametricContext.Require(cabinet == CabinetBaseline && sameMates && references == ReferencesBaseline, "LEGACY_NON_TARGET_SAVE_CHANGE");
                    VerifyInventory(InitialInventory, "saved", true); SavedInventory = Inventory();
                }
                finally { Release(root); }
            }
            internal void Reopen(ISldWorks sw)
            {
                ParametricContext.Require(SavedInventory != null && CabinetBaseline != null && MatesBaseline != null && ReferencesBaseline != null, "LEGACY_REOPEN_BASELINE_MISSING");
                ReadOnly = true; Documents(sw, "empty-reopen", "empty"); VerifyInventory(SavedInventory, "before-reopen", false);
                sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory); sw.Visible = true; sw.CommandInProgress = false;
                ModelDoc2 root = null;
                try
                {
                    int errors = 0, warnings = 0; root = sw.OpenDoc6(Target, 2, 3, "", ref errors, ref warnings) as ModelDoc2;
                    Record(new { inspection = "legacy_open", mode = "readonly", errors = errors, warnings = warnings, readOnly = root == null ? (bool?)null : root.IsOpenedReadOnly(), viewOnly = root == null ? (bool?)null : root.IsOpenedViewOnly() });
                    ParametricContext.Require(root != null && errors == 0 && (warnings & 1) == 0, "LEGACY_READONLY_REOPEN_FAILED");
                    RootIdentity(root, "readonly"); Documents(sw, "readonly-reopened", "readonly"); sw.CommandInProgress = true;
                    Dictionary<string, Component2> direct = Direct(root, "readonly-reopened", true); ReleaseComponents(direct);
                    string cabinet = StableCabinet(root, "readonly-reopened"), mates = Mates(sw, "readonly-reopened"), references = ContextReferences(sw, "readonly-reopened");
                    bool sameMates = SameMates(sw, MatesBaseline, mates, "readonly-reopened");
                    Record(new { inspection = "legacy_reopen_comparison", cabinetEqual = CabinetBaseline == cabinet, matesEqual = sameMates, referencesEqual = ReferencesBaseline == references,
                        rawMateJsonEqual = MatesBaseline == mates, cabinet = cabinet, mates = mates, references = references });
                    ParametricContext.Require(CabinetBaseline == cabinet && sameMates && ReferencesBaseline == references, "LEGACY_READONLY_REOPEN_CHANGE");
                    RootIdentity(root, "readonly"); Documents(sw, "readonly-verified", "readonly"); VerifyInventory(SavedInventory, "readonly-verified", false);
                }
                finally { Release(root); }
            }
        }

        private static void RunHeightBraceSessions(string phase, List<object> records)
        {
            Action<ISldWorks> verifyRight = null;
            Action<ISldWorks> verifyMaintenance = null;
            NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw) {
                RequireEmptyDocuments(sw, "partition-write", records);
                sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory);
                verifyRight = RepairHeightBraces(sw, records);
            }, "partition-write");
            NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw) {
                RequireEmptyDocuments(sw, "maintenance-write", records);
                sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory);
                verifyMaintenance = NativeParametricWidth.RepairMaintenanceStrip(sw, Find("锁控维护条_源钣金.SLDPRT"), records);
            }, "maintenance-write");
            NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw) {
                RequireEmptyDocuments(sw, "placement-write", records);
                sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory);
                RepairHeightBracePlacements(sw, records);
            }, "placement-write");
            ParametricContext.Require(verifyRight != null && verifyMaintenance != null, "HEIGHT_BRACE_VERIFICATION_MISSING");
            ReadOnlyInspection = true;
            try {
                NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw) {
                    RequireEmptyDocuments(sw, "readonly", records);
                    sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory);
                    verifyRight(sw);
                    verifyMaintenance(sw);
                }, "readonly");
            } finally { ReadOnlyInspection = false; }
        }

        private static void RequireEmptyDocuments(ISldWorks sw, string mode, List<object> records)
        {
            Array documents = sw.GetDocuments() as Array;
            int count = documents == null ? 0 : documents.Length;
            records.Add(new { inspection = "height_brace_session_boundary", mode = mode, processId = sw.GetProcessID(), documentCount = count });
            ParametricContext.Require(count == 0, "HEIGHT_BRACE_SESSION_NOT_EMPTY " + mode);
        }

        private static void CloseReadOnlyInspectionDocuments(ISldWorks sw, string boundary)
        {
            ParametricContext.Require(ReadOnlyInspection, "DEPENDENCY_CLOSE_REQUIRES_READONLY_INSPECTION " + boundary);
            Array documents = sw.GetDocuments() as Array;
            var before = new List<object>();
            if (documents != null) foreach (ModelDoc2 document in documents) {
                string path = document.GetPathName();
                ParametricContext.CheckPath(path, ParametricContext.AttemptDirectory);
                before.Add(new { path = path, readOnly = document.IsOpenedReadOnly() });
            }
            bool closed = documents == null || documents.Length == 0 || sw.CloseAllDocuments(true);
            Array remaining = sw.GetDocuments() as Array;
            int count = remaining == null ? 0 : remaining.Length;
            DocumentStates.Add(new { operation = "readonly_dependency_boundary", boundary = boundary, before = before,
                closeSucceeded = closed, afterDocumentCount = count, isolatedReadOnlyInspection = ReadOnlyInspection });
            ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".document-state.json"), DocumentStates);
            ParametricContext.Require(closed && count == 0, "READONLY_DEPENDENCY_CLOSE_INCOMPLETE " + boundary + " " + count);
        }

        internal static ModelDoc2 Open(ISldWorks sw, string path, bool readOnly)
        {
            ParametricContext.CheckPath(path, ParametricContext.AttemptDirectory);
            ParametricContext.Require(!ReadOnlyInspection || readOnly, "WRITABLE_OPEN_DURING_READONLY_INSPECTION " + path);
            ModelDoc2 loaded = sw.GetOpenDocumentByName(path) as ModelDoc2;
            bool? loadedReadOnly = loaded == null ? (bool?)null : loaded.IsOpenedReadOnly();
            if (!readOnly) ParametricContext.Require(loadedReadOnly != true, "WRITABLE_OPEN_REUSES_READONLY_DOCUMENT " + path);
            int errors = 0, warnings = 0;
            int type = Path.GetExtension(path).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            ModelDoc2 model = sw.OpenDoc6(path, type, 1 | (readOnly ? 2 : 0), "", ref errors, ref warnings) as ModelDoc2;
            bool? actualReadOnly = model == null ? (bool?)null : model.IsOpenedReadOnly();
            DocumentStates.Add(new { operation = "open", path = path, requestedReadOnly = readOnly, loadedReadOnly = loadedReadOnly, actualReadOnly = actualReadOnly, errors = errors, warnings = warnings, isolatedReadOnlyInspection = ReadOnlyInspection });
            ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".document-state.json"), DocumentStates);
            ParametricContext.Require(model != null && errors == 0, "OPEN_FAILED " + path + " " + errors + "/" + warnings);
            ParametricContext.Require((warnings & 1) == 0, "OPEN_DOCUMENT_IDENTITY_MISMATCH " + path + " " + warnings);
            ParametricContext.Require(readOnly || actualReadOnly == false, "WRITABLE_OPEN_ACTUAL_READONLY " + path + " " + warnings);
            ParametricContext.Require(!ReadOnlyInspection || actualReadOnly == true, "READONLY_INSPECTION_ACTUAL_WRITABLE " + path);
            ParametricContext.Require((warnings & 128) == 0 || actualReadOnly == readOnly, "OPEN_MODE_WARNING_MISMATCH " + path + " " + warnings);
            ParametricContext.Require(string.Equals(Path.GetFullPath(model.GetPathName()), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase), "OPEN_PATH_MISMATCH " + path);
            return model;
        }

        internal static void Save(ModelDoc2 model)
        {
            ParametricContext.Require(!ReadOnlyInspection && !model.IsOpenedReadOnly(), "SAVE_REQUIRES_WRITABLE_DOCUMENT " + model.GetPathName());
            ParametricContext.AssertMutation(model.GetPathName());
            int errors = 0, warnings = 0;
            bool rebuilt=model.ForceRebuild3(false);
            if(!rebuilt){
                var issues=new List<object>();
                Action<ModelDoc2> capture=delegate(ModelDoc2 document){
                    Feature feature=document.FirstFeature() as Feature;
                    while(feature!=null){
                        if(!feature.IsSuppressed()&&feature.GetErrorCode()>0)issues.Add(new{path=document.GetPathName(),feature=feature.Name,type=feature.GetTypeName2(),error=feature.GetErrorCode()});
                        Feature child=feature.GetFirstSubFeature() as Feature;
                        while(child!=null){if(!child.IsSuppressed()&&child.GetErrorCode()>0)issues.Add(new{path=document.GetPathName(),feature=child.Name,type=child.GetTypeName2(),error=child.GetErrorCode()});child=child.GetNextSubFeature() as Feature;}
                        feature=feature.GetNextFeature() as Feature;
                    }
                };
                capture(model);var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);AssemblyDoc assembly=model as AssemblyDoc;
                if(assembly!=null)foreach(Component2 component in (Array)assembly.GetComponents(false)){
                    ModelDoc2 document=component.GetModelDoc2() as ModelDoc2;if(document!=null&&paths.Add(document.GetPathName()))capture(document);
                }
                Console.WriteLine("REBUILD_FAILURE_FEATURES "+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(issues));
            }
            ParametricContext.Require(rebuilt, "REBUILD_FAILED " + model.GetPathName());
            ParametricContext.Require(!model.IsOpenedReadOnly(), "SAVE_REBUILD_CHANGED_READONLY_STATE " + model.GetPathName());
            ParametricContext.Require(GuardedSave3(model, 1, ref errors, ref warnings) && errors == 0 && warnings == 0,
                "SAVE_FAILED " + model.GetPathName() + " " + errors + "/" + warnings);
        }

        private static string Find(string name) { if(name.Equals("箱体竖隔板R.SLDPRT",StringComparison.OrdinalIgnoreCase)) name=NativeParametricLocks.CurrentRightName; return Directory.GetFiles(ParametricContext.CadDirectory).Single(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase)); }

        private static void Relink(ISldWorks sw, string directory, List<object> rows)
        {
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(path => new[] { ".SLDASM", ".SLDPRT" }.Contains(Path.GetExtension(path).ToUpperInvariant())).ToList();
            var available = new List<string>(files);
            available.AddRange(Directory.GetFiles(ParametricContext.CadDirectory).Where(path => new[] { ".SLDASM", ".SLDPRT" }.Contains(Path.GetExtension(path).ToUpperInvariant())));
            string doorRoot = Path.Combine(ParametricContext.AttemptDirectory, "doors");
            if (Directory.Exists(doorRoot)) available.AddRange(Directory.GetFiles(doorRoot, "*", SearchOption.AllDirectories).Where(path => new[] { ".SLDASM", ".SLDPRT" }.Contains(Path.GetExtension(path).ToUpperInvariant())));
            var local = available.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(Path.GetFileName, path => path, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                ParametricContext.AssertMutation(file);
                Array dependencies = sw.GetDocumentDependencies2(file, false, false, false) as Array;
                if (dependencies == null) continue;
                var replacements = new List<object>();
                for (int i = 1; i < dependencies.Length; i += 2)
                {
                    string old = Convert.ToString(dependencies.GetValue(i));
                    string target;
                    if (!local.TryGetValue(Path.GetFileName(old), out target)) throw new InvalidOperationException("CLONE_REFERENCE_NOT_IN_PACK " + old);
                    if (old.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;
                    ParametricContext.AssertMutation(target); bool replaced;
                    try { replaced = sw.ReplaceReferencedDocument(file, old, target); }
                    finally { AssertProtectedTemplateInputs(); }
                    replacements.Add(new { source = old, target = target, replaced = replaced });
                    ParametricContext.Require(replaced, "CLONE_REFERENCE_REPLACE_FAILED " + file + " " + old);
                }
                rows.Add(new { file = file, replacements = replacements });
            }
            PreprunedTemplateBinding binding = GetTemplateBinding();
            if (binding != null && TemplateSamePath(directory, ParametricContext.CadDirectory))
            {
                binding.ClosedReferences(sw, "clone_relinked_closed", rows);
                rows.Add(new { inspection = "prepruned_clone_protection", protection = binding.AssertProtected() });
            }
        }

        private static void Probe(ISldWorks sw, List<object> rows,string[] names=null)
        {
            foreach (string name in names??new[] { "标准寄存柜 模型.SLDPRT", "箱体竖隔板L.sldprt", "箱体竖隔板R.SLDPRT", "箱体右侧板.sldprt", "底座加强筋.sldprt" })
            {
                string path = Find(name);
                ModelDoc2 model = Open(sw, path, true);
                try
                {
                    var features = new List<object>();
                    Feature feature = model.FirstFeature() as Feature;
                    while (feature != null)
                    {
                        var dimensions = new List<object>();
                        DisplayDimension display = feature.GetFirstDisplayDimension() as DisplayDimension;
                        int guard = 0;
                        while (display != null && guard++ < 300)
                        {
                            Dimension dimension = display.GetDimension2(0) as Dimension;
                            if (dimension != null) dimensions.Add(new { name = dimension.FullName, value = dimension.SystemValue * 1000, driven = dimension.DrivenState });
                            display = feature.GetNextDisplayDimension(display) as DisplayDimension;
                        }
                        features.Add(new { name = feature.Name, type = feature.GetTypeName2(), error = feature.GetErrorCode(), suppressed = feature.IsSuppressed(), dimensions = dimensions });
                        feature = feature.GetNextFeature() as Feature;
                    }
                    object referenceFiles, referenceParts, referenceFeatures, referenceTypes, referenceStatuses, referenceEntities, referenceComponents;
                    model.ListExternalFileReferences2(out referenceFiles, out referenceParts, out referenceFeatures, out referenceTypes, out referenceStatuses, out referenceEntities, out referenceComponents);
                    rows.Add(new { path = path, features = features, box = BodyBox(model), referenceFiles = referenceFiles, referenceFeatures = referenceFeatures, referenceStatuses = referenceStatuses, referenceEntities = referenceEntities, referenceComponents = referenceComponents });
                    if(names!=null){
                        var cylinders=new List<object>();
                        foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                            Surface surface=(Surface)face.GetSurface();if(surface.IsCylinder()){Feature owner=face.GetFeature() as Feature;cylinders.Add(new{feature=owner==null?null:owner.Name,parameters=surface.CylinderParams});}
                        }
                        rows.Add(new{path=path,cylinders=cylinders});
                        var faces=new List<object>();
                        foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                            Surface surface=(Surface)face.GetSurface();if(!surface.IsPlane())continue;
                            var loops=new List<object>();
                            foreach(Loop2 loop in (Array)face.GetLoops()){
                                var vertices=new List<double[]>();foreach(Edge edge in (Array)loop.GetEdges()){
                                    Vertex vertex=edge.GetStartVertex() as Vertex;if(vertex!=null)vertices.Add(((double[])vertex.GetPoint()).Select(x=>x*1000).ToArray());
                                }
                                loops.Add(new{outer=loop.IsOuter(),verticesMm=vertices});
                            }
                            Feature owner=face.GetFeature() as Feature;faces.Add(new{feature=owner==null?null:owner.Name,plane=surface.PlaneParams,box=face.GetBox(),loops=loops});
                        }
                        rows.Add(new{path=path,planarFaces=faces});
                    }
                }
                finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            }
        }

        private static void BuildRoot(ISldWorks sw, List<object> rows)
        {
            var contract = ParametricContext.Object(ParametricContext.Plan, "contract");
            var rowMap = ParametricContext.Object(contract, "rowsByColumn");
            var boundaryMap = ParametricContext.Object(contract, "boundaries");
            ModelDoc2 frame = Open(sw, Find("门框焊接.sldasm"), false);
            try
            {
                int activateError = 0; sw.ActivateDoc3(frame.GetTitle(), false, 0, ref activateError);
                DeleteDirect(frame, component => Path.GetFileName(component.GetPathName()).Equals("门框 横隔板.sldprt", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(component.GetPathName()).Equals("门框 横隔板R.SLDPRT", StringComparison.OrdinalIgnoreCase), rows);
                foreach (string side in new[] { "L", "R" })
                    foreach (Dictionary<string, object> boundary in (IEnumerable)boundaryMap[side])
                        AddAt(sw, frame, Find(side == "L" ? "门框 横隔板.sldprt" : "门框 横隔板R.SLDPRT"), "crossbar_" + side + "_" + boundary["index"], 0, ParametricContext.Number(boundary, "crossbarCenterYmm") - 1700-NativeParametricWidth.FrameGridHeightTranslation(ParametricContext.HeightMm), 0, true);
                Save(frame);
            }
            finally { sw.CloseDoc(frame.GetTitle()); Marshal.ReleaseComObject(frame); }
            string root = Find("标准寄存柜1917×1000×550(总装配).SLDASM");
            ModelDoc2 model = Open(sw, root, false);
            try
            {
                int activateError = 0; sw.ActivateDoc3(model.GetTitle(), false, 0, ref activateError);
                DeleteDirect(model, component => Path.GetFileName(component.GetPathName()).StartsWith("储物柜门装配_", StringComparison.Ordinal) || Path.GetFileName(component.GetPathName()).Equals("箱体横层板L焊接.SLDASM", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(component.GetPathName()).Equals("箱体横层板R焊接.SLDASM", StringComparison.OrdinalIgnoreCase), rows);
                foreach (string side in new[] { "L", "R" })
                {
                    foreach (Dictionary<string, object> row in (IEnumerable)rowMap[side])
                    {
                        string height = ParametricContext.Number(row, "doorHeightMm").ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p');
                        string size = "W" + ParametricContext.DoorWidthMm.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p') + "_H" + height;
                        string target = Path.Combine(ParametricContext.AttemptDirectory, "doors", "H" + height, "assemblies", "ordinary_door_" + (side == "L" ? "left" : "right") + "_" + size + ".SLDASM");
                        ParametricContext.Require(File.Exists(target), "DOOR_MODULE_MISSING " + target);
                        AddAt(sw, model, target, "door_" + side + "_" + row["index"], (side == "L" ? -1 : 1) * (ParametricContext.WidthMm / 4 + 8.5), ParametricContext.Number(row, "centerYmm"), 0, false);
                    }
                    foreach (Dictionary<string, object> boundary in (IEnumerable)boundaryMap[side])
                        AddAt(sw, model, Find("箱体横层板" + side + "焊接.SLDASM"), "shelf_" + side + "_" + boundary["index"], 0, ParametricContext.Number(boundary, "shelfCenterYmm") - 1705-NativeParametricWidth.FrameGridHeightTranslation(ParametricContext.HeightMm), 0, true);
                }
                Save(model);
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
        }

        private static void RepairInterfaces(ISldWorks sw, List<object> rows)
        {
            ModelDoc2 model = Open(sw, Find("箱体右侧板.sldprt"), false);
            try
            {
                Feature feature = model.FirstFeature() as Feature;
                while (feature != null && feature.Name != "草图19") feature = feature.GetNextFeature() as Feature;
                ParametricContext.Require(feature != null, "RIGHT_SIDE_INTERFACE_SKETCH_MISSING");
                Sketch sketch = feature.GetSpecificFeature2() as Sketch;
                SketchRelationManager manager = sketch.RelationManager;
                Array relations = manager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                double[] before = BodyBox(model);
                object referenceFiles, referenceParts, referenceFeatures, referenceTypes, referenceStatuses, referenceEntities, referenceComponents;
                model.ListExternalFileReferences2(out referenceFiles, out referenceParts, out referenceFeatures, out referenceTypes, out referenceStatuses, out referenceEntities, out referenceComponents);
                if (relations != null && relations.Length > 0)
                {
                    ParametricContext.Require(relations.Length == 2 && ((Array)referenceEntities).Cast<object>().Count(value => Convert.ToString(value).Contains("衣杆固定支架")) == 2, "RIGHT_SIDE_INTERFACE_REFERENCE_DRIFT");
                    foreach (SketchRelation relation in relations) ParametricContext.Require(manager.DeleteRelation(relation), "RIGHT_SIDE_INTERFACE_DETACH_FAILED");
                }
                Save(model);
                double[] after = BodyBox(model);
                ParametricContext.Require(before.Zip(after, (a,b) => Math.Abs(a-b) < .001).All(value => value), "RIGHT_SIDE_INTERFACE_GEOMETRY_CHANGED");
                ParametricContext.Require(manager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal) == 0, "RIGHT_SIDE_INTERFACE_EXTERNAL_CONSTRAINT_REMAINS");
                rows.Add(new { part = model.GetPathName(), removedReferences = relations == null ? 0 : relations.Length, before = before, after = after, originalReferencedEntities = referenceEntities });
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            foreach (string name in new[] { "箱体右侧板焊接.SLDASM", "标准寄存柜1917×1000×550(总装配).SLDASM" })
            {
                model = Open(sw, Find(name), false);
                try { Save(model); rows.Add(new { rebuilt = name }); }
                finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            }
        }

        private static void DeleteDirect(ModelDoc2 model, Func<Component2, bool> predicate, List<object> rows)
        {
            ParametricContext.AssertMutation(model.GetPathName());
            var targets = ((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().Where(predicate).ToList();
            ParametricContext.Require(targets.Count > 0, "PARAMETRIC_COMPONENT_REPLACEMENT_TARGET_MISSING");
            var names = targets.Select(component => component.Name2).ToList();
            foreach (Component2 target in targets) if (target.IsSuppressed()) target.SetSuppression2((int)swComponentSuppressionState_e.swComponentResolved);
            model.ClearSelection2(true);
            foreach (Component2 target in targets)
            {
                ParametricContext.Require(target.Select4(true, null, false), "PARAMETRIC_COMPONENT_DELETE_SELECT_FAILED");
            }
            int selected = ((SelectionMgr)model.SelectionManager).GetSelectedObjectCount2(-1);
            ParametricContext.Require(selected == targets.Count, "PARAMETRIC_DELETE_SELECTION_COUNT_MISMATCH");
            bool deleted = model.Extension.DeleteSelection2(0); model.ClearSelection2(true);
            var remaining = ((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().Select(component => component.Name2).ToList();
            rows.Add(new { assembly = model.GetPathName(), requested = names, selected = selected, deleted = deleted, remaining = remaining });
            ParametricContext.Require(names.All(name => !remaining.Contains(name)), "PARAMETRIC_COMPONENT_DELETE_FAILED");
            rows.Add(new { assembly = model.GetPathName(), removedDirectInstances = names });
        }

        private static void AddAt(ISldWorks sw, ModelDoc2 model, string path, string role, double x, double y, double z, bool fixedRequired)
        {
            ParametricContext.AssertMutation(model.GetPathName());
            ModelDoc2 componentModel = Open(sw, path, true);
            int errors = 0;
            sw.ActivateDoc2(model.GetTitle(), false, ref errors);
            AssemblyDoc assembly = model as AssemblyDoc;
            Component2 component = assembly.AddComponent5(path, 0, "", false, "", x / 1000, y / 1000, z / 1000) as Component2;
            ParametricContext.Require(component != null, "PARAMETRIC_COMPONENT_ADD_FAILED " + role);
            component.Name2 = role;
            MathUtility math = sw.GetMathUtility() as MathUtility;
            MathTransform transform = math.CreateTransform(new double[] { 1,0,0,0,1,0,0,0,1,x/1000,y/1000,z/1000,1,0,0,0 }) as MathTransform;
            model.ClearSelection2(true);
            if (component.IsFixed()) { component.Select4(false, null, false); assembly.UnfixComponent(); }
            ParametricContext.Require(component.SetTransformAndSolve2(transform), "PARAMETRIC_PLACEMENT_FAILED " + role);
            if (fixedRequired) { component.Select4(false, null, false); assembly.FixComponent(); }
            double[] actual = component.Transform2.ArrayData as double[];
            ParametricContext.Require(actual != null && Math.Abs(actual[9] * 1000 - x) < .001 && Math.Abs(actual[10] * 1000 - y) < .001 && Math.Abs(actual[11] * 1000 - z) < .001, "PARAMETRIC_TRANSFORM_READBACK_FAILED " + role);
            model.ClearSelection2(true);
            Marshal.ReleaseComObject(transform); Marshal.ReleaseComObject(math); Marshal.ReleaseComObject(component);
            sw.CloseDoc(componentModel.GetTitle()); Marshal.ReleaseComObject(componentModel);
        }

        private static void VerifyRoot(ISldWorks sw, List<object> rows)
        {
            string root = Find("标准寄存柜1917×1000×550(总装配).SLDASM");
            string before = ParametricContext.Hash(root);
            ModelDoc2 model = Open(sw, root, true);
            try
            {
                AssemblyDoc assembly = model as AssemblyDoc;
                foreach (Component2 component in (Array)assembly.GetComponents(false))
                {
                    string path = component.GetPathName();
                    if (!string.IsNullOrEmpty(path)) ParametricContext.CheckPath(path, ParametricContext.AttemptDirectory);
                    MathTransform transform = component.Transform2;
                    rows.Add(new { name = component.Name2, path = path, suppressed = component.IsSuppressed(), transform = transform == null ? null : transform.ArrayData });
                }
                model.ShowNamedView2("*Isometric", 7);
                model.ViewZoomtofit2();
                string preview = Path.Combine(ParametricContext.AttemptDirectory, "evidence", "isometric.bmp");
                ParametricContext.AssertMutation(preview);
                ParametricContext.Require(model.SaveBMP(preview, 1280, 960), "PREVIEW_SAVE_FAILED");
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            ParametricContext.Require(ParametricContext.Hash(root) == before, "READONLY_ROOT_CHANGED");
        }

        internal static double[] BodyBox(ModelDoc2 model)
        {
            PartDoc part = model as PartDoc;
            Array bodies = part.GetBodies2(0, false) as Array;
            double[] box = { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
            foreach (Body2 body in bodies)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    double x, y, z;
                    body.GetExtremePoint(axis == 0 ? -1 : 0, axis == 1 ? -1 : 0, axis == 2 ? -1 : 0, out x, out y, out z);
                    box[axis] = Math.Min(box[axis], new[] { x, y, z }[axis] * 1000);
                    body.GetExtremePoint(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0, out x, out y, out z);
                    box[axis + 3] = Math.Max(box[axis + 3], new[] { x, y, z }[axis] * 1000);
                }
            }
            return box;
        }
    }
}

namespace Winnsen.StructureAgent.NativeDoorModule888x14
{
    internal static partial class NativeParametricDoor
    {
        private static bool sessionPreviousUpdateNames;
        private static string SizeToken { get { return "W" + TargetDoorWidthMm.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p') + "_H" + TargetDoorHeightMm.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p'); } }
        private static object NativeRootFeatureNames(ModelDoc2 model)
        {
            var rows = new List<object>();
            Feature feature = model.FirstFeature() as Feature;
            while (feature != null) { rows.Add(new { name = feature.Name, type = feature.GetTypeName2(), error = feature.GetErrorCode() }); feature = feature.GetNextFeature() as Feature; }
            return rows;
        }
        private static object NativeComponentDiagnostics(AssemblyDoc assembly)
        {
            var rows = new List<object>();
            foreach (Component2 component in (Array)assembly.GetComponents(true))
                rows.Add(new { name = component.Name2, path = component.GetPathName(), hash = Sha256(component.GetPathName()), transform = component.Transform2.ArrayData, fixedState = component.IsFixed() });
            return rows;
        }
        private static void StabilizeCopiedHardware(ISldWorks sw, BuildPaths paths)
        {
            foreach (string path in new[] { paths.latch_plate, paths.hook_pad, paths.bushing, paths.hinge_pin, paths.circlip, paths.lock_tongue })
            {
                int errors = 0, warnings = 0;
                ModelDoc2 model = OpenDocument(sw, path, 1, false, ref errors, ref warnings);
                try
                {
                    Require(model != null && errors == 0 && (warnings == 0 || warnings == 32), "HARDWARE_COPY_OPEN_FAILED", 21, path);
                    PartHealth before = CapturePartHealth(model);
                    Require(model.ForceRebuild3(false), "HARDWARE_COPY_REBUILD_FAILED", 21, path);
                    PartHealth after = CapturePartHealth(model);
                    Require(before.body_count == after.body_count && after.traversal_complete && after.error_feature_count == 0 && after.warning_feature_count == 0 && PartBboxGate(after, before.x_length_mm, before.y_length_mm) && Math.Abs(after.z_length_mm - before.z_length_mm) < .001, "HARDWARE_COPY_GEOMETRY_CHANGED", 21, path);
                    SaveModel(model, "copied hardware");
                }
                finally { CloseDocument(sw, ref model); }
            }
        }
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
        private static void InspectAndDetachCopiedPanelReferences(ModelDoc2 model)
        {
            Winnsen.StructureAgent.Generation.ParametricContext.AssertMutation(model.GetPathName());
            if (ExternalReferenceCount(model) == 0) return;
            object files, parts, features, types, statuses, entities, components;
            model.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
            Console.WriteLine("PANEL_SOURCE_REFERENCES " + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { files = files, parts = parts, features = features, types = types, statuses = statuses, entities = entities, components = components }));
            PartHealth before = CapturePartHealth(model);
            Feature externalSketch = model.FirstFeature() as Feature;
            while (externalSketch != null && externalSketch.Name != "草图12") externalSketch = externalSketch.GetNextFeature() as Feature;
            Require(externalSketch != null, "PANEL_EXTERNAL_SKETCH_MISSING", 21, "草图12");
            Sketch sketch = externalSketch.GetSpecificFeature2() as Sketch;
            Require(sketch != null, "PANEL_EXTERNAL_SKETCH_UNAVAILABLE", 21, "草图12");
            SketchRelationManager manager = sketch.RelationManager;
            Array relations = manager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
            Require(relations != null && relations.Length == 2, "PANEL_EXTERNAL_RELATION_COUNT_CHANGED", 21, "expected the two audited old assembly point references");
            foreach (SketchRelation relation in relations) Require(manager.DeleteRelation(relation), "PANEL_EXTERNAL_RELATION_DELETE_FAILED", 21, "草图12");
            Require(model.ForceRebuild3(false), "PANEL_DETACH_REBUILD_FAILED", 21, model.GetPathName());
            PartHealth after = CapturePartHealth(model);
            Console.WriteLine("PANEL_DETACHED_REFERENCES " + new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { count = ExternalReferenceCount(model), after = after }));
            Require(NativeSheetMetalGate(after) && PartBboxGate(after, before.x_length_mm, before.y_length_mm), "PANEL_DETACH_GEOMETRY_CHANGED", 21, "native feature or body dimensions changed while detaching copied panel");
        }
        internal static void WithSession(string name, Action<ISldWorks> action, string evidenceSuffix = "")
        {
            Winnsen.StructureAgent.Generation.ParametricContext.AssertAuthorized(name);
            RunResult result = new RunResult();
            DoorRequest request = new DoorRequest { visible = false };
            try
            {
                OwnedSession session = StartOwnedSession(request, name, result);
                Exception actionError = null;
                try { action(session.sw); }
                catch (Exception error) { actionError = error; }
                try { StopOwnedSession(session, result); }
                catch (Exception error) { if (actionError != null) throw new AggregateException(actionError, error); throw; }
                if (actionError != null) throw actionError;
            }
            finally { Winnsen.StructureAgent.Generation.ParametricContext.Write(Path.Combine(Winnsen.StructureAgent.Generation.ParametricContext.AttemptDirectory, "evidence", name + (evidenceSuffix.Length == 0 ? "" : "." + evidenceSuffix) + ".sessions.json"), result); }
        }

        internal static void CaptureProvenOwnedMonitors(OwnedSession session)
        {
            Require(session != null && session.process_id > 0 &&
                session.process_start_ticks_utc > 0 && session.baseline_monitor_ids != null,
                "MONITOR_CAPTURE_SESSION_IDENTITY_MISSING", 27, "owned CAD identity is incomplete");
            using (var parent = System.Diagnostics.Process.GetProcessById(session.process_id))
                Require(!parent.HasExited &&
                    parent.StartTime.ToUniversalTime().Ticks == session.process_start_ticks_utc &&
                    string.Equals(parent.MainModule.FileName, session.executable_path,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(parent.ProcessName, "SLDWORKS", StringComparison.OrdinalIgnoreCase),
                    "MONITOR_CAPTURE_PARENT_IDENTITY_CHANGED", 27, "owned CAD identity changed");

            var captured = new List<OwnedMonitorIdentity>();
            using (var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, CreationDate, CommandLine, ExecutablePath " +
                "FROM Win32_Process WHERE Name='sldProcMon.exe'"))
            using (System.Management.ManagementObjectCollection rows = searcher.Get())
            foreach (System.Management.ManagementObject row in rows)
            using (row)
            {
                OwnedMonitorIdentity identity = ReadOwnedMonitorIdentity(row);
                if (identity == null) continue;
                if (session.baseline_monitor_ids.Contains(identity.process_id) ||
                    identity.parent_process_id != session.process_id ||
                    identity.creation_ticks_utc < session.process_start_ticks_utc ||
                    !System.Text.RegularExpressions.Regex.IsMatch(identity.command_line ?? "", "(?:^|\\s)--ppid=" +
                        session.process_id.ToString(CultureInfo.InvariantCulture) + "(?:\\s|$)",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                    string.IsNullOrWhiteSpace(identity.executable_path) ||
                    !string.Equals(Path.GetFileName(identity.executable_path), "sldProcMon.exe",
                        StringComparison.OrdinalIgnoreCase))
                {
                    session.cleanup_errors.Add("preserved unproven monitor PID " + identity.process_id);
                    continue;
                }
                identity.cleanup_action = "captured_before_parent_exit";
                captured.Add(identity);
            }
            session.owned_monitors = captured;
        }

        private static OwnedMonitorIdentity ReadOwnedMonitorIdentity(
            System.Management.ManagementObject row)
        {
            int pid = Convert.ToInt32(row["ProcessId"], CultureInfo.InvariantCulture);
            string creation = Convert.ToString(row["CreationDate"], CultureInfo.InvariantCulture);
            if (pid <= 0 || string.IsNullOrWhiteSpace(creation))
                throw new InvalidOperationException("monitor PID or creation time is unavailable");
            long creationTicks = System.Management.ManagementDateTimeConverter.ToDateTime(creation)
                .ToUniversalTime().Ticks;
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById(pid))
                {
                    if (process.HasExited) return null;
                    long started = process.StartTime.ToUniversalTime().Ticks;
                    if (Math.Abs(started - creationTicks) >= TimeSpan.TicksPerMillisecond)
                        throw new InvalidOperationException("monitor WMI/process creation time differs: " + pid);
                    return new OwnedMonitorIdentity
                    {
                        process_id = pid,
                        parent_process_id = Convert.ToInt32(row["ParentProcessId"], CultureInfo.InvariantCulture),
                        creation_ticks_utc = creationTicks,
                        process_start_ticks_utc = started,
                        executable_path = Convert.ToString(row["ExecutablePath"], CultureInfo.InvariantCulture),
                        command_line = Convert.ToString(row["CommandLine"], CultureInfo.InvariantCulture),
                        captured_at_ticks_utc = DateTime.UtcNow.Ticks
                    };
                }
            }
            catch (ArgumentException) { return null; }
        }

        private static OwnedMonitorIdentity ReadCurrentOwnedMonitorIdentity(int pid)
        {
            using (var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, CreationDate, CommandLine, ExecutablePath " +
                "FROM Win32_Process WHERE ProcessId=" + pid.ToString(CultureInfo.InvariantCulture)))
            using (System.Management.ManagementObjectCollection rows = searcher.Get())
            foreach (System.Management.ManagementObject row in rows)
            using (row) return ReadOwnedMonitorIdentity(row);
            return null;
        }

        private static bool ProcessIdExists(int pid)
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById(pid))
                    return !process.HasExited;
            }
            catch (ArgumentException) { return false; }
        }

        private static void RequireOrphanMonitorIdentity(OwnedSession session,
            OwnedMonitorIdentity captured, OwnedMonitorIdentity current)
        {
            Require(session.process_exited_naturally && ProcessIds("SLDWORKS").Count == 0 &&
                !ProcessIdExists(session.process_id), "MONITOR_PARENT_NOT_NATURALLY_EXITED", 27,
                "all CAD processes and the exact parent PID must be absent before monitor cleanup");
            Require(captured != null && current != null && session.baseline_monitor_ids != null &&
                !session.baseline_monitor_ids.Contains(captured.process_id) &&
                captured.process_id == current.process_id &&
                captured.parent_process_id == session.process_id &&
                captured.parent_process_id == current.parent_process_id &&
                captured.creation_ticks_utc == current.creation_ticks_utc &&
                captured.creation_ticks_utc >= session.process_start_ticks_utc &&
                captured.creation_ticks_utc <= captured.captured_at_ticks_utc &&
                captured.process_start_ticks_utc == current.process_start_ticks_utc &&
                string.Equals(captured.executable_path, current.executable_path,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(captured.executable_path) &&
                string.Equals(Path.GetFileName(captured.executable_path), "sldProcMon.exe",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(captured.command_line, current.command_line, StringComparison.Ordinal) &&
                System.Text.RegularExpressions.Regex.IsMatch(current.command_line ?? "", "(?:^|\\s)--ppid=" +
                    session.process_id.ToString(CultureInfo.InvariantCulture) + "(?:\\s|$)",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant), "MONITOR_OWNERSHIP_RECHECK_FAILED", 27,
                "monitor identity changed or does not prove this session; process was preserved");
        }

        internal static void StopProvenOwnedMonitors(OwnedSession session)
        {
            Require(session != null && session.owned_monitors != null,
                "MONITOR_OWNERSHIP_SNAPSHOT_MISSING", 27, "monitor ownership snapshot is unavailable");
            var errors = new List<Exception>();
            foreach (OwnedMonitorIdentity captured in session.owned_monitors)
            {
                try
                {
                    OwnedMonitorIdentity current = ReadCurrentOwnedMonitorIdentity(captured.process_id);
                    if (current == null)
                    {
                        captured.cleanup_action = "already_exited";
                        continue;
                    }
                    RequireOrphanMonitorIdentity(session, captured, current);
                    using (var monitor = System.Diagnostics.Process.GetProcessById(captured.process_id))
                    {
                        IntPtr handle = monitor.Handle;
                        if (monitor.WaitForExit(10000))
                        {
                            captured.cleanup_action = "exited_naturally";
                            continue;
                        }
                        current = ReadCurrentOwnedMonitorIdentity(captured.process_id);
                        if (current == null)
                        {
                            captured.cleanup_action = "already_exited";
                            continue;
                        }
                        RequireOrphanMonitorIdentity(session, captured, current);
                        Require(monitor.StartTime.ToUniversalTime().Ticks == captured.process_start_ticks_utc &&
                            string.Equals(monitor.ProcessName, "sldProcMon", StringComparison.OrdinalIgnoreCase),
                            "MONITOR_HANDLE_IDENTITY_CHANGED", 27, "monitor handle identity changed");
                        monitor.Kill();
                        captured.cleanup_action = "terminated_proven_orphan";
                        Require(monitor.WaitForExit(10000), "OWNED_MONITOR_EXIT_FAILED", 27,
                            "proven orphan monitor did not exit");
                    }
                }
                catch (ArgumentException)
                {
                    if (!ProcessIdExists(captured.process_id)) captured.cleanup_action = "already_exited";
                    else
                    {
                        captured.cleanup_action = "preserved_on_identity_error";
                        errors.Add(new InvalidOperationException("monitor PID changed: " + captured.process_id));
                    }
                }
                catch (Exception error)
                {
                    captured.cleanup_action = "preserved_or_cleanup_failed";
                    errors.Add(error);
                }
            }
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        internal static void Generate(double height, string evidence)
        {
            var context = Winnsen.StructureAgent.Generation.ParametricContext.Plan;
            Winnsen.StructureAgent.Generation.ParametricContext.ActiveDoorHeightMm = height;
            string root = Winnsen.StructureAgent.Generation.ParametricContext.AttemptDirectory;
            string token = height.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p');
            string requestPath = Path.Combine(root, "door_sources.json");
            SourceSet sources = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<SourceSet>(File.ReadAllText(requestPath));
            DoorRequest request = new DoorRequest {
                task_id = Winnsen.StructureAgent.Generation.ParametricContext.Text(context, "taskId") + "-H" + token,
                isolated_root = root, output_dir = Path.Combine(root, "doors", "H" + token),
                right_panel_strategy = RightStrategyMirrorPart, sources = sources, visible = false
            };
            RunResult result = new RunResult();
            try
            {
                Execute(request, result);
                Winnsen.StructureAgent.Generation.ParametricContext.Write(evidence, new { success = true, heightMm = height, result = result });
            }
            catch (Exception error)
            {
                Winnsen.StructureAgent.Generation.ParametricContext.Write(evidence, new { success = false, heightMm = height, error = error.ToString(), result = result });
                throw;
            }
        }
    }
}
