using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Winnsen.StructureAgent.Generation;

namespace Winnsen.StructureAgent.Native888
{
    internal static partial class NativeParametricLocks
    {
        internal const string GeneratedRightName = "箱体竖隔板R_parametric.SLDPRT";
        internal static string CurrentRightName { get { return File.Exists(Path.Combine(ParametricContext.CadDirectory, GeneratedRightName)) ? GeneratedRightName : "箱体竖隔板R.SLDPRT"; } }
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
            internal RightSourceContextProof ContextProof;
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
                string built = Path.Combine(ParametricContext.CadDirectory, GeneratedRightName);
                ParametricContext.AssertMutation(built);
                Require(!File.Exists(built), "LOCK_TEMPORARY_RIGHT_ALREADY_EXISTS", 18, built);
                Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
                {
                    BeginIsolated(sw, records, "mirror-right");
                    CreateRightFromPersistedSource(sw, records, right, built);
                }, "lock-mirror-right");
                SettleRightBeforeReadback(records, right, built);
                InspectRightOnly(records, right, built, "temporary-right-only");
                NativeParametricAssembly.AssertProtectedTemplateInputs();
                CommitRightOwner(records, right, built);
                InspectRightOnly(records, right, built, "committed-right-only");
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
            string target = Directory.GetFiles(ParametricContext.CadDirectory).Single(path => Path.GetFileName(path).Equals(side == "L" ? "箱体竖隔板L.sldprt" : CurrentRightName, StringComparison.OrdinalIgnoreCase));
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
                RightSourceContextProof detachedContext = side == "R" ? DetachAuditedRightSourceContext(model, records, work) : null;
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
                RightSourceContextProof sourceProof = side == "R" ? CaptureRightSourceContext(model) : null;
                if (sourceProof != null) Require(sourceProof.externalRelations.SequenceEqual(detachedContext.externalRelations), "RIGHT_CONTEXT_EXTERNAL_RELATIONS_CHANGED_DURING_GENERATION", 17, side);
                if (sourceProof != null) RequireDetachedRightSourceContext(model, records, sourceProof, "before-source-save");
                NativeParametricAssembly.Save(model);
                if (side == "R")
                {
                    string rightSource=Path.Combine(ParametricContext.CadDirectory,"箱体竖隔板R参数源.SLDPRT");
                    ParametricContext.AssertMutation(rightSource);
                    int errors=0,warnings=0;
                    Require(NativeParametricAssembly.GuardedSaveAs(model,rightSource,0,1,null,ref errors,ref warnings)&&errors==0&&warnings==0,"PARAMETRIC_RIGHT_SOURCE_SAVE_FAILED",17,side);
                    RequireDetachedRightSourceContext(model, records, sourceProof, "after-source-save");
                    right = new RightPreparation { Source = rightSource, Target = target, SourceSha256 = ParametricContext.Hash(rightSource), BendRadius = bendRadius, KFactor = kFactor, BendAllowance = bendAllowance, AutoRelief = autoRelief, ReliefRatio = reliefRatio, FixedPlane = fixedPlane, ContextProof = sourceProof };
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
            int openRecordCount = ReadLockOpenRecords().Length;
            ModelDoc2 model = NativeParametricAssembly.Open(sw, rightSource, true), mirrored = null;
            try
            {
                RequireNewCleanLockOpen(sw, model, records, rightSource, openRecordCount, "source-disk-reopen-before-mirror");
                Require(model.IsOpenedReadOnly() && !model.GetSaveFlag(), "RIGHT_SOURCE_DISK_REOPEN_NOT_CLEAN_READONLY", 17, rightSource);
                RequireDetachedRightSourceContext(model, records, right.ContextProof, "source-disk-reopen-before-mirror");
                CaptureLoaded(sw, records, "source-disk-reopen-before-mirror", true);
                Require(sw.GetDocumentCount() == 1 && GeometryGate(CaptureGeometry(model, "L"), "L"), "RIGHT_SOURCE_DISK_REOPEN_GEOMETRY_FAILED", 17, rightSource);
                Require(!model.GetSaveFlag(), "RIGHT_SOURCE_DISK_REOPEN_BECAME_DIRTY", 17, rightSource);
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


        private static void OwnerNeed(bool value,string message){Require(value,message,18,"different-name-owner-commit");}
        private static object[] OwnerValues(object raw){return raw==null?new object[0]:((IEnumerable)raw).Cast<object>().ToArray();}
        private static void OwnerRelease(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        private static Dictionary<string,object> OwnerRow(params object[] values){var row=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)row[(string)values[i]]=values[i+1];return row;}
        private static void CommitRightOwner(List<object> records,RightPreparation right,string built)
        {
            string owner=Path.Combine(ParametricContext.CadDirectory,"箱体竖隔板R焊接.SLDASM");
            var attributes=new Dictionary<string,FileAttributes>(StringComparer.OrdinalIgnoreCase);
            var hashes=Directory.GetFiles(ParametricContext.CadDirectory).Where(p=>!Path.GetFileName(p).StartsWith("~$")&&(p.EndsWith(".sldprt",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".sldasm",StringComparison.OrdinalIgnoreCase))).ToDictionary(p=>p,p=>ParametricContext.Hash(p),StringComparer.OrdinalIgnoreCase);
            string oldRightHash=ParametricContext.Hash(right.Target);
            Exception failure=null;
            try
            {
                foreach(string path in hashes.Keys.Where(p=>!p.Equals(owner,StringComparison.OrdinalIgnoreCase)))
                {ParametricContext.AssertMutation(path);attributes[path]=File.GetAttributes(path);File.SetAttributes(path,attributes[path]|FileAttributes.ReadOnly);}
                records.Add(new{inspection="owner_commit_dependency_attributes",boundary="protected",files=attributes.Select(x=>new{path=x.Key,before=(int)x.Value,actual=(int)File.GetAttributes(x.Key),sha256=hashes[x.Key]}).ToArray()});
                Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase,delegate(ISldWorks sw)
                {
                    BeginIsolated(sw,records,"replace-right-owner");
                    ModelDoc2 m=NativeParametricAssembly.Open(sw,owner,false);
                    try
                    {
                        OwnerNeed(!m.GetSaveFlag(),"OWNER_INITIAL_OPEN_DIRTY");
                        AssemblyDoc asm=(AssemblyDoc)m;var components=OwnerValues(asm.GetComponents(true)).Cast<Component2>().ToArray();
                        var before=components.ToDictionary(c=>c.Name2,c=>(double[])c.Transform2.ArrayData);
                        var targets=components.Where(c=>c.GetPathName().Equals(right.Target,StringComparison.OrdinalIgnoreCase)).ToArray();
                        OwnerNeed(targets.Length==1&&targets[0].ReferencedConfiguration=="Default"&&targets[0].IsFixed(),"OWNER_TOPLEVEL_RIGHT_IDENTITY_CHANGED");
                        string oldName=targets[0].Name2;double[] oldMatrix=(double[])targets[0].Transform2.ArrayData;
                        OwnerNeed(!Path.GetFileName(built).Equals(Path.GetFileName(right.Target),StringComparison.OrdinalIgnoreCase),"OWNER_REPLACEMENT_SAME_NAME");
                        m.ClearSelection2(true);OwnerNeed(targets[0].Select4(false,null,false),"OWNER_RIGHT_SELECT_FAILED");
                        bool replaced=asm.ReplaceComponents2(built,"Default",false,0,true);
                        records.Add(new{inspection="owner_replace_components2",oldPath=right.Target,newPath=built,replaced=replaced,reattachMates=true});OwnerNeed(replaced,"OWNER_REPLACE_COMPONENTS_FAILED");m.ClearSelection2(true);
                        OwnerNeed(m.ForceRebuild3(false),"OWNER_AFTER_REPLACE_REBUILD_FAILED");
                        RebindOriginalOwnerMates(m,built,records);
                        foreach(Component2 c in OwnerValues(asm.GetComponents(true)))
                        {double[] expected=c.GetPathName().Equals(built,StringComparison.OrdinalIgnoreCase)?oldMatrix:before[c.Name2];double[] actual=(double[])c.Transform2.ArrayData;double delta=expected.Zip(actual,(x,y)=>Math.Abs(x-y)).Max();records.Add(new{inspection="owner_commit_original_matrix",component=c.Name2,maximumDifference=delta});OwnerNeed(delta<1e-8,"OWNER_REPLACEMENT_MOVED_COMPONENT");OwnerRelease(c);}
                        NativeParametricAssembly.Save(m);OwnerNeed(!m.GetSaveFlag(),"OWNER_COMMIT_SAVE_DIRTY");
                        records.Add(new{inspection="owner_commit_saved",path=owner,sha256=ParametricContext.Hash(owner),oldRightUnchanged=ParametricContext.Hash(right.Target)==oldRightHash});
                        OwnerNeed(ParametricContext.Hash(right.Target)==oldRightHash,"OLD_RIGHT_BYTES_CHANGED");
                    }
                    finally{sw.CloseDoc(m.GetTitle());OwnerRelease(m);}
                },"lock-replace-right-owner");
            }
            catch(Exception e){failure=e;}
            finally
            {
                var errors=new List<Exception>();
                foreach(var item in attributes)try{File.SetAttributes(item.Key,item.Value);OwnerNeed(File.GetAttributes(item.Key)==item.Value&&ParametricContext.Hash(item.Key)==hashes[item.Key],"OWNER_DEPENDENCY_RESTORE_OR_SHA_FAILED "+item.Key);}catch(Exception e){errors.Add(e);}
                try{records.Add(new{inspection="owner_commit_dependency_attributes",boundary="restored",files=attributes.Select(x=>new{path=x.Key,before=(int)x.Value,actual=(int)File.GetAttributes(x.Key),sha256=ParametricContext.Hash(x.Key),unchanged=ParametricContext.Hash(x.Key)==hashes[x.Key]}).ToArray()});NativeParametricAssembly.AssertProtectedTemplateInputs();}catch(Exception e){errors.Add(e);}
                if(errors.Count>0){if(failure!=null)errors.Insert(0,failure);failure=new AggregateException(errors);}
            }
            if(failure!=null)throw failure;
        }

    private static void RebindOriginalOwnerMates(ModelDoc2 m,string newPath,List<object> records){
        var original=OwnerValues(((AssemblyDoc)m).GetComponents(true)).Cast<Component2>().ToDictionary(c=>c.Name2,c=>(double[])c.Transform2.ArrayData);
        Feature group=m.FirstFeature() as Feature;while(group!=null&&group.GetTypeName2()!="MateGroup")group=group.GetNextFeature() as Feature;OwnerNeed(group!=null,"MATE_GROUP_MISSING");Feature f=group.GetFirstSubFeature() as Feature;var frozenNames=new List<string>();while(f!=null){Feature next=f.GetNextSubFeature() as Feature;Mate2 mm=f.GetSpecificFeature2() as Mate2;if(mm!=null&&f.GetErrorCode()!=0)frozenNames.Add(f.Name);OwnerRelease(mm);OwnerRelease(f);f=next;}OwnerNeed(frozenNames.Count==5,"INITIAL_MATE_COUNT_NOT_FIVE");int repaired=0;
        foreach(string frozenName in frozenNames){f=group.GetFirstSubFeature() as Feature;while(f!=null&&f.Name!=frozenName){Feature skip=f.GetNextSubFeature() as Feature;OwnerRelease(f);f=skip;}OwnerNeed(f!=null,"ORIGINAL_MATE_NOT_FOUND "+frozenName);Mate2 mate=f.GetSpecificFeature2() as Mate2;
            if(mate!=null){OwnerNeed(!f.IsSuppressed()&&mate.GetMateEntityCount()==2,"REPAIR_SCOPE_INVALID");string kind=f.GetTypeName2();OwnerNeed(kind=="MateConcentric"||kind=="MateDistanceDim","MATE_TYPE_UNEXPECTED");var refs=new object[2];var ents=new MateEntity2[2];int missing=-1;
                for(int i=0;i<2;i++){ents[i]=mate.MateEntity(i) as MateEntity2;refs[i]=ents[i].Reference;if(refs[i]==null){OwnerNeed(missing==-1,"TWO_MISSING_ENTITIES");missing=i;}}
                OwnerNeed(missing>=0,"ERROR_WITHOUT_MISSING_ENTITY");Component2 c=ents[missing].ReferenceComponent as Component2;OwnerNeed(c!=null&&c.GetPathName().Equals(newPath,StringComparison.OrdinalIgnoreCase),"WRONG_MISSING_COMPONENT");double[] old=ents[missing].EntityParams as double[],other=ents[1-missing].EntityParams as double[];ModelDoc2 part=c.GetModelDoc2() as ModelDoc2;OwnerNeed(part!=null,"PART_NOT_LOADED");var matches=new List<Face2>();var info=new List<object>();
                foreach(Body2 body in OwnerValues(((PartDoc)part).GetBodies2(0,false)))foreach(Face2 face in OwnerValues(body.GetFaces())){Surface surf=face.GetSurface() as Surface;bool hit=false;double[] p=null;if(kind=="MateConcentric"&&surf.IsCylinder()){p=surf.CylinderParams as double[];hit=Math.Abs(Math.Abs(p[3])-1)<1e-6&&Math.Abs(p[4])<1e-6&&Math.Abs(p[5])<1e-6&&Math.Abs(p[1]-other[1])<1e-6&&Math.Abs(p[2]-other[2])<1e-6&&Math.Abs(p[6]-old[6])<1e-6;}else if(kind=="MateDistanceDim"&&surf.IsPlane()){p=surf.PlaneParams as double[];hit=Math.Abs(Math.Abs(p[0])-1)<1e-6&&Math.Abs(p[3]-old[0])<1e-6;}if(hit){matches.Add(face);info.Add(OwnerRow("parameters",p,"box",face.GetBox(),"area",face.GetArea()));}OwnerRelease(surf);}
                records.Add(new{inspection="mate_face_unique_match",mate=f.Name,type=kind,oldParameters=old,oppositeParameters=other,count=matches.Count,candidates=info});OwnerNeed(matches.Count==1,"MATE_FACE_NOT_UNIQUE "+f.Name);refs[missing]=c.GetCorrespondingEntity(matches[0]);OwnerNeed(refs[missing]!=null,"CONTEXT_FACE_NULL");object def=f.GetDefinition();double distance=0;int alignment=mate.Alignment;bool flip=false,lockRotation=false;
                if(kind=="MateDistanceDim"){var data=(DistanceMateFeatureData)def;distance=data.Distance;flip=data.FlipDimension;OwnerNeed(data.MateAlignment==alignment,"DISTANCE_ALIGNMENT_MISMATCH");data.EntitiesToMate=refs;}
                else{var data=(ConcentricMateFeatureData)def;lockRotation=data.LockRotation;OwnerNeed(data.MateAlignment==alignment,"CONCENTRIC_ALIGNMENT_MISMATCH");data.EntitiesToMate=refs;}
                m.ClearSelection2(true);SelectData sd=((SelectionMgr)m.SelectionManager).CreateSelectData();sd.Mark=1;OwnerNeed(((Entity)refs[0]).Select4(false,sd)&&((Entity)refs[1]).Select4(true,sd)&&f.Select2(true,0),"EDIT_MATE_SELECTION_FAILED");int editError=0;((AssemblyDoc)m).EditMate3(mate.Type,alignment,flip,distance,distance,distance,1,1,0,0,0,false,lockRotation,0,out editError);m.ClearSelection2(true);bool changed=editError==1;records.Add(new{inspection="edit_mate3",mate=f.Name,errorStatus=editError});records.Add(new{inspection="mate_modify_definition",mate=f.Name,result=changed,type=kind,preservedAlignment=alignment,preservedDistance=distance,preservedFlip=flip,preservedLockRotation=lockRotation});OwnerNeed(changed,"MATE_MODIFY_FAILED "+f.Name);repaired++;
            }OwnerRelease(mate);OwnerRelease(f);
        }OwnerNeed(repaired==frozenNames.Count&&repaired==5,"REPAIR_COUNT_NOT_FIVE");OwnerNeed(m.ForceRebuild3(false),"REPAIR_REBUILD_FAILED");
        foreach(Component2 c in OwnerValues(((AssemblyDoc)m).GetComponents(true))){double[] before=original[c.Name2],after=(double[])c.Transform2.ArrayData;double max=before.Zip(after,(x,y)=>Math.Abs(x-y)).Max();records.Add(new{inspection="repair_matrix_check",component=c.Name2,maximumDifference=max});OwnerNeed(max<1e-8,"REPAIR_MOVED_COMPONENT "+c.Name2);}
        f=group.GetFirstSubFeature() as Feature;while(f!=null){Feature next=f.GetNextSubFeature() as Feature;Mate2 mate=f.GetSpecificFeature2() as Mate2;if(mate!=null){OwnerNeed(f.GetErrorCode()==0&&!f.IsSuppressed(),"MATE_ERROR_AFTER_REPAIR "+f.Name);for(int i=0;i<mate.GetMateEntityCount();i++)OwnerNeed(mate.MateEntity(i).Reference!=null,"REFERENCE_NULL_AFTER_REPAIR");}OwnerRelease(mate);OwnerRelease(f);f=next;}
    }
        private static void SettleRightBeforeReadback(List<object> records, RightPreparation right, string path)
        {
            var dependencyAttributes = new List<RightReadOnlyDependency>();
            Exception settleError = null;
            try
            {
                Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
                {
                    BeginIsolated(sw, records, "settle-generated-right");
                    ProtectRightReadbackDependencies(sw, records, right, path, "temporary-right-only", dependencyAttributes);
                    string sourceHash = ParametricContext.Hash(right.Source);
                    ModelDoc2 model = NativeParametricAssembly.Open(sw, path, false);
                    try
                    {
                        records.Add(new { inspection = "right_settle_before_save", path = path, saveFlag = model.GetSaveFlag(), sourceSha256 = sourceHash });
                        GeometrySnapshot before = CaptureGeometry(model, "R");
                        Require(MirrorGeometryGate(before, "R"), "RIGHT_SETTLE_GEOMETRY_FAILED", 18, path);
                        NativeParametricAssembly.Save(model);
                        Require(!model.GetSaveFlag() && MirrorGeometryGate(CaptureGeometry(model, "R"), "R"), "RIGHT_SETTLE_SAVE_NOT_CLEAN", 18, path);
                        Require(ParametricContext.Hash(right.Source) == sourceHash && sourceHash == right.SourceSha256, "RIGHT_SETTLE_SOURCE_CHANGED", 18, path);
                        records.Add(new { inspection = "right_settle_after_save", path = path, saveFlag = model.GetSaveFlag(), sha256 = ParametricContext.Hash(path) });
                    }
                    finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
                }, "lock-settle-generated-right");
            }
            catch (Exception error) { settleError = error; }
            finally
            {
                var errors = new List<Exception>();
                foreach (RightReadOnlyDependency dependency in dependencyAttributes)
                    try
                    {
                        ParametricContext.CheckPath(dependency.path, ParametricContext.AttemptDirectory);
                        File.SetAttributes(dependency.path, (FileAttributes)dependency.beforeAttributes);
                        dependency.restoredAttributes = (int)File.GetAttributes(dependency.path);
                        dependency.afterSha256 = ParametricContext.Hash(dependency.path);
                        dependency.restored = dependency.restoredAttributes == dependency.beforeAttributes && dependency.afterSha256 == dependency.beforeSha256;
                        if (!dependency.restored) throw new InvalidOperationException("RIGHT_SETTLE_DEPENDENCY_RESTORE_MISMATCH " + dependency.path);
                    }
                    catch (Exception error) { dependency.restoreError = error.ToString(); errors.Add(error); }
                try { WriteRightDependencyAttributes(records, "settle-generated-right", dependencyAttributes, "restored-after-session"); }
                catch (Exception error) { errors.Add(error); }
                if (errors.Count > 0) { if (settleError != null) errors.Insert(0, settleError); settleError = new AggregateException(errors); }
            }
            if (settleError != null) throw settleError;
        }

        private static void InspectRightOnly(List<object> records, RightPreparation right, string path, string stage)
        {
            var dependencyAttributes = new List<RightReadOnlyDependency>();
            Exception readbackError = null;
            try
            {
            Winnsen.StructureAgent.NativeDoorModule888x14.NativeParametricDoor.WithSession(ParametricContext.Phase, delegate(ISldWorks sw)
            {
                BeginIsolated(sw, records, stage);
                ProtectRightReadbackDependencies(sw, records, right, path, stage, dependencyAttributes);
                string beforeHash = ParametricContext.Hash(path), sourceHash = ParametricContext.Hash(right.Source);
                int openRecordCount = ReadLockOpenRecords().Length;
                ModelDoc2 model = NativeParametricAssembly.Open(sw, path, true);
                try
                {
                    RequireNewCleanLockOpen(sw, model, records, path, openRecordCount, stage);
                    Require(model.IsOpenedReadOnly(), "RIGHT_INDEPENDENT_REOPEN_NOT_READONLY", 18, stage);
                    GeometrySnapshot geometry = CaptureGeometry(model, "R");
                    object files,parts,features,types,statuses,entities,components;
                    model.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components);
                    Array refs=files as Array;
                    bool bound=refs!=null&&refs.Length>0&&refs.Cast<object>().All(file=>Path.GetFullPath(Convert.ToString(file)).Equals(right.Source,StringComparison.OrdinalIgnoreCase));
                    records.Add(new { inspection = "right_independent_readback", stage = stage, path = path, geometry = geometry, referenceFiles = files, referenceParts = parts, referenceFeatures = features, referenceTypes = types, referenceStatuses = statuses, referenceEntities = entities, referenceComponents = components, localSourceBound = bound, saveFlag = model.GetSaveFlag(), sourcePreloaded = false });
                    RequireNaturallyLoadedRightSourceClean(sw, records, right.Source, stage);
                    CaptureLoaded(sw, records, stage, true);
                    Require(!model.GetSaveFlag(), "RIGHT_INDEPENDENT_REOPEN_BECAME_DIRTY", 18, stage);
                    Require(MirrorGeometryGate(geometry,"R")&&bound, "RIGHT_INDEPENDENT_REOPEN_GEOMETRY_OR_LINK_FAILED", 18, stage);
                }
                finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
                Require(ParametricContext.Hash(path)==beforeHash && ParametricContext.Hash(right.Source)==sourceHash && sourceHash==right.SourceSha256, "RIGHT_READONLY_REOPEN_CHANGED_BYTES", 18, stage);
                NativeParametricAssembly.AssertProtectedTemplateInputs();
            }, "lock-" + stage);
            }
            catch (Exception error) { readbackError = error; }
            finally
            {
                var restoreErrors = new List<Exception>();
                foreach (RightReadOnlyDependency dependency in dependencyAttributes)
                {
                    try
                    {
                        // Restore even if the authorization lease or diagnostic logging failed.
                        ParametricContext.CheckPath(dependency.path, ParametricContext.AttemptDirectory);
                        File.SetAttributes(dependency.path, (FileAttributes)dependency.beforeAttributes);
                        dependency.restoredAttributes = (int)File.GetAttributes(dependency.path);
                        dependency.afterSha256 = ParametricContext.Hash(dependency.path);
                        dependency.restored = dependency.restoredAttributes == dependency.beforeAttributes && dependency.afterSha256 == dependency.beforeSha256;
                        if (!dependency.restored) throw new InvalidOperationException("LOCK_DEPENDENCY_ATTRIBUTE_OR_SHA_RESTORE_MISMATCH: " + dependency.path);
                    }
                    catch (Exception error) { dependency.restoreError = error.ToString(); restoreErrors.Add(error); }
                }
                try { WriteRightDependencyAttributes(records, stage, dependencyAttributes, "restored-after-session"); }
                catch (Exception error) { restoreErrors.Add(error); }
                if (restoreErrors.Count > 0)
                {
                    if (readbackError != null) restoreErrors.Insert(0, readbackError);
                    readbackError = new AggregateException("LOCK_DEPENDENCY_ATTRIBUTE_RESTORE_OR_EVIDENCE_FAILED", restoreErrors);
                }
            }
            if (readbackError != null) throw readbackError;
        }

        private sealed class RightReadOnlyDependency
        {
            public string path, beforeSha256, afterSha256, restoreError;
            public int beforeAttributes;
            public int? protectedAttributes, restoredAttributes;
            public bool restored;
        }

        private static void WriteRightDependencyAttributes(List<object> records, string stage, List<RightReadOnlyDependency> dependencies, string boundary)
        {
            records.Add(new { inspection = "right_dependency_attributes", stage = stage, boundary = boundary, dependencies = dependencies.ToArray() });
            ParametricContext.Write(Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + "." + stage + ".dependency-attributes.json"),
                new { stage = stage, boundary = boundary, dependencies = dependencies });
        }

        private static void ProtectRightReadbackDependencies(ISldWorks sw, List<object> records, RightPreparation right, string path, string stage, List<RightReadOnlyDependency> dependencies)
        {
            Require(stage == "temporary-right-only" || stage == "committed-right-only", "LOCK_DEPENDENCY_ATTRIBUTE_STAGE_INVALID", 18, stage);
            Require(sw.GetDocumentCount() == 0, "LOCK_DEPENDENCY_ATTRIBUTES_REQUIRE_CLOSED_DOCUMENTS", 18, stage);
            Array raw = sw.GetDocumentDependencies2(path, true, false, false) as Array;
            Require(raw != null && raw.Rank == 1 && raw.Length > 0 && raw.Length % 2 == 0 && raw.Cast<object>().All(value => value is string), "LOCK_DEPENDENCY_LIST_INVALID", 18, stage);
            records.Add(new { inspection = "right_dependency_closed_list", stage = stage, owner = path, raw = raw });
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < raw.Length; index += 2)
            {
                string dependency = Convert.ToString(raw.GetValue(index));
                Require(Path.IsPathRooted(dependency), "LOCK_DEPENDENCY_PATH_NOT_ABSOLUTE", 18, dependency);
                dependency = Path.GetFullPath(dependency);
                ParametricContext.AssertMutation(dependency);
                Require(File.Exists(dependency) && !dependency.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase), "LOCK_DEPENDENCY_MISSING_OR_SELF", 18, dependency);
                paths.Add(dependency);
            }
            Require(paths.Contains(Path.GetFullPath(right.Source)), "LOCK_DEPENDENCY_RIGHT_SOURCE_MISSING", 18, stage);
            // Validate and retain the entire restore list before changing any attribute.
            foreach (string dependency in paths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                dependencies.Add(new RightReadOnlyDependency { path = dependency, beforeAttributes = (int)File.GetAttributes(dependency), beforeSha256 = ParametricContext.Hash(dependency) });
            WriteRightDependencyAttributes(records, stage, dependencies, "before");
            foreach (RightReadOnlyDependency dependency in dependencies)
            {
                ParametricContext.AssertMutation(dependency.path);
                File.SetAttributes(dependency.path, (FileAttributes)dependency.beforeAttributes | FileAttributes.ReadOnly);
                dependency.protectedAttributes = (int)File.GetAttributes(dependency.path);
                Require(dependency.protectedAttributes == (dependency.beforeAttributes | (int)FileAttributes.ReadOnly) && ParametricContext.Hash(dependency.path) == dependency.beforeSha256,
                    "LOCK_DEPENDENCY_ATTRIBUTE_PROTECTION_FAILED", 18, dependency.path);
            }
            Require(sw.GetDocumentCount() == 0, "LOCK_DEPENDENCY_ATTRIBUTE_PRELOADED_DOCUMENT", 18, stage);
            WriteRightDependencyAttributes(records, stage, dependencies, "protected-before-open");
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

        private sealed class RightSourceContextProof
        {
            public string path, configuration;
            public string[] features, externalRelations;
            public readonly List<double[]> vertices = new List<double[]>();
            public readonly Dictionary<string, double[]> contour = new Dictionary<string, double[]>();
            public double[] massProperties, sketchTransform;
            public int bodyCount, faceCount, edgeCount;
            public GeometrySnapshot nativeGeometry;
        }

        private static void ContextRelease(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }
        private static object[] ReadLockOpenRecords()
        {
            string path = Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".document-state.json");
            if (!File.Exists(path)) return new object[0];
            object[] rows = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(path)) as object[];
            Require(rows != null, "LOCK_OPEN_REPORT_NOT_ARRAY", 17, path);
            return rows;
        }

        private static void RequireNewCleanLockOpen(ISldWorks sw, ModelDoc2 model, List<object> records, string path, int beforeCount, string stage)
        {
            object[] rows = ReadLockOpenRecords();
            Require(rows.Length == beforeCount + 1, "LOCK_OPEN_REPORT_NOT_ONE_NEW_RECORD", 17, stage);
            var row = rows[rows.Length - 1] as Dictionary<string, object>;
            Require(row != null && row.ContainsKey("operation") && row.ContainsKey("path") &&
                row.ContainsKey("requestedReadOnly") && row.ContainsKey("actualReadOnly") && row.ContainsKey("errors") && row.ContainsKey("warnings"),
                "LOCK_OPEN_REPORT_FIELDS_MISSING", 17, stage);
            Require(row["errors"] is int && row["warnings"] is int && row["requestedReadOnly"] is bool && row["actualReadOnly"] is bool,
                "LOCK_OPEN_REPORT_FIELD_TYPES", 17, stage);
            int errors = (int)row["errors"], warnings = (int)row["warnings"];
            bool actualReadOnly = model.IsOpenedReadOnly(), saveFlag = model.GetSaveFlag();
            records.Add(new { inspection = "lock_new_readonly_open_gate", stage = stage, beforeCount = beforeCount, afterCount = rows.Length,
                report = row, rawErrors = errors, rawWarnings = warnings, warning1 = warnings & 1, warning32 = warnings & 32,
                actualReadOnly = actualReadOnly, saveFlag = saveFlag });
            object files, parts, features, types, statuses, entities, components;
            model.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
            records.Add(new { inspection = "lock_readonly_open_raw_references", stage = stage, path = model.GetPathName(),
                files = files, parts = parts, features = features, types = types, statuses = statuses, entities = entities, components = components });
            CaptureLoaded(sw, records, stage + "-open-observed", true);
            Require(Convert.ToString(row["operation"]) == "open" && ContextPathEquals(Convert.ToString(row["path"]), path) &&
                (bool)row["requestedReadOnly"] && (bool)row["actualReadOnly"] && ContextPathEquals(model.GetPathName(), path) &&
                errors == 0 && (warnings & 1) == 0 && (warnings & 32) == 0 && actualReadOnly && !saveFlag,
                "LOCK_NEW_READONLY_OPEN_NOT_CLEAN", 17, stage + " " + errors + "/" + warnings);
        }

        private static void RequireNaturallyLoadedRightSourceClean(ISldWorks sw, List<object> records, string sourcePath, string stage)
        {
            ModelDoc2 source = sw.GetOpenDocumentByName(sourcePath) as ModelDoc2;
            try
            {
                bool? actualReadOnly = source == null ? (bool?)null : source.IsOpenedReadOnly();
                bool? saveFlag = source == null ? (bool?)null : source.GetSaveFlag();
                string actualPath = source == null ? null : source.GetPathName();
                records.Add(new { inspection = "right_naturally_loaded_source", stage = stage, path = sourcePath, loaded = source != null,
                    actualPath = actualPath, actualReadOnly = actualReadOnly, saveFlag = saveFlag, sourcePreloaded = false });
                Require(source == null || (ContextPathEquals(actualPath, sourcePath) && saveFlag == false), "RIGHT_NATURALLY_LOADED_SOURCE_DIRTY_OR_PATH_MISMATCH", 18, stage);
            }
            finally { ContextRelease(source); }
        }

        private static bool ContextPathEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
                Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static object[] ContextArray(object raw, string label)
        {
            if (raw == null) return new object[0];
            Array values = raw as Array;
            Require(values != null && values.Rank == 1 && values.GetLowerBound(0) == 0 && values.Length <= 20000,
                "RIGHT_CONTEXT_ARRAY_INVALID", 17, label);
            return values.Cast<object>().ToArray();
        }

        private static int[] ContextIds(object raw)
        {
            object[] values = ContextArray(raw, "sketch entity ID");
            Require(values.Length == 2 && values.All(value => value is int), "RIGHT_CONTEXT_ENTITY_ID_UNAVAILABLE", 17, "point/segment scoped ID");
            return values.Cast<int>().ToArray();
        }

        private static string ContextRelationSignature(SketchRelation relation)
        {
            Require(relation != null, "RIGHT_CONTEXT_RELATION_UNAVAILABLE", 17, "swExternal");
            object[] types = ContextArray(relation.GetEntitiesType(), "entity types"), entities = ContextArray(relation.GetEntities(), "entities");
            try
            {
                Require(entities.Length > 0 && relation.GetEntitiesCount() == entities.Length && types.Length == entities.Length && types.All(value => value is int),
                    "RIGHT_CONTEXT_RELATION_ENTITIES_MISMATCH", 17, "swExternal");
                var ids = new List<string>();
                foreach (object entity in entities)
                {
                    SketchPoint point = entity as SketchPoint; SketchSegment segment = entity as SketchSegment;
                    Require(point != null || segment != null, "RIGHT_CONTEXT_EXTERNAL_ENTITY_NOT_AUDITED_TYPE", 17, "point/segment only");
                    int[] id = ContextIds(point != null ? point.GetID() : segment.GetID());
                    ids.Add((point != null ? "P:" : "S:") + string.Join(",", id));
                }
                return relation.GetRelationType() + "|" + string.Join(",", types) + "|" + string.Join(";", ids);
            }
            finally { foreach (object entity in entities) ContextRelease(entity); }
        }

        private static int[] ContextParentIds(Feature feature)
        {
            object[] parents = ContextArray(feature.GetParents(), "feature parents");
            try
            {
                Require(parents.All(value => value is Feature), "RIGHT_CONTEXT_PARENT_UNAVAILABLE", 17, feature.Name);
                return parents.Cast<Feature>().Select(parent => parent.GetID()).ToArray();
            }
            finally { foreach (object parent in parents) ContextRelease(parent); }
        }

        private static void CaptureContextFeatures(Feature first, bool topLevel, int depth, HashSet<int> seen, List<string> features, List<string> relations, ref int occurrences)
        {
            Feature feature = first;
            try
            {
                while (feature != null)
                {
                    Feature next = null;
                    try
                    {
                        Require(++occurrences <= 5000 && depth <= 30, "RIGHT_CONTEXT_FEATURE_TRAVERSAL_LIMIT", 17, "native tree");
                        next = topLevel ? feature.GetNextFeature() as Feature : feature.GetNextSubFeature() as Feature;
                        int id = feature.GetID();
                        if (!seen.Add(id)) continue;
                        string name = feature.Name, type = feature.GetTypeName2(); int[] parents = ContextParentIds(feature);
                        bool warning; int error = feature.GetErrorCode2(out warning);
                        features.Add(new JavaScriptSerializer().Serialize(new { id = id, name = name, type = type, parents = parents,
                            suppressed = feature.IsSuppressed(), error = error, warning = error != 0 && warning, legacyError = feature.GetErrorCode() }));
                        if (name == "草图21" || id == 321)
                            Require(id == 321 && name == "草图21" && type == "ProfileFeature" && parents.SequenceEqual(new[] { 19 }), "RIGHT_CONTEXT_SKETCH_OWNER_MISMATCH", 17, name);
                        if (name == "切除-拉伸8" || id == 329)
                            Require(id == 329 && name == "切除-拉伸8" && type == "Cut" && parents.SequenceEqual(new[] { 321, 19 }), "RIGHT_CONTEXT_CUT_OWNER_MISMATCH", 17, name);
                        if (id == 19)
                            Require(name == "基体零件-标准寄存柜 模型-1" && type == "SplitBody" && parents.Length == 0, "RIGHT_CONTEXT_MASTER_OWNER_MISMATCH", 17, name);
                        if (type == "ProfileFeature" || type == "3DProfileFeature")
                        {
                            Sketch sketch = feature.GetSpecificFeature2() as Sketch; SketchRelationManager manager = null; object[] values = null;
                            try
                            {
                                Require(sketch != null, "RIGHT_CONTEXT_SKETCH_UNAVAILABLE", 17, name);
                                manager = sketch.RelationManager;
                                Require(manager != null, "RIGHT_CONTEXT_MANAGER_UNAVAILABLE", 17, name);
                                values = ContextArray(manager.GetRelations((int)swSketchRelationFilterType_e.swExternal), "external relations");
                                Require(manager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal) == values.Length, "RIGHT_CONTEXT_EXTERNAL_COUNT_MISMATCH", 17, name);
                                foreach (object value in values) relations.Add(id + "|" + name + "|" + ContextRelationSignature(value as SketchRelation));
                            }
                            finally { if (values != null) foreach (object value in values) ContextRelease(value); ContextRelease(manager); ContextRelease(sketch); }
                        }
                        CaptureContextFeatures(feature.GetFirstSubFeature() as Feature, false, depth + 1, seen, features, relations, ref occurrences);
                    }
                    finally { ContextRelease(feature); feature = next; }
                }
            }
            finally { ContextRelease(feature); }
        }

        private static double[] ContextPoint(SketchPoint point)
        {
            Require(point != null, "RIGHT_CONTEXT_CONTOUR_POINT_UNAVAILABLE", 17, "草图21");
            try { return new[] { point.X, point.Y, point.Z }; }
            finally { ContextRelease(point); }
        }

        private static bool SameConfigurationObject(object left, object right)
        {
            if (left == null || right == null) return false;
            IntPtr a = IntPtr.Zero, b = IntPtr.Zero;
            try { a = Marshal.GetIUnknownForObject(left); b = Marshal.GetIUnknownForObject(right); return a == b; }
            finally { if (a != IntPtr.Zero) Marshal.Release(a); if (b != IntPtr.Zero) Marshal.Release(b); }
        }

        private static string CaptureAuditedRightConfiguration(ModelDoc2 model)
        {
            var evidence = new Dictionary<string, object>();
            var rows = new List<object>(); evidence["configurations"] = rows;
            ConfigurationManager manager = null; Configuration active = null, root = null;
            bool valid = false; string activeName = null;
            try
            {
                int count = model.GetConfigurationCount(); object rawNames = model.GetConfigurationNames();
                object[] names = ContextArray(rawNames, "configurations");
                evidence["path"] = model.GetPathName(); evidence["configurationCount"] = count;
                evidence["configurationNames"] = rawNames;
                manager = model.ConfigurationManager; active = manager.ActiveConfiguration;
                activeName = active == null ? null : active.Name; evidence["activeName"] = activeName;
                root = model.GetConfigurationByName("Default") as Configuration;
                int topCount = 0, derivedCount = 0; var unique = new HashSet<string>(StringComparer.Ordinal);
                valid = active != null && root != null && activeName == "Default" && count == names.Length && count >= 1 && count <= 2;
                int activeSame = SameConfigurationObject(active, root) ? 1 : 0;
                evidence["activeSameAsDefault"] = activeSame; valid &= activeSame == 1;
                foreach (object item in names)
                {
                    string requested = Convert.ToString(item); Configuration configuration = null, parent = null;
                    var row = new Dictionary<string, object>(); row["requestedName"] = requested; rows.Add(row);
                    try
                    {
                        configuration = model.GetConfigurationByName(requested) as Configuration;
                        row["rawNull"] = configuration == null;
                        if (configuration == null) { valid = false; continue; }
                        string name = configuration.Name; bool derived = configuration.IsDerived(); parent = configuration.GetParent() as Configuration;
                        row["name"] = name; row["id"] = configuration.GetID(); row["streamName"] = configuration.GetStreamName(); row["isDerived"] = derived;
                        row["parentNull"] = parent == null;
                        row["parentName"] = parent == null ? null : parent.Name;
                        row["parentId"] = parent == null ? (object)null : parent.GetID();
                        row["parentStreamName"] = parent == null ? null : parent.GetStreamName();
                        int parentSame = SameConfigurationObject(parent, root) ? 1 : 0;
                        row["parentSameAsDefault"] = parentSame;
                        valid &= unique.Add(requested) && name == requested;
                        if (derived) { derivedCount++; valid &= name == "DefaultSM-FLAT-PATTERN" && parent != null && parent.Name == "Default" && parentSame == 1; }
                        else { topCount++; valid &= name == "Default" && parent == null; }
                    }
                    finally { ContextRelease(parent); ContextRelease(configuration); }
                }
                evidence["topLevelCount"] = topCount; evidence["derivedCount"] = derivedCount;
                valid &= topCount == 1 && derivedCount <= 1 && topCount + derivedCount == count;
            }
            catch (Exception error) { evidence["error"] = error.ToString(); valid = false; }
            finally
            {
                ContextRelease(root); ContextRelease(active); ContextRelease(manager);
                evidence["valid"] = valid;
                string output = Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".configuration-state-" + Guid.NewGuid().ToString("N") + ".json");
                ParametricContext.Write(output, evidence);
            }
            Require(valid, "RIGHT_CONTEXT_CONFIGURATION_MISMATCH", 17, model.GetPathName());
            return activeName;
        }

        private static RightSourceContextProof CaptureRightSourceContext(ModelDoc2 model)
        {
            var proof = new RightSourceContextProof { path = model.GetPathName() };
            ParametricContext.CheckPath(proof.path, ParametricContext.AttemptDirectory);
            proof.configuration = CaptureAuditedRightConfiguration(model);
            var features = new List<string>(); var relations = new List<string>(); var seen = new HashSet<int>(); int occurrences = 0;
            CaptureContextFeatures(model.FirstFeature() as Feature, true, 0, seen, features, relations, ref occurrences);
            Require(seen.Contains(19) && seen.Contains(321) && seen.Contains(329), "RIGHT_CONTEXT_AUDITED_FEATURE_MISSING", 17, proof.path);
            proof.features = features.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            proof.externalRelations = relations.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            proof.nativeGeometry = CaptureGeometry(model, "L");
            Require(proof.nativeGeometry.traversal_complete && proof.nativeGeometry.body_count == 1 && proof.nativeGeometry.has_sheet_metal &&
                proof.nativeGeometry.has_flat_pattern && proof.nativeGeometry.error_feature_count == 0 && proof.nativeGeometry.warning_feature_count == 0,
                "RIGHT_CONTEXT_NATIVE_HEALTH_FAILED", 17, proof.path);
            object[] bodies = ContextArray(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false), "solid bodies");
            proof.bodyCount = bodies.Length;
            Require(bodies.Length == 1, "RIGHT_CONTEXT_BODY_COUNT_MISMATCH", 17, proof.path);
            try
            {
                Body2 body = bodies[0] as Body2;
                Require(body != null, "RIGHT_CONTEXT_BODY_UNAVAILABLE", 17, proof.path);
                proof.faceCount = body.GetFaceCount(); proof.edgeCount = body.GetEdgeCount();
                proof.massProperties = body.GetMassProperties(1.0) as double[];
                Require(proof.massProperties != null && proof.massProperties.Length > 0, "RIGHT_CONTEXT_MASS_UNAVAILABLE", 17, proof.path);
                object[] vertices = ContextArray(body.GetVertices(), "body vertices");
                try
                {
                    foreach (object value in vertices)
                    {
                        Vertex vertex = value as Vertex; double[] point = vertex == null ? null : vertex.GetPoint() as double[];
                        Require(point != null && point.Length == 3 && point.All(number => !double.IsNaN(number) && !double.IsInfinity(number)), "RIGHT_CONTEXT_VERTEX_UNAVAILABLE", 17, proof.path);
                        proof.vertices.Add(point);
                    }
                }
                finally { foreach (object value in vertices) ContextRelease(value); }
            }
            finally { foreach (object body in bodies) ContextRelease(body); }
            Require(proof.vertices.Count > 0, "RIGHT_CONTEXT_VERTICES_EMPTY", 17, proof.path);
            Feature contourFeature = FindFeature(model, "草图21"); Sketch contour = null; object[] segments = null, points = null; MathTransform transform = null;
            try
            {
                Require(contourFeature != null && contourFeature.GetID() == 321, "RIGHT_CONTEXT_CONTOUR_OWNER_MISMATCH", 17, proof.path);
                contour = contourFeature.GetSpecificFeature2() as Sketch;
                Require(contour != null, "RIGHT_CONTEXT_CONTOUR_UNAVAILABLE", 17, proof.path);
                transform = contour.ModelToSketchTransform; proof.sketchTransform = transform.ArrayData as double[];
                Require(proof.sketchTransform != null && proof.sketchTransform.Length >= 12, "RIGHT_CONTEXT_CONTOUR_TRANSFORM_UNAVAILABLE", 17, proof.path);
                segments = ContextArray(contour.GetSketchSegments(), "草图21 segments");
                points = ContextArray(contour.GetSketchPoints2(), "草图21 points");
                Require(segments.Length > 0, "RIGHT_CONTEXT_CONTOUR_EMPTY", 17, proof.path);
                foreach (object value in segments)
                {
                    SketchSegment segment = value as SketchSegment; SketchLine line = value as SketchLine; SketchArc arc = value as SketchArc;
                    Require(segment != null && (line != null || arc != null), "RIGHT_CONTEXT_CONTOUR_TYPE_UNAUDITED", 17, "草图21 line/arc only");
                    string key = "segment:" + string.Join(",", ContextIds(segment.GetID())) + ":" + segment.GetType() + ":" + segment.ConstructionGeometry;
                    double[] coordinates = line != null ? ContextPoint(line.IGetStartPoint2()).Concat(ContextPoint(line.IGetEndPoint2())).ToArray() :
                        ContextPoint(arc.IGetStartPoint2()).Concat(ContextPoint(arc.IGetEndPoint2())).Concat(ContextPoint(arc.IGetCenterPoint2())).Concat(new[] { arc.GetRadius(), (double)arc.IsCircle() }).ToArray();
                    proof.contour.Add(key, coordinates.Concat(new[] { segment.GetLength() }).ToArray());
                }
                foreach (object value in points)
                {
                    SketchPoint point = value as SketchPoint;
                    Require(point != null, "RIGHT_CONTEXT_SKETCH_POINT_UNAVAILABLE", 17, "草图21");
                    proof.contour.Add("point:" + string.Join(",", ContextIds(point.GetID())), new[] { point.X, point.Y, point.Z });
                }
            }
            finally
            {
                if (segments != null) foreach (object value in segments) ContextRelease(value);
                if (points != null) foreach (object value in points) ContextRelease(value);
                ContextRelease(transform); ContextRelease(contour); ContextRelease(contourFeature);
            }
            return proof;
        }

        private static void RequireContextNumbersEqual(double[] before, double[] after, double tolerance, string label)
        {
            Require(before != null && after != null && before.Length == after.Length && before.Zip(after, (left, right) =>
                !double.IsNaN(left) && !double.IsNaN(right) && !double.IsInfinity(left) && !double.IsInfinity(right) && Math.Abs(left - right) <= tolerance).All(value => value),
                "RIGHT_CONTEXT_NUMERIC_GEOMETRY_CHANGED", 17, label);
        }

        private static void RequireRightContextUnchanged(RightSourceContextProof before, RightSourceContextProof after, bool removedTwo)
        {
            Require(before != null && after != null && before.configuration == after.configuration && before.features.SequenceEqual(after.features) &&
                before.bodyCount == after.bodyCount && before.faceCount == after.faceCount && before.edgeCount == after.edgeCount,
                "RIGHT_CONTEXT_NATIVE_STRUCTURE_CHANGED", 17, after == null ? "missing snapshot" : after.path);
            string[] expected = removedTwo ? before.externalRelations.Where(value => !value.StartsWith("321|草图21|", StringComparison.Ordinal)).ToArray() : before.externalRelations;
            Require(expected.Length == 21 && after.externalRelations.Length == 21 && expected.SequenceEqual(after.externalRelations), "RIGHT_CONTEXT_OTHER_EXTERNAL_RELATIONS_CHANGED", 17, after.path);
            Require(before.vertices.Count == after.vertices.Count, "RIGHT_CONTEXT_VERTEX_COUNT_CHANGED", 17, after.path);
            var remaining = new List<double[]>(after.vertices);
            foreach (double[] point in before.vertices)
            {
                int index = remaining.FindIndex(candidate => Math.Abs(candidate[0] - point[0]) <= .000002 && Math.Abs(candidate[1] - point[1]) <= .000002 && Math.Abs(candidate[2] - point[2]) <= .000002);
                Require(index >= 0, "RIGHT_CONTEXT_VERTEX_POSITION_CHANGED", 17, after.path);
                remaining.RemoveAt(index);
            }
            RequireContextNumbersEqual(before.massProperties, after.massProperties, .0000000001, "body mass/volume/area/inertia");
            RequireContextNumbersEqual(before.sketchTransform, after.sketchTransform, .000000001, "草图21 transform");
            Require(before.contour.Count == after.contour.Count && before.contour.Keys.All(key => after.contour.ContainsKey(key)), "RIGHT_CONTEXT_CONTOUR_STRUCTURE_CHANGED", 17, after.path);
            foreach (var row in before.contour) RequireContextNumbersEqual(row.Value, after.contour[row.Key], .000002, "草图21/切除-拉伸8 " + row.Key);
        }

        private static void RequireAuditedRightReferences(ModelDoc2 model, List<object> records, bool beforeDetach, string stage)
        {
            object files, parts, features, types, statuses, entities, components;
            model.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
            object[][] columns = new[] { ContextArray(files, "files"), ContextArray(parts, "parts"), ContextArray(features, "features"), ContextArray(types, "types"),
                ContextArray(statuses, "statuses"), ContextArray(entities, "entities"), ContextArray(components, "components") };
            records.Add(new { inspection = "right_source_context_references", stage = stage, path = model.GetPathName(), beforeDetach = beforeDetach,
                files = files, parts = parts, features = features, types = types, statuses = statuses, entities = entities, components = components });
            int count = beforeDetach ? 4 : 2;
            Require(columns.All(column => column.Length == count), "RIGHT_CONTEXT_FILE_REFERENCE_COUNT_CHANGED", 17, stage);
            string master = Path.Combine(ParametricContext.CadDirectory, "标准寄存柜 模型.SLDPRT"), root = Path.Combine(ParametricContext.CadDirectory, "标准寄存柜1917×1000×550(总装配).SLDASM");
            var actual = new List<string>();
            for (int i = 0; i < count; i++)
            {
                Require(columns[4][i] is int && ((int)columns[4][i] == 3 || (int)columns[4][i] == 4), "RIGHT_CONTEXT_REFERENCE_LOCKED_BROKEN_OR_UNAUDITED", 17, stage);
                string file = Convert.ToString(columns[0][i]), part = Convert.ToString(columns[1][i]);
                bool isMaster = ContextPathEquals(file, master), isRoot = ContextPathEquals(file, root);
                Require(isMaster || (beforeDetach && isRoot), "RIGHT_CONTEXT_REFERENCE_PATH_MISMATCH", 17, file);
                Require(isMaster ? ContextPathEquals(part, master) : part == "", "RIGHT_CONTEXT_REFERENCE_COMPONENT_PATH_MISMATCH", 17, part);
                actual.Add(string.Join("|", new[] { isMaster ? "master" : "context", Convert.ToString(columns[2][i]), Convert.ToString(columns[3][i]), Convert.ToString(columns[5][i]), Convert.ToString(columns[6][i]) }));
            }
            var expected = new List<string> { "master|基体零件-标准寄存柜 模型-1|实体|实体 ← 标准寄存柜 模型|", "master|基体零件-标准寄存柜 模型-1|特征|特征 ← 标准寄存柜 模型|" };
            if (beforeDetach) for (int i = 0; i < 2; i++) expected.Add("context|草图21  ←  切除-拉伸8|点|面 ← 箱体左侧板焊接<1>/箱体左侧板<1>|箱体竖隔板L焊接<1>/箱体竖隔板L<1>");
            Require(actual.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)), "RIGHT_CONTEXT_FILE_REFERENCE_WHITELIST_MISMATCH", 17, stage);
        }

        private static RightSourceContextProof DetachAuditedRightSourceContext(ModelDoc2 model, List<object> records, string work)
        {
            ParametricContext.AssertMutation(model.GetPathName());
            Require(!model.IsOpenedReadOnly() && ContextPathEquals(model.GetPathName(), work) &&
                ContextPathEquals(work, Path.Combine(ParametricContext.AttemptDirectory, "lock_work_R.SLDPRT")), "RIGHT_CONTEXT_DETACH_REQUIRES_EXACT_WRITABLE_R_WORK", 17, work);
            RequireAuditedRightReferences(model, records, true, "before-detach");
            RightSourceContextProof before = CaptureRightSourceContext(model);
            Require(before.externalRelations.Length == 23, "RIGHT_CONTEXT_EXTERNAL_TOTAL_NOT_AUDITED", 17, work);
            Feature feature = FindFeature(model, "草图21"); Sketch sketch = null; SketchRelationManager manager = null; object[] values = null;
            try
            {
                Require(feature != null && feature.GetID() == 321, "RIGHT_CONTEXT_DELETE_OWNER_MISMATCH", 17, work);
                sketch = feature.GetSpecificFeature2() as Sketch;
                Require(sketch != null, "RIGHT_CONTEXT_DELETE_SKETCH_UNAVAILABLE", 17, work);
                manager = sketch.RelationManager;
                Require(manager != null, "RIGHT_CONTEXT_DELETE_MANAGER_UNAVAILABLE", 17, work);
                values = ContextArray(manager.GetRelations((int)swSketchRelationFilterType_e.swExternal), "audited two relations");
                string[] expected = new[] { "9|2,2|P:2,3;P:0,2", "9|2,2|P:5,6;P:3,5" };
                Require(manager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal) == 2 && values.Length == 2 &&
                    values.Select(value => ContextRelationSignature(value as SketchRelation)).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)),
                    "RIGHT_CONTEXT_DELETE_RELATION_WHITELIST_MISMATCH", 17, work);
                records.Add(new { inspection = "right_source_context_detach_before", proof = before, deletedRelationSignatures = expected,
                    auditSha256 = "086F86C7586B4BF0ECF01C8E1B11B03A409261FB2B14D8565C7BACD0E79A1BB8", scopedPointIds = true });
                foreach (object value in values) Require(manager.DeleteRelation((SketchRelation)value), "RIGHT_CONTEXT_DELETE_RELATION_FAILED", 17, work);
            }
            finally { if (values != null) foreach (object value in values) ContextRelease(value); ContextRelease(manager); ContextRelease(sketch); ContextRelease(feature); }
            Require(model.ForceRebuild3(false), "RIGHT_CONTEXT_DETACH_REBUILD_FAILED", 17, work);
            RightSourceContextProof after = CaptureRightSourceContext(model);
            RequireRightContextUnchanged(before, after, true);
            RequireAuditedRightReferences(model, records, false, "after-detach");
            records.Add(new { inspection = "right_source_context_detach_after", proof = after, deletedExternalRelations = 2, retainedExternalRelations = 21,
                vertexToleranceMm = .002, pointIdScope = "unique 草图21 manager in this work path and Default configuration" });
            return after;
        }

        private static void RequireDetachedRightSourceContext(ModelDoc2 model, List<object> records, RightSourceContextProof expected, string stage)
        {
            Require(expected != null, "RIGHT_CONTEXT_PERSISTED_PROOF_MISSING", 17, stage);
            RightSourceContextProof actual = CaptureRightSourceContext(model);
            Require(actual.externalRelations.Length == 21 && !actual.externalRelations.Any(value => value.StartsWith("321|草图21|", StringComparison.Ordinal)), "RIGHT_CONTEXT_TARGET_EXTERNAL_RELATION_REAPPEARED", 17, stage);
            RequireRightContextUnchanged(expected, actual, false);
            RequireAuditedRightReferences(model, records, false, stage);
            records.Add(new { inspection = "right_source_context_persisted_gate", stage = stage, proof = actual, retainedExternalRelations = 21, vertexToleranceMm = .002 });
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
