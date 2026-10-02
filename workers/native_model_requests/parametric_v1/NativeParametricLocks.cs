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

        internal static void Run(ISldWorks sw, List<object> records, bool readOnly)
        {
            var contractRows = ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "rowsByColumn");
            string source = Path.Combine(ParametricContext.CadDirectory, "箱体竖隔板L.sldprt");
            string sourceCopy = Path.Combine(ParametricContext.AttemptDirectory, "lock_seed_left.SLDPRT");
            if (!readOnly && !File.Exists(sourceCopy)) { ParametricContext.AssertMutation(sourceCopy); File.Copy(source, sourceCopy, false); }
            foreach (string side in new[] { "L", "R" })
            {
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
                if (GeometryGate(existing, side)) { records.Add(new { side = side, reusedMatchingNativeSlots = true, geometry = existing }); continue; }
                ParametricContext.Require(!readOnly, "LOCK_MEASURED_GEOMETRY_MISMATCH " + side);
                string work = Path.Combine(ParametricContext.AttemptDirectory, "lock_work_" + side + ".SLDPRT");
                ParametricContext.AssertMutation(work); File.Copy(sourceCopy, work, false);
                model = NativeParametricAssembly.Open(sw, work, false);
                var result = new Result();
                ModelDoc2 mirrored = null;
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
                        Require(model.Extension.SaveAs(rightSource,0,1,null,ref errors,ref warnings)&&errors==0&&warnings==0,"PARAMETRIC_RIGHT_SOURCE_SAVE_FAILED",17,side);
                        built = Path.Combine(ParametricContext.AttemptDirectory, "lock_right_native.SLDPRT");
                        ParametricContext.AssertMutation(built);
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
                        geometry=CaptureGeometry(mirrored,"R");
                        object files,parts,features,types,statuses,entities,components;
                        mirrored.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components);
                        Array referenceFiles=files as Array;
                        bool localSourceBound=referenceFiles!=null&&referenceFiles.Length>0&&referenceFiles.Cast<object>().All(file=>Path.GetFullPath(Convert.ToString(file)).Equals(rightSource,StringComparison.OrdinalIgnoreCase));
                        records.Add(new{side=side,mirrorGeometry=geometry,rightParameterSource=rightSource,referenceFiles=files,referenceStatuses=statuses,localSourceBound=localSourceBound});
                        Require(MirrorGeometryGate(geometry,"R")&&localSourceBound,"PARAMETRIC_RIGHT_LOCK_EDGE_GATE_FAILED",17,side);
                        errors=0;warnings=0;
                        Require(mirrored.Extension.SaveAs(built,0,1,null,ref errors,ref warnings)&&errors==0&&warnings==0,"PARAMETRIC_RIGHT_LOCK_SAVE_FAILED",18,side);
                    }
                    records.Add(new { side = side, reusedMatchingNativeSlots = false, geometry = geometry });
                }
                finally
                {
                    if (mirrored != null) { sw.CloseDoc(mirrored.GetTitle()); Marshal.ReleaseComObject(mirrored); }
                    sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model);
                }
                ParametricContext.AssertMutation(target);
                string backup = target + ".before-parametric-lock.bak";
                File.Replace(built, target, backup);
                Console.WriteLine("PARAMETRIC_LOCK_COLUMN_VERIFIED " + side);
            }
        }
    }
}
