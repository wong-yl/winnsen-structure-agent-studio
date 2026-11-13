using System.Security.Cryptography;
using System.Text;
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
        internal static string DoorHeightToken(double height)
        {
            var matches = ContractDoorHeights().Where(item => item.Value == height).ToArray();
            ParametricContext.Require(matches.Length == 1, "PARAMETRIC_DOOR_HEIGHT_NOT_IN_CONTRACT");
            return matches[0].Key;
        }

        internal static Dictionary<string, double> ContractDoorHeights()
        {
            var rowMap = ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "rowsByColumn");
            var rowHeights = new HashSet<double>();
            foreach (string side in new[] { "L", "R" })
                foreach (var row in ((IEnumerable)rowMap[side]).Cast<Dictionary<string, object>>())
                {
                    double height = ParametricContext.Number(row, "doorHeightMm");
                    ParametricContext.Require(height > 10.5 && !double.IsNaN(height) && !double.IsInfinity(height), "PARAMETRIC_DOOR_HEIGHT_INVALID");
                    rowHeights.Add(height);
                }
            object modules;
            ParametricContext.Require(ParametricContext.Plan.TryGetValue("doorModules", out modules) && modules is IList, "PARAMETRIC_DOOR_MODULE_MAP_INVALID");
            var heights = new Dictionary<string, double>(StringComparer.Ordinal);
            var moduleHeights = new HashSet<double>();
            foreach (object item in (IEnumerable)modules)
            {
                var module = item as Dictionary<string, object>;
                ParametricContext.Require(module != null && module.Count == 2 && module.ContainsKey("token") && module.ContainsKey("heightMm"), "PARAMETRIC_DOOR_MODULE_MAP_INVALID");
                string token = module["token"] as string;
                ParametricContext.Require(token != null && System.Text.RegularExpressions.Regex.IsMatch(token, @"\A[1-9][0-9]*(?:p[0-9]{1,6})?\z"), "PARAMETRIC_DOOR_MODULE_TOKEN_INVALID");
                object heightValue = module["heightMm"];
                ParametricContext.Require(heightValue is double || heightValue is decimal || heightValue is int || heightValue is long, "PARAMETRIC_DOOR_MODULE_MAP_INVALID");
                double height = ParametricContext.Number(module, "heightMm");
                ParametricContext.Require(rowHeights.Contains(height), "PARAMETRIC_DOOR_MODULE_HEIGHT_NOT_IN_CONTRACT");
                ParametricContext.Require(!heights.ContainsKey(token), "PARAMETRIC_DOOR_HEIGHT_TOKEN_COLLISION");
                ParametricContext.Require(moduleHeights.Add(height), "PARAMETRIC_DOOR_HEIGHT_DUPLICATE_MODULE");
                heights.Add(token, rowHeights.Single(value => value == height));
            }
            ParametricContext.Require(heights.Count == rowHeights.Count && heights.Count > 0, "PARAMETRIC_DOOR_MODULE_COVERAGE_INVALID");
            return heights;
        }

        internal static double ResolveContractDoorHeight(string token)
        {
            double height;
            ParametricContext.Require(ContractDoorHeights().TryGetValue(token, out height), "PARAMETRIC_DOOR_HEIGHT_NOT_IN_CONTRACT");
            return height;
        }

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
                if (!doorPhase)
                    ParametricContext.Require(new[] {
                        "dimensions", "derived", "base-hole", "assemblies", "clone", "probe", "probe-original-master",
                        "probe-frame", "probe-top", "probe-height-braces", "repair-frame-slots", "repair-height-braces",
                        "repair-depth-placements", "repair-right-owner", "repair-obsolete-doors", "probe-envelope", "envelope", "repair-interfaces", "repair-door-placements",
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
                    double height = ResolveContractDoorHeight(phase.Substring(5));
                    NativeParametricDoor.Generate(height, evidence);
                    return 0;
                }
                var rows = new List<object>();
                ReadOnlyInspection = new[] { "verify-package", "verify-relocated", "physical-audit" }.Contains(phase);
                try {
                if (phase == "repair-right-owner") {
                    NativeParametricDoor.WithSession(phase,sw=>RightOwnerMateRepair.Run(sw,rows),"owner-write");
                    NativeParametricDoor.WithSession(phase,sw=>RightOwnerMateRepair.Verify(sw,rows),"owner-cold");
                }
                else if (phase == "repair-height-braces") RunHeightBraceSessions(phase, rows);
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
                    else if (phase == "repair-obsolete-doors") ObsoleteDoorMigration.Run(sw,rows);
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
                    else if (phase == "locks") NativeParametricLocks.Run(sw, rows, false);
                    else if (phase == "locks-verify") NativeParametricLocks.Run(sw, rows, true);
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
            ParametricContext.Require(model.Save3(1, ref errors, ref warnings) && errors == 0 && warnings == 0,
                "SAVE_FAILED " + model.GetPathName() + " " + errors + "/" + warnings);
        }

        private static string Find(string name) { return Directory.GetFiles(ParametricContext.CadDirectory).Single(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase)); }

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
                    bool replaced = sw.ReplaceReferencedDocument(file, old, target);
                    replacements.Add(new { source = old, target = target, replaced = replaced });
                    ParametricContext.Require(replaced, "CLONE_REFERENCE_REPLACE_FAILED " + file + " " + old);
                }
                rows.Add(new { file = file, replacements = replacements });
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
                        string height = DoorHeightToken(ParametricContext.Number(row, "doorHeightMm"));
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
        private static string SizeToken { get { return "W" + TargetDoorWidthMm.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', 'p') + "_H" + Winnsen.StructureAgent.Generation.NativeParametricAssembly.DoorHeightToken(TargetDoorHeightMm); } }
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
            OwnedSession session = StartOwnedSession(request, name, result);
            Exception actionError = null;
            try { action(session.sw); }
            catch (Exception error) { actionError = error; }
            try { StopOwnedSession(session, result); }
            catch (Exception error) { if (actionError != null) throw new AggregateException(actionError, error); throw; }
            finally { Winnsen.StructureAgent.Generation.ParametricContext.Write(Path.Combine(Winnsen.StructureAgent.Generation.ParametricContext.AttemptDirectory, "evidence", name + (evidenceSuffix.Length == 0 ? "" : "." + evidenceSuffix) + ".sessions.json"), result); }
            if (actionError != null) throw actionError;
        }

        private static void StopProvenOwnedMonitors(OwnedSession session)
        {
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name='sldProcMon.exe'"))
            foreach (System.Management.ManagementObject row in searcher.Get())
            {
                int parent = Convert.ToInt32(row["ParentProcessId"]);
                string command = Convert.ToString(row["CommandLine"]);
                string executable = Convert.ToString(row["ExecutablePath"]);
                if (parent != session.process_id || !System.Text.RegularExpressions.Regex.IsMatch(command, @"(?:^|\s)--ppid=" + session.process_id + @"(?:\s|$)") || !Path.GetFileName(executable).Equals("sldProcMon.exe", StringComparison.OrdinalIgnoreCase)) continue;
                int monitorPid = Convert.ToInt32(row["ProcessId"]);
                using (System.Diagnostics.Process monitor = System.Diagnostics.Process.GetProcessById(monitorPid))
                {
                    if (monitor.StartTime.ToUniversalTime().Ticks < session.process_start_ticks_utc) continue;
                    try { if (!monitor.HasExited) monitor.Kill(); monitor.WaitForExit(10000); }
                    catch (System.ComponentModel.Win32Exception) { if (!WaitForExit(monitorPid, 2000)) throw; }
                }
            }
        }

        internal static void Generate(double height, string evidence)
        {
            var context = Winnsen.StructureAgent.Generation.ParametricContext.Plan;
            Winnsen.StructureAgent.Generation.ParametricContext.ActiveDoorHeightMm = height;
            string root = Winnsen.StructureAgent.Generation.ParametricContext.AttemptDirectory;
            string token = Winnsen.StructureAgent.Generation.NativeParametricAssembly.DoorHeightToken(height);
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

namespace Winnsen.StructureAgent.Generation
{
internal static class ObsoleteDoorMigration
{

    private static string Pack,Target,Mode;
    private static List<object> Events;
    private static bool LoggingFailed;
    private static readonly System.Web.Script.Serialization.JavaScriptSerializer Json=ParametricContext.Json;
    private static readonly Dictionary<string,string> ExpectedHashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,FileBinding> ReferenceSourceBindings=new Dictionary<string,FileBinding>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> RemovedExclusivePaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private sealed class FileBinding { internal string sourcePath,path,sha256; internal long size; }
    private static readonly string[] OldNames={"储物柜门装配_L2-2","储物柜门装配_L4-2","储物柜门装配_L6-2","储物柜门装配_R2-2","储物柜门装配_R4-2","储物柜门装配_R6-2"};
    private static readonly string[] RootMateNames={"重合1","重合2","同心39"};
    private static readonly HashSet<string> MateFeatureTypes=new HashSet<string>(new[]{
        "MateCamTangent","MateCoincident","MateConcentric","MateDistanceDim","MateGearDim","MateHinge","MateInPlace",
        "MateLinearCoupler","MateLock","MateParallel","MatePerpendicular","MatePlanarAngleDim","MateProfileCenter",
        "MateRackPinionDim","MateScrew","MateSlot","MateSymmetric","MateTangent","MateUniversalJoint","MateWidth"
    },StringComparer.Ordinal);
    private static Dictionary<string,object> D(params object[] values){var d=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)d[(string)values[i]]=values[i+1];return d;}
    private static void Require(bool condition,string message){ParametricContext.Require(condition,message);}
    private static string Full(string path){Require(!string.IsNullOrWhiteSpace(path),"EMPTY_PATH");return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);}
    private static bool Same(string a,string b){return string.Equals(Full(a),Full(b),StringComparison.OrdinalIgnoreCase);}
    private static bool Inside(string root,string path){return Full(path).StartsWith(Full(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    private static string Hash(string path){return ParametricContext.Hash(path);}
    private static void Plain(string path){for(string p=Full(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if(File.Exists(p)||Directory.Exists(p))Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"REPARSE_PATH_REJECTED "+p);}
    private static void Record(object row){Events.Add(row);ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory,"evidence",ParametricContext.Phase+".migration.json"),Events);}
    internal static void Run(ISldWorks sw,List<object> records)
    {
        Require(ParametricContext.Phase=="repair-obsolete-doors","OBSOLETE_DOOR_PHASE_REQUIRED");
        ParametricContext.AssertAuthorized(ParametricContext.Phase);
        Require(sw.GetDocumentCount()==0,"MIGRATION_REQUIRES_EMPTY_SESSION");
        Pack=ParametricContext.CadDirectory;Target=Path.Combine(Pack,"标准寄存柜1917×1000×550(总装配).SLDASM");Mode="edit";
        Events=records;ExpectedHashes.Clear();ReferenceSourceBindings.Clear();RemovedExclusivePaths.Clear();LoggingFailed=false;
        var authorization=ParametricContext.Read(System.Environment.GetEnvironmentVariable("WINNSEN_PARAMETRIC_AUTH_PATH"));
        foreach(Dictionary<string,object> row in (IEnumerable)authorization["preInventory"]){
            string path=Full(ParametricContext.Text(row,"path"));ParametricContext.CheckPath(path,Pack);
            Require(Path.GetDirectoryName(path)==Pack,"MIGRATION_FLAT_INVENTORY_REQUIRED");
            string hash=ParametricContext.Text(row,"sha256");Require(Hash(path)==hash&&!ExpectedHashes.ContainsKey(path),"MIGRATION_INPUT_HASH_MISMATCH");ExpectedHashes.Add(path,hash);
        }
        Require(ExpectedHashes.Count==75,"MIGRATION_TEMPLATE_INVENTORY_CHANGED");
        foreach(Dictionary<string,object> row in (IEnumerable)ParametricContext.Plan["obsoleteDoorSourceInventory"]){
            string source=Full(ParametricContext.Text(row,"path")),candidate=Path.Combine(Pack,Path.GetFileName(source)),hash=ParametricContext.Text(row,"sha256");
            Require(ExpectedHashes.ContainsKey(candidate)&&Hash(source)==hash,"MIGRATION_SOURCE_BINDING_MISMATCH");
            ParametricContext.CheckPath(source,Path.GetDirectoryName(source));
            ReferenceSourceBindings.Add(source,new FileBinding{sourcePath=source,path=candidate,sha256=hash,size=new FileInfo(source).Length});
        }
        Require(ReferenceSourceBindings.Count==75,"MIGRATION_SOURCE_COVERAGE_CHANGED");
        ModelDoc2 model=null;
        try{
            int errors=0,warnings=0;
            // Only this explicit repair reads the exact known failed input. Normal Open() stays strict.
            model=sw.OpenDoc6(Target,2,33,"",ref errors,ref warnings) as ModelDoc2;
            Record(D("inspection","migration_raw_open","errors",errors,"warnings",warnings,"options",33,"path",Target));
            Require(model!=null&&errors==0&&warnings==32769,"MIGRATION_EXACT_INPUT_WARNING_REQUIRED");
            Require(Same(model.GetPathName(),Target)&&((IModelDoc2)model).GetType()==2&&!model.IsOpenedReadOnly(),"MIGRATION_ROOT_IDENTITY_OR_MODE_FAILED");
            var before=Snapshot(sw,model,"before-delete",true);
            ParametricContext.AssertMutation(Target);DeleteOldDoors(model);
            var deleted=Snapshot(sw,model,"after-delete-before-rebuild",false);Equal(before,deleted,"retained-after-delete");RootHealthy(deleted);
            bool rebuilt=model.ForceRebuild3(true);Record(D("inspection","rebuild","topOnly",true,"result",rebuilt));Require(rebuilt,"MIGRATION_ROOT_REBUILD_FAILED");
            var rebuiltState=Snapshot(sw,model,"after-rebuild",false);Equal(before,rebuiltState,"retained-after-rebuild");RootHealthy(rebuiltState);
            ParametricContext.AssertMutation(Target);int saveErrors=0,saveWarnings=0;bool saved=model.Save3(1,ref saveErrors,ref saveWarnings);
            Record(D("inspection","save_root_only","path",Target,"saved",saved,"errors",saveErrors,"warnings",saveWarnings));
            Require(saved&&saveErrors==0&&saveWarnings==0&&!model.GetSaveFlag(),"MIGRATION_ROOT_SAVE_FAILED");
            ExpectedHashes[Target]=Hash(Target);
            var savedState=Snapshot(sw,model,"after-save",false);Equal(before,savedState,"retained-after-save");RootHealthy(savedState);
            Require(!model.GetSaveFlag(),"MIGRATION_POST_AUDIT_ROOT_DIRTY");
            foreach(var row in ExpectedHashes)Require(Hash(row.Key)==row.Value,"MIGRATION_NONROOT_FILE_CHANGED "+row.Key);
            Record(D("inspection","migration_completed","removedOldDoors",6,"retainedInstances",80,"retainedDocuments",41,"rootOnlySaved",true,"normalIdentityGateUnchanged",true));
        }finally{if(model!=null){sw.CloseDoc(model.GetTitle());Release(model);}}
    }
    private static object[] ArrayValues(object value)
    {
        if (value == null) return new object[0]; Require(value is Array || value is IList, "EXPECTED_VARIANT_ARRAY " + value.GetType().FullName);
        return ((IEnumerable)value).Cast<object>().ToArray();
    }
    private static double[] Numbers(object value, int minimum, string context)
    {
        Require(value is Array, "NUMERIC_ARRAY_UNAVAILABLE " + context);
        var result = ArrayValues(value).Select(Convert.ToDouble).ToArray();
        Require(result.Length >= minimum && result.All(n => !double.IsNaN(n) && !double.IsInfinity(n)), "INVALID_NUMERIC_ARRAY " + context); return result;
    }
    private static void Release(object value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
    private sealed class IdentityScope : IDisposable
    {
        private readonly Dictionary<long, IntPtr> Held = new Dictionary<long, IntPtr>();
        internal long Acquire(object value)
        {
            IntPtr pointer = Marshal.GetIUnknownForObject(value); long key = pointer.ToInt64();
            if (Held.ContainsKey(key)) Marshal.Release(pointer); else Held.Add(key, pointer); return key;
        }
        public void Dispose() { foreach (IntPtr pointer in Held.Values) Marshal.Release(pointer); Held.Clear(); }
    }
    private static string BindPath(string path)
    {
        string full = Full(path), expected = null; Require(Inside(Pack, full) && ExpectedHashes.TryGetValue(full, out expected), "PATH_OUTSIDE_BOUND_CANDIDATE " + full);
        Plain(full); Require(Hash(full) == expected, "BOUND_CANDIDATE_FILE_CHANGED " + full); return full;
    }
    private static Dictionary<string, object> BindReferencePath(string rawPath)
    {
        string full = Full(rawPath);
        if (Inside(Pack, full)) return D("rawPath", rawPath, "canonicalPath", full, "bindingKind", "candidate", "candidatePath", BindPath(full));
        FileBinding file; Require(ReferenceSourceBindings.TryGetValue(full, out file), "REFERENCE_SOURCE_NOT_IN_BASELINE_WHITELIST " + full);
        Plain(full); Require(File.Exists(full) && (File.GetAttributes(full) & FileAttributes.Directory) == 0, "REFERENCE_SOURCE_NOT_REGULAR_FILE " + full);
        Require(new FileInfo(full).Length == file.size && Hash(full) == file.sha256, "REFERENCE_SOURCE_CHECKPOINT_CHANGED " + full);
        string counterpart = BindPath(file.path);
        return D("rawPath", rawPath, "canonicalPath", full, "bindingKind", "serialized-source-checkpoint", "sourcePath", Full(file.sourcePath),
            "sourceSha256", file.sha256, "sourceSize", file.size, "candidatePath", counterpart);
    }
    private static object Persistent(ModelDocExtension extension, object value, string context)
    {
        Require(extension != null && value != null, "PERSISTENT_REFERENCE_INPUT_NULL " + context);
        object raw = extension.GetPersistReference3(value); object[] bytes = ArrayValues(raw);
        Require(bytes.Length > 0, "PERSISTENT_REFERENCE_UNAVAILABLE " + context); return Convert.ToBase64String(bytes.Select(Convert.ToByte).ToArray());
    }
    private static bool NumberType(object value) { return value is double || value is float || value is decimal; }
    private static void Equal(object before, object after, string context)
    {
        if (before == null || after == null) { Require(before == null && after == null, "STATE_DRIFT " + context); return; }
        var a = before as IDictionary<string, object>; var b = after as IDictionary<string, object>;
        if (a != null || b != null)
        {
            Require(a != null && b != null && a.Count == b.Count && a.Keys.All(b.ContainsKey), "OBJECT_FIELDS_DRIFT " + context);
            foreach (string key in a.Keys) Equal(a[key], b[key], context + "/" + key); return;
        }
        if (!(before is string) && before is IEnumerable && !(after is string) && after is IEnumerable)
        {
            object[] aa = ((IEnumerable)before).Cast<object>().ToArray(), bb = ((IEnumerable)after).Cast<object>().ToArray();
            Require(aa.Length == bb.Length, "ARRAY_COUNT_DRIFT " + context); for (int i = 0; i < aa.Length; i++) Equal(aa[i], bb[i], context + "[" + i + "]"); return;
        }
        if (NumberType(before) || NumberType(after))
        {
            double x = Convert.ToDouble(before), y = Convert.ToDouble(after);
            Require(!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y) && Math.Abs(x - y) <= 1e-12 + Math.Max(Math.Abs(x), Math.Abs(y)) * 1e-10, "NUMERIC_STATE_DRIFT " + context); return;
        }
        Require(string.Equals(Convert.ToString(before, CultureInfo.InvariantCulture), Convert.ToString(after, CultureInfo.InvariantCulture), StringComparison.Ordinal), "STATE_DRIFT " + context);
    }

    private static bool ObsoleteName(string text)
    {
        return !string.IsNullOrEmpty(text) && OldNames.Any(name => text.IndexOf(name.Substring(0, name.Length - 2), StringComparison.OrdinalIgnoreCase) >= 0);
    }
    private static Dictionary<string, object> References(object files, object parts, object features, object types, object statuses, object entities, object components, string context, bool audit)
    {
        string[] keys = { "modelPathNames", "componentPathNames", "features", "dataTypes", "statuses", "referencedEntities", "featureComponents" };
        object[][] arrays = new[] { files, parts, features, types, statuses, entities, components }.Select(ArrayValues).ToArray();
        if (arrays.Any(array => array.Length > 0))
        {
            var rawRows = new List<object>();
            for (int i = 0; i < arrays.Max(array => array.Length); i++)
            {
                var rawRow = new Dictionary<string, object>(); for (int n = 0; n < keys.Length; n++) rawRow[keys[n]] = i < arrays[n].Length ? arrays[n][i] : null;
                rawRows.Add(rawRow);
            }
            Record(D("inspection", "external_reference_raw", "owner", context, "auditPreservedStructure", audit,
                "arrayLengths", arrays.Select(array => array.Length).ToArray(), "rows", rawRows));
        }
        Require(arrays.All(a => a.Length == arrays[0].Length), "EXTERNAL_REFERENCE_ARRAY_LENGTH_MISMATCH " + context);
        var rows = new List<object>();
        for (int i = 0; i < arrays[0].Length; i++)
        {
            var row = new Dictionary<string, object>(); for (int n = 0; n < keys.Length; n++) row[keys[n]] = arrays[n][i];
            if (audit)
            {
                for (int n = 0; n < keys.Length; n++)
                {
                    Require(arrays[n][i] != null, "EXTERNAL_REFERENCE_VALUE_UNKNOWN " + context + "/" + keys[n]);
                    string value = Convert.ToString(arrays[n][i]);
                    Require(!ObsoleteName(value), "PRESERVED_REFERENCE_POINTS_TO_OBSOLETE_DOOR " + context + "/" + keys[n] + "=" + value);
                    if (n < 2 && !string.IsNullOrWhiteSpace(value))
                    {
                        var binding = BindReferencePath(value); row[keys[n] + "Binding"] = binding;
                        string path = Convert.ToString(binding["candidatePath"]); Require(!RemovedExclusivePaths.Contains(path), "PRESERVED_REFERENCE_POINTS_TO_REMOVED_SUBTREE " + context + "=" + path);
                    }
                }
                Require(!string.IsNullOrWhiteSpace(Convert.ToString(arrays[0][i])) &&
                    (!string.IsNullOrWhiteSpace(Convert.ToString(arrays[5][i])) || !string.IsNullOrWhiteSpace(Convert.ToString(arrays[6][i]))), "EXTERNAL_REFERENCE_TARGET_UNKNOWN " + context);
            }
            rows.Add(row);
        }
        return D("count", rows.Count, "auditPreservedStructure", audit, "rows", rows);
    }
    private static Dictionary<string, object> DocumentReferences(ModelDoc2 model, string path, bool audit)
    {
        object files, parts, features, types, statuses, entities, components;
        model.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
        return References(files, parts, features, types, statuses, entities, components, path, audit);
    }
    private static Dictionary<string, object> FeatureReferences(Feature feature, string path)
    {
        object files, parts, features, types, statuses, entities, components; int configOption; string configName;
        feature.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components, out configOption, out configName);
        var row = References(files, parts, features, types, statuses, entities, components, path + "/" + feature.Name, true);
        row["configurationOption"] = configOption; row["configurationName"] = configName; return row;
    }
    private static Dictionary<string, object> ComponentReferences(Component2 component)
    {
        object files, parts, features, types, statuses, entities, components; int configOption; string configName;
        component.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components, out configOption, out configName);
        var row = References(files, parts, features, types, statuses, entities, components, component.Name2, true);
        row["configurationOption"] = configOption; row["configurationName"] = configName; return row;
    }
    private static Dictionary<string, object> MateState(Mate2 mate, ModelDocExtension extension, string owner)
    {
        Require(mate != null, "MATE_INTERFACE_UNKNOWN " + owner); int count = mate.GetMateEntityCount();
        Require(count > 0 && count <= 32, "MATE_ENTITY_COUNT_UNKNOWN " + owner); var entities = new List<object>();
        for (int i = 0; i < count; i++)
        {
            MateEntity2 entity = null; Component2 component = null; object reference = null;
            try
            {
                entity = mate.MateEntity(i); Require(entity != null, "MATE_ENTITY_NULL " + owner);
                component = entity.ReferenceComponent; Require(component != null, "MATE_REFERENCE_COMPONENT_UNKNOWN " + owner);
                reference = entity.Reference; Require(reference != null, "MATE_REFERENCE_NULL " + owner);
                string path = BindPath(component.GetPathName()); string name = component.Name2;
                Require(!ObsoleteName(name) && !RemovedExclusivePaths.Contains(path), "MATE_DEPENDS_ON_OBSOLETE_DOOR " + owner + "/" + name);
                int type = entity.ReferenceType; Require(type >= 0, "MATE_REFERENCE_TYPE_UNKNOWN " + owner);
                object parameters = entity.EntityParams;
                entities.Add(D("index", i, "componentName", name, "componentPath", path, "ownerDocumentReference", false,
                    "referenceType", type, "persistentReference", Persistent(extension, reference, owner + "/mate-entity-" + i),
                    "parameters", parameters == null ? null : (object)Numbers(parameters, 1, owner + "/mate-entity-parameters")));
            }
            finally { Release(reference); Release(component); Release(entity); }
        }
        return D("ownerDocumentPath", owner, "type", mate.Type, "alignment", mate.Alignment, "flipped", mate.Flipped, "canBeFlipped", mate.CanBeFlipped, "entityCount", count, "entities", entities);
    }
    private static void Features(Feature first, bool subfeatures, int depth, string owner, ModelDocExtension extension, bool root, List<object> rows, HashSet<long> visited, IdentityScope identities)
    {
        Feature current = first; int guard = 0;
        while (current != null)
        {
            Feature next = null;
            try
            {
                Require(depth <= 128 && guard++ < 20000 && rows.Count < 100000, "FEATURE_TRAVERSAL_LIMIT " + owner);
                next = (subfeatures ? current.GetNextSubFeature() : current.GetNextFeature()) as Feature;
                long id = identities.Acquire(current);
                if (!visited.Add(id)) continue;
                string name = current.Name, type = current.GetTypeName2();
                if (type != null && type.StartsWith("Mate", StringComparison.Ordinal)) Record(D("inspection", "feature_identity", "document", owner, "name", name, "type", type, "depth", depth));
                Require(name != null && type != null, "FEATURE_IDENTITY_UNKNOWN " + owner);
                if (root && type == "Reference" && OldNames.Contains(name, StringComparer.Ordinal)) continue;
                bool warning; int error2 = current.GetErrorCode2(out warning); int error = current.GetErrorCode();
                var row = D("name", name, "type", type, "id", current.GetID(), "depth", depth, "suppressed", current.IsSuppressed(),
                    "errorCode", error, "errorCode2", error2, "warning", warning, "externalReferences", FeatureReferences(current, owner));
                bool isMate = MateFeatureTypes.Contains(type);
                Require(!type.StartsWith("Mate", StringComparison.Ordinal) || isMate || type == "MateGroup" || type == "MateReference" ||
                    type == "MateReferenceGroupFolder" || type == "MaterialFolder", "MATE_FEATURE_TYPE_UNKNOWN " + owner + "/" + name + "=" + type);
                if (isMate)
                {
                    Mate2 mate = current.GetSpecificFeature2() as Mate2;
                    try { row["mate"] = MateState(mate, extension, owner); row["persistentReference"] = Persistent(extension, current, owner + "/" + name); }
                    finally { Release(mate); }
                }
                rows.Add(row);
                Features(current.GetFirstSubFeature() as Feature, true, depth + 1, owner, extension, root, rows, visited, identities);
            }
            finally { Release(current); current = next; }
        }
    }
    private static List<object> FeatureSnapshot(ModelDoc2 model, bool root)
    {
        string path = BindPath(model.GetPathName()); ModelDocExtension extension = model.Extension; var rows = new List<object>();
        try
        {
            Require(extension != null, "MODEL_EXTENSION_NULL " + path);
            using (var identities = new IdentityScope()) Features(model.FirstFeature() as Feature, false, 0, path, extension, root, rows, new HashSet<long>(), identities);
            Require(rows.Count > 0, "FEATURE_TREE_EMPTY " + path);
            if (root)
            {
                var mates = rows.Cast<Dictionary<string, object>>().Where(r => r.ContainsKey("mate")).ToArray();
                Require(mates.Length == 3 && mates.Select(r => (string)r["name"]).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(RootMateNames.OrderBy(n => n, StringComparer.Ordinal)), "ROOT_THREE_MATES_CHANGED");
            }
            return rows;
        }
        finally { Release(extension); }
    }
    private sealed class ComponentHandle { public Component2 value; public string name, path; public int depth; public bool obsolete; }
    private sealed class ComponentSet : IDisposable
    {
        internal readonly List<ComponentHandle> rows = new List<ComponentHandle>();
        private readonly IdentityScope identities = new IdentityScope();
        private readonly HashSet<long> visited = new HashSet<long>();
        internal void Add(Component2 component, int depth, bool obsolete)
        {
            Require(component != null && depth <= 128 && rows.Count < 10000, "COMPONENT_TRAVERSAL_UNKNOWN");
            Require(visited.Add(identities.Acquire(component)), "COMPONENT_CYCLE_OR_DUPLICATE");
            string name = component.Name2, path = BindPath(component.GetPathName());
            Require(!string.IsNullOrWhiteSpace(name) && rows.All(r => r.name != name), "COMPONENT_NAME_NOT_UNIQUE " + name);
            if (depth == 1 && OldNames.Contains(name, StringComparer.Ordinal))
            {
                Require(Same(path, Path.Combine(Pack, name.Substring(0, name.Length - 2) + ".SLDASM")), "OBSOLETE_DOOR_NAME_PATH_MISMATCH " + name); obsolete = true;
            }
            rows.Add(new ComponentHandle { value = component, name = name, path = path, depth = depth, obsolete = obsolete });
            foreach (object child in ArrayValues(component.GetChildren())) { Require(child is Component2, "COMPONENT_CHILD_INTERFACE_UNKNOWN"); Add((Component2)child, depth + 1, obsolete); }
        }
        public void Dispose() { foreach (ComponentHandle row in rows) Release(row.value); identities.Dispose(); }
    }
    private static ComponentSet Tree(ModelDoc2 model)
    {
        var set = new ComponentSet();
        try { foreach (object component in ArrayValues(((AssemblyDoc)model).GetComponents(true))) { Require(component is Component2, "DIRECT_COMPONENT_INTERFACE_UNKNOWN"); set.Add((Component2)component, 1, false); } return set; }
        catch { set.Dispose(); throw; }
    }
    private static object Bodies(Component2 component, ModelDoc2 model)
    {
        object information; object[] bodies = ArrayValues(component.GetBodies3(0, out information)); var rows = new List<object>();
        object[] info = ArrayValues(information); Require(info.Length == 0 || info.Length == bodies.Length, "BODY_INFORMATION_COUNT_UNKNOWN " + component.Name2);
        object[] documentBodies = ArrayValues(((PartDoc)model).GetBodies2(0, false));
        try { Require(documentBodies.Length == bodies.Length, "DOCUMENT_COMPONENT_SOLID_COUNT_DISAGREES " + component.Name2); }
        finally { foreach (object body in documentBodies) Release(body); }
        try
        {
            foreach (object value in bodies)
            {
                Body2 body = value as Body2; Require(body != null, "BODY_INTERFACE_UNKNOWN " + component.Name2);
                double[] box = Numbers(body.GetBodyBox(), 6, component.Name2 + "/bounds"), mass = Numbers(body.GetMassProperties(1.0), 12, component.Name2 + "/mass-properties");
                Require(box.Length == 6 && box[0] <= box[3] && box[1] <= box[4] && box[2] <= box[5] && ((IBody2)body).GetType() == 0 && mass[3] > 0 && mass[4] > 0, "REAL_SOLID_BODY_STATE_INVALID " + component.Name2);
                FaultEntity faults = body.Check3;
                try
                {
                    Require(faults != null, "BODY_CHECK3_RESULT_UNKNOWN " + component.Name2); int faultCount = faults.Count;
                    Require(faultCount == 0, "BODY_CHECK3_FAULTS_PRESENT " + component.Name2 + "/" + faultCount);
                    rows.Add(D("name", body.Name, "bodyType", ((IBody2)body).GetType(), "healthCheckApi", "IBody2.Check3", "faultCount", faultCount, "visible", body.Visible, "sheetMetal", body.IsSheetMetal(),
                        "faceCount", body.GetFaceCount(), "edgeCount", body.GetEdgeCount(), "volumeM3", mass[3], "areaM2", mass[4], "boundsM", box, "massPropertiesDensity1", mass));
                }
                finally { Release(faults); }
            }
        }
        finally { foreach (object body in bodies) Release(body); }
        return D("solidCount", rows.Count, "documentSolidCount", documentBodies.Length, "bodyInformation", info,
            "zeroSolidStateCorroboratedByPartDoc", rows.Count == 0, "bodies", rows.OrderBy(r => Json.Serialize(r), StringComparer.Ordinal).ToArray());
    }
    private static string ActiveConfigurationName(ModelDoc2 model)
    {
        Configuration active = model.GetActiveConfiguration() as Configuration;
        try { Require(active != null && !string.IsNullOrWhiteSpace(active.Name), "LOADED_DOCUMENT_CONFIGURATION_UNKNOWN " + model.GetPathName()); return active.Name; }
        finally { Release(active); }
    }
    private static ComponentHandle ResolvedWitness(IEnumerable<ComponentHandle> handles, string configuration)
    {
        foreach (ComponentHandle candidate in handles.OrderBy(r => r.name, StringComparer.Ordinal))
        {
            if (candidate.value.GetSuppression2() != 2 || candidate.value.IsSuppressed() || candidate.value.ReferencedConfiguration != configuration) continue;
            ModelDoc2 document = candidate.value.GetModelDoc2() as ModelDoc2;
            try
            {
                if (document == null) continue;
                Require(Same(BindPath(document.GetPathName()), candidate.path), "WITNESS_DOCUMENT_PATH_MISMATCH " + candidate.name);
                Require(ActiveConfigurationName(document) == configuration, "WITNESS_ACTIVE_CONFIGURATION_MISMATCH " + candidate.name); return candidate;
            }
            finally { Release(document); }
        }
        throw new InvalidOperationException("RESOLVED_SAME_PATH_CONFIGURATION_WITNESS_MISSING " + configuration);
    }
    private static object ComponentSnapshot(ComponentHandle handle, ComponentHandle witness)
    {
        Component2 component = handle.value; ModelDoc2 model = null; MathTransform transform = null, totalTransform = null;
        try
        {
            Require(!component.IsVirtual, "VIRTUAL_COMPONENT_REJECTED " + handle.name);
            string config = component.ReferencedConfiguration; Require(!string.IsNullOrWhiteSpace(config), "PRESERVED_CONFIGURATION_UNKNOWN " + handle.name);
            int suppression = component.GetSuppression2(); bool suppressed = component.IsSuppressed();
            Require((suppression == 0 && suppressed) || (suppression == 2 && !suppressed), "PRESERVED_SUPPRESSION_OR_IDENTITY_UNKNOWN " + handle.name);
            Require(witness != null && Same(witness.path, handle.path) && witness.value.ReferencedConfiguration == config && witness.value.GetSuppression2() == 2 && !witness.value.IsSuppressed(), "PRESERVED_SAME_PATH_CONFIGURATION_WITNESS_INVALID " + handle.name);
            model = component.GetModelDoc2() as ModelDoc2; bool instanceModelLoaded = model != null;
            Require(instanceModelLoaded || suppressed, "UNSUPPRESSED_MODEL_NOT_LOADED " + handle.name);
            if (!instanceModelLoaded) model = witness.value.GetModelDoc2() as ModelDoc2;
            Require(model != null && Same(BindPath(model.GetPathName()), handle.path), "PRESERVED_WITNESS_MODEL_PATH_MISMATCH " + handle.name);
            Require(ActiveConfigurationName(model) == config, "PRESERVED_WITNESS_CONFIGURATION_MISMATCH " + handle.name);
            Require(ArrayValues(model.GetConfigurationNames()).Select(Convert.ToString).Contains(config, StringComparer.Ordinal), "PRESERVED_CONFIGURATION_MISSING " + handle.name);
            transform = component.Transform2; Require(transform != null, "COMPONENT_TRANSFORM_UNKNOWN " + handle.name);
            totalTransform = component.GetTotalTransform(false); Require(totalTransform != null, "COMPONENT_TOTAL_TRANSFORM_UNKNOWN " + handle.name);
            int docType = ((IModelDoc2)model).GetType(); Require(docType == 1 || docType == 2, "PRESERVED_DOCUMENT_TYPE_UNKNOWN " + handle.name);
            return D("name", handle.name, "path", handle.path, "fileSha256", ExpectedHashes[handle.path], "depth", handle.depth, "configuration", config,
                "transform", Numbers(transform.ArrayData, 13, handle.name + "/Transform2"), "totalTransform", Numbers(totalTransform.ArrayData, 13, handle.name + "/GetTotalTransform(false)"), "fixed", component.IsFixed(), "suppressed", suppressed,
                "suppression", component.GetSuppression(), "suppression2", suppression, "documentType", docType,
                "externalReferences", ComponentReferences(component), "mateEvidence", "owning-document-feature-snapshot", "instanceModelLoaded", instanceModelLoaded,
                "documentEvidenceWitness", suppressed ? witness.name : handle.name,
                "solidBodies", suppressed ? D("instanceGeometryState", "notLoaded-suppressed", "geometryReadPerformed", false, "resolvedSamePathConfigurationWitness", witness.name, "witnessDocumentPath", witness.path, "witnessConfiguration", config) :
                    (docType == 1 ? Bodies(component, model) : D("container", true)));
        }
        finally { Release(totalTransform); Release(transform); Release(model); }
    }
    private static Dictionary<string, object> Snapshot(ISldWorks sw, ModelDoc2 model, string stage, bool beforeDelete)
    {
        using (ComponentSet set = Tree(model))
        {
            var obsolete = set.rows.Where(r => r.depth == 1 && r.obsolete).ToArray();
            Require(set.rows.Count(r => r.depth == 1) == (beforeDelete ? 25 : 19) && obsolete.Length == (beforeDelete ? 6 : 0), "DIRECT_INSTANCE_CONTRACT_FAILED " + stage);
            if (beforeDelete)
            {
                Require(obsolete.Select(r => r.name).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(OldNames.OrderBy(n => n, StringComparer.Ordinal)), "SIX_OBSOLETE_NAMES_REQUIRED");
                var keepPaths = new HashSet<string>(set.rows.Where(r => !r.obsolete).Select(r => r.path), StringComparer.OrdinalIgnoreCase);
                foreach (string path in set.rows.Where(r => r.obsolete).Select(r => r.path).Distinct(StringComparer.OrdinalIgnoreCase)) if (!keepPaths.Contains(path)) RemovedExclusivePaths.Add(path);
                Record(D("inspection", "migration_plan", "obsoleteDirect", obsolete.Select(r => D("name", r.name, "path", r.path, "sha256", ExpectedHashes[r.path])).ToArray(),
                    "obsoleteSubtreeInstanceCount", set.rows.Count(r => r.obsolete), "retainedInstanceCount", set.rows.Count(r => !r.obsolete), "removedExclusivePaths", RemovedExclusivePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray()));
            }
            Require(set.rows.Count(r => !r.obsolete) == 80, "RETAINED_80_INSTANCE_CONTRACT_FAILED " + stage);
            ModelDocExtension extension = model.Extension; var components = new List<object>(); var docs = new List<object>();
            try
            {
                foreach (ComponentHandle handle in set.rows.Where(r => !r.obsolete).OrderBy(r => r.name, StringComparer.Ordinal))
                {
                    ComponentHandle witness = handle.value.GetSuppression2() == 2 && !handle.value.IsSuppressed() ? handle :
                        ResolvedWitness(set.rows.Where(r => !r.obsolete && Same(r.path, handle.path)), handle.value.ReferencedConfiguration);
                    components.Add(ComponentSnapshot(handle, witness));
                }
                foreach (var group in set.rows.Where(r => !r.obsolete).GroupBy(r => r.path, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    ComponentHandle witness = ResolvedWitness(group, group.First().value.ReferencedConfiguration);
                    ModelDoc2 document = witness.value.GetModelDoc2() as ModelDoc2;
                    try
                    {
                        Require(document != null, "PRESERVED_DOCUMENT_NULL " + group.Key);
                        docs.Add(D("path", BindPath(document.GetPathName()), "fileSha256", ExpectedHashes[group.Key], "resolvedWitness", witness.name, "activeConfiguration", ActiveConfigurationName(document), "externalReferences", DocumentReferences(document, group.Key, true), "features", FeatureSnapshot(document, false)));
                    }
                    finally { Release(document); }
                }
                Require(docs.Count == 41, "RETAINED_41_DOCUMENT_CONTRACT_FAILED " + stage);
                var snapshot = D("directRetainedCount", 19, "retainedInstanceCount", components.Count, "retainedDocumentCount", docs.Count,
                    "components", components, "documents", docs, "rootFeatures", FeatureSnapshot(model, true));
                Record(D("inspection", "root_aggregate_references", "stage", stage, "interpretation", "aggregate only; removed-document rows are not root-feature dependencies", "references", DocumentReferences(model, Target, false)));
                Record(D("inspection", "snapshot", "stage", stage, "state", snapshot)); Loaded(sw, stage); return snapshot;
            }
            finally { Release(extension); }
        }
    }

    private static void Loaded(ISldWorks sw, string stage)
    {
        object[] documents = ArrayValues(sw.GetDocuments()); var rows = new List<object>();
        try
        {
            foreach (object value in documents)
            {
                ModelDoc2 model = value as ModelDoc2; Require(model != null, "LOADED_DOCUMENT_INTERFACE_UNKNOWN");
                string path = BindPath(model.GetPathName());
                rows.Add(D("path", path, "sha256", ExpectedHashes[path], "readOnly", model.IsOpenedReadOnly(), "saveFlag", model.GetSaveFlag()));
            }
            Require(documents.Length == sw.GetDocumentCount(), "LOADED_DOCUMENT_COUNT_DISAGREES");
            Record(D("inspection", "loaded_documents", "stage", stage, "count", documents.Length, "allPathsBoundToCandidate", true, "documents", rows));
        }
        finally { foreach (object value in documents) Release(value); }
    }
    private static void DeleteOldDoors(ModelDoc2 model)
    {
        Require(Mode == "edit" && Same(model.GetPathName(), Target) && !model.IsOpenedReadOnly() && !LoggingFailed, "MUTATION_NOT_AUTHORIZED");
        using (ComponentSet set = Tree(model))
        {
            var targets = set.rows.Where(r => r.depth == 1 && OldNames.Contains(r.name, StringComparer.Ordinal)).ToArray();
            Require(targets.Length == 6 && set.rows.Count(r => r.depth == 1) == 25, "DELETE_EXACT_SIX_PRECONDITION_FAILED");
            foreach (ComponentHandle row in targets)
                Require(row.value.GetSuppression2() == 2 && !row.value.IsSuppressed(), "OBSOLETE_DOOR_MUST_ALREADY_BE_RESOLVED " + row.name);
            model.ClearSelection2(true);
            foreach (ComponentHandle row in targets) Require(row.value.Select4(true, null, false), "OBSOLETE_DOOR_SELECT_FAILED " + row.name);
            SelectionMgr selection = model.SelectionManager as SelectionMgr;
            try
            {
                Require(selection != null && selection.GetSelectedObjectCount2(-1) == 6, "DELETE_SELECTION_COUNT_NOT_SIX");
                for (int i = 1; i <= 6; i++)
                {
                    Component2 component = selection.GetSelectedObject6(i, -1) as Component2;
                    try { Require(component != null && targets.Any(r => r.name == component.Name2 && Same(r.path, component.GetPathName())), "DELETE_SELECTED_OBJECT_NOT_EXACT_OLD_DOOR"); }
                    finally { Release(component); }
                }
            }
            finally { Release(selection); }
            Require(!LoggingFailed && Same(model.GetPathName(), Target), "DELETE_LOG_OR_IDENTITY_GUARD_FAILED");
            bool deleted = model.Extension.DeleteSelection2(0); model.ClearSelection2(true);
            object[] remaining = ArrayValues(((AssemblyDoc)model).GetComponents(true));
            try
            {
                Require(deleted && remaining.Length == 19 && remaining.Cast<Component2>().All(c => !OldNames.Contains(c.Name2, StringComparer.Ordinal)), "OBSOLETE_DOOR_DELETE_FAILED");
                Record(D("inspection", "delete_direct", "assembly", Target, "requested", targets.Select(r => D("name", r.name, "path", r.path)).ToArray(), "selected", 6, "deleteSelectionOptions", 0, "deleted", deleted,
                    "remainingDirectNames", remaining.Cast<Component2>().Select(c => c.Name2).OrderBy(n => n, StringComparer.Ordinal).ToArray()));
            }
            finally { foreach (object value in remaining) Release(value); }
        }
    }
    private static void RootHealthy(Dictionary<string, object> snapshot)
    {
        foreach (var feature in ((IEnumerable)snapshot["rootFeatures"]).Cast<Dictionary<string, object>>())
            Require(Convert.ToInt32(feature["errorCode"]) == 0 && Convert.ToInt32(feature["errorCode2"]) == 0, "ROOT_FEATURE_ERROR_NONZERO " + feature["name"]);
    }

}
}

namespace Winnsen.StructureAgent.Generation
{
internal static class RightOwnerMateRepair
{

    private static readonly Dictionary<string,string> Known=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] TargetNames={"距离1","同心1","同心2","同心3","同心4"};
    private static readonly HashSet<string> MateFeatureTypes=new HashSet<string>(new[]{"MateCamTangent","MateCoincident","MateConcentric","MateDistanceDim","MateGearDim","MateHinge","MateInPlace","MateLinearCoupler","MateLock","MateParallel","MatePerpendicular","MatePlanarAngleDim","MateProfileCenter","MateRackPinionDim","MateScrew","MateSlot","MateSymmetric","MateTangent","MateUniversalJoint","MateWidth"},StringComparer.Ordinal);
    private sealed class Manifest { internal string owner,right; internal FacePick[] picks; }
    private static Manifest Input;
    private static List<object> Events;
    private static List<MateTarget> SavedTargets;
    private static object SavedComponents;
    private static DateTime Deadline;
    private static string Arm,SavedOwnerSha;
    private static int LastOpenWarnings;
    private static bool OwnerSaved,RebuildAttempted,SaveAttempted;
    private static void CheckTime(){Require(DateTime.UtcNow<Deadline,"RIGHT_OWNER_DEADLINE_REACHED");}
    private static void Record(object row){Events.Add(row);ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory,"evidence",ParametricContext.Phase+".right-owner.json"),Events);}
    private static void CheckBindings(){
        ParametricContext.AssertAuthorized(ParametricContext.Phase);
        foreach(var p in Known)if(!(OwnerSaved&&Same(p.Key,Input.owner)))Require(Hash(p.Key)==p.Value,"RIGHT_OWNER_NON_TARGET_CHANGED "+p.Key);
    }
    private static ModelDoc2 Open(ISldWorks sw,string path,bool writable,bool repairEntry){
        CheckTime();CheckBindings();Require(Bound(path)&&sw.GetDocumentCount()==0,"RIGHT_OWNER_EMPTY_BOUND_SESSION_REQUIRED");
        int errors=0,warnings=0;
        var model=sw.OpenDoc6(path,2,writable?1:3,"",ref errors,ref warnings) as ModelDoc2;
        Record(D("inspection","right_owner_open","path",path,"errors",errors,"warnings",warnings,"repairEntry",repairEntry));
        Require(model!=null&&errors==0&&Same(model.GetPathName(),path)&&model.IsOpenedReadOnly()==!writable,"RIGHT_OWNER_OPEN_FAILED");
        Require(repairEntry?(warnings==0||warnings==32||warnings==33):(warnings&1)==0,"RIGHT_OWNER_UNEXPECTED_WARNING");
        LastOpenWarnings=warnings;Documents(sw,"right-owner-open");return model;
    }
    private static FacePick[] ResolveCurrentPicks(ModelDoc2 owner,List<MateTarget> targets){
        Component2 component=OneRight(owner,Input.right);
        var transform=Numbers(component.Transform2.ArrayData,13,"right transform");
        Require(transform.Take(9).SequenceEqual(new[]{1.0,0,0,0,1,0,0,0,1})&&transform.Skip(9).Take(3).All(x=>Math.Abs(x)<1e-9)&&Math.Abs(transform[12]-1)<1e-9,"RIGHT_OWNER_IDENTITY_TRANSFORM_REQUIRED");
        ModelDoc2 part=(ModelDoc2)component.GetModelDoc2();
        var picks=new List<FacePick>();
        const double tol=1e-7;
        foreach(var target in targets){
            var matches=new List<FacePick>();
            foreach(Body2 body in Values(((PartDoc)part).GetBodies2(0,false)).Cast<Body2>())foreach(Face2 face in Values(body.GetFaces()).Cast<Face2>()){
                var g=SurveySurface(face,transform) as Dictionary<string,object>;
                if(g==null||(string)g["kind"]!=(target.Plane?"plane":"cylinder"))continue;
                var point=(double[])g["point"];var axis=(double[])g["axis"];var q=target.Params;
                if(Math.Abs(Math.Abs(axis[0])-1)>tol||Math.Abs(axis[1])>tol||Math.Abs(axis[2])>tol)continue;
                if(target.Plane?Math.Abs(point[0]-q[0])>tol:Math.Abs(point[1]-q[1])>tol||Math.Abs(point[2]-q[2])>tol||Math.Abs((double)g["radius"]-q[6])>tol)continue;
                var box=Numbers(face.GetBox(),6,"current finite box");var uv=Numbers(face.GetUVBounds(),4,"current finite UV");double area=face.GetArea();
                Require(area>0,"CURRENT_FACE_AREA_INVALID");
                if(!target.Plane)Require(Math.Abs((uv[1]-uv[0])-2*Math.PI)<tol&&Math.Abs(box[3]-box[0]-.0008)<tol&&Math.Abs(area-2*Math.PI*.001*.0008)<1e-10,"CURRENT_CYLINDER_FINITE_BOUNDARY_CHANGED "+target.Name);
                matches.Add(new FacePick{name=target.Name,partReference=(string)Persistent(part.Extension,face),localBox=box,uv=uv,area=area});
            }
            Record(D("inspection","current_mate_face_matches","mate",target.Name,"count",matches.Count,"target",target.Params));
            Require(matches.Count==1,"CURRENT_MATE_FACE_NOT_UNIQUE "+target.Name+" "+matches.Count);picks.Add(matches[0]);
        }
        Require(picks.Select(p=>p.partReference).Distinct().Count()==5,"CURRENT_MATE_FACE_REUSED");return picks.ToArray();
    }
    internal static void Run(ISldWorks sw,List<object> rows){
        Require(ParametricContext.Phase=="repair-right-owner"&&sw.GetDocumentCount()==0,"RIGHT_OWNER_PHASE_OR_SESSION_INVALID");
        Events=rows;Arm="repair";Deadline=DateTime.UtcNow.AddMinutes(8);Known.Clear();OwnerSaved=false;SavedTargets=null;SavedComponents=null;
        Input=new Manifest{owner=Path.Combine(ParametricContext.CadDirectory,"箱体竖隔板R焊接.SLDASM"),right=Path.Combine(ParametricContext.CadDirectory,"箱体竖隔板R.SLDPRT")};
        var auth=ParametricContext.Read(System.Environment.GetEnvironmentVariable("WINNSEN_PARAMETRIC_AUTH_PATH"));
        foreach(string key in new[]{"preInventory","preDoorInventory"})foreach(Dictionary<string,object> row in (IEnumerable)auth[key]){
            string path=Full(ParametricContext.Text(row,"path"));ParametricContext.CheckPath(path,ParametricContext.AttemptDirectory);
            Require(!Known.ContainsKey(path)&&Hash(path)==ParametricContext.Text(row,"sha256"),"RIGHT_OWNER_INPUT_HASH_MISMATCH");Known.Add(path,Hash(path));
        }
        Require(Bound(Input.owner)&&Bound(Input.right),"RIGHT_OWNER_REQUIRED_INPUT_MISSING");
        try{RepairTargeted(sw);CheckBindings();SavedOwnerSha=Hash(Input.owner);}
        finally{sw.CloseAllDocuments(true);Require(sw.GetDocumentCount()==0,"RIGHT_OWNER_DOCUMENTS_REMAIN");}
    }
    internal static void Verify(ISldWorks sw,List<object> rows){
        Events=rows;Arm="cold-owner";CheckBindings();Require(Hash(Input.owner)==SavedOwnerSha,"SAVED_OWNER_CHANGED_BEFORE_COLD");
        var owner=NativeParametricAssembly.Open(sw,Input.owner,true);
        try{
            var current=Snapshot(owner,Input.right,"cold-owner");
            Require(FeatureRows(current).All(f=>(int)f["error"]==0&&(int)f["error2"]==0&&!(bool)f["warning"]),"COLD_RIGHT_OWNER_FEATURE_ISSUE");
            if(SavedTargets!=null){
                Equal(SavedComponents,current["components"],"cold-components");
                foreach(var target in SavedTargets){
                    var feature=TargetFeature(owner,target.Name);Equal(target.Settings,Settings(feature),"cold-settings/"+target.Name);
                    var mate=(Mate2)feature.GetSpecificFeature2();Require(mate.GetMateEntityCount()==2,"COLD_MATE_ENTITY_COUNT_CHANGED");
                    var face=FindTargetFace(OneRight(owner,Input.right),target);
                    Require(SameComObject(mate.MateEntity(0).Reference,face)&&mate.MateEntity(1).Reference!=null,"COLD_FINITE_MATE_FACE_MISMATCH");
                }
            }
            CheckBindings();Require(Hash(Input.owner)==SavedOwnerSha,"SAVED_OWNER_CHANGED_DURING_COLD");Record(D("inspection","right_owner_cold_verified","targetCount",SavedTargets==null?0:SavedTargets.Count,"nonTargetFilesUnchanged",true,"normalIdentityGateUnchanged",true));
        }finally{sw.CloseAllDocuments(true);Require(sw.GetDocumentCount()==0,"COLD_RIGHT_OWNER_DOCUMENTS_REMAIN");}
    }
    private static Dictionary<string, object> D(params object[] values) { var row = new Dictionary<string, object>(); for (int i = 0; i < values.Length; i += 2) row[(string)values[i]] = values[i + 1]; return row; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string Full(string path) { Require(!string.IsNullOrWhiteSpace(path), "EMPTY_PATH"); return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
    private static bool Same(string a, string b) { return string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase); }
    private static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
    private static object Read(Func<object> action) { try { return action(); } catch (Exception error) { return D("readError", error.ToString()); } }
    private static object[] Values(object raw) { return raw is Array || raw is IList ? ((IEnumerable)raw).Cast<object>().ToArray() : new object[0]; }
    private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private static bool SameComObject(object a, object b) { if (a == null || b == null) return false; IntPtr pa = Marshal.GetIUnknownForObject(a), pb = IntPtr.Zero; try { pb = Marshal.GetIUnknownForObject(b); return pa == pb; } finally { if (pb != IntPtr.Zero) Marshal.Release(pb); Marshal.Release(pa); } }
    // Hold each distinct IUnknown until the entire traversal ends, so its address cannot be reused.
    private sealed class ComIdentityScope : IDisposable
    {
        private readonly HashSet<long> keys = new HashSet<long>();
        private readonly List<IntPtr> held = new List<IntPtr>();
        internal bool First(object value) { if (value == null) return false; IntPtr pointer = Marshal.GetIUnknownForObject(value); if (!keys.Add(pointer.ToInt64())) { Marshal.Release(pointer); return false; } held.Add(pointer); return true; }
        public void Dispose() { foreach (IntPtr pointer in held) Marshal.Release(pointer); held.Clear(); keys.Clear(); }
    }
    private static bool NumberType(object value) { return value is double || value is float || value is decimal; }
    private static void Equal(object before, object after, string context)
    {
        if (before == null || after == null) { Require(before == null && after == null, "STATE_DRIFT " + context); return; }
        var a = before as IDictionary<string, object>; var b = after as IDictionary<string, object>;
        if (a != null || b != null)
        {
            Require(a != null && b != null && a.Count == b.Count && a.Keys.All(b.ContainsKey), "OBJECT_FIELDS_DRIFT " + context);
            foreach (string key in a.Keys) Equal(a[key], b[key], context + "/" + key); return;
        }
        if (!(before is string) && before is IEnumerable && !(after is string) && after is IEnumerable)
        {
            object[] aa = ((IEnumerable)before).Cast<object>().ToArray(), bb = ((IEnumerable)after).Cast<object>().ToArray();
            Require(aa.Length == bb.Length, "ARRAY_COUNT_DRIFT " + context); for (int i = 0; i < aa.Length; i++) Equal(aa[i], bb[i], context + "[" + i + "]"); return;
        }
        if (NumberType(before) || NumberType(after))
        {
            double x = Convert.ToDouble(before), y = Convert.ToDouble(after);
            Require(!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y) && Math.Abs(x - y) <= 1e-12 + Math.Max(Math.Abs(x), Math.Abs(y)) * 1e-10, "NUMERIC_STATE_DRIFT " + context); return;
        }
        Require(string.Equals(Convert.ToString(before, CultureInfo.InvariantCulture), Convert.ToString(after, CultureInfo.InvariantCulture), StringComparison.Ordinal), "STATE_DRIFT " + context);
    }


    private static bool Bound(string path) { try { return Known.ContainsKey(Full(path)); } catch { return false; } }
    private static double[] Numbers(object raw, int minimum, string context)
    {
        Require(raw is Array, "NUMERIC_ARRAY_UNAVAILABLE " + context); double[] row = Values(raw).Select(Convert.ToDouble).ToArray();
        Require(row.Length >= minimum && row.All(n => !double.IsNaN(n) && !double.IsInfinity(n)), "NUMERIC_ARRAY_INVALID " + context); return row;
    }
    private static string Config(ModelDoc2 model) { Configuration c = model.ConfigurationManager.ActiveConfiguration; try { Require(c != null, "ACTIVE_CONFIGURATION_NULL"); return c.Name; } finally { Release(c); } }
    private static object Persistent(ModelDocExtension extension, object value)
    {
        object raw = extension.GetPersistReference3(value); Require(raw is Array && Values(raw).Length > 0, "PERSISTENT_REFERENCE_UNAVAILABLE"); return Convert.ToBase64String(Values(raw).Select(Convert.ToByte).ToArray());
    }
    private static void Documents(ISldWorks sw, string stage)
    {
        var rows = new List<object>();
        foreach (object raw in Values(sw.GetDocuments())) { ModelDoc2 doc = raw as ModelDoc2; Require(doc != null && Bound(doc.GetPathName()), "LOADED_DOCUMENT_OUTSIDE_CANDIDATE"); rows.Add(D("path", doc.GetPathName(), "readOnly", doc.IsOpenedReadOnly(), "saveFlag", doc.GetSaveFlag())); }
        Record(D("inspection", "loaded_documents", "stage", stage, "count", sw.GetDocumentCount(), "rows", rows));
    }
    private static Dictionary<string, object> MateState(Mate2 mate, ModelDocExtension extension, string owner, string featureName, string featureType, int depth, string stage)
    {
        Require(mate != null, "MATE_INTERFACE_UNKNOWN " + owner + "/" + featureName); int count = mate.GetMateEntityCount(); Require(count > 0 && count <= 32, "MATE_ENTITY_COUNT_UNKNOWN"); var entities = new List<object>();
        for (int i = 0; i < count; i++)
        {
            MateEntity2 entity = null; Component2 component = null; object reference = null;
            try
            {
                entity = mate.MateEntity(i); Require(entity != null, "MATE_ENTITY_NULL " + featureName + "/" + i); component = entity.ReferenceComponent; reference = entity.Reference;
                string path = component == null ? null : Full(component.GetPathName());
                object parameters = Read(delegate { return entity.EntityParams; }); int referenceType = entity.ReferenceType;
                var diagnostic = D("inspection", "mate_entity_raw", "stage", stage, "owner", owner, "featureName", featureName, "featureType", featureType, "featureDepth", depth, "mateType", mate.Type, "entityCount", count, "entityIndex", i, "referenceNull", reference == null, "referenceType", referenceType, "componentNull", component == null, "componentPath", path, "componentName", component == null ? null : component.Name2, "configuration", component == null ? null : component.ReferencedConfiguration, "entityParams", parameters);
                Record(diagnostic);
                bool beforeTargetMissing = (stage == "before-replace" || stage == "before-targeted-repair") && TargetNames.Contains(featureName) && i == 0 && reference == null && component != null && Same(path, Input.right);
                Require(reference != null || beforeTargetMissing, "MATE_REFERENCE_NULL_NOT_BOUND_BEFORE_TARGET " + featureName + "/" + i + "/" + stage);
                Require(component == null || Bound(path), "MATE_REFERENCE_OUTSIDE_CANDIDATE");
                Require(parameters == null || parameters is Array, "MATE_PARAMETERS_READ_FAILED " + featureName + "/" + i);
                entities.Add(D("index", i, "componentName", component == null ? null : component.Name2, "componentPath", component == null ? Full(owner) : path, "configuration", component == null ? "owner-active-configuration-in-snapshot" : component.ReferencedConfiguration, "ownerDocumentReference", component == null, "referenceType", referenceType, "referenceNull", reference == null, "persistentReference", reference == null ? null : Persistent(extension, reference), "parameters", parameters == null ? null : (object)Numbers(parameters, 1, "mate-parameters")));
                if (beforeTargetMissing) Record(D("inspection", "bound_target_missing_mate_before", "featureName", featureName, "featureType", featureType, "featureDepth", depth, "entityIndex", i, "componentPath", path, "componentName", component.Name2, "configuration", component.ReferencedConfiguration, "referenceType", referenceType, "entityParams", parameters, "productionGateChanged", false, "modelAccepted", false));
            }
            finally { Release(reference); Release(component); Release(entity); }
        }
        return D("type", mate.Type, "alignment", mate.Alignment, "flipped", mate.Flipped, "canBeFlipped", mate.CanBeFlipped, "entityCount", count, "entities", entities);
    }
    private static void Features(Feature first, bool sub, int depth, string owner, ModelDocExtension extension, ComIdentityScope identities, List<object> rows, string stage)
    {
        Require(depth <= 64 && rows.Count < 20000, "FEATURE_TRAVERSAL_LIMIT");
        for (Feature f = first; f != null; f = (sub ? f.GetNextSubFeature() : f.GetNextFeature()) as Feature)
        {
            CheckTime(); if (!identities.First(f)) break; string type = f.GetTypeName2(); bool warning; int error2 = f.GetErrorCode2(out warning);
            var row = D("name", f.Name, "type", type, "suppressed", f.IsSuppressed(), "error", f.GetErrorCode(), "error2", error2, "warning", warning, "depth", depth);
            bool isMate = MateFeatureTypes.Contains(type); Require(!type.StartsWith("Mate", StringComparison.Ordinal) || isMate || type == "MateGroup" || type == "MateReference" || type == "MateReferenceGroupFolder", "UNKNOWN_MATE_TYPE " + type);
            if (isMate) { Mate2 mate = f.GetSpecificFeature2() as Mate2; try { row["mate"] = MateState(mate, extension, owner, f.Name, type, depth, stage); row["persistentReference"] = Persistent(extension, f); } finally { Release(mate); } }
            rows.Add(row); Features(f.GetFirstSubFeature() as Feature, true, depth + 1, owner, extension, identities, rows, stage);
        }
    }
    private static Dictionary<string, object> Snapshot(ModelDoc2 owner, string rightPath, string stage)
    {
        var components = new List<object>(); var features = new List<object>(); ModelDocExtension ext = owner.Extension;
        object[] raw = Values(((AssemblyDoc)owner).GetComponents(true)); Require(raw.Length > 0 && raw.All(c => c is Component2), "TOP_COMPONENTS_UNKNOWN");
        Require(raw.Cast<Component2>().Count(c => Same(c.GetPathName(), rightPath)) == 1, "UNIQUE_TOP_RIGHT_REQUIRED");
        foreach (Component2 c in raw)
        {
            Require(Bound(c.GetPathName()) && !c.IsSuppressed(), "TOP_COMPONENT_UNBOUND_OR_SUPPRESSED"); ModelDoc2 doc = c.GetModelDoc2() as ModelDoc2; Require(doc != null && Same(doc.GetPathName(), c.GetPathName()), "COMPONENT_MODEL_UNKNOWN");
            MathTransform t = c.Transform2, total = c.GetTotalTransform(false); Require(t != null && total != null, "TRANSFORM_UNKNOWN");
            components.Add(D("name", c.Name2, "path", Full(c.GetPathName()), "configuration", c.ReferencedConfiguration, "activeConfiguration", Config(doc), "transform", Numbers(t.ArrayData, 13, "Transform2"), "totalTransform", Numbers(total.ArrayData, 13, "TotalTransform"), "fixed", c.IsFixed(), "suppression", c.GetSuppression(), "suppression2", c.GetSuppression2(), "geometryResampled", false, "evidenceOnlyResearch", true));
        }
        using (var ids = new ComIdentityScope()) Features(owner.FirstFeature() as Feature, false, 0, owner.GetPathName(), ext, ids, features, stage);
        Record(D("inspection", "owner_mate_count", "stage", stage, "count", features.Cast<Dictionary<string, object>>().Count(f => f.ContainsKey("mate")), "zeroMateCountIsObservedNotRejected", true));
        var state = D("owner", Full(owner.GetPathName()), "configuration", Config(owner), "componentCount", raw.Length, "components", components.Cast<Dictionary<string, object>>().OrderBy(c => Same(Convert.ToString(c["path"]), rightPath) ? "0-RIGHT" : "1-" + Convert.ToString(c["path"]) + "/" + Convert.ToString(c["name"]), StringComparer.Ordinal).ToArray(), "features", features);
        Record(D("inspection", "owner_snapshot", "stage", stage, "state", state, "finiteMateBoundaryWitnessComplete", false, "geometryResampled", false, "evidenceOnlyResearch", true)); return state;
    }
    private static Component2 OneRight(ModelDoc2 owner, string path) { Component2[] rows = Values(((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().Where(c => Same(c.GetPathName(), path)).ToArray(); Require(rows.Length == 1, "EXACT_ONE_TOP_RIGHT_REQUIRED"); return rows[0]; }
    public sealed class FacePick { public string name,partReference; public double[] localBox,uv; public double area; }
    private sealed class MateTarget
    {
        public string Name;
        public double[] Params;
        public bool Plane;
        public Dictionary<string, object> Settings;
    }
    private static Feature TargetFeature(ModelDoc2 owner, string name)
    {
        Feature f = ((AssemblyDoc)owner).FeatureByName(name) as Feature;
        Require(f != null && f.Name == name && !f.IsSuppressed(), "EXACT_MATE_UNAVAILABLE " + name);
        return f;
    }
    private static Dictionary<string, object> Settings(Feature f)
    {
        if (f.GetTypeName2() == "MateDistanceDim")
        {
            var d = (DistanceMateFeatureData)f.GetDefinition();
            return D("distance",d.Distance,"minimum",d.MinimumDistance,"maximum",d.MaximumDistance,
                "alignment",d.MateAlignment,"flip",d.FlipDimension,"advanced",d.IsAdvancedMate);
        }
        Require(f.GetTypeName2() == "MateConcentric", "UNEXPECTED_TARGET_TYPE " + f.Name);
        var c = (ConcentricMateFeatureData)f.GetDefinition();
        return D("alignment",c.MateAlignment,"lockRotation",c.LockRotation);
    }
    private static Face2 FindTargetFace(Component2 component, MateTarget target)
    {
        ModelDoc2 part = (ModelDoc2)component.GetModelDoc2();
        var t = Numbers(component.Transform2.ArrayData,13,"repair transform");
        const double tol = 1e-7;
        FacePick pick = Input.picks.Single(x=>x.name==target.Name);
        int code;
        var face = part.Extension.GetObjectByPersistReference3(Convert.FromBase64String(pick.partReference),out code) as Face2;
        Require(face!=null && code==0,"SURVEY_FACE_REFERENCE_UNRESOLVED "+target.Name+" "+code);
        var g=(Dictionary<string,object>)SurveySurface(face,t);
        Require(g!=null && (string)g["kind"]==(target.Plane?"plane":"cylinder"),"SURVEY_FACE_TYPE_CHANGED "+target.Name);
        double[] p=(double[])g["point"], a=(double[])g["axis"], old=target.Params;
        Require(Math.Abs(Math.Abs(a[0])-1)<tol && Math.Abs(a[1])<tol && Math.Abs(a[2])<tol &&
            (target.Plane ? Math.Abs(p[0]-old[0])<tol :
            Math.Abs(p[1]-old[1])<tol && Math.Abs(p[2]-old[2])<tol && Math.Abs((double)g["radius"]-old[6])<tol),
            "SURVEY_FACE_GEOMETRY_CHANGED "+target.Name);
        double[] box=Numbers(face.GetBox(),6,"face bounds"),uv=Numbers(face.GetUVBounds(),4,"face UV");
        double area=face.GetArea();
        Equal(pick.localBox,box,target.Name+"/finiteBox"); Equal(pick.uv,uv,target.Name+"/UV"); Equal(pick.area,area,target.Name+"/area");
        var mapped = component.GetCorrespondingEntity(face) as Face2;
        Require(mapped != null && SameComObject(((Entity)mapped).GetComponent(),component),"FACE_CONTEXT_MISMATCH " + target.Name);
        Record(D("inspection","target_face_proven","mate",target.Name,"box",box,"uv",uv,"area",area,"target",target.Params));
        return mapped;
    }
    private static List<Dictionary<string,object>> FeatureRows(Dictionary<string,object> state)
    {
        return ((IEnumerable<object>)state["features"]).Cast<Dictionary<string,object>>().ToList();
    }
    private static Dictionary<string,object>[] EntityRows(Dictionary<string,object> feature)
    {
        return ((IEnumerable<object>)((Dictionary<string,object>)feature["mate"])["entities"]).Cast<Dictionary<string,object>>().ToArray();
    }
    private static void ConfirmTargetedState(ModelDoc2 owner, Dictionary<string,object> before, List<MateTarget> targets, string phase)
    {
        var after = Snapshot(owner,Input.right,phase);
        Equal(before["components"],after["components"],phase+"/components");
        var a = FeatureRows(before); var b = FeatureRows(after);
        Require(a.Count == b.Count,"FEATURE_COUNT_CHANGED " + phase);
        for (int i=0;i<a.Count;i++)
        {
            var target = targets.SingleOrDefault(x=>x.Name == (string)a[i]["name"]);
            if (target == null)
            {
                if ((string)a[i]["type"]=="MateGroup" && (string)a[i]["name"]=="配合")
                {
                    Require((int)a[i]["error"]==1 && (int)a[i]["error2"]==1 && (bool)a[i]["warning"] &&
                        (int)b[i]["error"]==0 && (int)b[i]["error2"]==0 && !(bool)b[i]["warning"],"TARGET_MATE_GROUP_NOT_CLEARED");
                    var expected=new Dictionary<string,object>(a[i]);
                    expected["error"]=0; expected["error2"]=0; expected["warning"]=false;
                    Equal(expected,b[i],phase+"/target-mate-group"); continue;
                }
                Equal(a[i],b[i],phase+"/"+a[i]["name"]); continue;
            }
            Require((string)b[i]["name"] == target.Name && (string)b[i]["type"] == (string)a[i]["type"] &&
                !(bool)b[i]["suppressed"] && (int)b[i]["error"] == 0 && (int)b[i]["error2"] == 0 && !(bool)b[i]["warning"],
                "TARGET_MATE_NOT_HEALTHY " + target.Name);
            var m = (Dictionary<string,object>)a[i]["mate"]; var n = (Dictionary<string,object>)b[i]["mate"];
            foreach (string key in m.Keys.Where(k=>k!="entities")) Equal(m[key],n[key],phase+"/"+target.Name+"/"+key);
            var old = EntityRows(a[i]); var current = EntityRows(b[i]);
            Require(current.Length==2 && !(bool)current[0]["referenceNull"] && Same((string)current[0]["componentPath"],Input.right),"TARGET_REFERENCE_NOT_BOUND");
            Equal(old[1],current[1],phase+"/"+target.Name+"/healthy-side");
            double[] p = (double[])current[0]["parameters"], q=target.Params;
            const double tol=1e-7;
            Require(Math.Abs(Math.Abs(p[3])-1)<tol && Math.Abs(p[4])<tol && Math.Abs(p[5])<tol &&
                (target.Plane ? Math.Abs(p[0]-q[0])<tol : Math.Abs(p[1]-q[1])<tol && Math.Abs(p[2]-q[2])<tol && Math.Abs(p[6]-q[6])<tol),
                "TARGET_GEOMETRY_READBACK_MISMATCH "+target.Name);
            var f = TargetFeature(owner,target.Name);
            Equal(target.Settings,Settings(f),phase+"/"+target.Name+"/settings");
            var rebound=(Mate2)f.GetSpecificFeature2();
            var expectedFace=FindTargetFace(OneRight(owner,Input.right),target);
            Require(SameComObject(rebound.MateEntity(0).Reference,expectedFace),
                "TARGET_FINITE_FACE_READBACK_MISMATCH "+target.Name);
        }
        CheckBindings();
    }
    private static double[] InAssembly(double[] p, double[] t, bool point)
    {
        double scale = point ? t[12] : 1;
        return Enumerable.Range(0, 3).Select(i =>
            scale * (p[0] * t[i] + p[1] * t[3+i] + p[2] * t[6+i]) + (point ? t[9+i] : 0)).ToArray();
    }
    private static object SurveySurface(Face2 face, double[] t)
    {
        var s = face.GetSurface() as Surface;
        Require(s != null, "SURFACE_MISSING");
        bool plane = s.IsPlane(), cylinder = s.IsCylinder();
        if (!plane && !cylinder) return null;
        var p = Numbers(plane ? s.PlaneParams : s.CylinderParams, plane ? 6 : 7, "surface");
        var point = InAssembly(plane ? p.Skip(3).Take(3).ToArray() : p, t, true);
        var axis = InAssembly(plane ? p : p.Skip(3).Take(3).ToArray(), t, false);
        return D("kind",plane ? "plane" : "cylinder","point",point,"axis",axis,"radius",cylinder ? p[6]*Math.Abs(t[12]) : 0);
    }
    private static void RepairTargeted(ISldWorks sw)
    {
        ModelDoc2 owner = Open(sw,Input.owner,true,true);
        if(LastOpenWarnings!=33) {
            var healthy=Snapshot(owner,Input.right,"healthy-owner-noop");
            Require(FeatureRows(healthy).All(f=>(int)f["error"]==0&&(int)f["error2"]==0&&!(bool)f["warning"]),"HEALTHY_OWNER_HAS_FEATURE_ISSUES");
            Record(D("inspection","right_owner_noop","warnings",LastOpenWarnings,"noSave",true));
            return;
        }
        Require(LastOpenWarnings == 33,"EXACT_FAILED_OWNER_REQUIRED");
        int activationError=0; ModelDoc2 active=sw.ActivateDoc3(owner.GetTitle(),false,0,ref activationError) as ModelDoc2;
        Require(active!=null && activationError==0 && Same(active.GetPathName(),Input.owner),"OWNER_ACTIVATION_FAILED");
        var before=Snapshot(owner,Input.right,"before-targeted-repair");
        var missing=FeatureRows(before).Where(f=>f.ContainsKey("mate")&&EntityRows(f).Any(e=>(bool)e["referenceNull"])).Select(f=>(string)f["name"]).ToArray();
        Require(missing.Length==5&&new HashSet<string>(missing).SetEquals(TargetNames),"EXACT_FIVE_MISSING_TARGETS_REQUIRED");
        Require(FeatureRows(before).Where(f=>!TargetNames.Contains((string)f["name"])&&(string)f["type"]!="MateGroup").All(f=>(int)f["error"]==0&&(int)f["error2"]==0&&!(bool)f["warning"]),"NON_TARGET_FEATURE_ISSUE");
        var targets=new List<MateTarget>();
        foreach (string name in TargetNames)
        {
            var feature=TargetFeature(owner,name);
            var rows=EntityRows(FeatureRows(before).Single(f=>(string)f["name"]==name));
            Require(rows.Length==2 && (bool)rows[0]["referenceNull"] && !(bool)rows[1]["referenceNull"],"EXACT_ONE_MISSING_SIDE_REQUIRED "+name);
            bool plane=name=="距离1";
            Require((int)rows[0]["referenceType"]==(plane?3:4),"TARGET_SURFACE_TYPE_CHANGED");
            var p=(double[])((double[])rows[0]["parameters"]).Clone();
            if (!plane)
            {
                Require(Math.Abs(p[6]-.001)<1e-9,"TARGET_RADIUS_CHANGED");
                if (name=="同心1" || name=="同心4") p[1]+=(ParametricContext.HeightMm-1917)/1000;
                var healthy=(double[])rows[1]["parameters"];
                Require(Math.Abs(p[1]-healthy[1])<1e-9 && Math.Abs(p[2]-healthy[2])<1e-9 && Math.Abs(p[6]-healthy[6])<1e-9,"PAIR_AXES_DO_NOT_AGREE "+name);
            }
            targets.Add(new MateTarget{Name=name,Params=p,Plane=plane,Settings=Settings(feature)});
        }
        Input.picks=ResolveCurrentPicks(owner,targets);
        var component=OneRight(owner,Input.right);
        // Resolve all five faces before mutating any mate, then resolve each again before its edit.
        foreach (var t in targets) FindTargetFace(component,t);
        foreach (var t in targets)
        {
            var feature=TargetFeature(owner,t.Name); var mate=(Mate2)feature.GetSpecificFeature2();
            Require(mate.GetMateEntityCount()==2 && mate.MateEntity(0).Reference==null,"TARGET_CHANGED_BEFORE_EDIT "+t.Name);
            var good=mate.MateEntity(1).Reference as Entity;
            var face=(Entity)FindTargetFace(OneRight(owner,Input.right),t);
            owner.ClearSelection2(true);
            var mgr=(SelectionMgr)owner.SelectionManager;
            var sel=mgr.CreateSelectData(); sel.Mark=1;
            Require(good!=null && feature.Select2(false,0) && face.Select4(true,sel) && good.Select4(true,sel) &&
                mgr.GetSelectedObjectCount2(-1)==3 && mgr.GetSelectedObjectCount2(1)==2 &&
                SameComObject(mgr.GetSelectedObject6(1,1),face) && SameComObject(mgr.GetSelectedObject6(2,1),good),
                "EXACT_MATE_SELECTION_FAILED "+t.Name);
            int status;
            var asm=(AssemblyDoc)owner;
            if (t.Plane)
            {
                var d=(DistanceMateFeatureData)feature.GetDefinition();
                Require(!d.IsAdvancedMate,"UNEXPECTED_ADVANCED_DISTANCE_MATE");
                asm.EditDistanceMate(d.MateAlignment,d.FlipDimension,d.Distance,d.MaximumDistance,d.MinimumDistance,
                    mate.DistanceFirstArcCondition,mate.DistanceSecondArcCondition,out status);
            }
            else
            {
                var d=(ConcentricMateFeatureData)feature.GetDefinition();
                asm.EditMate4(mate.Type,mate.Alignment,mate.Flipped,0,0,0,1,1,0,0,0,false,d.LockRotation,0,true,out status);
            }
            owner.ClearSelection2(true);
            Record(D("inspection","target_edit","mate",t.Name,"status",status));
            Require(status==(int)swAddMateError_e.swAddMateError_NoError,"TARGET_MATE_EDIT_FAILED "+t.Name+" "+status);
            var repaired=(Mate2)TargetFeature(owner,t.Name).GetSpecificFeature2();
            Require(repaired.MateEntity(0).Reference!=null,"TARGET_REFERENCE_STILL_MISSING "+t.Name);
        }
        ParametricContext.AssertMutation(Input.owner);
        RebuildAttempted=true;
        Require(owner.ForceRebuild3(false),"TARGET_OWNER_REBUILD_FAILED");
        ConfirmTargetedState(owner,before,targets,"after-targeted-rebuild");
        ParametricContext.AssertMutation(Input.owner);
        int errors=0,warnings=0; SaveAttempted=true;
        bool saved=owner.Save3(1,ref errors,ref warnings); OwnerSaved=true;
        Record(D("inspection","owner_save","saved",saved,"errors",errors,"warnings",warnings));
        Require(saved && errors==0 && warnings==0 && !owner.GetSaveFlag(),"STRICT_OWNER_SAVE_FAILED");
        ConfirmTargetedState(owner,before,targets,"after-targeted-save");
        SavedTargets=targets; SavedComponents=before["components"];
        Record(D("inspection","owner_saved_sha256","path",Input.owner,"sha256",Hash(Input.owner),"rightPartReplaced",false,"modelAccepted",false));
    }
}
}
