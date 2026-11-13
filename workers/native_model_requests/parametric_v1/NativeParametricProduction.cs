using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        internal const string ProductionProcessVersion = "indoor-weld-locators-20261029-r1";
        private static IEnumerable<Face2> ProductionFaces(ModelDoc2 model)
        {
            foreach (Body2 body in (Array)((PartDoc)model).GetBodies2(0, false))
                foreach (Face2 face in (Array)body.GetFaces()) yield return face;
        }
        private static void ProductionProbePart(ISldWorks sw, string path, List<object> rows)
        {
            string before = ParametricContext.Hash(path);
            ModelDoc2 model = Open(sw, path, true);
            try
            {
                var sketches = new List<object>();
                foreach (Feature feature in PhysicalFeatures(model))
                {
                    Sketch sketch = feature.GetSpecificFeature2() as Sketch;
                    if (sketch == null) continue;
                    var segments = new List<object>();
                    foreach (object segment in sketch.GetSketchSegments() as Array ?? new object[0])
                    {
                        SketchArc arc = segment as SketchArc;
                        if (arc != null) { SketchPoint center = arc.GetCenterPoint2() as SketchPoint; segments.Add(new { kind="arc", circle=arc.IsCircle(), radiusMm=arc.GetRadius()*1000, center=new[]{center.X,center.Y,center.Z} }); }
                    }
                    sketches.Add(new { name=feature.Name, suppressed=feature.IsSuppressed(), transform=sketch.ModelToSketchTransform.ArrayData, circles=segments });
                }
                var faces=ProductionFaces(model).Select(face=>{
                    Surface surface=face.GetSurface() as Surface;
                    Feature feature=face.GetFeature() as Feature;
                    return new {owner=feature==null?null:feature.Name, box=face.GetBox(), normal=face.Normal, area=face.GetArea(), cylinder=surface.IsCylinder()?surface.CylinderParams:null, plane=surface.IsPlane()?surface.PlaneParams:null};
                }).ToArray();
                rows.Add(new { inspection="production_source_geometry", path=path, sha256=before, readOnly=model.IsOpenedReadOnly(), boxMm=BodyBox(model), sketches=sketches, faces=faces });
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            ParametricContext.Require(ParametricContext.Hash(path)==before,"PRODUCTION_PROBE_CHANGED_SOURCE");
        }
        private static void ProductionProbe(ISldWorks sw, List<object> rows)
        {
            foreach (string name in new[]{"箱体横层板L.sldprt","箱体横层板R.SLDPRT","箱体左侧板.sldprt","箱体右侧板.sldprt","箱体竖隔板L.sldprt","箱体竖隔板R.SLDPRT","箱体侧板加强筋1.sldprt"}) ProductionProbePart(sw,Find(name),rows);
            foreach(string name in new[]{"left_panel_seed","stiffener_seed","latch_plate","hook_pad"}) ProductionProbePart(sw,Path.Combine(ParametricContext.AttemptDirectory,"door_sources",name+".SLDPRT"),rows);
            string doors=Path.Combine(ParametricContext.AttemptDirectory,"doors");
            if(Directory.Exists(doors)) foreach(string path in Directory.GetFiles(doors,"*.SLDPRT",SearchOption.AllDirectories).Where(path=>Path.GetFileName(path).StartsWith("door_panel_"))) ProductionProbePart(sw,path,rows);
        }
        private static object ProductionBaseFlatGeometry(ModelDoc2 model)
        {
            Array bodies = ((PartDoc)model).GetBodies2(0, false) as Array;
            ParametricContext.Require(bodies != null && bodies.Length == 1, "BASE_FLAT_BODY_COUNT_INVALID");
            Body2 body = bodies.GetValue(0) as Body2;
            ParametricContext.Require(body != null && body.IsSheetMetal(), "BASE_FLAT_SHEET_METAL_MISSING");
            double[] bounds = BodyBox(model);
            var holes = new List<double>();
            foreach (Face2 face in (Array)body.GetFaces())
            {
                Surface surface = face.GetSurface() as Surface;
                Feature owner = face.GetFeature() as Feature;
                if (surface == null || !surface.IsCylinder() || owner == null || owner.Name != "切除-拉伸_调整脚Ø15_X335_V33") continue;
                double[] cylinder = surface.CylinderParams as double[];
                if (cylinder != null && cylinder.Length >= 7 && Math.Abs(cylinder[6] * 1000 - 7.5) < .01) holes.Add(cylinder[0] * 1000);
            }
            ParametricContext.Require(Math.Abs(bounds[0] + 589.7) < .05 && Math.Abs(bounds[3] - 589.7) < .05 &&
                holes.Count == 2 && holes.Any(x => Math.Abs(x + 565) < .05) && holes.Any(x => Math.Abs(x - 565) < .05),
                "BASE_FLAT_WIDTH_OR_ADJUSTMENT_HOLES_INVALID");
            if (model.ConfigurationManager.ActiveConfiguration.Name == "DefaultSM-FLAT-PATTERN")
                ParametricContext.Require(Math.Abs(bounds[1]) < .05 && Math.Abs(bounds[4] - 2) < .05 &&
                    Math.Abs(bounds[2] + 137) < .05 && Math.Abs(bounds[5] + 23.6) < .05,
                    "BASE_FLAT_THICKNESS_OR_DEPTH_INVALID");
            var errors = PhysicalFeatures(model).Where(feature => !feature.IsSuppressed() && feature.GetErrorCode() != 0)
                .Select(feature => new { name = feature.Name, code = feature.GetErrorCode() }).ToArray();
            ParametricContext.Require(errors.Length == 0, "BASE_FLAT_FEATURE_ERRORS_PRESENT");
            return new { configuration = model.ConfigurationManager.ActiveConfiguration.Name, boundsMm = bounds, adjustmentHoleXmm = holes.OrderBy(x => x).ToArray(), featureErrorCount = errors.Length };
        }
        private static void RepairProductionBaseFlatPattern(ISldWorks sw, List<object> rows)
        {
            if (Math.Abs(ParametricContext.WidthMm - 1200) > .001 || Math.Abs(ParametricContext.HeightMm - 1917) > .001 ||
                Math.Abs(ParametricContext.DepthMm - 485) > .001) return;
            RequireProductionProfile();
            sw.CloseAllDocuments(true);
            string path = Find("底座加强筋.sldprt"), before = ParametricContext.Hash(path);
            ModelDoc2 model = Open(sw, path, false);
            string foldedGeometry;
            try
            {
                ParametricContext.Require(!model.IsOpenedReadOnly(), "BASE_FLAT_REPAIR_MODEL_READONLY");
                ParametricContext.Require(model.ConfigurationManager.ActiveConfiguration.Name == "Default" || model.ShowConfiguration2("Default"), "BASE_FLAT_DEFAULT_ACTIVATION_FAILED");
                ParametricContext.Require(model.ForceRebuild3(false), "BASE_FLAT_DEFAULT_REBUILD_FAILED");
                object folded = ProductionBaseFlatGeometry(model);
                foldedGeometry = PackageGeometryState(model);
                const string name = "DefaultSM-FLAT-PATTERN";
                Configuration old = model.GetConfigurationByName(name) as Configuration;
                ParametricContext.Require(old != null && old.IsDerived() && ((Configuration)old.GetParent()).Name == "Default", "BASE_FLAT_OLD_CONFIGURATION_INVALID");
                ParametricContext.Require(model.DeleteConfiguration2(name) && model.GetConfigurationByName(name) == null, "BASE_FLAT_CONFIGURATION_DELETE_FAILED");
                Configuration created = model.ConfigurationManager.AddConfiguration2(name, "", "", 0, "Default", "", true);
                ParametricContext.Require(created != null && created.Name == name && created.IsDerived() &&
                    ((Configuration)created.GetParent()).Name == "Default" && model.ConfigurationManager.ActiveConfiguration.Name == name,
                    "BASE_FLAT_CONFIGURATION_RECREATE_FAILED");
                Feature flat = PhysicalFeatures(model).SingleOrDefault(feature => feature.Name == "平板型式1");
                ParametricContext.Require(flat != null && (!flat.IsSuppressed() || flat.SetSuppression2(
                    (int)SolidWorks.Interop.swconst.swFeatureSuppressionAction_e.swUnSuppressFeature,
                    (int)SolidWorks.Interop.swconst.swInConfigurationOpts_e.swThisConfiguration, null)), "BASE_FLAT_FEATURE_UNSUPPRESS_FAILED");
                ParametricContext.Require(model.ForceRebuild3(false), "BASE_FLAT_RECREATED_REBUILD_FAILED");
                object unfolded = ProductionBaseFlatGeometry(model);
                Save(model);
                ParametricContext.Require(model.ShowConfiguration2("Default") && model.ForceRebuild3(false), "BASE_FLAT_DEFAULT_RESTORE_FAILED");
                ProductionBaseFlatGeometry(model);
                ParametricContext.Require(PackageGeometryState(model) == foldedGeometry, "BASE_FLAT_DEFAULT_GEOMETRY_CHANGED");
                Save(model);
                rows.Add(new { inspection = "production_base_flat_configuration_repair", path, oldSha256 = before,
                    newSha256 = ParametricContext.Hash(path), folded, unfolded, saved = true });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(model); }
            string after = ParametricContext.Hash(path);
            ParametricContext.Require(before != after, "BASE_FLAT_REPAIR_NOT_PERSISTED");
            ModelDoc2 reopened = Open(sw, path, true);
            try
            {
                ParametricContext.Require(reopened.IsOpenedReadOnly() && reopened.ConfigurationManager.ActiveConfiguration.Name == "Default" &&
                    reopened.ForceRebuild3(false), "BASE_FLAT_REOPEN_DEFAULT_FAILED");
                object folded = ProductionBaseFlatGeometry(reopened);
                ParametricContext.Require(PackageGeometryState(reopened) == foldedGeometry, "BASE_FLAT_REOPEN_DEFAULT_GEOMETRY_CHANGED");
                ParametricContext.Require(reopened.ShowConfiguration2("DefaultSM-FLAT-PATTERN") && reopened.ForceRebuild3(false), "BASE_FLAT_REOPEN_FLAT_FAILED");
                object unfolded = ProductionBaseFlatGeometry(reopened);
                rows.Add(new { inspection = "production_base_flat_reopen_verification", path, sha256 = after, folded, unfolded, readOnly = true, pass = true });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(reopened); }
            ParametricContext.Require(ParametricContext.Hash(path) == after, "BASE_FLAT_REOPEN_CHANGED_SAVED_PART");
        }
        private static object ProductionBottomPlateGeometry(ModelDoc2 model, bool flat)
        {
            Array bodies = ((PartDoc)model).GetBodies2(0, false) as Array;
            ParametricContext.Require(bodies != null && bodies.Length == 1, "BOTTOM_PLATE_BODY_COUNT_INVALID");
            Body2 body = bodies.GetValue(0) as Body2;
            ParametricContext.Require(body != null && body.IsSheetMetal() && body.Check2() == 0, "BOTTOM_PLATE_BODY_INVALID");
            double[] bounds = BodyBox(model);
            var slots = new List<Tuple<double, double>>();
            var cut5 = new List<Tuple<double, double, double>>();
            foreach (Face2 face in (Array)body.GetFaces())
            {
                Surface surface = face.GetSurface() as Surface;
                Feature owner = face.GetFeature() as Feature;
                if (surface == null || !surface.IsCylinder() || owner == null) continue;
                double[] cylinder = surface.CylinderParams as double[];
                if (cylinder == null || cylinder.Length < 7) continue;
                if (owner.Name == "切除-拉伸5")
                {
                    ParametricContext.Require(Math.Abs(cylinder[3]) < .001 && Math.Abs(cylinder[4] - 1) < .001 &&
                        Math.Abs(cylinder[5]) < .001, "BOTTOM_PLATE_CUT5_AXIS_DRIFT");
                    cut5.Add(Tuple.Create(cylinder[0] * 1000, cylinder[2] * 1000, cylinder[6] * 1000));
                }
                if (owner.Name == "切除-拉伸2" || owner.Name == "阵列(线性)1")
                {
                    ParametricContext.Require(Math.Abs(cylinder[6] * 1000 - 1.5) < .001 &&
                        Math.Abs(cylinder[3]) < .001 && Math.Abs(cylinder[4] + 1) < .001 &&
                        Math.Abs(cylinder[5]) < .001, "BOTTOM_PLATE_SLOT_RADIUS_OR_AXIS_DRIFT");
                    slots.Add(Tuple.Create(cylinder[0] * 1000, cylinder[2] * 1000));
                }
            }
            double expectedLeft = flat ? -584.6 : -579, expectedRight = flat ? 583.4 : 579;
            ParametricContext.Require(Math.Abs(bounds[0] - expectedLeft) < .05 && Math.Abs(bounds[3] - expectedRight) < .05 &&
                Math.Abs(bounds[1] - (flat ? 27 : 20.8)) < .05 && Math.Abs(bounds[4] - 27.8) < .05 &&
                Math.Abs(bounds[2] - (flat ? -469.6 : -464)) < .05 && Math.Abs(bounds[5] - (flat ? -13.9 : -19.5)) < .05 &&
                slots.Count == 30 && cut5.Count == 16, "BOTTOM_PLATE_BOUNDS_OR_CUT_GEOMETRY_INVALID");
            foreach (double x in new[] { -30.0, -15, 0, 15, 30 })
                foreach (double z in new[] { -149.5, -159.5, -229.5, -239.5, -309.5, -319.5 })
                    ParametricContext.Require(slots.Count(slot => Math.Abs(slot.Item1 - x) < .05 && Math.Abs(slot.Item2 - z) < .05) == 1,
                        "BOTTOM_PLATE_SLOT_POSITION_OR_MULTIPLICITY_INVALID");
            foreach (double x in new[] { -525.0, 525 })
                foreach (double z in new[] { -435.5, -74.5 })
                    foreach (double radius in new[] { 20.5, 22.5 })
                        ParametricContext.Require(cut5.Count(face => Math.Abs(face.Item1 - x) < .05 && Math.Abs(face.Item2 - z) < .05 &&
                            Math.Abs(face.Item3 - radius) < .01) == 2, "BOTTOM_PLATE_CUT5_POSITION_RADIUS_OR_MULTIPLICITY_INVALID");
            var errors = PhysicalFeatures(model).Where(feature => !feature.IsSuppressed() && feature.GetErrorCode() != 0)
                .Select(feature => new { name = feature.Name, code = feature.GetErrorCode() }).ToArray();
            ParametricContext.Require(errors.Length == 0, "BOTTOM_PLATE_FEATURE_ERRORS_PRESENT");
            return new { configuration = model.ConfigurationManager.ActiveConfiguration.Name, boundsMm = bounds, slotEndCount = slots.Count,
                cut5CylinderCount = cut5.Count, featureErrorCount = errors.Length };
        }
        private static void RepairProductionBottomPlateFlatPattern(ISldWorks sw, List<object> rows)
        {
            if (Math.Abs(ParametricContext.WidthMm - 1200) > .001 || Math.Abs(ParametricContext.HeightMm - 1917) > .001 ||
                Math.Abs(ParametricContext.DepthMm - 485) > .001) return;
            RequireProductionProfile();
            sw.CloseAllDocuments(true);
            string path = Find("底座底板.sldprt"), before = ParametricContext.Hash(path);
            ModelDoc2 model = Open(sw, path, false);
            string foldedGeometry;
            try
            {
                ParametricContext.Require(!model.IsOpenedReadOnly(), "BOTTOM_PLATE_REPAIR_MODEL_READONLY");
                ParametricContext.Require(model.ConfigurationManager.ActiveConfiguration.Name == "Default" || model.ShowConfiguration2("Default"), "BOTTOM_PLATE_DEFAULT_ACTIVATION_FAILED");
                ParametricContext.Require(model.ForceRebuild3(false), "BOTTOM_PLATE_DEFAULT_REBUILD_FAILED");
                object folded = ProductionBottomPlateGeometry(model, false);
                foldedGeometry = PackageGeometryState(model);
                const string name = "DefaultSM-FLAT-PATTERN";
                Configuration old = model.GetConfigurationByName(name) as Configuration;
                ParametricContext.Require(old != null && old.IsDerived() && ((Configuration)old.GetParent()).Name == "Default", "BOTTOM_PLATE_OLD_CONFIGURATION_INVALID");
                ParametricContext.Require(model.DeleteConfiguration2(name) && model.GetConfigurationByName(name) == null, "BOTTOM_PLATE_CONFIGURATION_DELETE_FAILED");
                Configuration created = model.ConfigurationManager.AddConfiguration2(name, "", "", 0, "Default", "", true);
                ParametricContext.Require(created != null && created.Name == name && created.IsDerived() &&
                    ((Configuration)created.GetParent()).Name == "Default" && model.ConfigurationManager.ActiveConfiguration.Name == name,
                    "BOTTOM_PLATE_CONFIGURATION_RECREATE_FAILED");
                Feature flat = PhysicalFeatures(model).SingleOrDefault(feature => feature.Name == "平板型式1");
                ParametricContext.Require(flat != null && (!flat.IsSuppressed() || flat.SetSuppression2(
                    (int)SolidWorks.Interop.swconst.swFeatureSuppressionAction_e.swUnSuppressFeature,
                    (int)SolidWorks.Interop.swconst.swInConfigurationOpts_e.swThisConfiguration, null)), "BOTTOM_PLATE_FLAT_UNSUPPRESS_FAILED");
                ParametricContext.Require(model.ForceRebuild3(false), "BOTTOM_PLATE_RECREATED_REBUILD_FAILED");
                object unfolded = ProductionBottomPlateGeometry(model, true);
                Save(model);
                ParametricContext.Require(model.ShowConfiguration2("Default") && model.ForceRebuild3(false), "BOTTOM_PLATE_DEFAULT_RESTORE_FAILED");
                ProductionBottomPlateGeometry(model, false);
                ParametricContext.Require(PackageGeometryState(model) == foldedGeometry, "BOTTOM_PLATE_DEFAULT_GEOMETRY_CHANGED");
                Save(model);
                rows.Add(new { inspection = "production_bottom_plate_flat_configuration_repair", path, oldSha256 = before,
                    newSha256 = ParametricContext.Hash(path), folded, unfolded, saved = true });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(model); }
            string after = ParametricContext.Hash(path);
            ParametricContext.Require(before != after, "BOTTOM_PLATE_REPAIR_NOT_PERSISTED");
            ModelDoc2 reopened = Open(sw, path, true);
            try
            {
                ParametricContext.Require(reopened.IsOpenedReadOnly() && reopened.ConfigurationManager.ActiveConfiguration.Name == "Default" &&
                    reopened.ForceRebuild3(false), "BOTTOM_PLATE_REOPEN_DEFAULT_FAILED");
                object folded = ProductionBottomPlateGeometry(reopened, false);
                ParametricContext.Require(PackageGeometryState(reopened) == foldedGeometry, "BOTTOM_PLATE_REOPEN_DEFAULT_GEOMETRY_CHANGED");
                ParametricContext.Require(reopened.ShowConfiguration2("DefaultSM-FLAT-PATTERN") && reopened.ForceRebuild3(false), "BOTTOM_PLATE_REOPEN_FLAT_FAILED");
                object unfolded = ProductionBottomPlateGeometry(reopened, true);
                rows.Add(new { inspection = "production_bottom_plate_flat_reopen_verification", path, sha256 = after,
                    folded, unfolded, readOnly = true, pass = true });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(reopened); }
            ParametricContext.Require(ParametricContext.Hash(path) == after, "BOTTOM_PLATE_REOPEN_CHANGED_SAVED_PART");
        }
        private static void ApplyProductionProcess(ISldWorks sw, List<object> rows)
        {
            RequireProductionProfile();
            var before = ProductionInventory(ParametricContext.CadDirectory);
            var doorsBefore = ProductionInventory(Path.Combine(ParametricContext.AttemptDirectory,"doors"));
            int referencePreference=(int)SolidWorks.Interop.swconst.swUserPreferenceToggle_e.swExtRefOpenReadOnly;
            bool previousReferencesReadonly=sw.GetUserPreferenceToggle(referencePreference);
            ModelDoc2 root=null;
            try
            {
                sw.SetUserPreferenceToggle(referencePreference,false);
                ParametricContext.Require(!sw.GetUserPreferenceToggle(referencePreference),"PRODUCTION_APPLY_REFERENCES_NOT_WRITABLE");
                rows.Add(new{inspection="production_apply_reference_policy",previous=previousReferencesReadonly,requested=false});
                root=Open(sw,Find("标准寄存柜1917×1000×550(总装配).SLDASM"),false);
                ParametricContext.Require(!root.IsOpenedReadOnly(),"PRODUCTION_APPLY_ROOT_READONLY");
                ((AssemblyDoc)root).ResolveAllLightWeightComponents(false);
                var components = ProductionComponents(root);
                var transforms = ProductionTransforms(components);
                var documents = components.GroupBy(c=>c.GetPathName(),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>(ModelDoc2)g.First().GetModelDoc2(),StringComparer.OrdinalIgnoreCase);
                var shelfPair=CaptureProductionShelfPair(sw,documents,components,rows);
                foreach(var group in components.Where(c=>PhysicalPartRole(Path.GetFileName(c.GetPathName()))=="door_panel").GroupBy(c=>c.GetPathName(),StringComparer.OrdinalIgnoreCase))
                    ApplyProductionDoor(sw,documents[group.Key],group.ToList(),rows);
                ApplyProductionShelfPair(sw,documents,shelfPair,rows);
                ApplyProductionMarks(sw,root,rows);
                var mutationKinds=new HashSet<string>(new[]{"production_pin_change","production_notch_change","production_locator_change","production_marks_apply"},StringComparer.Ordinal);
                var saveTargets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(object item in rows)
                {
                    var operation=(Dictionary<string,object>)ParametricContext.Json.DeserializeObject(ParametricContext.Json.Serialize(item));
                    if(!mutationKinds.Contains(ParametricContext.Text(operation,"inspection")))continue;
                    string path=Path.GetFullPath(ParametricContext.Text(operation,"path"));
                    ParametricContext.CheckPath(path,ParametricContext.CadDirectory);
                    ParametricContext.Require(before.ContainsKey(path)&&documents.ContainsKey(path)&&Path.GetExtension(path).Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase),"PRODUCTION_SAVE_TARGET_INVALID "+path);
                    saveTargets.Add(path);
                }
                ParametricContext.Require(saveTargets.Count>0,"PRODUCTION_SAVE_TARGETS_MISSING");
                foreach(string path in saveTargets) { ProductionPartHealth(documents[path]); Save(documents[path]); }
                ParametricContext.Require(root.ForceRebuild3(false),"PRODUCTION_ROOT_REBUILD_FAILED");
                RequireProductionTransforms(root,transforms);
                foreach(var group in components.Where(c=>PhysicalPartRole(Path.GetFileName(c.GetPathName()))=="door_panel").GroupBy(c=>c.GetPathName(),StringComparer.OrdinalIgnoreCase))
                    VerifyProductionDoor(documents[group.Key],group.ToList(),rows);
                foreach(var entry in before.Where(entry=>!saveTargets.Contains(entry.Key)))
                    ParametricContext.Require(ParametricContext.Hash(entry.Key)==entry.Value,"PRODUCTION_NON_TARGET_FILE_CHANGED "+entry.Key);
                rows.Add(new{inspection="production_save_scope",targetPaths=saveTargets.OrderBy(path=>path,StringComparer.OrdinalIgnoreCase).ToArray(),unchangedFileCount=before.Count-saveTargets.Count,pass=true});
                WriteProductionRevisions(before,documents,rows);
            }
            finally
            {
                try { sw.CloseAllDocuments(true); }
                finally
                {
                    try
                    {
                        sw.SetUserPreferenceToggle(referencePreference,previousReferencesReadonly);
                        bool restored=sw.GetUserPreferenceToggle(referencePreference)==previousReferencesReadonly;
                        rows.Add(new{inspection="production_apply_reference_policy_restored",previous=previousReferencesReadonly,restored});
                        ParametricContext.Require(restored,"PRODUCTION_APPLY_REFERENCE_POLICY_NOT_RESTORED");
                    }
                    finally { if(root!=null)Marshal.ReleaseComObject(root); }
                }
            }
            RequireProductionInventory(doorsBefore,"PRODUCTION_CHANGED_DOOR_SOURCE");
        }
        private static void VerifyProductionProcess(ISldWorks sw, List<object> rows)
        {
            RequireProductionProfile();
            var before=ProductionInventory(ParametricContext.CadDirectory);
            var doorsBefore=ProductionInventory(Path.Combine(ParametricContext.AttemptDirectory,"doors"));
            string applyPath=Path.Combine(ParametricContext.AttemptDirectory,"evidence","production-apply.json");
            var apply=ParametricContext.Read(applyPath);
            ParametricContext.Require(Convert.ToBoolean(apply["success"]),"PRODUCTION_APPLY_NOT_SUCCESSFUL");
            string rootPath=Find("标准寄存柜1917×1000×550(总装配).SLDASM");
            Array recursive=sw.GetDocumentDependencies2(rootPath,true,true,false) as Array;
            ParametricContext.Require(recursive!=null&&recursive.Length>0&&recursive.Length%2==0,"PRODUCTION_VERIFY_DEPENDENCIES_MISSING");
            var closure=new HashSet<string>(StringComparer.OrdinalIgnoreCase){rootPath};
            for(int i=1;i<recursive.Length;i+=2)
            {
                string path=Convert.ToString(recursive.GetValue(i));
                if(!IsPackageCad(path))continue;
                path=Path.GetFullPath(path);ParametricContext.CheckPath(path,ParametricContext.CadDirectory);
                ParametricContext.Require(before.ContainsKey(path),"PRODUCTION_VERIFY_UNMAPPED_DEPENDENCY "+path);closure.Add(path);
            }
            int referencePreference=(int)SolidWorks.Interop.swconst.swUserPreferenceToggle_e.swExtRefOpenReadOnly;
            bool previousReferencesReadonly=sw.GetUserPreferenceToggle(referencePreference);
            string policyPath=Path.Combine(ParametricContext.AttemptDirectory,"evidence","production-verify.readonly-policy.json");
            ParametricContext.Write(policyPath,new{inspection="production_readonly_reference_policy",previous=previousReferencesReadonly,requested=true,restored=false});
            ModelDoc2 root=null;
            try
            {
                sw.SetUserPreferenceToggle(referencePreference,true);
                ParametricContext.Require(sw.GetUserPreferenceToggle(referencePreference),"PRODUCTION_VERIFY_REFERENCE_READONLY_POLICY_FAILED");
                root=Open(sw,rootPath,true);
                ParametricContext.Require(root.IsOpenedReadOnly(),"PRODUCTION_VERIFY_ROOT_NOT_READONLY");
                foreach(ModelDoc2 document in sw.GetDocuments() as Array??new object[0])
                    ParametricContext.Require(closure.Contains(document.GetPathName())&&document.IsOpenedReadOnly(),"PRODUCTION_VERIFY_ROOT_IMPLICIT_DOCUMENT_NOT_READONLY "+document.GetPathName());
                rows.Add(new{inspection="production_readonly_reference_policy",paths=closure.OrderBy(path=>path,StringComparer.OrdinalIgnoreCase).ToArray(),previous=previousReferencesReadonly,requested=true,allLoadedDocumentsReadOnly=true});
                ((AssemblyDoc)root).ResolveAllLightWeightComponents(false);
                var cachedTransforms=ProductionTransforms(ProductionComponents(root));
                ParametricContext.Require(root.ForceRebuild3(false),"PRODUCTION_VERIFY_ROOT_REBUILD_FAILED");
                var rebuiltTransforms=ProductionTransforms(ProductionComponents(root));
                ParametricContext.Require(cachedTransforms.Count==rebuiltTransforms.Count&&cachedTransforms.Keys.All(rebuiltTransforms.ContainsKey),"PRODUCTION_VERIFY_REBUILD_COMPONENT_SET_CHANGED");
                rows.Add(new{inspection="production_readonly_root_rebuild",rebuildSucceeded=true,componentCount=rebuiltTransforms.Count,
                    changedTransforms=cachedTransforms.Where(entry=>!entry.Value.Zip(rebuiltTransforms[entry.Key],(a,b)=>Math.Abs(a-b)<1e-9).All(equal=>equal))
                        .Select(entry=>new{component=entry.Key,before=entry.Value,after=rebuiltTransforms[entry.Key]}).ToArray(),saved=false});
                foreach(ModelDoc2 document in sw.GetDocuments() as Array??new object[0])
                    ParametricContext.Require(closure.Contains(document.GetPathName())&&document.IsOpenedReadOnly(),"PRODUCTION_VERIFY_RESOLVED_DOCUMENT_NOT_READONLY "+document.GetPathName());
                var components=ProductionComponents(root);
                foreach(var group in components.Where(c=>PhysicalPartRole(Path.GetFileName(c.GetPathName()))=="door_panel").GroupBy(c=>c.GetPathName(),StringComparer.OrdinalIgnoreCase))
                    VerifyProductionDoor((ModelDoc2)group.First().GetModelDoc2(),group.ToList(),rows);
                VerifyProductionMarks(sw,root,rows);
                foreach(Dictionary<string,object> change in (IEnumerable)apply["rows"])
                {
                    string inspection=ParametricContext.Text(change,"inspection");
                    if(inspection=="production_pin_change") VerifyProductionPinBaseline(change,components,rows);
                    if(inspection=="production_locator_change") VerifyProductionLocatorChange(sw,change,components,rows);
                    if(inspection!="production_part_revision")continue;
                    string path=ParametricContext.Text(change,"path");
                    ModelDoc2 model=(ModelDoc2)components.First(c=>c.GetPathName().Equals(path,StringComparison.OrdinalIgnoreCase)).GetModelDoc2();
                    ProductionPartHealth(model);
                    ParametricContext.Require(ParametricContext.Hash(path)==ParametricContext.Text(change,"newProductionSha256"),"PRODUCTION_REVISION_HASH_DRIFT");
                    rows.Add(new{inspection="production_part_verification",file=Path.GetFileName(path),path=path,sha256=ParametricContext.Hash(path),role=PhysicalPartRole(Path.GetFileName(path)),productionProfile=ProductionProcessVersion,pass=true});
                }
                VerifyProductionMarksEvidence(apply,rows);
                foreach(ModelDoc2 document in sw.GetDocuments() as Array??new object[0])
                    ParametricContext.Require(closure.Contains(document.GetPathName())&&document.IsOpenedReadOnly(),"PRODUCTION_VERIFY_FINAL_DOCUMENT_NOT_READONLY "+document.GetPathName());
            }
            finally
            {
                try { sw.CloseAllDocuments(true); }
                finally
                {
                    try
                    {
                        sw.SetUserPreferenceToggle(referencePreference,previousReferencesReadonly);
                        bool restored=sw.GetUserPreferenceToggle(referencePreference)==previousReferencesReadonly;
                        ParametricContext.Write(policyPath,new{inspection="production_readonly_reference_policy",previous=previousReferencesReadonly,requested=true,restored=restored});
                        rows.Add(new{inspection="production_readonly_reference_policy_restored",previous=previousReferencesReadonly,restored=restored});
                        ParametricContext.Require(restored,"PRODUCTION_VERIFY_REFERENCE_READONLY_POLICY_NOT_RESTORED");
                    }
                    finally { if(root!=null)Marshal.ReleaseComObject(root); }
                }
            }
            RequireProductionInventory(before,"PRODUCTION_VERIFY_CHANGED_CAD");
            RequireProductionInventory(doorsBefore,"PRODUCTION_VERIFY_CHANGED_DOOR_SOURCE");
        }
        private static void RequireProductionSolidMatch(Dictionary<string,object> expected,Dictionary<string,object> actual)
        {
            foreach(string key in new[]{"geometrySha256","bodies","vertices","edges","faces"})
                ParametricContext.Require(ParametricContext.Text(expected,key)==ParametricContext.Text(actual,key),"PRODUCTION_MARK_SOLID_STATE_DRIFT "+key);
            double volume=ParametricContext.Number(expected,"volumeM3"),current=ParametricContext.Number(actual,"volumeM3");
            ParametricContext.Require(!double.IsNaN(volume)&&!double.IsInfinity(volume)&&volume>0&&!double.IsNaN(current)&&!double.IsInfinity(current)&&Math.Abs(volume-current)<=Math.Max(1e-12,volume*1e-9),"PRODUCTION_MARK_VOLUME_DRIFT");
            double[] a=((IEnumerable)expected["exactBoundsM"]).Cast<object>().Select(Convert.ToDouble).ToArray(),b=((IEnumerable)actual["exactBoundsM"]).Cast<object>().Select(Convert.ToDouble).ToArray();
            ParametricContext.Require(a.Length==6&&b.Length==6&&a.Zip(b,(x,y)=>!double.IsNaN(x)&&!double.IsInfinity(x)&&!double.IsNaN(y)&&!double.IsInfinity(y)&&Math.Abs(x-y)<=1e-8).All(x=>x),"PRODUCTION_MARK_EXACT_BOUNDS_DRIFT");
        }
        private static List<Dictionary<string,object>> ProductionMarkEvidenceRows(Dictionary<string,object> item,string field)
        {
            ParametricContext.Require(item.ContainsKey(field)&&item[field] is IEnumerable&&!(item[field] is string),"PRODUCTION_MARK_EVIDENCE_ARRAY_MISSING "+field);
            var result=((IEnumerable)item[field]).Cast<Dictionary<string,object>>().ToList();
            ParametricContext.Require(result.Count>0&&result.All(row=>row!=null),"PRODUCTION_MARK_EVIDENCE_ARRAY_EMPTY "+field);
            return result;
        }
        private static string ProductionMarkRelationIdentity(Dictionary<string,object> item)
        {
            string[] values=new[]{"sourceComponent","sourcePath","sourceConfiguration","targetComponent","targetPath","targetConfiguration"}.Select(field=>ParametricContext.Text(item,field)).ToArray();
            ParametricContext.Require(values.All(value=>!String.IsNullOrWhiteSpace(value)),"PRODUCTION_MARK_SOURCE_IDENTITY_MISSING");
            return ParametricContext.Json.Serialize(values);
        }
        private static double ProductionMarkEvidenceNumber(Dictionary<string,object> item,string field)
        {
            ParametricContext.Require(item.ContainsKey(field)&&item[field]!=null,"PRODUCTION_MARK_SOURCE_NUMBER_MISSING "+field);
            double value=ParametricContext.Number(item,field);
            ParametricContext.Require(!Double.IsNaN(value)&&!Double.IsInfinity(value),"PRODUCTION_MARK_SOURCE_NUMBER_INVALID "+field);
            return value;
        }
        private static void RequireProductionMarkNumberArrayMatch(Dictionary<string,object> expected,Dictionary<string,object> actual,string field,int length,double tolerance)
        {
            ParametricContext.Require(expected.ContainsKey(field)&&actual.ContainsKey(field)&&expected[field] is IEnumerable&&actual[field] is IEnumerable&&!(expected[field] is string)&&!(actual[field] is string),"PRODUCTION_MARK_SOURCE_ARRAY_MISSING "+field);
            double[] a=((IEnumerable)expected[field]).Cast<object>().Select(Convert.ToDouble).ToArray(),b=((IEnumerable)actual[field]).Cast<object>().Select(Convert.ToDouble).ToArray();
            ParametricContext.Require(a.Length==length&&b.Length==length&&a.Zip(b,(x,y)=>!Double.IsNaN(x)&&!Double.IsInfinity(x)&&!Double.IsNaN(y)&&!Double.IsInfinity(y)&&Math.Abs(x-y)<=tolerance).All(value=>value),"PRODUCTION_MARK_SOURCE_ARRAY_DRIFT "+field);
        }
        private static void RequireProductionMarkRelationMatch(Dictionary<string,object> expected,Dictionary<string,object> actual,string context)
        {
            string relation=ParametricContext.Text(expected,"relation");
            ParametricContext.Require(expected.ContainsKey("actualContact")&&actual.ContainsKey("actualContact")&&expected["actualContact"] is bool&&actual["actualContact"] is bool&&
                (bool)expected["actualContact"]==(bool)actual["actualContact"],"PRODUCTION_MARK_SOURCE_CONTACT_TYPE_DRIFT "+context);
            bool contact=(bool)expected["actualContact"];
            double nominal=relation=="exact_planar_contact"?0:relation=="approved_0p5mm_partition_standoff_projection"?.5:relation=="approved_0p2mm_vertical_rib_standoff_projection"?.2:-1;
            ParametricContext.Require(nominal>=0&&contact==(nominal==0)&&relation==ParametricContext.Text(actual,"relation"),"PRODUCTION_MARK_SOURCE_RELATION_DRIFT "+context);
            ParametricContext.Require(ProductionMarkEvidenceNumber(expected,"nominalStandOffMm")==nominal&&ProductionMarkEvidenceNumber(actual,"nominalStandOffMm")==nominal,"PRODUCTION_MARK_SOURCE_NOMINAL_STANDOFF_DRIFT "+context);
            foreach(string field in new[]{"normalAxis","sourceNormalSign","targetNormalSign"})
            {
                double value=ProductionMarkEvidenceNumber(expected,field),current=ProductionMarkEvidenceNumber(actual,field);
                ParametricContext.Require(value==current&&(field=="normalAxis"?value>=0&&value<=2&&value==Math.Truncate(value):Math.Abs(value)==1),"PRODUCTION_MARK_SOURCE_NORMAL_DRIFT "+field+" "+context);
            }
            foreach(string field in new[]{"sourcePlaneM","contactPlaneM","normalProjectionOffsetM","signedNormalGapM"})
                ParametricContext.Require(Math.Abs(ProductionMarkEvidenceNumber(expected,field)-ProductionMarkEvidenceNumber(actual,field))<=PMMaterialTolerance,"PRODUCTION_MARK_SOURCE_PLANE_DRIFT "+field+" "+context);
            RequireProductionMarkNumberArrayMatch(expected,actual,"sourceToRoot",16,1e-9);
            RequireProductionMarkNumberArrayMatch(expected,actual,"rootToTarget",16,1e-9);
            RequireProductionMarkNumberArrayMatch(expected,actual,"exactSourceBoundsInTargetM",6,PMMaterialTolerance);
            foreach(var item in new[]{expected,actual})
            {
                int axis=(int)ProductionMarkEvidenceNumber(item,"normalAxis");
                double sign=ProductionMarkEvidenceNumber(item,"sourceNormalSign"),offset=ProductionMarkEvidenceNumber(item,"normalProjectionOffsetM"),gap=ProductionMarkEvidenceNumber(item,"signedNormalGapM");
                double sourcePlane=ProductionMarkEvidenceNumber(item,"sourcePlaneM"),targetPlane=ProductionMarkEvidenceNumber(item,"contactPlaneM");
                ParametricContext.Require(ProductionMarkEvidenceNumber(item,"targetNormalSign")==-sign&&Math.Abs(targetPlane-sourcePlane-offset)<=PMMaterialTolerance&&
                    Math.Abs(offset*sign-gap)<=PMMaterialTolerance&&Math.Abs(gap-nominal/1000)<=PMMaterialTolerance,"PRODUCTION_MARK_SOURCE_GEOMETRY_INCONSISTENT "+context);
                string sourceFile=Path.GetFileName(ParametricContext.Text(item,"sourcePath")),targetFile=Path.GetFileName(ParametricContext.Text(item,"targetPath"));
                bool central=sourceFile.Equals("箱体竖隔板加强件.sldprt",StringComparison.OrdinalIgnoreCase),vertical=sourceFile.Equals("箱体侧板加强筋1.sldprt",StringComparison.OrdinalIgnoreCase);
                bool partition=new[]{"箱体竖隔板L.sldprt","箱体竖隔板R.SLDPRT"}.Contains(targetFile,StringComparer.OrdinalIgnoreCase);
                bool side=new[]{"箱体左侧板.sldprt","箱体右侧板.sldprt"}.Contains(targetFile,StringComparer.OrdinalIgnoreCase);
                ParametricContext.Require(nominal==0?!central&&!vertical:nominal==.5?central&&partition&&axis==0:vertical&&(partition&&axis==0||side&&(axis==0||axis==2)),"PRODUCTION_MARK_SOURCE_ROLE_INVALID "+context);
                if(nominal>0)
                {
                    double[] bounds=((IEnumerable)item["exactSourceBoundsInTargetM"]).Cast<object>().Select(Convert.ToDouble).ToArray();
                    ParametricContext.Require(Math.Abs(sourcePlane-bounds[axis+(sign>0?3:0)])<=PMMaterialTolerance,"PRODUCTION_MARK_SOURCE_NOT_OUTER_EXTREME "+context);
                }
            }
            foreach(string field in new[]{"originalContactOuterLoop","projectedContactOuterLoop","materialClippedContactOuterLoop"})
            {
                var a=ProductionMarkEvidenceRows(expected,field).Select(segment=>ParametricContext.Json.ConvertToType<PMSegment>(segment)).ToList();
                var b=ProductionMarkEvidenceRows(actual,field).Select(segment=>ParametricContext.Json.ConvertToType<PMSegment>(segment)).ToList();
                PMSameGeometry(a,b,"cross-stage "+field+" "+context);
            }
        }
        private static void RequireProductionMarkSourcesMatch(Dictionary<string,object> expected,Dictionary<string,object> actual,Dictionary<string,Dictionary<string,object>> sourceRelations,Dictionary<string,Dictionary<string,object>> verifiedRelations,Dictionary<string,HashSet<string>> relationFamilies)
        {
            var before=ProductionMarkEvidenceRows(expected,"marks");
            var after=ProductionMarkEvidenceRows(actual,"marks");
            Func<Dictionary<string,object>,string> family=item=>ParametricContext.Text(item,"family");
            ParametricContext.Require(before.Count==after.Count&&before.All(item=>!String.IsNullOrWhiteSpace(family(item)))&&after.All(item=>!String.IsNullOrWhiteSpace(family(item)))&&
                before.Select(family).Distinct(StringComparer.Ordinal).Count()==before.Count&&after.Select(family).Distinct(StringComparer.Ordinal).Count()==after.Count,"PRODUCTION_MARK_FAMILY_COVERAGE_INVALID");
            foreach(var mark in before)
            {
                var matches=after.Where(item=>family(item)==family(mark)).ToList();
                ParametricContext.Require(matches.Count==1,"PRODUCTION_MARK_FAMILY_MISSING "+family(mark));
                Func<Dictionary<string,object>,Dictionary<string,object>> contact=item=>item.ContainsKey("sourceContact")?ParametricContext.Object(item,"sourceContact"):item;
                var sources=ProductionMarkEvidenceRows(mark,"sourceContactsAndTransforms").Select(contact).ToList();
                var checks=ProductionMarkEvidenceRows(matches[0],"sourceContactsAndTransforms").Select(contact).ToList();
                ParametricContext.Require(sources.Count==checks.Count&&sources.Select(ProductionMarkRelationIdentity).Distinct(StringComparer.Ordinal).Count()==sources.Count&&
                    checks.Select(ProductionMarkRelationIdentity).Distinct(StringComparer.Ordinal).Count()==checks.Count,"PRODUCTION_MARK_SOURCE_COVERAGE_INVALID "+family(mark));
                foreach(var source in sources)
                {
                    string identity=ProductionMarkRelationIdentity(source);
                    var candidates=checks.Where(item=>ProductionMarkRelationIdentity(item)==identity).ToList();
                    ParametricContext.Require(candidates.Count==1,"PRODUCTION_MARK_SOURCE_MISSING "+identity);
                    RequireProductionMarkRelationMatch(source,candidates[0],family(mark)+" "+identity);
                    bool arrow=family(mark)=="侧加强筋朝上箭头与上";
                    string owner=arrow?"source":"target";
                    ParametricContext.Require(ParametricContext.Text(expected,"path").Equals(ParametricContext.Text(source,owner+"Path"),StringComparison.OrdinalIgnoreCase)&&
                        ParametricContext.Text(expected,"configuration")==ParametricContext.Text(source,owner+"Configuration"),"PRODUCTION_MARK_SOURCE_OWNER_INVALID "+family(mark));
                    if(!(bool)source["actualContact"])
                    {
                        bool central=ParametricContext.Text(source,"relation")=="approved_0p5mm_partition_standoff_projection";
                        string endFamily=ProductionMarkEvidenceNumber(source,"normalAxis")==0?"侧加强筋首尾50mm_侧面X":"侧加强筋首尾50mm_后折面Z";
                        ParametricContext.Require(central?family(mark)=="隔板加强件四角L":arrow||family(mark)==endFamily,"PRODUCTION_MARK_PROJECTION_FAMILY_INVALID");
                    }
                    ParametricContext.Require(!arrow||ParametricContext.Text(source,"relation")=="approved_0p2mm_vertical_rib_standoff_projection","PRODUCTION_MARK_ARROW_SOURCE_INVALID");
                    if(sourceRelations.ContainsKey(identity))
                    {
                        RequireProductionMarkRelationMatch(sourceRelations[identity],source,"repeated apply "+identity);
                        RequireProductionMarkRelationMatch(verifiedRelations[identity],candidates[0],"repeated verify "+identity);
                    }
                    else
                    {
                        sourceRelations.Add(identity,source); verifiedRelations.Add(identity,candidates[0]);
                        relationFamilies.Add(identity,new HashSet<string>(StringComparer.Ordinal));
                    }
                    relationFamilies[identity].Add(family(mark));
                }
            }
        }
        private static void VerifyProductionMarksEvidence(Dictionary<string,object> apply,List<object> rows)
        {
            var applied=((IEnumerable)apply["rows"]).Cast<Dictionary<string,object>>().ToList();
            var verified=((IEnumerable)ParametricContext.Json.DeserializeObject(ParametricContext.Json.Serialize(rows))).Cast<Dictionary<string,object>>().Where(r=>ParametricContext.Text(r,"inspection")=="production_marks_verify").ToList();
            var marks=applied.Where(r=>ParametricContext.Text(r,"inspection")=="production_marks_apply").ToList();
            var revisions=applied.Where(r=>ParametricContext.Text(r,"inspection")=="production_part_revision").ToDictionary(r=>ParametricContext.Text(r,"path"),StringComparer.OrdinalIgnoreCase);
            Func<Dictionary<string,object>,string> key=r=>ParametricContext.Text(r,"path")+"|"+ParametricContext.Text(r,"configuration");
            ParametricContext.Require(marks.Count>0&&marks.Count==verified.Count&&marks.Select(key).Distinct(StringComparer.OrdinalIgnoreCase).Count()==marks.Count&&verified.Select(key).Distinct(StringComparer.OrdinalIgnoreCase).Count()==verified.Count,"PRODUCTION_MARK_EVIDENCE_COVERAGE_INVALID");
            var sourceRelations=new Dictionary<string,Dictionary<string,object>>(StringComparer.Ordinal);
            var verifiedRelations=new Dictionary<string,Dictionary<string,object>>(StringComparer.Ordinal);
            var relationFamilies=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
            foreach(var mark in marks)
            {
                string path=ParametricContext.Text(mark,"path");
                ParametricContext.Require(revisions.ContainsKey(path),"PRODUCTION_MARK_REVISION_MISSING "+path);
                var check=verified.Single(r=>key(r).Equals(key(mark),StringComparison.OrdinalIgnoreCase));
                RequireProductionSolidMatch(ParametricContext.Object(mark,"markSolidBefore"),ParametricContext.Object(mark,"markSolidAfter"));
                RequireProductionSolidMatch(ParametricContext.Object(mark,"markSolidAfter"),ParametricContext.Object(check,"markSolidCurrent"));
                ParametricContext.Require(ParametricContext.Text(mark,"protectedFeatureStateSha256")==ParametricContext.Text(check,"protectedFeatureStateSha256")&&ParametricContext.Text(check,"sha256")==ParametricContext.Text(revisions[path],"newProductionSha256"),"PRODUCTION_MARK_FEATURE_OR_FILE_DRIFT");
                RequireProductionMarkSourcesMatch(mark,check,sourceRelations,verifiedRelations,relationFamilies);
            }
            ParametricContext.Require(revisions.Keys.All(path=>marks.Any(mark=>ParametricContext.Text(mark,"path").Equals(path,StringComparison.OrdinalIgnoreCase))),"PRODUCTION_REVISION_WITHOUT_MARK_VERIFICATION");
            var projected=sourceRelations.Values.Where(item=>ParametricContext.Text(item,"relation")=="approved_0p5mm_partition_standoff_projection").ToList();
            var vertical=sourceRelations.Values.Where(item=>ParametricContext.Text(item,"relation")=="approved_0p2mm_vertical_rib_standoff_projection").ToList();
            ParametricContext.Require(projected.Count==4,"PRODUCTION_MARK_PARTITION_PROJECTION_COVERAGE_INVALID");
            Func<Dictionary<string,object>,string,string> instanceKey=(item,prefix)=>ParametricContext.Json.Serialize(new[]{ParametricContext.Text(item,prefix+"Component"),ParametricContext.Text(item,prefix+"Path"),ParametricContext.Text(item,prefix+"Configuration")});
            var projectionSources=projected.GroupBy(item=>instanceKey(item,"source")).ToList();
            ParametricContext.Require(projectionSources.Count==2&&projectionSources.All(group=>group.Count()==2&&group.All(item=>Path.GetFileName(ParametricContext.Text(item,"sourcePath")).Equals("箱体竖隔板加强件.sldprt",StringComparison.OrdinalIgnoreCase))&&
                group.Select(item=>Path.GetFileName(ParametricContext.Text(item,"targetPath"))).OrderBy(value=>value,StringComparer.OrdinalIgnoreCase).SequenceEqual(new[]{"箱体竖隔板L.sldprt","箱体竖隔板R.SLDPRT"},StringComparer.OrdinalIgnoreCase)),"PRODUCTION_MARK_PARTITION_PROJECTION_PAIR_INVALID");
            ParametricContext.Require(vertical.Count==10&&vertical.Select(item=>instanceKey(item,"source")).Distinct(StringComparer.Ordinal).Count()==10,"PRODUCTION_MARK_VERTICAL_PROJECTION_COVERAGE_INVALID");
            var verticalTargets=vertical.GroupBy(item=>instanceKey(item,"target")).ToList();
            ParametricContext.Require(verticalTargets.Count==4&&verticalTargets.Select(group=>Path.GetFileName(ParametricContext.Text(group.First(),"targetPath"))).OrderBy(value=>value,StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(new[]{"箱体右侧板.sldprt","箱体左侧板.sldprt","箱体竖隔板L.sldprt","箱体竖隔板R.SLDPRT"}.OrderBy(value=>value,StringComparer.OrdinalIgnoreCase),StringComparer.OrdinalIgnoreCase)&&
                verticalTargets.All(group=>Path.GetFileName(ParametricContext.Text(group.First(),"targetPath")).StartsWith("箱体竖隔板",StringComparison.OrdinalIgnoreCase)?
                    group.Count()==2&&group.All(item=>ProductionMarkEvidenceNumber(item,"normalAxis")==0):
                    group.Count()==3&&group.Count(item=>ProductionMarkEvidenceNumber(item,"normalAxis")==0)==2&&group.Count(item=>ProductionMarkEvidenceNumber(item,"normalAxis")==2)==1),"PRODUCTION_MARK_VERTICAL_PROJECTION_PAIR_INVALID");
            foreach(var item in vertical)
            {
                string source=ParametricContext.Text(item,"sourceComponent"),target=ParametricContext.Text(item,"targetComponent");
                int sourceSlash=source.LastIndexOf('/'),targetSlash=target.LastIndexOf('/');
                ParametricContext.Require(sourceSlash>0&&targetSlash>0&&source.Substring(0,sourceSlash)==target.Substring(0,targetSlash),"PRODUCTION_MARK_VERTICAL_PROJECTION_PARENT_INVALID");
                string endFamily=ProductionMarkEvidenceNumber(item,"normalAxis")==0?"侧加强筋首尾50mm_侧面X":"侧加强筋首尾50mm_后折面Z";
                ParametricContext.Require(relationFamilies[ProductionMarkRelationIdentity(item)].SetEquals(new[]{endFamily,"侧加强筋朝上箭头与上"}),"PRODUCTION_MARK_VERTICAL_PROJECTION_MARK_COVERAGE_INVALID");
            }
            rows.Add(new{inspection="production_marks_cross_stage_verification",partCount=revisions.Count,configurationCount=marks.Count,solidGeometryUnchanged=true,sourceRelationsUnchanged=true,partitionProjectionRelationCount=projected.Count,verticalRibProjectionRelationCount=vertical.Count,pass=true});
        }
    }
}
