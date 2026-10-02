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
        internal static void RepairMaintenanceStrip(ISldWorks sw, string path, List<object> records)
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
            model = NativeParametricAssembly.Open(sw, path, true);
            try { inspect(model, "reopened"); }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
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
                        "locks-verify", "root", "verify", "package", "verify-package", "verify-relocated", "physical-audit"
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
                try { NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw)
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
                    else if (phase == "repair-height-braces") RepairHeightBraces(sw,rows);
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
                ParametricContext.Write(evidence, new { phase = phase, success = true, rows = rows });
                Console.WriteLine("PARAMETRIC_PHASE_COMPLETE " + phase);
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error.ToString()); return 1; }
        }

        internal static ModelDoc2 Open(ISldWorks sw, string path, bool readOnly)
        {
            ParametricContext.CheckPath(path, ParametricContext.AttemptDirectory);
            int errors = 0, warnings = 0;
            int type = Path.GetExtension(path).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            ModelDoc2 model = sw.OpenDoc6(path, type, 1 | (readOnly ? 2 : 0), "", ref errors, ref warnings) as ModelDoc2;
            ParametricContext.Require(model != null && errors == 0, "OPEN_FAILED " + path + " " + errors + "/" + warnings);
            return model;
        }

        internal static void Save(ModelDoc2 model)
        {
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
        internal static void WithSession(string name, Action<ISldWorks> action)
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
            finally { Winnsen.StructureAgent.Generation.ParametricContext.Write(Path.Combine(Winnsen.StructureAgent.Generation.ParametricContext.AttemptDirectory, "evidence", name + ".sessions.json"), result); }
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
