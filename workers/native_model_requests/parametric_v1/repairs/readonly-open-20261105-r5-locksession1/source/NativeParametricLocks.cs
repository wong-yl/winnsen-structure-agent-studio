using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Winnsen.StructureAgent.Generation;

namespace Winnsen.StructureAgent.Native888
{
    internal static partial class NativeParametricLocks
    {
        private static double[] ActiveRows;
        private static double ActivePatternSpacingMm;
        private static int ActivePatternIndex;
        private static bool ActivePatternReverse;

        private sealed class RightPreparation
        {
            internal string Source, Target, SourceSha256;
            internal double BendRadius, KFactor, BendAllowance, ReliefRatio;
            internal bool AutoRelief;
            internal double[] FixedPlane;
        }

        internal static void RunIsolated(List<object> records, bool readOnly)
        {
            string source = Path.Combine(ParametricContext.CadDirectory, "箱体竖隔板L.sldprt");
            string sourceCopy = Path.Combine(ParametricContext.AttemptDirectory, "lock_seed_left.SLDPRT");
            if (!readOnly && !File.Exists(sourceCopy)) { ParametricContext.AssertMutation(sourceCopy); File.Copy(source, sourceCopy, false); }
            foreach (string side in new[] { "L", "R" })
            {
                RightPreparation right = null;
                Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
                {
                    BeginIsolated(sw, records, "prepare-" + side);
                    right = RunSide(sw, records, readOnly, side, sourceCopy);
                }, "lock-prepare-" + side);
                if (right == null) continue;
                string built = Path.Combine(ParametricContext.AttemptDirectory, "lock_right_native.SLDPRT");
                ParametricContext.AssertMutation(built);
                Require(!File.Exists(built), "LOCK_TEMPORARY_RIGHT_ALREADY_EXISTS", 18, built);
                Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
                {
                    BeginIsolated(sw, records, "mirror-right");
                    CreateRightFromPersistedSource(sw, records, right, built);
                }, "lock-mirror-right");
                InspectRightOnly(records, right, built, "temporary-right-only");
                NativeParametricAssembly.AssertProtectedTemplateInputs();
                ParametricContext.AssertMutation(right.Target);
                ParametricContext.AssertMutation(right.Target + ".before-parametric-lock.bak");
                File.Replace(built, right.Target, right.Target + ".before-parametric-lock.bak");
                NativeParametricAssembly.AssertProtectedTemplateInputs();
                records.Add(new { inspection = "right_committed_after_independent_temporary_readback", path = right.Target, sha256 = ParametricContext.Hash(right.Target) });
                InspectRightOnly(records, right, right.Target, "committed-right-only");
                InspectOwnerOnly(records, Path.Combine(ParametricContext.CadDirectory, "箱体竖隔板R焊接.SLDASM"), "right-weldment-only");
                InspectOwnerOnly(records, Path.Combine(ParametricContext.CadDirectory, "标准寄存柜1917×1000×550(总装配).SLDASM"), "root-owner-only");
                Console.WriteLine("PARAMETRIC_LOCK_COLUMN_VERIFIED R");
            }
        }

        private static void BeginIsolated(ISldWorks sw, List<object> records, string stage)
        {
            sw.SetCurrentWorkingDirectory(ParametricContext.CadDirectory);
            int count = sw.GetDocumentCount();
            records.Add(new { inspection = "lock_session_empty_boundary", stage = stage, processId = sw.GetProcessID(), documentCount = count });
            Require(count == 0, "LOCK_SESSION_REQUIRES_EMPTY_DOCUMENTS", 18, stage);
            NativeParametricAssembly.AssertProtectedTemplateInputs();
        }

        private static RightPreparation RunSide(ISldWorks sw, List<object> records, bool readOnly, string side, string sourceCopy)
        {
            var contractRows = ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "rowsByColumn");
            RightPreparation right = null;
            ActiveRows = ((IEnumerable)contractRows[side]).Cast<Dictionary<string, object>>().Select(row => ParametricContext.Number(row, "centerYmm")).ToArray();
            string target = Directory.GetFiles(ParametricContext.CadDirectory).Single(path => Path.GetFileName(path).Equals(side == "L" ? "箱体竖隔板L.sldprt" : "箱体竖隔板R.SLDPRT", StringComparison.OrdinalIgnoreCase));
            ModelDoc2 model = NativeParametricAssembly.Open(sw, target, true);
            GeometrySnapshot existing;
            double bendRadius=0,kFactor=0,bendAllowance=0,reliefRatio=0;bool autoRelief=false;double[] fixedPlane=null;
            try {
                existing = CaptureGeometry(model, side);
                if(side=="R"&&!GeometryGate(existing,side))
                {
                    Feature sheetFeature=FindFeature(model,"钣金2");
                    SheetMetalFeatureData sheetData=sheetFeature==null?null:sheetFeature.GetDefinition() as SheetMetalFeatureData;
                    Require(sheetData!=null&&sheetData.AccessSelections(model,null),"V35_RIGHT_SHEET_METAL_DEFINITION_MISSING",17,side);
                    try{
                        bendRadius=sheetData.BendRadius;kFactor=sheetData.KFactor;bendAllowance=sheetData.BendAllowance;autoRelief=sheetData.UseAutoRelief;reliefRatio=sheetData.ReliefRatio;
                        Face2 fixedFace=sheetData.FixedReference as Face2;
                        Require(fixedFace!=null,"V35_RIGHT_FIXED_FACE_MISSING",17,side);
                        Surface fixedSurface=fixedFace.GetSurface() as Surface;
                        Require(fixedSurface!=null&&fixedSurface.IsPlane(),"V35_RIGHT_FIXED_FACE_NOT_PLANAR",17,side);
                        fixedPlane=fixedSurface.PlaneParams as double[];
                        records.Add(new{side=side,v35BendRadiusMm=bendRadius*1000,v35ThicknessMm=sheetData.Thickness*1000,v35KFactor=kFactor,v35BendAllowance=bendAllowance,v35BendAllowanceType=sheetData.BendAllowanceType,v35AutoRelief=autoRelief,v35ReliefRatio=reliefRatio,v35FixedPlane=fixedPlane});
                    }finally{sheetData.ReleaseSelectionAccess();}
                }
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            if (GeometryGate(existing, side)) { records.Add(new { side = side, reusedMatchingNativeSlots = true, geometry = existing }); return null; }
            ParametricContext.Require(!readOnly, "LOCK_MEASURED_GEOMETRY_MISMATCH " + side);
            string work = Path.Combine(ParametricContext.AttemptDirectory, "lock_work_" + side + ".SLDPRT");
            ParametricContext.AssertMutation(work); File.Copy(sourceCopy, work, false);
            model = NativeParametricAssembly.Open(sw, work, false);
            var result = new Result();
            string built = work;
            try
            {
                foreach (string name in LegacyPatternNames)
                {
                    Feature feature = FindFeature(model, name);
                    Require(feature != null && SetSuppression(feature, true), "LEGACY_PATTERN_SUPPRESSION_FAILED", 13, name);
                }
                Require(model.ForceRebuild3(false), "LOCK_SEED_REBUILD_FAILED", 13, side);
                GeometrySnapshot seed=CaptureGeometry(model,"L");
                Require(seed.error_feature_count==0&&seed.warning_feature_count==0&&seed.slot_rows_mm.Length==1&&seed.circle_rows_mm.Length==1&&
                    seed.slot_analyses.Count(row=>row.full_fingerprint)==1&&seed.circle_analyses.Count(row=>row.full_diameter5_pair)==1&&
                    Math.Abs(seed.circle_rows_mm[0]-(seed.slot_rows_mm[0]-60))<RowToleranceMm,"LOCK_NATIVE_SEED_MEASUREMENT_FAILED",13,side);
                double sourceSeedRow=seed.slot_rows_mm[0];
                records.Add(new{side=side,inspection="lock_native_seed_row",slotRowMm=sourceSeedRow,circleRowMm=seed.circle_rows_mm[0]});
                double top = ActiveRows.Where(row=>row<=sourceSeedRow).DefaultIfEmpty(ActiveRows.First()).Last();
                Feature slotSketch=FindFeature(model,SlotSketchName);
                int activateError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activateError);
                model.ClearSelection2(true);Require(slotSketch.Select2(false,0),"LOCK_SKETCH_SELECT_FAILED",14,SlotSketchName);model.EditSketch();
                try {
                    Dimension verticalDatum=model.Parameter("D2@草图6") as Dimension;
                    Require(verticalDatum!=null&&verticalDatum.DrivenState==2,"LOCK_VERTICAL_DIMENSION_MISSING",14,side);
                    double original=verticalDatum.SystemValue*1000;
                    Require(Math.Abs(original-76.25)<.001,"LOCK_VERTICAL_DIMENSION_SOURCE_DRIFT",14,side);
                    verticalDatum.SystemValue=(76.25+sourceSeedRow-top)/1000;
                    records.Add(new{side=side,drivingDimension="D2@草图6",beforeMm=original,targetMm=76.25+sourceSeedRow-top});
                }
                finally { model.SketchManager.InsertSketch(true); }
                Require(model.ForceRebuild3(false),"LOCK_SLOT_REBUILD_BEFORE_CIRCLE_FAILED",14,side);
                GeometrySnapshot afterSlotMove=CaptureGeometry(model,"L");
                records.Add(new{side=side,slotRowsAfterMove=afterSlotMove.slot_rows_mm,circleRowsAfterMove=afterSlotMove.circle_rows_mm,seedGeometry=afterSlotMove});
                if(afterSlotMove.slot_rows_mm.Length==0){
                    var faces=new List<object>();
                    foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                        double[] box=face.GetBox() as double[];
                        if(box==null||box[1]*1000>top+12||box[4]*1000<top-12||box[5]*1000 < -50)continue;
                        Feature owner=face.GetFeature() as Feature;
                        faces.Add(new{feature=owner==null?null:owner.Name,type=owner==null?null:owner.GetTypeName2(),box=box});
                    }
                    records.Add(new{side=side,slotNeighbourFaces=faces});
                    Feature fillSketch=FindFeature(model,"草图7");Sketch fill=(Sketch)fillSketch.GetSpecificFeature2();
                    var lines=new List<object>();
                    foreach(object segment in (Array)fill.GetSketchSegments()){SketchLine line=segment as SketchLine;if(line!=null)lines.Add(new{start=new[]{line.IGetStartPoint2().X,line.IGetStartPoint2().Y,line.IGetStartPoint2().Z},end=new[]{line.IGetEndPoint2().X,line.IGetEndPoint2().Y,line.IGetEndPoint2().Z}});}
                    records.Add(new{side=side,fillSketchLines=lines,fillSketchTransform=fill.ModelToSketchTransform.ArrayData});
                }
                Require(RowsEqual(afterSlotMove.slot_rows_mm,new[]{top},RowToleranceMm) &&
                    RowsEqual(afterSlotMove.circle_rows_mm,new[]{top-60},RowToleranceMm),
                    "LOCK_NATIVE_DIMENSION_LINKAGE_FAILED",14,side);
                ActivePatternIndex=0;
                foreach(double targetRow in ActiveRows.Where(row=>Math.Abs(row-top)>.001))
                {
                    ActivePatternSpacingMm = Math.Abs(top-targetRow); ActivePatternReverse=targetRow>top; ActivePatternIndex++;
                    Feature pattern = CreateSevenRowPattern(model, result);
                    Require(pattern != null && model.ForceRebuild3(false), "PARAMETRIC_LOCK_ROW_FAILED", 15, side + "/" + ActivePatternIndex);
                    if(ActivePatternReverse){
                        bool reordered=model.Extension.ReorderFeature(pattern.Name,"拉伸-薄壁4",(int)swMoveLocation_e.swMoveAfter);
                        records.Add(new{side=side,pattern=pattern.Name,targetRowMm=targetRow,movedAfterNativeFill=reordered});
                        Require(reordered&&model.ForceRebuild3(false),"PARAMETRIC_LOCK_AFTER_FILL_REORDER_FAILED",15,side);
                    }
                }
                GeometrySnapshot geometry = CaptureGeometry(model, "L");
                records.Add(new{side=side,generatedSlotRows=geometry.slot_rows_mm,generatedCircleRows=geometry.circle_rows_mm,featureErrors=geometry.error_feature_count,featureWarnings=geometry.warning_feature_count,generatedGeometry=geometry});
                Require(GeometryGate(geometry, "L"), "PARAMETRIC_LOCK_REAL_EDGE_GATE_FAILED", 16, side);
                NativeParametricAssembly.Save(model);
                if (side == "R")
                {
                    string rightSource=Path.Combine(ParametricContext.CadDirectory,"箱体竖隔板R参数源.SLDPRT");
                    ParametricContext.AssertMutation(rightSource);
                    int errors=0,warnings=0;
                    Require(NativeParametricAssembly.GuardedSaveAs(model,rightSource,0,1,null,ref errors,ref warnings)&&errors==0&&warnings==0,"PARAMETRIC_RIGHT_SOURCE_SAVE_FAILED",17,side);
                    right = new RightPreparation { Source = rightSource, Target = target, SourceSha256 = ParametricContext.Hash(rightSource), BendRadius = bendRadius, KFactor = kFactor, BendAllowance = bendAllowance, AutoRelief = autoRelief, ReliefRatio = reliefRatio, FixedPlane = fixedPlane };
                    records.Add(new { inspection = "right_source_saved_for_independent_session", rightParameterSource = rightSource, sourceSha256 = ParametricContext.Hash(rightSource) });
                }
                records.Add(new { side = side, reusedMatchingNativeSlots = false, geometry = geometry });
            }
            finally
            {
                sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model);
            }
            if (right != null) return right;
            NativeParametricAssembly.AssertProtectedTemplateInputs();
            ParametricContext.AssertMutation(target);
            string backup = target + ".before-parametric-lock.bak";
            ParametricContext.AssertMutation(backup);
            File.Replace(built, target, backup);
            NativeParametricAssembly.AssertProtectedTemplateInputs();
            Console.WriteLine("PARAMETRIC_LOCK_COLUMN_VERIFIED " + side);
            return null;
        }

        private static void CreateRightFromPersistedSource(ISldWorks sw, List<object> records, RightPreparation right, string built)
        {
            string side = "R", rightSource = right.Source;
            double bendRadius=right.BendRadius,kFactor=right.KFactor,bendAllowance=right.BendAllowance,reliefRatio=right.ReliefRatio;
            bool autoRelief=right.AutoRelief; double[] fixedPlane=right.FixedPlane;
            Require(ParametricContext.Hash(rightSource)==right.SourceSha256, "RIGHT_SOURCE_CHANGED_BETWEEN_SESSIONS", 17, rightSource);
            ModelDoc2 model = NativeParametricAssembly.Open(sw, rightSource, true), mirrored = null;
            try
            {
                Require(model.IsOpenedReadOnly() && !model.GetSaveFlag(), "RIGHT_SOURCE_DISK_REOPEN_NOT_CLEAN_READONLY", 17, rightSource);
                CaptureLoaded(sw, records, "source-disk-reopen-before-mirror", true);
                Require(sw.GetDocumentCount() == 1 && GeometryGate(CaptureGeometry(model, "L"), "L"), "RIGHT_SOURCE_DISK_REOPEN_GEOMETRY_FAILED", 17, rightSource);
                ParametricContext.AssertMutation(built);
                int activateError=0;
                sw.DocumentVisible(true,(int)swDocumentTypes_e.swDocPART);
                sw.ActivateDoc3(model.GetTitle(),false,0,ref activateError);
                model.ClearSelection2(true);Require(SelectRightPlane(model),"PARAMETRIC_RIGHT_MIRROR_PLANE_FAILED",17,side);
                Feature mirrorFeature=((PartDoc)model).MirrorPart2(false,(int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSolids,out mirrored);
                Require(mirrorFeature!=null&&mirrored!=null,"PARAMETRIC_RIGHT_LINKED_MIRROR_FAILED",17,side);
                sw.ActivateDoc3(mirrored.GetTitle(),false,0,ref activateError);
                Face2 fixedCandidate=null;double maximumArea=0;
                foreach(Body2 body in (Array)((PartDoc)mirrored).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces())
                {
                    Surface surface=face.GetSurface() as Surface;
                    if(surface==null||!surface.IsPlane())continue;
                    double[] plane=surface.PlaneParams as double[];
                    double dot=plane[0]*fixedPlane[0]+plane[1]*fixedPlane[1]+plane[2]*fixedPlane[2];
                    double offset=fixedPlane[0]*(plane[3]-fixedPlane[3])+fixedPlane[1]*(plane[4]-fixedPlane[4])+fixedPlane[2]*(plane[5]-fixedPlane[5]);
                    if(Math.Abs(Math.Abs(dot)-1)<.000001&&Math.Abs(offset)<.000001&&face.GetArea()>maximumArea){fixedCandidate=face;maximumArea=face.GetArea();}
                }
                mirrored.ClearSelection2(true);
                Require(fixedCandidate!=null&&((Entity)fixedCandidate).Select4(false,null),"PARAMETRIC_RIGHT_FIXED_FACE_SELECT_FAILED",17,side);
                Require(((PartDoc)mirrored).InsertBends2(bendRadius,"",kFactor,bendAllowance,autoRelief,reliefRatio,true),"PARAMETRIC_RIGHT_NATIVE_BENDS_FAILED",17,side);
                Require(mirrored.ForceRebuild3(false),"PARAMETRIC_RIGHT_REBUILD_FAILED",17,side);
                Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.VerifyReflectedVertices(model,mirrored);
                GeometrySnapshot geometry=CaptureGeometry(mirrored,"R");
                object files,parts,features,types,statuses,entities,components;
                mirrored.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components);
                Array referenceFiles=files as Array;
                bool localSourceBound=referenceFiles!=null&&referenceFiles.Length>0&&referenceFiles.Cast<object>().All(file=>Path.GetFullPath(Convert.ToString(file)).Equals(rightSource,StringComparison.OrdinalIgnoreCase));
                records.Add(new{side=side,mirrorGeometry=geometry,rightParameterSource=rightSource,referenceFiles=files,referenceStatuses=statuses,localSourceBound=localSourceBound});
                Require(MirrorGeometryGate(geometry,"R")&&localSourceBound,"PARAMETRIC_RIGHT_LOCK_EDGE_GATE_FAILED",17,side);
                int errors=0,warnings=0;
                Require(NativeParametricAssembly.GuardedSaveAs(mirrored,built,0,1,null,ref errors,ref warnings)&&errors==0&&warnings==0,"PARAMETRIC_RIGHT_LOCK_SAVE_FAILED",18,side);
                Require(ParametricContext.Hash(rightSource)==right.SourceSha256, "RIGHT_SOURCE_READONLY_MIRROR_CHANGED_BYTES", 18, rightSource);
                records.Add(new { inspection = "right_mirror_saved_with_source_loaded", sourcePath = model.GetPathName(), sourceReadOnly = model.IsOpenedReadOnly(), temporaryPath = built, temporarySha256 = ParametricContext.Hash(built) });
            }
            finally
            {
                if (mirrored != null) { sw.CloseDoc(mirrored.GetTitle()); Marshal.ReleaseComObject(mirrored); }
                sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model);
            }
        }

        private static void InspectRightOnly(List<object> records, RightPreparation right, string path, string stage)
        {
            Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
            {
                BeginIsolated(sw, records, stage);
                string beforeHash = ParametricContext.Hash(path), sourceHash = ParametricContext.Hash(right.Source);
                ModelDoc2 model = NativeParametricAssembly.Open(sw, path, true);
                try
                {
                    Require(model.IsOpenedReadOnly(), "RIGHT_INDEPENDENT_REOPEN_NOT_READONLY", 18, stage);
                    GeometrySnapshot geometry = CaptureGeometry(model, "R");
                    object files,parts,features,types,statuses,entities,components;
                    model.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components);
                    Array refs=files as Array;
                    bool bound=refs!=null&&refs.Length>0&&refs.Cast<object>().All(file=>Path.GetFullPath(Convert.ToString(file)).Equals(right.Source,StringComparison.OrdinalIgnoreCase));
                    records.Add(new { inspection = "right_independent_readback", stage = stage, path = path, geometry = geometry, referenceFiles = files, referenceParts = parts, referenceFeatures = features, referenceTypes = types, referenceStatuses = statuses, referenceEntities = entities, referenceComponents = components, localSourceBound = bound, saveFlag = model.GetSaveFlag(), sourcePreloaded = false });
                    CaptureLoaded(sw, records, stage, true);
                    Require(MirrorGeometryGate(geometry,"R")&&bound, "RIGHT_INDEPENDENT_REOPEN_GEOMETRY_OR_LINK_FAILED", 18, stage);
                }
                finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
                Require(ParametricContext.Hash(path)==beforeHash && ParametricContext.Hash(right.Source)==sourceHash && sourceHash==right.SourceSha256, "RIGHT_READONLY_REOPEN_CHANGED_BYTES", 18, stage);
                NativeParametricAssembly.AssertProtectedTemplateInputs();
            }, "lock-" + stage);
        }

        private static void InspectOwnerOnly(List<object> records, string path, string stage)
        {
            Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
            {
                BeginIsolated(sw, records, stage);
                string beforeHash=ParametricContext.Hash(path);
                ModelDoc2 model=NativeParametricAssembly.Open(sw,path,true);
                try
                {
                    object files,parts,features,types,statuses,entities,components;
                    model.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components);
                    records.Add(new { inspection = "right_reference_owner_readback", stage = stage, path = path, actualReadOnly = model.IsOpenedReadOnly(), saveFlag = model.GetSaveFlag(), referenceFiles = files, referenceParts = parts, referenceFeatures = features, referenceTypes = types, referenceStatuses = statuses, referenceEntities = entities, referenceComponents = components });
                    CaptureLoaded(sw, records, stage, true);
                }
                finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
                Require(ParametricContext.Hash(path)==beforeHash, "RIGHT_OWNER_READONLY_REOPEN_CHANGED_BYTES", 18, stage);
                NativeParametricAssembly.AssertProtectedTemplateInputs();
            }, "lock-" + stage);
        }

        private static void CaptureLoaded(ISldWorks sw, List<object> records, string stage, bool requireReadOnly)
        {
            Array documents=sw.GetDocuments() as Array;
            var rows=new List<object>(); bool valid=true;
            try
            {
                if(documents!=null) foreach(ModelDoc2 document in documents)
                {
                    string path=document.GetPathName();
                    ParametricContext.CheckPath(path,ParametricContext.AttemptDirectory);
                    bool readOnly=document.IsOpenedReadOnly(); valid &= !requireReadOnly||readOnly;
                    rows.Add(new { path = path, actualReadOnly = readOnly, saveFlag = document.GetSaveFlag() });
                }
                records.Add(new { inspection = "lock_loaded_document_modes", stage = stage, processId = sw.GetProcessID(), documentCount = sw.GetDocumentCount(), requireReadOnly = requireReadOnly, loaded = rows, valid = valid });
                Require(valid, "LOCK_READONLY_DEPENDENCY_ACTUALLY_WRITABLE", 18, stage);
            }
            finally { if(documents!=null) foreach(object document in documents) if(document!=null&&Marshal.IsComObject(document)) Marshal.ReleaseComObject(document); }
        }
    }
}
