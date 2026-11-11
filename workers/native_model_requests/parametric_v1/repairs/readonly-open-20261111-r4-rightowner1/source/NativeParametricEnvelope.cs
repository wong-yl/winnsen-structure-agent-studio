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
    internal static partial class NativeParametricWidth
    {
        internal static double ShallowShelfPitch(double depth)
        {
            // Keep the 16 mm tabs clear of the fixed 47.8 mm front corner and its R2 fillet.
            return ShallowShelfBack(depth)-ShallowShelfFront(depth);
        }

        private static double ShallowShelfBasePitch(double depth)
        {
            return Math.Min(250,depth-130);
        }

        private static double ShallowShelfBaseFront(double depth)
        {
            return (depth-3-ShallowShelfBasePitch(depth))/2;
        }

        internal static double ShallowShelfFront(double depth)
        {
            // F=85 is the first tested position that clears the front side return and frame at D250.
            return Math.Max(85,ShallowShelfBaseFront(depth));
        }

        internal static double ShallowShelfBack(double depth)
        {
            return ShallowShelfBaseFront(depth)+ShallowShelfBasePitch(depth);
        }

        private static double ShallowBraceLength(double depth)
        {
            return ShallowShelfPitch(depth)+50;
        }

        internal static void CaptureShallowVerticalFeatures(ModelDoc2 model,List<object> records,string stage)
        {
            var features=new List<object>();
            Action<Feature,string,int> capture=null;
            capture=(feature,path,level)=>{
                if(feature==null||level>12)return;
                var dimensions=new List<object>();DisplayDimension display=feature.GetFirstDisplayDimension() as DisplayDimension;
                while(display!=null){Dimension value=display.GetDimension2(0);dimensions.Add(new{name=value.FullName,valueMm=value.SystemValue*1000,driven=value.DrivenState});display=feature.GetNextDisplayDimension(display) as DisplayDimension;}
                Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                object definition=Safe(delegate{return feature.GetDefinition();},null);
                LibraryFeatureData library=definition as LibraryFeatureData;
                object libraryDetails=null,patternDetails=null;
                if(library!=null)libraryDetails=new{part=Safe(delegate{return library.LibraryPart;},""),referenceCount=Safe(delegate{return library.GetReferencesCount();},-1)};
                LinearPatternFeatureData pattern=definition as LinearPatternFeatureData;
                if(pattern!=null){
                    bool accessed=pattern.AccessSelections(model,null);
                    try{if(accessed)patternDetails=new{axis=ShallowShelfReferenceGeometry(pattern.D1Axis),spacingMm=pattern.D1Spacing*1000,count=pattern.D1TotalInstances,reverse=pattern.D1ReverseDirection,features=ShallowShelfReferences(pattern.PatternFeatureArray),faces=ShallowShelfReferences(pattern.PatternFaceArray),bodies=ShallowShelfReferences(pattern.PatternBodyArray)};}
                    finally{if(accessed)pattern.ReleaseSelectionAccess();}
                }
                features.Add(new{path=path,name=feature.Name,type=feature.GetTypeName2(),error=feature.GetErrorCode(),suppressed=feature.IsSuppressed(),definitionType=definition==null?null:definition.GetType().FullName,library=libraryDetails,pattern=patternDetails,dimensions=dimensions,sketch=CaptureDerivedFailureSketch(feature),externalRelationCount=sketch==null?-1:sketch.RelationManager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal),points=sketch==null?null:Safe(delegate{return ((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X*1000,p.Y*1000,p.Z*1000}).ToArray();},null)});
                Feature child=feature.GetFirstSubFeature() as Feature;
                while(child!=null){capture(child,path+"/"+feature.Name,level+1);child=child.GetNextSubFeature() as Feature;}
            };
            Feature root=model.FirstFeature() as Feature;
            while(root!=null){capture(root,"root",0);root=root.GetNextFeature() as Feature;}
            records.Add(new{inspection="shallow_vertical_features",stage=stage,file=Path.GetFileName(model.GetPathName()),snapshot=CapturePartSnapshot(model),features=features});
        }

        private static void PrepareShallowVerticalStiffener(ISldWorks sw,string folder,List<object> records,double depth,PartSnapshot baseline)
        {
            ModelDoc2 model=NativeParametricAssembly.Open(sw,ExactFile(folder,"标准寄存柜 模型.SLDPRT"),false);
            try{
                int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
                double front=ShallowShelfFront(depth);
                foreach(string name in new[]{"草图100","草图104","草图112"})records.Add(new{inspection="vertical_stiffener_seed_before",name=name,geometry=CaptureDerivedFailureSketch(FindFeature(model,name))});
                Feature feature=FindFeature(model,"草图100");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                double[][] before=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                Array references=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                if(references!=null)foreach(SketchRelation relation in references)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_VERTICAL_SEED_DETACH_FAILED");
                model.ClearSelection2(true);feature.Select2(false,0);model.EditSketch();model.ClearSelection2(true);
                Array segments=sketch.GetSketchSegments() as Array;ParametricContext.Require(segments!=null&&segments.Length==5,"SHALLOW_VERTICAL_SEED_SEGMENT_DRIFT");
                foreach(SketchSegment segment in segments)ParametricContext.Require(segment.Select4(true,null),"SHALLOW_VERTICAL_SEED_SELECT_FAILED");
                model.Extension.MoveOrCopy(false,0,true,0,0,0,(front-148.5)/1000,0,0);
                double[][] actual=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                ParametricContext.Require(before.All(p=>actual.Any(q=>Math.Abs(q[0]-p[0]-(front-148.5)/1000)<.000001&&Math.Abs(q[1]-p[1])<.000001&&Math.Abs(q[2]-p[2])<.000001)),"SHALLOW_VERTICAL_SEED_MOVE_READBACK_FAILED");
                model.SketchManager.InsertSketch(true);
                Dimension pitch=model.Parameter("D3@阵列(线性)7") as Dimension;ParametricContext.Require(pitch!=null&&Math.Abs(pitch.SystemValue*1000-250)<.001,"SHALLOW_VERTICAL_PATTERN_DRIFT");pitch.SystemValue=ShallowShelfPitch(depth)/1000;
                bool rebuilt=model.ForceRebuild3(false);PartSnapshot after=CapturePartSnapshot(model);
                foreach(string name in new[]{"草图100","草图104","草图112"})records.Add(new{inspection="vertical_stiffener_seed_after",name=name,geometry=CaptureDerivedFailureSketch(FindFeature(model,name))});
                records.Add(new{inspection="vertical_stiffener_seed_result",rebuilt=rebuilt,snapshot=after,removedReferences=references==null?0:references.Length,targetFrontMm=front,pitchMm=ShallowShelfPitch(depth)});
                ParametricContext.Require(rebuilt&&DerivedPartHealthGate(after,baseline),"SHALLOW_VERTICAL_SEED_REBUILD_FAILED");
                NativeParametricAssembly.Save(model);
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }

        private static void UpdateVerticalFormingPatternHeight(ModelDoc2 model,List<object> records,double height)
        {
            Dimension pitch=model.Parameter("D3@阵列(线性)1") as Dimension,count=model.Parameter("D1@阵列(线性)1") as Dimension;
            double unit=(height-87)/12;
            ParametricContext.Require(pitch!=null&&count!=null&&pitch.DrivenState==2&&(Math.Abs(pitch.SystemValue*1000-152.5)<.001||Math.Abs(pitch.SystemValue*1000-unit)<.001)&&Math.Abs(count.SystemValue-11)<.000001,"VERTICAL_FORMING_PATTERN_SOURCE_DRIFT");
            pitch.SystemValue=unit/1000;
            records.Add(new{inspection="vertical_forming_height_pitch",measuredPitchMm=pitch.SystemValue*1000,count=count.SystemValue});
            ParametricContext.Require(Math.Abs(pitch.SystemValue*1000-unit)<.001,"VERTICAL_FORMING_PATTERN_READBACK_FAILED");
        }

        private static void RelocateShallowVerticalTool(ISldWorks sw,ModelDoc2 model,List<object> records,double depth)
        {
            int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);
            CaptureShallowVerticalFeatures(model,records,"forming-before-move");
            Feature tool=FindFeature(model,"拱桥成型模具1");ParametricContext.Require(tool!=null,"SHALLOW_VERTICAL_TOOL_MISSING");
            Feature feature=tool.GetFirstSubFeature() as Feature;
            while(feature!=null&&feature.Name!="草图8")feature=feature.GetNextSubFeature() as Feature;
            ParametricContext.Require(feature!=null,"SHALLOW_VERTICAL_TOOL_PLACEMENT_MISSING");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
            double[][] before=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
            MathUtility math=sw.GetMathUtility() as MathUtility;
            double[][] beforeWorld=before.Select(p=>(double[])((MathPoint)((MathPoint)math.CreatePoint(p)).MultiplyTransform(sketch.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData).ToArray();
            Array references=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
            if(references!=null)foreach(SketchRelation relation in references)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_VERTICAL_TOOL_DETACH_FAILED");
            double front=ShallowShelfFront(depth);
            double currentZ=beforeWorld[0][2],currentTop=beforeWorld.Max(p=>p[1]),targetTop=(1703+FrameGridHeightTranslation(ParametricContext.HeightMm))/1000;
            ParametricContext.Require(beforeWorld.All(p=>Math.Abs(p[2]-currentZ)<.000001)&&(Math.Abs(currentZ+.1485)<.000001||Math.Abs(currentZ+front/1000)<.000001),"SHALLOW_VERTICAL_TOOL_DEPTH_DRIFT");
            ParametricContext.Require(Math.Abs(currentTop-1.703)<.000001||Math.Abs(currentTop-targetTop)<.000001,"SHALLOW_VERTICAL_TOOL_HEIGHT_DRIFT");
            double[] worldVector=new[]{0.0,targetTop-currentTop,-front/1000-currentZ};
            double[] vector=(double[])((MathVector)((MathVector)math.CreateVector(worldVector)).MultiplyTransform(sketch.ModelToSketchTransform)).ArrayData;
            records.Add(new{inspection="shallow_vertical_tool_move_before",removedReferences=references==null?0:references.Length,beforePoints=before,afterDetachPoints=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),vector=vector});
            model.ClearSelection2(true);ParametricContext.Require(feature.Select2(false,0),"SHALLOW_VERTICAL_TOOL_SKETCH_SELECT_FAILED");
            model.SketchModifyTranslate(0,0,vector[0],vector[1]);
            double[][] actual=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
            double[][] actualWorld=actual.Select(p=>(double[])((MathPoint)((MathPoint)math.CreatePoint(p)).MultiplyTransform(sketch.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData).ToArray();
            records.Add(new{inspection="shallow_vertical_tool_move_after",points=actual,worldPoints=actualWorld,transform=sketch.ModelToSketchTransform.ArrayData});
            ParametricContext.Require(before.Length==actual.Length&&beforeWorld.All(p=>actualWorld.Any(q=>Enumerable.Range(0,3).All(i=>Math.Abs(q[i]-p[i]-worldVector[i])<.000001))),"SHALLOW_VERTICAL_TOOL_MOVE_READBACK_FAILED");
            if(ParametricContext.HeightMm!=1917)UpdateVerticalFormingPatternHeight(model,records,ParametricContext.HeightMm);
            model.ForceRebuild3(false);
            var formedFaces=new List<object>();
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Feature owner=face.GetFeature() as Feature;string name=owner==null?null:owner.Name;
                if(name=="拱桥成型模具1"||name=="阵列(线性)1")formedFaces.Add(new{owner=name,box=face.GetBox(),area=face.GetArea()});
            }
            records.Add(new{inspection="shallow_vertical_forming_faces",faces=formedFaces});
        }

        private static void RelocateShallowVerticalHoles(ISldWorks sw,ModelDoc2 model,List<object> records,double depth)
        {
            Feature feature=FindFeature(model,"草图10");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
            SketchArc[] arcs=((Array)sketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>().ToArray();
            ParametricContext.Require(arcs.Length==2&&arcs.All(a=>a.IsCircle()==1&&Math.Abs(a.GetRadius()-.0026)<.000001),"SHALLOW_VERTICAL_HOLE_PROFILE_DRIFT");
            MathUtility math=sw.GetMathUtility() as MathUtility;
            double[][] before=arcs.Select(a=>{SketchPoint p=a.IGetCenterPoint2();return (double[])((MathPoint)((MathPoint)math.CreatePoint(new[]{p.X,p.Y,p.Z})).MultiplyTransform(sketch.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData;}).ToArray();
            double front=ShallowShelfFront(depth),currentZ=before[0][2];
            ParametricContext.Require(before.All(p=>Math.Abs(p[2]-currentZ)<.000001)&&(Math.Abs(currentZ+.1485)<.000001||Math.Abs(currentZ+front/1000)<.000001),"SHALLOW_VERTICAL_HOLE_DEPTH_DRIFT");
            ParametricContext.Require(new[]{.6748,1.5898}.All(y=>before.Any(p=>Math.Abs(p[1]-y)<.000001)),"SHALLOW_VERTICAL_HOLE_HEIGHT_DRIFT");
            Array references=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
            if(references!=null)foreach(SketchRelation relation in references)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_VERTICAL_HOLE_DETACH_FAILED");
            double[] vector=(double[])((MathVector)((MathVector)math.CreateVector(new[]{0.0,0.0,-front/1000-currentZ})).MultiplyTransform(sketch.ModelToSketchTransform)).ArrayData;
            model.ClearSelection2(true);ParametricContext.Require(feature.Select2(false,0),"SHALLOW_VERTICAL_HOLE_SKETCH_SELECT_FAILED");model.SketchModifyTranslate(0,0,vector[0],vector[1]);
            double[][] actual=((Array)sketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>().Select(a=>{SketchPoint p=a.IGetCenterPoint2();ParametricContext.Require(Math.Abs(a.GetRadius()-.0026)<.000001,"SHALLOW_VERTICAL_HOLE_RADIUS_CHANGED");return (double[])((MathPoint)((MathPoint)math.CreatePoint(new[]{p.X,p.Y,p.Z})).MultiplyTransform(sketch.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData;}).ToArray();
            records.Add(new{inspection="shallow_vertical_hole_move",removedReferences=references==null?0:references.Length,beforeWorld=before,afterWorld=actual,targetFrontMm=front});
            ParametricContext.Require(actual.Length==2&&before.All(p=>actual.Any(q=>Math.Abs(q[0]-p[0])<.000001&&Math.Abs(q[1]-p[1])<.000001&&Math.Abs(q[2]+front/1000)<.000001)),"SHALLOW_VERTICAL_HOLE_READBACK_FAILED");
            model.ForceRebuild3(false);
            var cylinders=new List<double[]>();
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsCylinder())continue;
                double[] c=surface.CylinderParams as double[];
                if(c!=null&&Math.Abs(Math.Abs(c[3])-1)<.000001&&Math.Abs(c[6]-.0026)<.000001)cylinders.Add(c);
            }
            records.Add(new{inspection="shallow_vertical_mount_cylinders",cylinders=cylinders,targetFrontMm=front});
            ParametricContext.Require(cylinders.Count==2&&new[]{.6748,1.5898}.All(y=>cylinders.Any(c=>Math.Abs(c[1]-y)<.000001&&Math.Abs(c[2]+front/1000)<.000001)),"SHALLOW_VERTICAL_MOUNT_CYLINDERS_FAILED");
        }

        private static void VerifyShallowVerticalFlat(ModelDoc2 model,List<object> records)
        {
            PartSnapshot folded=CapturePartSnapshot(model);Feature flat=model.FirstFeature() as Feature;
            while(flat!=null&&flat.GetTypeName2()!="FlatPattern")flat=flat.GetNextFeature() as Feature;
            ParametricContext.Require(flat!=null&&flat.IsSuppressed(),"SHALLOW_VERTICAL_FLAT_SOURCE_DRIFT");string name=flat.Name;
            bool opened=flat.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null);
            bool rebuilt=model.ForceRebuild3(false);PartSnapshot unfolded=CapturePartSnapshot(model);
            records.Add(new{inspection="shallow_vertical_flat",opened=opened,rebuilt=rebuilt,snapshot=unfolded});
            flat=FindFeature(model,name);ParametricContext.Require(flat.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null)&&model.ForceRebuild3(false),"SHALLOW_VERTICAL_FOLD_RESTORE_FAILED");
            PartSnapshot restored=CapturePartSnapshot(model);
            ParametricContext.Require(opened&&rebuilt&&PartHealthGate(unfolded,true)&&DerivedPartHealthGate(restored,folded)&&folded.part_box_m.Zip(restored.part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v),"SHALLOW_VERTICAL_FLAT_FAILED");
        }

        internal static void ProbeShallowAssemblyLinks(ISldWorks sw,List<object> records)
        {
            string folder=ParametricContext.CadDirectory;
            ModelDoc2 root=NativeParametricAssembly.Open(sw,ExactFile(folder,"标准寄存柜1917×1000×550(总装配).SLDASM"),true);
            try{
                records.Add(new{inspection="shallow_root_rebuild",rebuilt=root.ForceRebuild3(false)});
                foreach(string name in new[]{"标准寄存柜 模型.SLDPRT","箱体侧板加强筋1.sldprt","箱体左侧板.sldprt","箱体右侧板.sldprt","箱体竖隔板L.sldprt","箱体竖隔板R.SLDPRT"}){
                    ModelDoc2 model=NativeParametricAssembly.Open(sw,ExactFile(folder,name),true);
                    try{
                        records.Add(new{inspection="shallow_assembly_context_part",file=name,details=CaptureDerivedRebuildFailure(model)});
                        if(name=="箱体竖隔板R.SLDPRT"){
                            var cylinders=new List<double[]>();
                            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                                Surface surface=face.GetSurface() as Surface;if(surface!=null&&surface.IsCylinder())cylinders.Add(surface.CylinderParams as double[]);
                            }
                            records.Add(new{inspection="shallow_right_partition_cylinders",cylinders=cylinders});
                        }
                        if(name=="标准寄存柜 模型.SLDPRT"||name=="箱体侧板加强筋1.sldprt"){
                            var sketches=new List<object>();Feature feature=model.FirstFeature() as Feature;
                            while(feature!=null){if(feature.GetTypeName2()=="ProfileFeature")sketches.Add(new{name=feature.Name,geometry=CaptureDerivedFailureSketch(feature)});feature=feature.GetNextFeature() as Feature;}
                            records.Add(new{inspection="shallow_assembly_seed_sketches",file=name,sketches=sketches});
                        }
                    }finally{Marshal.ReleaseComObject(model);}
                }
            }finally{sw.CloseDoc(root.GetTitle());Marshal.ReleaseComObject(root);}
        }

        private static void UpdateShallowSketchHeight(ISldWorks sw,ModelDoc2 model,List<object> records,string name,int segmentCount,double sourceMinMm,double sourceMaxMm,bool linesOnly=false,double? translationMm=null)
        {
            Feature feature=FindFeature(model,name);Sketch sketch=feature.GetSpecificFeature2() as Sketch;
            SketchSegment[] segments=((Array)sketch.GetSketchSegments()).Cast<SketchSegment>().Where(s=>!linesOnly||s is SketchLine).ToArray();
            ParametricContext.Require(segments.Length==segmentCount,"SHALLOW_HEIGHT_SEGMENT_COUNT_CHANGED "+name);
            MathUtility math=sw.GetMathUtility() as MathUtility;MathTransform toModel=sketch.ModelToSketchTransform.Inverse() as MathTransform;
            var selectedPoints=new List<double[]>();
            foreach(SketchSegment segment in segments){
                SketchLine line=segment as SketchLine;SketchArc arc=segment as SketchArc;
                ParametricContext.Require(line!=null||arc!=null,"SHALLOW_HEIGHT_SEGMENT_TYPE_CHANGED "+name);
                foreach(SketchPoint p in line!=null?new[]{line.GetStartPoint2() as SketchPoint,line.GetEndPoint2() as SketchPoint}:new[]{arc.IGetCenterPoint2()})selectedPoints.Add(new[]{p.X,p.Y,p.Z});
            }
            double[] worldY=selectedPoints.Select(p=>((double[])((MathPoint)((MathPoint)math.CreatePoint(p)).MultiplyTransform(toModel)).ArrayData)[1]*1000).ToArray();
            double delta=translationMm??ParametricContext.HeightMm-1917;
            bool atSource=Math.Abs(worldY.Min()-sourceMinMm)<.001&&Math.Abs(worldY.Max()-sourceMaxMm)<.001;
            bool atTarget=Math.Abs(worldY.Min()-(sourceMinMm+delta))<.001&&Math.Abs(worldY.Max()-(sourceMaxMm+delta))<.001;
            records.Add(new{inspection="shallow_sketch_height_before",file=Path.GetFileName(model.GetPathName()),sketch=name,worldYminMm=worldY.Min(),worldYmaxMm=worldY.Max(),atSource=atSource,atTarget=atTarget});
            ParametricContext.Require(atSource||atTarget,"SHALLOW_HEIGHT_POSITION_DRIFT "+name);
            if(atSource&&!atTarget){
                double[][] before=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                double[] vector=(double[])((MathVector)((MathVector)math.CreateVector(new[]{0.0,delta/1000,0.0})).MultiplyTransform(sketch.ModelToSketchTransform)).ArrayData;
                ParametricContext.Require(Math.Abs(vector[2])<.000001,"SHALLOW_HEIGHT_TRANSLATION_OFF_PLANE "+name);
                model.ClearSelection2(true);ParametricContext.Require(feature.Select2(false,0),"SHALLOW_HEIGHT_SKETCH_SELECT_FAILED "+name);model.EditSketch();model.ClearSelection2(true);
                foreach(SketchSegment segment in segments)ParametricContext.Require(segment.Select4(true,null),"SHALLOW_HEIGHT_SEGMENT_SELECT_FAILED "+name);
                model.Extension.MoveOrCopy(false,0,true,0,0,0,vector[0],vector[1],0);
                double[][] readback=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                foreach(double[] p in before){
                    bool move=!linesOnly||selectedPoints.Any(q=>p.Zip(q,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v));
                    double[] expected=p.Select((value,index)=>value+(move?vector[index]:0)).ToArray();
                    ParametricContext.Require(readback.Any(q=>expected.Zip(q,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v)),"SHALLOW_HEIGHT_TRANSLATION_READBACK_FAILED "+name);
                }
                model.SketchManager.InsertSketch(true);
            }
            records.Add(new{inspection="shallow_sketch_height_updated",file=Path.GetFileName(model.GetPathName()),sketch=name,heightTranslationMm=atTarget?0:delta,geometry=CaptureDerivedFailureSketch(FindFeature(model,name))});
        }

        private static void UpdateShallowVerticalSeedHeight(ISldWorks sw,ModelDoc2 model,List<object> records)
        {
            Feature feature=FindFeature(model,"草图100");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
            ParametricContext.Require(((Array)sketch.GetSketchSegments()).Length==5,"SHALLOW_VERTICAL_HEIGHT_PROFILE_DRIFT");
            MathUtility math=sw.GetMathUtility() as MathUtility;
            Func<SketchPoint,double[]> worldPoint=p=>(double[])((MathPoint)((MathPoint)math.CreatePoint(new[]{p.X,p.Y,p.Z})).MultiplyTransform(sketch.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData;
            SketchPoint[] points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();double[][] before=points.Select(worldPoint).ToArray();
            var dimensions=new List<object>();DisplayDimension display=feature.GetFirstDisplayDimension() as DisplayDimension;
            while(display!=null){Dimension d=display.GetDimension2(0);dimensions.Add(new{name=d.FullName,valueMm=d.SystemValue*1000,driven=d.DrivenState});display=feature.GetNextDisplayDimension(display) as DisplayDimension;}
            double target=(1836.4+ParametricContext.HeightMm-1917)/1000;
            bool atSource=before.Count(p=>Math.Abs(p[1]-1.8364)<.000001)==3,atTarget=before.Count(p=>Math.Abs(p[1]-target)<.000001)==3;
            records.Add(new{inspection="shallow_vertical_height_before",worldPoints=before,dimensions=dimensions,atSource=atSource,atTarget=atTarget,targetTopMm=target*1000});
            ParametricContext.Require(before.Length==6&&before.Count(p=>Math.Abs(p[1]-.0306)<.000001)==3&&(atSource||atTarget),"SHALLOW_VERTICAL_HEIGHT_POSITION_DRIFT");
            if(atSource&&!atTarget){
                model.ClearSelection2(true);ParametricContext.Require(feature.Select2(false,0),"SHALLOW_VERTICAL_HEIGHT_SKETCH_SELECT_FAILED");model.EditSketch();
                for(int i=0;i<points.Length;i++)if(Math.Abs(before[i][1]-1.8364)<.000001){
                    double[] targetWorld=(double[])before[i].Clone();targetWorld[1]=target;
                    double[] local=(double[])((MathPoint)((MathPoint)math.CreatePoint(targetWorld)).MultiplyTransform(sketch.ModelToSketchTransform)).ArrayData;
                    ParametricContext.Require(points[i].SetCoords(local[0],local[1],local[2]),"SHALLOW_VERTICAL_HEIGHT_POINT_FAILED");
                }
                model.SketchManager.InsertSketch(true);
            }
            double[][] actual=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(worldPoint).ToArray();
            records.Add(new{inspection="shallow_vertical_height_after",worldPoints=actual,targetTopMm=target*1000});
            ParametricContext.Require(actual.Length==6&&before.All(p=>actual.Any(q=>Math.Abs(q[0]-p[0])<.000001&&Math.Abs(q[2]-p[2])<.000001&&Math.Abs(q[1]-(Math.Abs(p[1]-.0306)<.000001?.0306:target))<.000001)),"SHALLOW_VERTICAL_HEIGHT_READBACK_FAILED");
        }

        private static List<double[]> ShallowTopMountCylinders(ModelDoc2 model)
        {
            var measured=new List<double[]>();
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsCylinder())continue;
                double[] cylinder=surface.CylinderParams as double[],box=face.GetBox() as double[];
                if(cylinder!=null&&box!=null&&Math.Abs(Math.Abs(cylinder[4])-1)<.000001&&
                    (Math.Abs(cylinder[6]-.0044)<.000001||Math.Abs(cylinder[6]-.0055)<.000001))
                    measured.Add(new[]{cylinder[0],cylinder[2],cylinder[6],box[1],box[4]});
            }
            return measured;
        }

        private static List<string> ShallowTopSideFeatureInventory(ModelDoc2 model)
        {
            var entries=new List<string>();Feature feature=model.FirstFeature() as Feature;
            while(feature!=null){
                entries.Add(feature.Name+"|"+feature.GetTypeName2());
                Feature child=feature.GetFirstSubFeature() as Feature;
                while(child!=null){entries.Add(feature.Name+"/"+child.Name+"|"+child.GetTypeName2());child=child.GetNextSubFeature() as Feature;}
                feature=feature.GetNextFeature() as Feature;
            }
            return entries;
        }

        private sealed class ShallowTopBlock
        {
            internal string path;
            internal double[] position,axis,normal;
            internal double scale;
        }

        private static List<ShallowTopBlock> PreserveShallowTopBlocks(ISldWorks sw,ModelDoc2 model,List<object> records,string sketchName)
        {
            Sketch sketch=FindFeature(model,sketchName).GetSpecificFeature2() as Sketch;
            MathUtility math=sw.GetMathUtility() as MathUtility;MathTransform toModel=sketch.ModelToSketchTransform.Inverse() as MathTransform;
            var blocks=new List<ShallowTopBlock>();Array instances=sketch.GetSketchBlockInstances() as Array;
            if(instances!=null)foreach(SketchBlockInstance instance in instances){
                var seed=new ShallowTopBlock{path=Path.Combine(ParametricContext.AttemptDirectory,"evidence",sketchName+"-block-"+blocks.Count+".SLDBLK"),scale=instance.Scale};
                seed.position=((MathPoint)instance.InstancePosition.MultiplyTransform(toModel)).ArrayData as double[];
                seed.axis=((MathVector)((MathVector)math.CreateVector(new[]{Math.Cos(instance.Angle),Math.Sin(instance.Angle),0.0})).MultiplyTransform(toModel)).ArrayData as double[];
                seed.normal=((MathVector)((MathVector)math.CreateVector(new[]{0.0,0.0,1.0})).MultiplyTransform(toModel)).ArrayData as double[];
                ParametricContext.Require(instance.Definition.Save(seed.path),"SHALLOW_TOP_BLOCK_PRESERVE_FAILED");blocks.Add(seed);
                records.Add(new{inspection="shallow_top_block_seed",sketch=sketchName,name=instance.Name,position=seed.position,axis=seed.axis,normal=seed.normal,scale=seed.scale,path=seed.path,sha256=ParametricContext.Hash(seed.path)});
            }
            ParametricContext.Require(blocks.Count==(sketchName=="草图3"?1:3),"SHALLOW_TOP_BLOCK_COUNT_CHANGED");
            return blocks;
        }

        private static bool CreateShallowTopMountSketch(ISldWorks sw,ModelDoc2 model,List<object> records,double y,double x,double[] rows,bool boss,List<ShallowTopBlock> blocks)
        {
            Face2 selected=null;double area=0;
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsPlane())continue;
                double[] plane=surface.PlaneParams as double[],box=face.GetBox() as double[];
                if(plane!=null&&box!=null&&Math.Abs(Math.Abs(plane[1])-1)<.000001&&Math.Abs(box[1]-y)<.000001&&Math.Abs(box[4]-y)<.000001&&
                    box[0]<x-.0055&&box[3]>x+.0055&&rows.All(z=>-z/1000>box[2]+.0055&&-z/1000<box[5]-.0055)&&face.GetArea()>area){selected=face;area=face.GetArea();}
            }
            model.ClearSelection2(true);ParametricContext.Require(selected!=null&&((Entity)selected).Select4(false,null),"SHALLOW_TOP_MOUNT_HORIZONTAL_FACE_MISSING");
            model.SketchManager.InsertSketch(true);Sketch sketch=model.SketchManager.ActiveSketch;
            ParametricContext.Require(sketch!=null,"SHALLOW_TOP_MOUNT_SKETCH_FAILED");
            MathUtility math=sw.GetMathUtility() as MathUtility;MathTransform toSketch=sketch.ModelToSketchTransform;
            MathVector normal=(MathVector)((MathVector)math.CreateVector(new[]{0.0,0.0,1.0})).MultiplyTransform(toSketch.Inverse() as MathTransform);
            double[] direction=normal.ArrayData as double[];
            ParametricContext.Require(direction!=null&&Math.Abs(Math.Abs(direction[1])-1)<.000001,"SHALLOW_TOP_MOUNT_SKETCH_NORMAL_INVALID");
            model.SketchManager.AddToDB=true;
            try{
                foreach(double z in boss?new double[0]:rows){
                    double[] p=((MathPoint)((MathPoint)math.CreatePoint(new[]{x,y,-z/1000})).MultiplyTransform(toSketch)).ArrayData as double[];
                    ParametricContext.Require(Math.Abs(p[2])<.000001,"SHALLOW_TOP_MOUNT_POINT_OFF_PLANE");
                    ParametricContext.Require(model.SketchManager.CreateCircleByRadius(p[0],p[1],0,boss?.0055:.0044)!=null,"SHALLOW_TOP_MOUNT_CIRCLE_FAILED");
                }
                foreach(ShallowTopBlock block in blocks){
                    ParametricContext.Require(block.normal.Zip(direction,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v),"SHALLOW_TOP_BLOCK_ORIENTATION_CHANGED");
                    double[] world=(double[])block.position.Clone();world[1]=y;
                    if(boss){
                        double sourceDepth=-world[2]*1000+5.5;
                        int index=Math.Abs(sourceDepth-100)<.001?0:Math.Abs(sourceDepth-200)<.001?1:Math.Abs(sourceDepth-420)<.001?2:-1;
                        ParametricContext.Require(index>=0,"SHALLOW_TOP_BOSS_BLOCK_SOURCE_POSITION_CHANGED");world[0]=x-.0055;world[2]=-rows[index]/1000+.0055;
                    }
                    double[] p=((MathPoint)((MathPoint)math.CreatePoint(world)).MultiplyTransform(toSketch)).ArrayData as double[];
                    double[] axis=((MathVector)((MathVector)math.CreateVector(block.axis)).MultiplyTransform(toSketch)).ArrayData as double[];
                    SketchBlockDefinition definition=model.SketchManager.MakeSketchBlockFromFile((MathPoint)math.CreatePoint(p),block.path,false,block.scale,Math.Atan2(axis[1],axis[0]));
                    ParametricContext.Require(definition!=null,"SHALLOW_TOP_BLOCK_RECREATE_FAILED");
                    records.Add(new{inspection="shallow_top_block_recreated",boss=boss,worldPosition=world,definitionInstances=definition.GetInstanceCount()});
                }
            }finally{model.SketchManager.AddToDB=false;}
            model.SketchManager.InsertSketch(true);
            records.Add(new{inspection="shallow_top_mount_sketch",boss=boss,planeY=y,centerX=x,depthRowsMm=rows,normal=direction});
            return boss?direction[1]>0:direction[1]<0;
        }

        private static void RebuildShallowTopSide(ISldWorks sw,ModelDoc2 model,List<object> records,double height,double depth)
        {
            ParametricContext.AssertMutation(model.GetPathName());
            List<ShallowTopBlock> cutBlocks=PreserveShallowTopBlocks(sw,model,records,"草图3"),bossBlocks=PreserveShallowTopBlocks(sw,model,records,"草图4");
            records.Add(new{inspection="shallow_top_mount_features_before",features=ShallowTopSideFeatureInventory(model)});
            foreach(string name in new[]{"凸台-拉伸2","切除-拉伸2"}){
                Feature original=FindFeature(model,name);
                ParametricContext.Require(original!=null,"SHALLOW_TOP_MOUNT_SOURCE_FEATURE_MISSING "+name);
                model.ClearSelection2(true);ParametricContext.Require(original.Select2(false,0)&&model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed),"SHALLOW_TOP_MOUNT_FEATURE_DELETE_FAILED "+name);
                records.Add(new{inspection="shallow_top_mount_features_deleted",name=name,features=ShallowTopSideFeatureInventory(model)});
            }
            ParametricContext.Require(model.ForceRebuild3(false),"SHALLOW_TOP_MOUNT_BASE_REBUILD_FAILED");
            double x=-(ParametricContext.WidthMm/2-25)/1000;
            double[] rows=new[]{100.0,100+Math.Min(100,(depth-150)/2),depth-50};
            bool reverse=CreateShallowTopMountSketch(sw,model,records,height/1000,x,rows,false,cutBlocks);
            Feature cut=model.FeatureManager.FeatureCut3(true,false,reverse,0,0,.005,.005,false,false,false,false,0,0,false,false,false,false,true,true,true,false,false,false,0,0,false);
            ParametricContext.Require(cut!=null,"SHALLOW_TOP_MOUNT_CUT_FAILED");cut.Name="切除-拉伸2";
            reverse=CreateShallowTopMountSketch(sw,model,records,(height-.8)/1000,x,rows,true,bossBlocks);
            Feature extrusion=model.FeatureManager.FeatureExtrusion2(true,false,reverse,0,0,.004,.004,false,false,false,false,0,0,false,false,false,false,true,true,true,0,0,false);
            ParametricContext.Require(extrusion!=null,"SHALLOW_TOP_MOUNT_BOSS_FAILED");extrusion.Name="凸台-拉伸2";
            bool rebuilt=model.ForceRebuild3(false);List<double[]> measured=ShallowTopMountCylinders(model);
            records.Add(new{inspection="shallow_top_mount_geometry",rebuilt=rebuilt,cylinders=measured,features=ShallowTopSideFeatureInventory(model),snapshot=CapturePartSnapshot(model)});
            foreach(double z in rows)foreach(double radius in new[]{.0044,.0055}){
                double ymin=(height-(radius==.0055?4.8:.8))/1000,ymax=(height-(radius==.0055?.8:0))/1000;
                ParametricContext.Require(measured.Any(p=>Math.Abs(p[0]-x)<.000001&&Math.Abs(p[1]+z/1000)<.000001&&Math.Abs(p[2]-radius)<.000001&&Math.Abs(p[3]-ymin)<.000001&&Math.Abs(p[4]-ymax)<.000001),"SHALLOW_TOP_MOUNT_CYLINDER_MISMATCH");
            }
            VerifyShallowTopSideFlat(model,records);
        }

        private static void VerifyShallowTopSideFlat(ModelDoc2 model,List<object> records)
        {
            Feature flat=model.FirstFeature() as Feature;
            while(flat!=null&&flat.GetTypeName2()!="FlatPattern")flat=flat.GetNextFeature() as Feature;
            ParametricContext.Require(flat!=null,"SHALLOW_TOP_SIDE_FLAT_SOURCE_CHANGED");
            string flatName=flat.Name;bool suppressed=flat.IsSuppressed();
            bool opened=!suppressed||flat.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null);
            bool rebuilt=model.ForceRebuild3(false);PartSnapshot snapshot=CapturePartSnapshot(model);
            records.Add(new{inspection="shallow_top_side_flat",file=Path.GetFileName(model.GetPathName()),opened=opened,rebuilt=rebuilt,snapshot=snapshot});
            if(suppressed){flat=FindFeature(model,flatName);ParametricContext.Require(flat.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null)&&model.ForceRebuild3(false),"SHALLOW_TOP_SIDE_FOLD_RESTORE_FAILED");}
            ParametricContext.Require(opened&&rebuilt&&PartHealthGate(snapshot,true),"SHALLOW_TOP_SIDE_FLAT_FAILED");
        }

        private static void PrepareShallowBodyMountSketch(ISldWorks sw,string folder,string name,List<object> records,double height,double depth,PartSnapshot baseline)
        {
            ModelDoc2 model=NativeParametricAssembly.Open(sw,ExactFile(folder,name),false);
            try{
                int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
                bool shelf=name=="箱体横层板L.sldprt";string sketchName=shelf?"草图17":"草图19";
                Feature feature=FindFeature(model,sketchName);Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                double[][] coordinates=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                Array relations=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                ParametricContext.Require(relations!=null&&relations.Length==(shelf?5:2),"SHALLOW_BODY_MOUNT_REFERENCE_DRIFT "+name);
                if(!shelf&&depth>=450){
                    object files,parts,features,types,statuses,entities,components;int option;string config;
                    feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out option,out config);
                    Array sources=entities as Array;
                    ParametricContext.Require(name=="箱体右侧板.sldprt"&&sources!=null&&sources.Length==2&&sources.Cast<object>().All(item=>Convert.ToString(item).Contains("衣杆固定支架")),"HEIGHT_SIDE_MOUNT_REFERENCE_SOURCE_DRIFT");
                }
                foreach(SketchRelation relation in relations)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_BODY_MOUNT_DETACH_FAILED");
                ParametricContext.Require(sketch.RelationManager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal)==0,"SIDE_MOUNT_REFERENCE_DETACH_INCOMPLETE "+name);
                model.ClearSelection2(true);feature.Select2(false,0);model.EditSketch();
                SketchPoint[] points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
                ParametricContext.Require(points.Length==coordinates.Length,"SHALLOW_BODY_MOUNT_POINT_COUNT_CHANGED");
                double front=ShallowShelfFront(depth),back=ShallowShelfBack(depth),pitch=back-front;
                for(int i=0;i<points.Length;i++){
                    double[] p=(double[])coordinates[i].Clone();
                    if(shelf){double oldCenter=p[1]<-.2735?398.5:148.5;double targetCenter=oldCenter==398.5?back:front;p[1]+=(oldCenter-targetCenter)/1000;}
                    else if(depth<450){p[0]+=(name=="箱体右侧板.sldprt"?-1:1)*(550-depth)/2000;}
                    ParametricContext.Require(points[i].SetCoords(p[0],p[1],p[2]),"SHALLOW_BODY_MOUNT_POINT_MOVE_FAILED");
                }
                model.SketchManager.InsertSketch(true);bool rebuilt=model.ForceRebuild3(false);PartSnapshot after=CapturePartSnapshot(model);
                records.Add(new{inspection="shallow_body_mount_prepared",file=name,sketch=sketchName,removedReferences=relations.Length,centerDepthMm=(front+back)/2,frontDepthMm=front,backDepthMm=back,rowPitchMm=shelf?(double?)pitch:null,geometry=CaptureDerivedFailureSketch(FindFeature(model,sketchName)),snapshot=after,rebuilt=rebuilt});
                ParametricContext.Require(rebuilt&&DerivedPartHealthGate(after,baseline),"SHALLOW_BODY_MOUNT_PREPARE_FAILED "+name);
                if(depth>=450)ParametricContext.Require(after.part_box_m.Zip(baseline.part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v),"HEIGHT_SIDE_MOUNT_DETACH_CHANGED_BASELINE");
                NativeParametricAssembly.Save(model);
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }

        private static void PrepareShallowBrace(ISldWorks sw,string folder,List<object> records,double depth,PartSnapshot baseline)
        {
            double front=ShallowShelfFront(depth),rear=ShallowShelfBack(depth),length=rear-front+50;
            ModelDoc2 partition=NativeParametricAssembly.Open(sw,ExactFile(folder,"箱体竖隔板L.sldprt"),false);
            try{
                int error=0;sw.ActivateDoc3(partition.GetTitle(),false,0,ref error);
                PartSnapshot before=CapturePartSnapshot(partition);
                Feature feature=FindFeature(partition,"草图9");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                double[][] original=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                Array relations=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                ParametricContext.Require(relations!=null&&relations.Length==2,"SHALLOW_PARTITION_LIP_REFERENCE_DRIFT");
                foreach(SketchRelation relation in relations)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_PARTITION_LIP_DETACH_FAILED");
                partition.ClearSelection2(true);feature.Select2(false,0);partition.EditSketch();
                SketchPoint[] points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
                ParametricContext.Require(points.Length==original.Length,"SHALLOW_PARTITION_LIP_POINT_COUNT_CHANGED");
                for(int i=0;i<points.Length;i++){
                    double[] p=(double[])original[i].Clone();
                    ParametricContext.Require(Math.Abs(p[0]*1000+138.5)<.001||Math.Abs(p[0]*1000+408.5)<.001,"SHALLOW_PARTITION_LIP_SOURCE_POSITION_DRIFT");
                    p[0]=-(Math.Abs(p[0]*1000+138.5)<.001?front+15:rear-15)/1000;
                    ParametricContext.Require(points[i].SetCoords(p[0],p[1],p[2]),"SHALLOW_PARTITION_LIP_POINT_MOVE_FAILED");
                }
                partition.SketchManager.InsertSketch(true);
                Dimension datum=partition.Parameter("D4@草图19") as Dimension;
                ParametricContext.Require(datum!=null&&Math.Abs(datum.SystemValue*1000-240)<.001,"SHALLOW_PARTITION_MOUNT_SOURCE_DRIFT");
                datum.SystemValue=(240+(depth-550)/2)/1000;
                bool rebuilt=partition.ForceRebuild3(false);PartSnapshot after=CapturePartSnapshot(partition);
                records.Add(new{inspection="shallow_partition_mount_driver",name="D4@草图19",targetMm=240+(depth-550)/2,geometry=CaptureDerivedFailureSketch(FindFeature(partition,"草图19")),lip=CaptureDerivedFailureSketch(FindFeature(partition,"草图9")),snapshot=after,rebuilt=rebuilt});
                ParametricContext.Require(rebuilt&&DerivedPartHealthGate(after,before),"SHALLOW_PARTITION_LIP_PREPARE_FAILED");
                NativeParametricAssembly.Save(partition);
            }finally{sw.CloseDoc(partition.GetTitle());Marshal.ReleaseComObject(partition);}
            ModelDoc2 model=NativeParametricAssembly.Open(sw,ExactFile(folder,"标准寄存柜 模型.SLDPRT"),false);
            try{
                int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
                foreach(string name in new[]{"草图118","草图105"}){
                    Feature feature=FindFeature(model,name);Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                    double[][] original=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                    Array relations=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                    if(relations!=null)foreach(SketchRelation relation in relations)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_BRACE_REFERENCE_DETACH_FAILED");
                    model.ClearSelection2(true);feature.Select2(false,0);model.EditSketch();
                    SketchPoint[] points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
                    ParametricContext.Require(points.Length==original.Length,"SHALLOW_BRACE_POINT_COUNT_CHANGED");
                    for(int i=0;i<points.Length;i++){
                        double[] p=(double[])original[i].Clone();
                        if(name=="草图118"){
                            if(Math.Abs(p[1]*1000-123.5)<.001)p[1]=front/1000;
                            else if(Math.Abs(p[1]*1000-423.5)<.001)p[1]=rear/1000;
                        }else p[0]+=(p[0]<-.2735?423.5-rear:123.5-front)/1000;
                        ParametricContext.Require(points[i].SetCoords(p[0],p[1],p[2]),"SHALLOW_BRACE_POINT_MOVE_FAILED");
                    }
                    model.SketchManager.InsertSketch(true);
                    records.Add(new{inspection="shallow_brace_sketch_prepared",name=name,removedReferences=relations==null?0:relations.Length,lengthMm=length,centerDepthMm=(front+rear)/2,frontDepthMm=front,rearDepthMm=rear,geometry=CaptureDerivedFailureSketch(FindFeature(model,name))});
                }
                double rowPitch=ShallowShelfPitch(depth);
                foreach(string name in new[]{"草图106"}){
                    Feature tabFeature=FindFeature(model,name);Sketch tabSketch=tabFeature.GetSpecificFeature2() as Sketch;
                    double[][] tabCoordinates=((Array)tabSketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                    var relations=new List<object>();Array all=tabSketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll) as Array;
                    if(all!=null)foreach(SketchRelation relation in all){
                        Array entities=relation.GetEntities() as Array;
                        relations.Add(new{type=relation.GetRelationType(),entities=entities==null?new object[0]:entities.Cast<object>().Select(entity=>{
                            SketchPoint point=entity as SketchPoint;if(point!=null)return (object)new{point=new[]{point.X,point.Y,point.Z}};
                            SketchLine line=entity as SketchLine;if(line!=null){SketchPoint a=line.GetStartPoint2() as SketchPoint,b=line.GetEndPoint2() as SketchPoint;return (object)new{start=new[]{a.X,a.Y,a.Z},end=new[]{b.X,b.Y,b.Z}};}
                            return (object)new{kind=entity==null?null:entity.GetType().FullName};
                        }).ToArray()});
                    }
                    records.Add(new{inspection="shallow_shelf_tab_relations",name=name,relations=relations});
                    Array tabReferences=tabSketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                    if(tabReferences!=null)foreach(SketchRelation relation in tabReferences)ParametricContext.Require(tabSketch.RelationManager.DeleteRelation(relation),"SHALLOW_SHELF_TAB_DETACH_FAILED");
                    model.ClearSelection2(true);tabFeature.Select2(false,0);model.EditSketch();
                    foreach(double oldCenter in new[]{148.5,398.5}){
                        double newCenter=oldCenter==398.5?ShallowShelfBack(depth):ShallowShelfFront(depth);
                        model.ClearSelection2(true);int selected=0;
                        foreach(SketchSegment segment in (Array)tabSketch.GetSketchSegments()){
                            SketchLine line=segment as SketchLine;ParametricContext.Require(line!=null,"SHALLOW_SHELF_TAB_SOURCE_TYPE_DRIFT");
                            SketchPoint a=line.GetStartPoint2() as SketchPoint,b=line.GetEndPoint2() as SketchPoint;
                            if(Math.Abs(-a.X*1000-oldCenter)<=8.001&&Math.Abs(-b.X*1000-oldCenter)<=8.001){ParametricContext.Require(segment.Select4(true,null),"SHALLOW_SHELF_TAB_SELECT_FAILED");selected++;}
                        }
                        ParametricContext.Require(selected==(name=="草图106"?5:4),"SHALLOW_SHELF_TAB_GROUP_COUNT_CHANGED");
                        model.Extension.MoveOrCopy(false,0,true,0,0,0,(oldCenter-newCenter)/1000,0,0);
                        records.Add(new{inspection="shallow_shelf_tab_group_moved",name=name,oldCenterMm=oldCenter,targetCenterMm=newCenter,selected=selected,geometry=CaptureDerivedFailureSketch(tabFeature)});
                        SketchPoint[] readback=((Array)tabSketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
                        ParametricContext.Require(tabCoordinates.Where(p=>Math.Abs(-p[0]*1000-oldCenter)<=8.001).All(p=>readback.Any(actual=>Math.Abs(actual.X-(p[0]+(oldCenter-newCenter)/1000))<.000001&&Math.Abs(actual.Y-p[1])<.000001&&Math.Abs(actual.Z-p[2])<.000001)),"SHALLOW_SHELF_TAB_TRANSLATION_READBACK_FAILED");
                    }
                    model.SketchManager.InsertSketch(true);
                    records.Add(new{inspection="shallow_shelf_tabs_prepared",name=name,removedReferences=tabReferences==null?0:tabReferences.Length,rowPitchMm=rowPitch,geometry=CaptureDerivedFailureSketch(FindFeature(model,name))});
                }
                bool rebuilt=model.ForceRebuild3(false);PartSnapshot after=CapturePartSnapshot(model);
                records.Add(new{inspection="shallow_shelf_tabs_rebuilt",left=CaptureDerivedFailureSketch(FindFeature(model,"草图106")),right=CaptureDerivedFailureSketch(FindFeature(model,"草图107"))});
                records.Add(new{inspection="shallow_brace_prepared",rebuilt=rebuilt,snapshot=after});
                ParametricContext.Require(rebuilt&&DerivedPartHealthGate(after,baseline),"SHALLOW_BRACE_PREPARE_FAILED");
                NativeParametricAssembly.Save(model);
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }

        private static void CaptureShallowDeleteFaces(ModelDoc2 model,List<object> records,string stage)
        {
            Feature feature=FindFeature(model,"删除面1");if(feature==null)return;
            DeleteFaceFeatureData definition=feature.GetDefinition() as DeleteFaceFeatureData;
            if(definition==null)return;
            var faces=new List<object>();bool accessed=definition.AccessSelections(model,null);
            try{
                Array selected=definition.GetDeletedFaces() as Array;
                if(selected!=null)foreach(object item in selected){
                    Face2 face=item as Face2;if(face==null){faces.Add(new{missing=true});continue;}
                    Surface surface=face.GetSurface() as Surface;
                    if(surface==null){faces.Add(new{missing=true});continue;}
                    faces.Add(new{box=face.GetBox(),area=face.GetArea(),plane=surface.IsPlane()?surface.PlaneParams:null,cylinder=surface.IsCylinder()?surface.CylinderParams:null});
                }
                records.Add(new{inspection="delete_face_references",stage=stage,file=Path.GetFileName(model.GetPathName()),accessed=accessed,options=definition.Options,count=definition.GetDeletedFacesCount(),faces=faces});
            }finally{if(accessed)definition.ReleaseSelectionAccess();}
        }

        private static object ShallowShelfReferenceGeometry(object item)
        {
            if(item==null)return new{missing=true};
            Edge edge=item as Edge;
            if(edge!=null){
                Curve curve=edge.GetCurve() as Curve;var range=edge.GetCurveParams3();
                Vertex start=edge.GetStartVertex() as Vertex,end=edge.GetEndVertex() as Vertex;
                var faces=new List<object>();Array adjacent=edge.GetTwoAdjacentFaces2() as Array;
                if(adjacent!=null)foreach(Face2 face in adjacent){
                    if(face==null){faces.Add(new{missing=true});continue;}
                    Surface surface=face.GetSurface() as Surface;Feature owner=face.GetFeature() as Feature;
                    faces.Add(new{owner=owner==null?null:owner.Name,box=face.GetBox(),plane=surface!=null&&surface.IsPlane()?surface.PlaneParams:null,cylinder=surface!=null&&surface.IsCylinder()?surface.CylinderParams:null});
                }
                return new{kind="edge",line=curve!=null&&curve.IsLine(),circle=curve!=null&&curve.IsCircle(),circleParams=curve!=null&&curve.IsCircle()?curve.CircleParams:null,start=start==null?null:start.GetPoint(),end=end==null?null:end.GetPoint(),length=curve==null||range==null?(double?)null:curve.GetLength3(range.UMinValue,range.UMaxValue),faces=faces};
            }
            Feature feature=item as Feature;if(feature!=null)return new{kind="feature",name=feature.Name,type=feature.GetTypeName2(),error=feature.GetErrorCode()};
            Face2 selected=item as Face2;if(selected!=null)return new{kind="face",box=selected.GetBox()};
            return new{kind=item.GetType().FullName};
        }

        private static object[] ShallowShelfReferences(object items)
        {
            Array array=items as Array;return array==null?new object[0]:array.Cast<object>().Select(ShallowShelfReferenceGeometry).ToArray();
        }

        private static void CaptureShallowShelfDefinitions(ModelDoc2 model,List<object> records,string stage)
        {
            foreach(string name in new[]{"圆角1","倒角1","阵列(线性)1","开口卡模具1","开口卡模具2"}){
                Feature feature=FindFeature(model,name);if(feature==null)continue;
                var dimensions=new List<object>();DisplayDimension dimension=feature.GetFirstDisplayDimension() as DisplayDimension;
                while(dimension!=null){Dimension value=dimension.GetDimension2(0);dimensions.Add(new{name=value.FullName,valueMm=value.SystemValue*1000});dimension=feature.GetNextDisplayDimension(dimension) as DisplayDimension;}
                object definition=feature.GetDefinition();object data=null;
                SimpleFilletFeatureData2 fillet=definition as SimpleFilletFeatureData2;
                ChamferFeatureData2 chamfer=definition as ChamferFeatureData2;
                LinearPatternFeatureData pattern=definition as LinearPatternFeatureData;
                if(fillet!=null){
                    bool accessed=fillet.AccessSelections(model,null);
                    try{data=new{accessed=accessed,type=fillet.Type,radius=fillet.DefaultRadius,items=fillet.FilletItemsCount,edges=ShallowShelfReferences(fillet.Edges),features=ShallowShelfReferences(fillet.Features),faces=ShallowShelfReferences(fillet.GetFaces(0))};}
                    finally{fillet.ReleaseSelectionAccess();}
                }else if(chamfer!=null){
                    bool accessed=chamfer.AccessSelections(model,null);
                    try{data=new{accessed=accessed,type=chamfer.Type,angle=chamfer.EdgeChamferAngle,equal=chamfer.EqualDistance,keepFeatures=chamfer.KeepFeatures,tangentPropagation=chamfer.TangentPropagation,edges=ShallowShelfReferences(chamfer.Edges),faces=ShallowShelfReferences(chamfer.Faces)};}
                    finally{chamfer.ReleaseSelectionAccess();}
                }else if(pattern!=null){
                    bool accessed=pattern.AccessSelections(model,null);
                    try{data=new{accessed=accessed,axis1=ShallowShelfReferenceGeometry(pattern.D1Axis),axis2=ShallowShelfReferenceGeometry(pattern.D2Axis),spacing1=pattern.D1Spacing,spacing2=pattern.D2Spacing,count1=pattern.D1TotalInstances,count2=pattern.D2TotalInstances,reverse1=pattern.D1ReverseDirection,reverse2=pattern.D2ReverseDirection,geometryPattern=pattern.GeometryPattern,features=ShallowShelfReferences(pattern.PatternFeatureArray),faces=ShallowShelfReferences(pattern.PatternFaceArray),bodies=ShallowShelfReferences(pattern.PatternBodyArray)};}
                    finally{pattern.ReleaseSelectionAccess();}
                }
                var sketches=new List<object>();Feature child=feature.GetFirstSubFeature() as Feature;
                while(child!=null){
                    if(child.GetTypeName2()=="ProfileFeature"){
                        var dims=new List<object>();DisplayDimension sd=child.GetFirstDisplayDimension() as DisplayDimension;
                        while(sd!=null){Dimension value=sd.GetDimension2(0);dims.Add(new{name=value.FullName,valueMm=value.SystemValue*1000});sd=child.GetNextDisplayDimension(sd) as DisplayDimension;}
                        Sketch sketch=child.GetSpecificFeature2() as Sketch;
                        Array points=sketch.GetSketchPoints2() as Array;
                        var relations=new List<object>();Array all=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll) as Array;
                        if(all!=null)foreach(SketchRelation relation in all)relations.Add(new{type=relation.GetRelationType()});
                        sketches.Add(new{name=child.Name,geometry=CaptureDerivedFailureSketch(child),dimensions=dims,points=points==null?new double[0][]:points.Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),userPoints=sketch.GetUserPoints2(),relations=relations});
                    }
                    child=child.GetNextSubFeature() as Feature;
                }
                Array outputFaces=feature.GetFaces() as Array;
                records.Add(new{inspection="shallow_shelf_definition",stage=stage,name=name,error=feature.GetErrorCode(),type=feature.GetTypeName2(),dimensions=dimensions,definition=data,sketches=sketches,outputFaceBoxes=outputFaces==null?new object[0]:outputFaces.Cast<Face2>().Select(f=>f.GetBox()).ToArray()});
            }
        }
        private static void CaptureMasterHeightLinks(ModelDoc2 model,List<object> records,string stage,string[] requestedNames=null)
        {
            string[] names=requestedNames??new[]{"阵列(线性)6","草图91","切除-拉伸44","实体-删除1","草图105","草图106","草图107","草图111","凸台-拉伸34","保存门框"};
            var paths=names.ToDictionary(name=>name,name=>new List<string>());
            var nodes=new Dictionary<string,object>();var errors=new List<object>();int visited=0;bool traversalComplete=true;
            Func<string,Func<object>,object> read=delegate(string field,Func<object> action){try{return action();}catch(Exception error){errors.Add(new{field=field,error=error.Message});return null;}};
            Func<SketchPoint,double[]> pointMm=point=>point==null?null:new[]{point.X*1000,point.Y*1000,point.Z*1000};
            Func<SketchSegment,int,string,object> segmentData=delegate(SketchSegment segment,int index,string field){
                if(segment==null)return new{index=index,missing=true};
                SketchPoint start=null,end=null,center=null;
                try{
                    SketchLine line=segment as SketchLine;SketchArc arc=segment as SketchArc;
                    if(line!=null){start=read(field+".start",delegate{return line.IGetStartPoint2();}) as SketchPoint;end=read(field+".end",delegate{return line.IGetEndPoint2();}) as SketchPoint;}
                    else if(arc!=null){start=read(field+".start",delegate{return arc.GetStartPoint2();}) as SketchPoint;end=read(field+".end",delegate{return arc.GetEndPoint2();}) as SketchPoint;center=read(field+".center",delegate{return arc.GetCenterPoint2();}) as SketchPoint;}
                    return new{index=index,type=read(field+".type",delegate{return segment.GetType();}),construction=read(field+".construction",delegate{return segment.ConstructionGeometry;}),kind=line!=null?"line":arc!=null?"arc":"other",startMm=read(field+".startMm",delegate{return pointMm(start);}),endMm=read(field+".endMm",delegate{return pointMm(end);}),centerMm=read(field+".centerMm",delegate{return pointMm(center);}),radiusMm=arc==null?null:read(field+".radiusMm",delegate{return arc.GetRadius()*1000;})};
                }finally{Release(start);Release(end);Release(center);}
            };
            Action<Feature,string,int> visit=null;
            visit=delegate(Feature feature,string path,int level){
                if(++visited>5000||level>12){traversalComplete=false;return;}
                try{
                    string name=feature.Name,type=feature.GetTypeName2();
                    if(paths.ContainsKey(name)){
                        paths[name].Add(path);
                        if(!nodes.ContainsKey(name)){
                            int error=feature.GetErrorCode();bool suppressed=feature.IsSuppressed();var dimensions=new List<object>();
                            DisplayDimension display=read(path+".dimensions",delegate{return feature.GetFirstDisplayDimension();}) as DisplayDimension;
                            while(display!=null){
                                DisplayDimension current=display;Dimension value=null;
                                try{value=current.GetDimension2(0);dimensions.Add(new{name=read(path+".dimension.name",delegate{return value.FullName;}),systemValue=read(path+".dimension.value",delegate{return value.SystemValue;}),drivenState=read(path+".dimension.drivenState",delegate{return value.DrivenState;})});}
                                catch(Exception failure){errors.Add(new{field=path+".dimension",error=failure.Message});}
                                finally{display=read(path+".dimension.next",delegate{return feature.GetNextDisplayDimension(current);}) as DisplayDimension;Release(value);Release(current);}
                            }
                            object definition=null,sketchData=null;
                            if(type=="LPattern"){
                                LinearPatternFeatureData pattern=read(path+".definition",delegate{return feature.GetDefinition();}) as LinearPatternFeatureData;
                                bool accessed=false,released=false;object parameters=null;
                                if(pattern!=null){
                                    try{
                                        accessed=pattern.AccessSelections(model,null);
                                        if(accessed)parameters=new{axis1=read(path+".axis1",delegate{return ShallowShelfReferenceGeometry(pattern.D1Axis);}),axis2=read(path+".axis2",delegate{return ShallowShelfReferenceGeometry(pattern.D2Axis);}),spacing1Mm=read(path+".spacing1",delegate{return pattern.D1Spacing*1000;}),spacing2Mm=read(path+".spacing2",delegate{return pattern.D2Spacing*1000;}),count1=read(path+".count1",delegate{return pattern.D1TotalInstances;}),count2=read(path+".count2",delegate{return pattern.D2TotalInstances;}),reverse1=read(path+".reverse1",delegate{return pattern.D1ReverseDirection;}),reverse2=read(path+".reverse2",delegate{return pattern.D2ReverseDirection;}),geometryPattern=read(path+".geometryPattern",delegate{return pattern.GeometryPattern;}),features=read(path+".seeds.features",delegate{return ShallowShelfReferences(pattern.PatternFeatureArray);}),faces=read(path+".seeds.faces",delegate{return ShallowShelfReferences(pattern.PatternFaceArray);}),bodies=read(path+".seeds.bodies",delegate{return ShallowShelfReferences(pattern.PatternBodyArray);})};
                                    }catch(Exception failure){errors.Add(new{field=path+".definition.access",error=failure.Message});}
                                    finally{if(accessed){try{pattern.ReleaseSelectionAccess();released=true;}catch(Exception failure){errors.Add(new{field=path+".definition.release",error=failure.Message});}}Release(pattern);}
                                }
                                definition=new{available=pattern!=null,accessed=accessed,selectionAccessReleased=released,parameters=parameters,referenceGeometryLengthUnit="m"};
                            }
                            if(requestedNames!=null&&(type=="ICE"||type=="Cut")){
                                ExtrudeFeatureData2 extrusion=read(path+".definition",delegate{return feature.GetDefinition();}) as ExtrudeFeatureData2;
                                bool accessed=false;object parameters=null;
                                if(extrusion!=null){
                                    try{accessed=extrusion.AccessSelections(model,null);if(accessed)parameters=new{reverse=extrusion.ReverseDirection,both=extrusion.BothDirections,end1=extrusion.GetEndCondition(true),depth1=extrusion.GetDepth(true),end2=extrusion.GetEndCondition(false),depth2=extrusion.GetDepth(false),from=extrusion.FromType,fromOffset=extrusion.FromOffsetDistance,featureScope=extrusion.FeatureScope,autoSelect=extrusion.AutoSelect,bodyScopes=ShallowShelfReferences(extrusion.FeatureScopeBodies)};}
                                    catch(Exception failure){errors.Add(new{field=path+".definition.access",error=failure.Message});}
                                    finally{if(accessed)extrusion.ReleaseSelectionAccess();Release(extrusion);}
                                }
                                definition=new{available=extrusion!=null,accessed=accessed,parameters=parameters};
                            }
                            if(type=="ProfileFeature"){
                                Sketch sketch=read(path+".sketch",delegate{return feature.GetSpecificFeature2();}) as Sketch;
                                if(sketch!=null){
                                    try{
                                        Array segments=read(path+".segments",delegate{return sketch.GetSketchSegments();}) as Array;var allSegments=new List<object>();int index=0;
                                        if(segments!=null)foreach(object item in segments){SketchSegment segment=item as SketchSegment;try{allSegments.Add(segmentData(segment,index,path+".segment["+index+"]"));}finally{Release(item);}index++;}
                                        var relations=new List<object>();SketchRelationManager manager=sketch.RelationManager;
                                        try{
                                            foreach(swSketchRelationFilterType_e filter in new[]{swSketchRelationFilterType_e.swDangling,swSketchRelationFilterType_e.swBroken,swSketchRelationFilterType_e.swOverDefining,swSketchRelationFilterType_e.swExternal}){
                                                Array selected=read(path+".relations."+filter,delegate{return manager.GetRelations((int)filter);}) as Array;var entries=new List<object>();int relationIndex=0;
                                                if(selected!=null)foreach(SketchRelation relation in selected){
                                                    try{
                                                        string field=path+".relations."+filter+"["+relationIndex+"]";Array linked=read(field+".entities",delegate{return relation.GetEntities();}) as Array;var entities=new List<object>();int entityIndex=0;
                                                        if(linked!=null)foreach(object entity in linked){
                                                            try{SketchPoint point=entity as SketchPoint;SketchSegment segment=entity as SketchSegment;entities.Add(point!=null?(object)new{index=entityIndex,kind="sketch_point",pointMm=read(field+".point["+entityIndex+"]",delegate{return pointMm(point);})}:segment!=null?segmentData(segment,entityIndex,field+".segment["+entityIndex+"]"):read(field+".entity["+entityIndex+"]",delegate{return ShallowShelfReferenceGeometry(entity);}));}
                                                            finally{Release(entity);}entityIndex++;
                                                        }
                                                        entries.Add(new{index=relationIndex,type=read(field+".type",delegate{return relation.GetRelationType();}),entityCount=linked==null?(int?)null:linked.Length,entities=entities});
                                                    }finally{Release(relation);}relationIndex++;
                                                }
                                                relations.Add(new{filter=filter.ToString(),available=selected!=null,count=selected==null?(int?)null:selected.Length,entries=entries});
                                            }
                                        }finally{Release(manager);}
                                        MathTransform transform=read(path+".transform",delegate{return sketch.ModelToSketchTransform;}) as MathTransform;object transformData=null;
                                        try{if(transform!=null)transformData=read(path+".transform.data",delegate{return transform.ArrayData;});}finally{Release(transform);}
                                        sketchData=new{coordinateSystem="sketch_local",lengthUnit="mm",modelToSketchTransform=transformData,segmentCount=segments==null?(int?)null:segments.Length,recordedSegmentCount=allSegments.Count,segments=allSegments,relations=relations};
                                    }finally{Release(sketch);}
                                }
                            }
                            nodes.Add(name,new{name=name,path=path,type=type,error=error,suppressed=suppressed,dimensions=dimensions,definition=definition,sketch=sketchData});
                        }
                    }
                    Feature child=feature.GetFirstSubFeature() as Feature;int childIndex=0;
                    while(child!=null&&visited<=5000){Feature current=child;try{visit(current,path+"/"+name+"["+childIndex+"]",level+1);}finally{child=current.GetNextSubFeature() as Feature;Release(current);}childIndex++;}
                }catch(Exception failure){traversalComplete=false;errors.Add(new{field=path,error=failure.Message});}
            };
            object snapshot=read("snapshot",delegate{return CapturePartSnapshot(model);});Feature root=null;
            try{root=model.FirstFeature() as Feature;int index=0;while(root!=null&&visited<=5000){Feature current=root;try{visit(current,"root["+index+"]",0);}finally{root=current.GetNextFeature() as Feature;Release(current);}index++;}}
            catch(Exception failure){traversalComplete=false;errors.Add(new{field="root",error=failure.Message});}
            finally{Release(root);}
            records.Add(new{inspection="master_height_links",stage=stage,file=Path.GetFileName(model.GetPathName()),targetHeightMm=ParametricContext.HeightMm,targetDepthMm=ParametricContext.DepthMm,snapshot=snapshot,traversalComplete=traversalComplete,matchedPaths=paths,missingNames=names.Where(name=>!nodes.ContainsKey(name)).ToArray(),features=names.Where(nodes.ContainsKey).Select(name=>nodes[name]).ToArray(),readErrors=errors});
        }
        internal static double FrameGridHeightTranslation(double height)
        {
            return height-1917+152.5-(height-87)/12;
        }

        private static void UpdateRightRearHolePatternHeight(ModelDoc2 model,List<object> records,double height)
        {
            Dimension offset=model.Parameter("D3@草图18") as Dimension,pitch=model.Parameter("D3@阵列(线性)1") as Dimension,count=model.Parameter("D1@阵列(线性)1") as Dimension;
            ParametricContext.Require(offset!=null&&pitch!=null&&count!=null&&offset.DrivenState==2&&pitch.DrivenState==2&&Math.Abs(offset.SystemValue*1000-870)<.001&&Math.Abs(pitch.SystemValue*1000-290)<.001&&Math.Abs(count.SystemValue-7)<.000001,"RIGHT_REAR_HOLE_PATTERN_SOURCE_DRIFT");
            // Seven paired rows retain the original 30.9 mm end margins on the rear flange.
            offset.SystemValue=(height-177)/2000;pitch.SystemValue=(height-177)/6000;
            records.Add(new{inspection="right_rear_hole_height_driver",offsetMm=offset.SystemValue*1000,pitchMm=pitch.SystemValue*1000,count=count.SystemValue});
        }

        private static void RefreshPartAssemblyContext(ISldWorks sw,ModelDoc2 model,List<object> records,string folder,string sketchName,out ModelDoc2 contextRoot)
        {
            ModelDoc2 root=NativeParametricAssembly.Open(sw,ExactFile(folder,"标准寄存柜1917×1000×550(总装配).SLDASM"),false);
            contextRoot=root;
            AssemblyDoc assembly=root as AssemblyDoc;bool editing=false;
            try{
                Component2[] components=((Array)assembly.GetComponents(false)).Cast<Component2>().Where(c=>c.GetPathName().Equals(model.GetPathName(),StringComparison.OrdinalIgnoreCase)).ToArray();
                ParametricContext.Require(components.Length==1,"ENVELOPE_CONTEXT_COMPONENT_NOT_UNIQUE "+Path.GetFileName(model.GetPathName()));
                foreach(Component2 c in (Array)assembly.GetComponents(false))if(!string.IsNullOrEmpty(c.GetPathName()))ParametricContext.CheckPath(c.GetPathName(),folder);
                int activationError=0;sw.ActivateDoc3(root.GetTitle(),false,0,ref activationError);root.ClearSelection2(true);
                int information=0,status=-1;bool selected=components[0].Select4(false,null,false);
                if(selected){status=assembly.EditPart2(true,false,ref information);Component2 target=assembly.GetEditTargetComponent();editing=target!=null&&target.GetPathName().Equals(model.GetPathName(),StringComparison.OrdinalIgnoreCase);}
                bool rebuilt=editing&&model.ForceRebuild3(false);
                records.Add(new{inspection="envelope_part_context_refresh",file=Path.GetFileName(model.GetPathName()),rootPath=root.GetPathName(),componentPath=components[0].GetPathName(),selected=selected,status=status,information=information,editing=editing,rebuilt=rebuilt,geometry=CaptureDerivedFailureSketch(FindFeature(model,sketchName))});
                ParametricContext.Require(selected&&status==0&&editing,"ENVELOPE_PART_CONTEXT_FAILED "+Path.GetFileName(model.GetPathName()));
            }finally{assembly.EditAssembly();}
            int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
        }

        private static void UpdateLeftRearHolePatternHeight(ISldWorks sw,ModelDoc2 model,List<object> records,double height,string folder,out ModelDoc2 contextRoot)
        {
            Dimension pitch=model.Parameter("D3@阵列(线性)1") as Dimension,count=model.Parameter("D1@阵列(线性)1") as Dimension;
            ParametricContext.Require(pitch!=null&&count!=null&&pitch.DrivenState==2&&Math.Abs(pitch.SystemValue*1000-290)<.001&&Math.Abs(count.SystemValue-7)<.000001,"LEFT_REAR_HOLE_PATTERN_SOURCE_DRIFT");
            pitch.SystemValue=(height-177)/6000;
            RefreshPartAssemblyContext(sw,model,records,folder,"草图18",out contextRoot);
            Feature feature=FindFeature(model,"草图18");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
            SketchArc[] circles=((Array)sketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>().ToArray();
            ParametricContext.Require(circles.Length==2&&circles.All(c=>c.IsCircle()==1&&Math.Abs(c.GetRadius()*1000-2.6)<.001),"LEFT_REAR_HOLE_PROFILE_DRIFT");
            MathUtility math=sw.GetMathUtility() as MathUtility;
            double[] worldY=circles.Select(c=>(double[])((MathPoint)((MathPoint)math.CreatePoint(new[]{c.IGetCenterPoint2().X,c.IGetCenterPoint2().Y,c.IGetCenterPoint2().Z})).MultiplyTransform(sketch.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData).Select(p=>p[1]*1000).ToArray();
            bool restored=Math.Abs(worldY.Min()-(height-121))<.001&&Math.Abs(worldY.Max()-(height-111))<.001;
            ParametricContext.Require(restored&&sketch.RelationManager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal)==3,"LEFT_REAR_HOLE_CONTEXT_GEOMETRY_FAILED");
            pitch.SystemValue=(height-177)/6000;
            records.Add(new{inspection="left_rear_hole_height_driver",contextRestoredSeed=restored,pitchMm=pitch.SystemValue*1000,count=count.SystemValue});
        }

        private static void VerifyRearHolePatternHeight(ModelDoc2 model,List<object> records,double height,string stage,bool left=false)
        {
            double pitch=(height-177)/6;var holes=new List<double[]>();var faces=new List<object>();bool complete=true;
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Feature owner=face.GetFeature() as Feature;if(owner==null||(owner.Name!="切除-拉伸5"&&owner.Name!="阵列(线性)1"))continue;
                Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsCylinder())continue;
                double[] cylinder=surface.CylinderParams as double[],box=face.GetBox() as double[];
                if(cylinder==null||box==null||Math.Abs(cylinder[6]-.0026)>.000001||Math.Abs(Math.Abs(cylinder[5])-1)>.000001)continue;
                double area=face.GetArea(),fullArea=2*Math.PI*cylinder[6]*(box[5]-box[2]);
                complete=complete&&Math.Abs(box[5]-box[2]-.0008)<.000001&&Math.Abs(area-fullArea)<.00000001;
                holes.Add(new[]{cylinder[0]*1000,cylinder[1]*1000});faces.Add(new{owner=owner.Name,cylinder=cylinder,box=box,area=area,fullCylinderArea=fullArea});
            }
            double[] expected=Enumerable.Range(0,7).SelectMany(i=>new[]{56+i*pitch,66+i*pitch}).ToArray();
            bool unique=holes.Count==14&&holes.Select(p=>Math.Round(p[1],4)).Distinct().Count()==14;
            records.Add(new{inspection=left?"left_rear_hole_axes":"right_rear_hole_axes",stage=stage,axesMm=holes,expectedYmm=expected,completeCylinders=complete,faces=faces});
            ParametricContext.Require(unique&&complete&&expected.All(y=>holes.Any(p=>Math.Abs(p[0]-6.5)<.001&&Math.Abs(p[1]-y)<.001)),"REAR_HOLE_GEOMETRY_FAILED "+Path.GetFileName(model.GetPathName())+" "+stage);
            ParametricContext.Require((left||(Math.Abs(((Dimension)model.Parameter("D3@草图18")).SystemValue*1000-3*pitch)<.001&&Math.Abs(((Dimension)model.Parameter("D1@草图18")).SystemValue*1000-5.2)<.001&&Math.Abs(((Dimension)model.Parameter("D2@草图18")).SystemValue*1000-10)<.001))&&Math.Abs(((Dimension)model.Parameter("D3@阵列(线性)1")).SystemValue*1000-pitch)<.001&&Math.Abs(((Dimension)model.Parameter("D1@阵列(线性)1")).SystemValue-7)<.000001,"REAR_HOLE_PARAMETER_READBACK_FAILED "+stage);
        }

        private static void VerifySideMountHeight(ISldWorks sw,ModelDoc2 model,List<object> records,double height,double depth,string stage,bool left)
        {
            Feature profile=FindFeature(model,"草图19");
            Sketch sketch=profile.GetSpecificFeature2() as Sketch;
            SketchArc[] circles=((Array)sketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>().Where(arc=>!((SketchSegment)arc).ConstructionGeometry).ToArray();
            ParametricContext.Require(circles.Length==2&&circles.All(arc=>arc.IsCircle()==1&&Math.Abs(arc.GetRadius()-.001)<.000001),"SIDE_MOUNT_PROFILE_CHANGED");
            int externalCount=sketch.RelationManager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal);
            ParametricContext.Require(externalCount==(left&&depth>=450?2:0),"SIDE_MOUNT_REFERENCE_COUNT_CHANGED");
            MathUtility math=sw.GetMathUtility() as MathUtility;MathTransform toModel=sketch.ModelToSketchTransform.Inverse() as MathTransform;
            double[][] local=circles.Select(arc=>{SketchPoint center=arc.IGetCenterPoint2();return new[]{center.X,center.Y,center.Z};}).ToArray();
            double[][] world=local.Select(p=>(double[])((MathPoint)((MathPoint)math.CreatePoint(p)).MultiplyTransform(toModel)).ArrayData).ToArray();
            double shift=depth<450?(550-depth)/2:0;
            double[] expectedX=left?new[]{-311+shift,-236+shift}:new[]{236-shift,311-shift};
            ParametricContext.Require(expectedX.All(x=>local.Any(p=>Math.Abs(p[0]*1000-x)<.001))&&world.All(p=>Math.Abs(p[1]*1000-(height-129.6))<.001),"SIDE_MOUNT_HEIGHT_POSITION_FAILED");
            if(left){
                Sketch rear=FindFeature(model,"草图18").GetSpecificFeature2() as Sketch;
                ParametricContext.Require(rear.RelationManager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal)==3,"LEFT_REAR_HOLE_REFERENCE_CHANGED");
                if(depth>=450){Dimension datum=model.Parameter("D3@草图19") as Dimension;ParametricContext.Require(datum!=null&&Math.Abs(datum.SystemValue*1000-50)<.001,"LEFT_SIDE_MOUNT_DATUM_CHANGED");}
            }
            Feature cut=FindFeature(model,"切除-拉伸6");
            ParametricContext.Require(cut!=null&&!cut.IsSuppressed()&&cut.GetTypeName2()==(left?"ICE":"Cut")&&cut.GetErrorCode()==0,"SIDE_MOUNT_NATIVE_CUT_CHANGED");
            ExtrudeFeatureData2 definition=cut.GetDefinition() as ExtrudeFeatureData2;
            ParametricContext.Require(definition!=null&&definition.AccessSelections(model,null),"SIDE_MOUNT_CUT_DEFINITION_UNAVAILABLE");
            try{
                Array scopes=definition.FeatureScopeBodies as Array;
                ParametricContext.Require(!definition.ReverseDirection&&!definition.BothDirections&&definition.GetEndCondition(true)==0&&Math.Abs(definition.GetDepth(true)-.005)<.000001&&definition.FromType==0&&Math.Abs(definition.FromOffsetDistance)<.000001&&definition.FeatureScope&&!definition.AutoSelect&&scopes!=null&&scopes.Length==1,"SIDE_MOUNT_CUT_DEFINITION_CHANGED");
            }finally{definition.ReleaseSelectionAccess();}
            var cylinders=new List<double[]>();var faces=new List<object>();bool complete=true;
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Feature owner=face.GetFeature() as Feature;if(owner==null||owner.Name!="切除-拉伸6")continue;
                Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsCylinder())continue;
                double[] cylinder=surface.CylinderParams as double[],box=face.GetBox() as double[];
                if(cylinder==null||box==null||Math.Abs(cylinder[6]-.001)>.000001||Math.Abs(Math.Abs(cylinder[3])-1)>.000001)continue;
                double area=face.GetArea(),fullArea=2*Math.PI*cylinder[6]*(box[3]-box[0]);
                complete=complete&&Math.Abs(box[3]-box[0]-.0008)<.000001&&Math.Abs(area-fullArea)<.00000001;
                cylinders.Add(cylinder);faces.Add(new{cylinder=cylinder,box=box,area=area,fullCylinderArea=fullArea});
            }
            records.Add(new{inspection="side_mount_height_geometry",stage=stage,file=Path.GetFileName(model.GetPathName()),targetYmm=height-129.6,externalCount=externalCount,localCentersMm=local.Select(p=>p.Select(v=>v*1000).ToArray()).ToArray(),modelCentersMm=world.Select(p=>p.Select(v=>v*1000).ToArray()).ToArray(),completeCylinders=complete,faces=faces});
            ParametricContext.Require(cylinders.Count==2&&complete&&world.All(p=>cylinders.Any(c=>Math.Abs(c[1]-p[1])<.000001&&Math.Abs(c[2]-p[2])<.000001)),"SIDE_MOUNT_SOLID_HOLES_FAILED");
        }

        private static void UpdateMasterFramePatternHeight(ModelDoc2 model,List<object> records,double height)
        {
            // The original 12-unit opening budget is H-87; eleven internal bands keep 3 mm width and 2 mm edge offsets.
            Dimension seed=model.Parameter("D1@草图76") as Dimension,pitch=model.Parameter("D3@阵列(线性)6") as Dimension,count=model.Parameter("D1@阵列(线性)6") as Dimension;
            ParametricContext.Require(seed!=null&&pitch!=null&&count!=null&&seed.DrivenState==2&&pitch.DrivenState==2&&Math.Abs(seed.SystemValue*1000-145.5)<.001&&Math.Abs(pitch.SystemValue*1000-152.5)<.001&&Math.Abs(count.SystemValue-11)<.000001,"MASTER_FRAME_PATTERN_SOURCE_DRIFT");
            double unit=(height-87)/12;
            seed.SystemValue=(unit-7)/1000;pitch.SystemValue=unit/1000;
            records.Add(new{inspection="master_frame_pattern_height_driver",targetHeightMm=height,unitPitchMm=unit,seedHeightMm=seed.SystemValue*1000,readbackPitchMm=pitch.SystemValue*1000,instanceCount=count.SystemValue});
            ParametricContext.Require(Math.Abs(seed.SystemValue*1000-(unit-7))<.001&&Math.Abs(pitch.SystemValue*1000-unit)<.001&&Math.Abs(count.SystemValue-11)<.000001,"MASTER_FRAME_PATTERN_DRIVER_READBACK_FAILED");
        }

        private static void VerifyMasterFramePatternHeight(ModelDoc2 model,List<object> records,double height,string stage)
        {
            double unit=(height-87)/12;
            string[] names=new[]{"D1@草图76","D3@阵列(线性)6","D1@阵列(线性)6","D7@草图76","D8@草图76","D2@草图76","D3@草图76","D4@草图76","D5@草图76","D6@草图76"};
            double[] expected=new[]{unit-7,unit,11,unit-3,ParametricContext.DoorWidthMm,3,2,2,3,2};
            double[] actual=names.Select((name,index)=>{Dimension d=model.Parameter(name) as Dimension;ParametricContext.Require(d!=null,"MASTER_FRAME_PATTERN_DIMENSION_MISSING "+name);return d.SystemValue*(index==2?1:1000);}).ToArray();
            records.Add(new{inspection="master_frame_pattern_height_readback",stage=stage,dimensions=names,measuredValues=actual,expectedValues=expected});
            ParametricContext.Require(actual.Zip(expected,(a,b)=>Math.Abs(a-b)<.001).All(v=>v),"MASTER_FRAME_PATTERN_PERSISTENCE_FAILED "+stage);
        }

        private static void ProbeMasterPatternGeometry(ISldWorks sw,ModelDoc2 model,List<object> records,string stage="pattern-geometry-source")
        {
            MathUtility math=sw.GetMathUtility() as MathUtility;
            CaptureMasterHeightLinks(model,records,stage);
            Feature seed=FindFeature(model,"凸台-拉伸19");
            ParametricContext.Require(seed!=null,"MASTER_PATTERN_SEED_MISSING");
            var sketches=new List<object>();Feature child=seed.GetFirstSubFeature() as Feature;
            while(child!=null){
                if(child.GetTypeName2()=="ProfileFeature"){
                    Sketch sketch=child.GetSpecificFeature2() as Sketch;MathTransform toModel=sketch.ModelToSketchTransform.Inverse() as MathTransform;
                    Array points=sketch.GetSketchPoints2() as Array;
                    sketches.Add(new{name=child.Name,geometry=CaptureDerivedFailureSketch(child),worldPointsMm=points==null?new double[0][]:points.Cast<SketchPoint>().Select(p=>((double[])((MathPoint)((MathPoint)math.CreatePoint(new[]{p.X,p.Y,p.Z})).MultiplyTransform(toModel)).ArrayData).Select(v=>v*1000).ToArray()).ToArray()});
                }
                child=child.GetNextSubFeature() as Feature;
            }
            records.Add(new{inspection="master_pattern_seed_sketches",stage=stage,sketches=sketches});
            try{
                foreach(string name in new[]{"凸台-拉伸19","阵列(线性)6"}){
                    ParametricContext.Require(model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToAfterFeature,name),"MASTER_PATTERN_ROLLBACK_FAILED "+name);
                    var bodies=new List<object>();
                    foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false)){
                        var faces=new List<object>();var owners=new HashSet<string>();
                        foreach(Face2 face in (Array)body.GetFaces()){
                            Feature owner=face.GetFeature() as Feature;string ownerName=owner==null?null:owner.Name;
                            if(ownerName!=null)owners.Add(ownerName);
                            if(ownerName=="凸台-拉伸19"||ownerName=="阵列(线性)6")faces.Add(new{owner=ownerName,box=face.GetBox(),area=face.GetArea()});
                        }
                        bodies.Add(new{name=body.Name,box=body.GetBodyBox(),owners=owners.ToArray(),patternFaces=faces});
                    }
                    records.Add(new{inspection="master_pattern_rollback_geometry",stage=stage,afterFeature=name,snapshot=CapturePartSnapshot(model),bodies=bodies});
                }
            }finally{ParametricContext.Require(model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,""),"MASTER_PATTERN_ROLLBACK_RESTORE_FAILED");}
        }

        // Apply verified H/D bindings; the separate probe mode only records isolated measurements.
        internal static void ProbeEnvelope(ISldWorks sw,List<object> records,bool apply=false)
        {
            var input=ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan,"contract"),"input");
            var probe=apply?new Dictionary<string,object>{{"heightMm",ParametricContext.HeightMm},{"depthMm",ParametricContext.DepthMm}}:ParametricContext.Object(input,"envelopeProbe");
            double height=ParametricContext.Number(probe,"heightMm"),depth=ParametricContext.Number(probe,"depthMm");
            ParametricContext.Require(height>=1700&&height<=2200&&depth>=250&&depth<=650,"ENVELOPE_PROBE_RANGE_INVALID");
            ParametricContext.Require(apply?(height!=1917||depth!=550):(height==1917)!=(depth==550),"ENVELOPE_PROBE_REQUIRES_ONE_AXIS_CHANGE");
            string folder=ParametricContext.CadDirectory;
            string masterPath=ExactFile(folder,"标准寄存柜 模型.SLDPRT");
            if(!apply&&probe.ContainsKey("masterPatternOnly")){
                ModelDoc2 model=NativeParametricAssembly.Open(sw,masterPath,!probe.ContainsKey("verifyHeightPattern"));
                try{
                    ProbeMasterPatternGeometry(sw,model,records);
                    if(probe.ContainsKey("verifyHeightPattern")){
                        UpdateMasterFramePatternHeight(model,records,height);
                        ((Dimension)model.Parameter("D2@草图1")).SystemValue=height/1000;
                        bool rebuilt=model.ForceRebuild3(false);PartSnapshot snapshot=CapturePartSnapshot(model);
                        records.Add(new{inspection="master_frame_pattern_height_diagnostic",diagnosticOnly=true,targetHeightMm=height,rebuilt=rebuilt,snapshot=snapshot});
                        ProbeMasterPatternGeometry(sw,model,records,"pattern-geometry-height-driven");
                        ParametricContext.Require(rebuilt&&PartHealthGate(snapshot,false)&&snapshot.body_count==20,"MASTER_FRAME_PATTERN_HEIGHT_DIAGNOSTIC_FAILED");
                    }
                }
                finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
                return;
            }
            var plans=DerivedPlans(folder);
            foreach(string name in new[]{"箱体竖隔板L.sldprt","箱体竖隔板R.SLDPRT","门框 竖隔板L.sldprt","门框 竖隔板R.SLDPRT","箱体竖隔板加强件.sldprt","箱体侧板加强筋2.sldprt"})
                plans.Add(new DerivedPlan{file_name=name});
            if(probe.ContainsKey("topCoverOnly")&&Convert.ToBoolean(probe["topCoverOnly"]))plans=plans.Where(plan=>plan.file_name=="上盖 模型.sldprt"||plan.file_name=="上盖壳体底板.sldprt").ToList();
            if(probe.ContainsKey("faceReferencesOnly"))plans=plans.Where(plan=>new[]{"底座 模型.sldprt","底座底板.sldprt","上盖 模型.sldprt"}.Contains(plan.file_name)).ToList();
            if(probe.ContainsKey("topPartsOnly"))plans=plans.Where(plan=>plan.file_name.StartsWith("上盖")).ToList();
            if(probe.ContainsKey("probeVerticalStiffeners"))plans=plans.Where(plan=>plan.file_name=="箱体侧板加强筋1.sldprt").ToList();
            var baseline=new Dictionary<string,PartSnapshot>(StringComparer.OrdinalIgnoreCase);
            var shallowTopPoints=new Dictionary<string,double[][]>();
            foreach(string name in new[]{"标准寄存柜 模型.SLDPRT"}.Concat(plans.Select(p=>p.file_name)))
            {
                ModelDoc2 model=NativeParametricAssembly.Open(sw,ExactFile(folder,name),true);
                try{
                    baseline.Add(name,CapturePartSnapshot(model));
                    if(height!=1917&&(name=="箱体右侧板.sldprt"||name=="箱体左侧板.sldprt"))CaptureMasterHeightLinks(model,records,"side-height-source",new[]{"草图18","切除-拉伸5","阵列(线性)1","草图19","切除-拉伸6"});
                    if(depth<450&&name=="箱体横层板L.sldprt")CaptureShallowShelfDefinitions(model,records,"before");
                    if(depth<450&&name=="上盖壳体左侧板.sldprt")records.Add(new{inspection="shallow_top_mount_baseline",cylinders=ShallowTopMountCylinders(model)});
                    if(depth<450){
                        string[] inspectSketches=name=="箱体横层板L.sldprt"?new[]{"草图17"}:name=="箱体左侧板.sldprt"?new[]{"草图19"}:name=="箱体竖隔板L.sldprt"?new[]{"草图9","草图10","草图19","草图20"}:name=="标准寄存柜 模型.SLDPRT"?new[]{"草图36","草图65","草图66"}:new string[0];
                        foreach(string sketchName in inspectSketches){Feature f=FindFeature(model,sketchName);if(f!=null)records.Add(new{inspection="shallow_link_sketch_baseline",file=name,name=sketchName,geometry=CaptureDerivedFailureSketch(f)});}
                        if(name=="标准寄存柜 模型.SLDPRT")foreach(string featureName in new[]{"凸台-拉伸31","凸台-拉伸32","凸台-拉伸36","抽壳9","切除-拉伸46","抽铆4"}){
                            Feature f=FindFeature(model,featureName);if(f==null)continue;
                            var dimensions=new List<object>();DisplayDimension d=f.GetFirstDisplayDimension() as DisplayDimension;
                            while(d!=null){Dimension value=d.GetDimension2(0);dimensions.Add(new{name=value.FullName,valueMm=value.SystemValue*1000});d=f.GetNextDisplayDimension(d) as DisplayDimension;}
                            var sketches=new List<object>();Feature child=f.GetFirstSubFeature() as Feature;
                            while(child!=null){if(child.GetTypeName2()=="ProfileFeature"){
                                var dims=new List<object>();DisplayDimension sd=child.GetFirstDisplayDimension() as DisplayDimension;
                                while(sd!=null){Dimension value=sd.GetDimension2(0);dims.Add(new{name=value.FullName,valueMm=value.SystemValue*1000});sd=child.GetNextDisplayDimension(sd) as DisplayDimension;}
                                sketches.Add(new{name=child.Name,geometry=CaptureDerivedFailureSketch(child),dimensions=dims});
                            }child=child.GetNextSubFeature() as Feature;}
                            records.Add(new{inspection="shallow_master_brace_driver",name=featureName,type=f.GetTypeName2(),dimensions=dimensions,sketches=sketches});
                        }
                        if(name=="上盖壳体底板.sldprt"||name=="上盖壳体左侧板.sldprt"){
                            var sketches=new List<object>();Feature sf=model.FirstFeature() as Feature;
                            while(sf!=null){if(sf.GetTypeName2()=="ProfileFeature")sketches.Add(new{name=sf.Name,sketch=CaptureDerivedFailureSketch(sf)});sf=sf.GetNextFeature() as Feature;}
                            records.Add(new{inspection="shallow_top_part_sketches",file=name,sketches=sketches});
                        }
                        if(name=="上盖 模型.sldprt")records.Add(new{inspection="shallow_top_bodies_before",bodies=((Array)((PartDoc)model).GetBodies2(0,false)).Cast<Body2>().Select(b=>new{name=b.Name,box=b.GetBodyBox(),mass=b.GetMassProperties(1)}).ToArray()});
                        if(name=="上盖 模型.sldprt")foreach(string sketchName in new[]{"草图10","草图4","草图9","草图5","草图6","草图11"}){
                            Feature f=FindFeature(model,sketchName);
                            records.Add(new{inspection="shallow_top_sketch_before",name=sketchName,sketch=CaptureDerivedFailureSketch(f)});
                        }
                    }
                    if(depth<550)CaptureShallowDeleteFaces(model,records,"before");
                }
                finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
            if(probe.ContainsKey("probeTopDimensions")){
                foreach(string file in new[]{"上盖壳体底板.sldprt","上盖壳体左侧板.sldprt"}){
                    ModelDoc2 part=NativeParametricAssembly.Open(sw,ExactFile(folder,file),false);
                    try{
                        string sketchName=file=="上盖壳体底板.sldprt"?"草图2":"草图3";
                        string[] names=file=="上盖壳体底板.sldprt"?new[]{"D1@草图2","D2@草图2"}:new[]{"D7@草图3","D8@草图3","D9@草图3"};
                        foreach(string name in names){
                            Dimension dim=part.Parameter(name) as Dimension;double original=dim.SystemValue;
                            dim.SystemValue=original+.005;part.ForceRebuild3(false);
                            records.Add(new{inspection="top_dimension_sensitivity",file=file,dimension=name,deltaMm=5,sketch=CaptureDerivedFailureSketch(FindFeature(part,sketchName))});
                            dim.SystemValue=original;ParametricContext.Require(part.ForceRebuild3(false),"TOP_DIMENSION_PROBE_RESTORE_FAILED");
                        }
                    }finally{sw.CloseDoc(part.GetTitle());Marshal.ReleaseComObject(part);}
                }
                return;
            }
            if(probe.ContainsKey("probeVerticalStiffeners")){
                ModelDoc2 seed=NativeParametricAssembly.Open(sw,ExactFile(folder,"箱体侧板加强筋1.sldprt"),true);
                try{CaptureShallowVerticalFeatures(seed,records,"before");}finally{sw.CloseDoc(seed.GetTitle());Marshal.ReleaseComObject(seed);}
                PrepareShallowBrace(sw,folder,records,depth,baseline["标准寄存柜 模型.SLDPRT"]);
                PrepareShallowVerticalStiffener(sw,folder,records,depth,baseline["标准寄存柜 模型.SLDPRT"]);
                ModelDoc2 derived=NativeParametricAssembly.Open(sw,ExactFile(folder,"箱体侧板加强筋1.sldprt"),false);
                try{
                    derived.ForceRebuild3(false);CaptureShallowVerticalFeatures(derived,records,"after-seed");
                    if(probe.ContainsKey("repairVerticalTool")){
                        RelocateShallowVerticalTool(sw,derived,records,depth);CaptureShallowVerticalFeatures(derived,records,"after-tool-move");
                        RelocateShallowVerticalHoles(sw,derived,records,depth);CaptureShallowVerticalFeatures(derived,records,"after-hole-move");
                        ParametricContext.Require(DerivedPartHealthGate(CapturePartSnapshot(derived),baseline["箱体侧板加强筋1.sldprt"]),"SHALLOW_VERTICAL_DERIVED_REBUILD_FAILED");
                        NativeParametricAssembly.Save(derived);
                        RelocateShallowVerticalTool(sw,derived,records,depth);RelocateShallowVerticalHoles(sw,derived,records,depth);
                        CaptureShallowVerticalFeatures(derived,records,"repeated-placement");NativeParametricAssembly.Save(derived);
                        sw.CloseDoc(derived.GetTitle());Marshal.ReleaseComObject(derived);derived=null;
                        derived=NativeParametricAssembly.Open(sw,ExactFile(folder,"箱体侧板加强筋1.sldprt"),true);derived.ForceRebuild3(false);CaptureShallowVerticalFeatures(derived,records,"reopened");
                        ParametricContext.Require(DerivedPartHealthGate(CapturePartSnapshot(derived),baseline["箱体侧板加强筋1.sldprt"]),"SHALLOW_VERTICAL_DERIVED_REOPEN_FAILED");
                    }
                }finally{sw.CloseDoc(derived.GetTitle());Marshal.ReleaseComObject(derived);}
                return;
            }
            foreach(string name in new[]{"箱体横层板L.sldprt","箱体左侧板.sldprt","箱体右侧板.sldprt"})
                if((depth<450||(height!=1917&&name=="箱体右侧板.sldprt"))&&baseline.ContainsKey(name))PrepareShallowBodyMountSketch(sw,folder,name,records,height,depth,baseline[name]);
            if(depth<450&&baseline.ContainsKey("箱体竖隔板L.sldprt"))PrepareShallowBrace(sw,folder,records,depth,baseline["标准寄存柜 模型.SLDPRT"]);
            if(depth<450)PrepareShallowVerticalStiffener(sw,folder,records,depth,baseline["标准寄存柜 模型.SLDPRT"]);
            if(depth<450){
                ModelDoc2 top=NativeParametricAssembly.Open(sw,ExactFile(folder,"上盖 模型.sldprt"),false);
                try{
                    MathUtility math=sw.GetMathUtility() as MathUtility;
                    foreach(string name in new[]{"草图10","草图4","草图9","草图5","草图6","草图11"}){
                        Feature f=FindFeature(top,name);Sketch sketch=f.GetSpecificFeature2() as Sketch;
                        var originalPoints=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                        MathTransform toSketch=sketch.ModelToSketchTransform,toModel=toSketch.Inverse() as MathTransform;
                        var targets=new List<double[]>();
                        foreach(SketchPoint point in (Array)sketch.GetSketchPoints2()){
                            MathPoint p=math.CreatePoint(new[]{point.X,point.Y,point.Z}) as MathPoint;
                            double[] world=((MathPoint)p.MultiplyTransform(toModel)).ArrayData as double[];
                            if(world[2]<-.3)world[2]+=(550-depth)/1000;
                            targets.Add(((MathPoint)((MathPoint)math.CreatePoint(world)).MultiplyTransform(toSketch)).ArrayData as double[]);
                        }
                        shallowTopPoints.Add(name,targets.ToArray());
                        Array links=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                        if(links!=null)foreach(SketchRelation relation in links)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_TOP_DETACH_FAILED "+name);
                        top.ClearSelection2(true);f.Select2(false,0);top.EditSketch();
                        var preserved=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
                        ParametricContext.Require(preserved.Length==originalPoints.Length,"SHALLOW_TOP_BASELINE_POINT_COUNT_CHANGED");
                        for(int i=0;i<preserved.Length;i++){double[] p=originalPoints[i];ParametricContext.Require(preserved[i].SetCoords(p[0],p[1],p[2]),"SHALLOW_TOP_BASELINE_RESTORE_FAILED");}
                        top.SketchManager.InsertSketch(true);
                        records.Add(new{inspection="shallow_top_detach_step",sketch=name,snapshot=CapturePartSnapshot(top)});
                    }
                    NativeParametricAssembly.Save(top);
                    PartSnapshot detached=CapturePartSnapshot(top);
                    records.Add(new{inspection="shallow_top_detach_check",before=baseline["上盖 模型.sldprt"],after=detached,details=CaptureDerivedRebuildFailure(top)});
                    ParametricContext.Require(DerivedPartHealthGate(detached,baseline["上盖 模型.sldprt"]),"SHALLOW_TOP_DETACH_HEALTH_FAILED");
                    ParametricContext.Require(detached.part_box_m.Zip(baseline["上盖 模型.sldprt"].part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(x=>x),"SHALLOW_TOP_DETACH_GEOMETRY_CHANGED");
                }finally{sw.CloseDoc(top.GetTitle());Marshal.ReleaseComObject(top);}
            }
            if(height!=1917){
                string name="上盖壳体底板.sldprt";
                ModelDoc2 panel=NativeParametricAssembly.Open(sw,ExactFile(folder,name),false);
                try{
                    foreach(string sketchName in new[]{"草图2","草图7","草图8"}){
                        Feature feature=FindFeature(panel,sketchName);Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                        Array relations=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                        int expected=sketchName=="草图2"?3:sketchName=="草图7"?7:4;
                        ParametricContext.Require(relations!=null&&relations.Length==expected,"TOP_PANEL_EXTERNAL_RELATION_DRIFT "+sketchName);
                        foreach(SketchRelation relation in relations)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"TOP_PANEL_EXTERNAL_RELATION_DELETE_FAILED "+sketchName);
                        records.Add(new{file=name,sketch=sketchName,removedExternalRelations=relations.Length,preservedNativeDimensions=true});
                    }
                    NativeParametricAssembly.Save(panel);
                    PartSnapshot detached=CapturePartSnapshot(panel);
                    ParametricContext.Require(DerivedPartHealthGate(detached,baseline[name])&&detached.part_box_m.Zip(baseline[name].part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(x=>x),"TOP_PANEL_DETACH_CHANGED_BASELINE");
                }finally{sw.CloseDoc(panel.GetTitle());Marshal.ReleaseComObject(panel);}
            }
            ModelDoc2 master=NativeParametricAssembly.Open(sw,masterPath,false);
            try{
                string dimensionName=height!=1917?"D2@草图1":"D1@凸台-拉伸1";
                double target=height!=1917?height:depth;
                Dimension dimension=master.Parameter(dimensionName) as Dimension;
                ParametricContext.Require(dimension!=null&&dimension.DrivenState==2,"ENVELOPE_MASTER_DRIVER_MISSING");
                double before=dimension.SystemValue*1000;
                if(depth<550){
                    Feature locator=FindFeature(master,"草图126");
                    records.Add(new{inspection="shallow_locator_before",sketch=CaptureDerivedFailureSketch(locator)});
                    Dimension pitch=master.Parameter("D2@草图126") as Dimension;
                    ParametricContext.Require(pitch!=null&&Math.Abs(pitch.SystemValue*1000-210)<.001,"SHALLOW_LOCATOR_PITCH_SOURCE_DRIFT");
                    pitch.SystemValue=(depth-130)/2000;
                    Sketch locatorSketch=locator.GetSpecificFeature2() as Sketch;
                    Array external=locatorSketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll) as Array;
                    int detached=0;
                    if(external!=null)foreach(SketchRelation relation in external){
                        Array linked=relation.GetEntities() as Array;
                        if(relation.GetRelationType()==9&&linked!=null&&linked.Cast<object>().OfType<SketchPoint>().Any(p=>Math.Abs(p.Y*1000-453)<.001)){
                            ParametricContext.Require(locatorSketch.RelationManager.DeleteRelation(relation),"SHALLOW_REAR_LOCATOR_DETACH_FAILED");detached++;
                        }
                    }
                    ParametricContext.Require(detached==1,"SHALLOW_REAR_LOCATOR_REFERENCE_CHANGED");
                    master.ClearSelection2(true);ParametricContext.Require(locator.Select2(false,0),"SHALLOW_LOCATOR_SELECT_FAILED");master.EditSketch();
                    int rearMoved=0;
                    foreach(SketchArc arc in ((Array)locatorSketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>()){
                        SketchPoint center=arc.IGetCenterPoint2();
                        if(Math.Abs(center.Y*1000-453)<.001){ParametricContext.Require(center.SetCoords(center.X,(depth-97)/1000,center.Z),"SHALLOW_REAR_LOCATOR_MOVE_FAILED");rearMoved++;}
                    }
                    master.SketchManager.InsertSketch(true);
                    records.Add(new{inspection="shallow_rear_locator",detachedReferences=detached,movedCenters=rearMoved,targetDepthMm=depth-97});
                }
                if(depth<450){
                    var bodySources=new List<object>();
                    foreach(Body2 body in (Array)((PartDoc)master).GetBodies2(0,false)){
                        double[] box=body.GetBodyBox() as double[];
                        if(box!=null&&Math.Abs(box[2]+.4235)<.001){
                            var owners=new HashSet<string>();foreach(Face2 face in (Array)body.GetFaces()){Feature f=face.GetFeature() as Feature;if(f!=null)owners.Add(f.Name);}
                            bodySources.Add(new{name=body.Name,box=box,features=owners.ToArray()});
                        }
                    }
                    records.Add(new{inspection="shallow_brace_sources",bodies=bodySources});
                }
                if(depth<550){
                    Dimension rearBase=master.Parameter("D4@草图130") as Dimension;
                    ParametricContext.Require(rearBase!=null&&Math.Abs(rearBase.SystemValue*1000-426)<.001,"SHALLOW_BASE_HOLE_SOURCE_DRIFT");
                    rearBase.SystemValue=(depth-124)/1000;
                    Dimension topOpening=master.Parameter("D3@草图84") as Dimension;
                    ParametricContext.Require(topOpening!=null&&Math.Abs(topOpening.SystemValue*1000-450)<.001,"SHALLOW_TOP_OPENING_SOURCE_DRIFT");
                    topOpening.SystemValue=(depth-100)/1000;
                    foreach(string name in new[]{"D6@草图120","D7@草图120","D8@草图120","D3@草图122","D4@草图122","D5@草图122"}){
                        Dimension spacing=master.Parameter(name) as Dimension;
                        ParametricContext.Require(spacing!=null&&Math.Abs(spacing.SystemValue*1000-150)<.001,"SHALLOW_TOP_HOLE_SOURCE_DRIFT "+name);
                        spacing.SystemValue=(depth-100)/3000;
                    }
                }
                if(height!=1917&&depth<450)CaptureMasterHeightLinks(master,records,"before-master-drive");
                ParametricContext.Require(Math.Abs(before-(height!=1917?1917:550))<.001,"ENVELOPE_MASTER_SOURCE_DRIFT");
                if(height!=1917)UpdateMasterFramePatternHeight(master,records,height);
                dimension.SystemValue=target/1000;
                if(height!=1917&&depth!=550){
                    Dimension depthDriver=master.Parameter("D1@凸台-拉伸1") as Dimension;
                    ParametricContext.Require(depthDriver!=null&&depthDriver.DrivenState==2&&Math.Abs(depthDriver.SystemValue*1000-550)<.001,"ENVELOPE_DEPTH_DRIVER_DRIFT");
                    depthDriver.SystemValue=depth/1000;
                }
                if(depth<450&&height!=1917){
                    UpdateShallowVerticalSeedHeight(sw,master,records);
                    UpdateShallowSketchHeight(sw,master,records,"草图106",10,1692.5,1705.5,false,FrameGridHeightTranslation(height));
                    UpdateShallowSketchHeight(sw,master,records,"草图105",11,1662.4633394043415,1703.75,true,FrameGridHeightTranslation(height));
                }
                bool rebuilt=master.ForceRebuild3(false);
                if(height!=1917&&depth<450)CaptureMasterHeightLinks(master,records,"after-first-rebuild");
                if(depth<550){
                    Feature locator=FindFeature(master,"草图126");Sketch sketch=locator.GetSpecificFeature2() as Sketch;
                    Array dangling=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swDangling) as Array;
                    if(dangling!=null)foreach(SketchRelation relation in dangling)ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_DANGLING_RELATION_REMOVE_FAILED");
                    Array external=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swOverDefining) as Array;
                    if(external!=null)foreach(SketchRelation relation in external){
                        Array linked=relation.GetEntities() as Array;
                        if(relation.GetRelationType()==12)
                            ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation),"SHALLOW_MIDPOINT_RELATION_REMOVE_FAILED");
                    }
                    master.ClearSelection2(true);locator.Select2(false,0);master.EditSketch();
                    foreach(SketchArc arc in ((Array)sketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>()){
                        SketchPoint center=arc.IGetCenterPoint2();if(Math.Abs(center.Y*1000-453)<.001)center.SetCoords(center.X,(depth-97)/1000,center.Z);
                    }
                    master.SketchManager.InsertSketch(true);rebuilt=master.ForceRebuild3(false);
                }
                PartSnapshot after=CapturePartSnapshot(master);
                if(depth<450){
                    var outside=new List<object>();
                    foreach(Body2 body in (Array)((PartDoc)master).GetBodies2(0,false)){
                        double[] box=body.GetBodyBox() as double[];if(box==null||(box[2]>=-depth/1000-.0001&&box[4]<=height/1000+.0001))continue;
                        var owners=new HashSet<string>();foreach(Face2 face in (Array)body.GetFaces()){Feature f=face.GetFeature() as Feature;if(f!=null)owners.Add(f.Name);}
                        outside.Add(new{name=body.Name,box=box,owners=owners.ToArray()});
                    }
                    records.Add(new{inspection="shallow_master_outside_bodies",bodies=outside});
                }
                if(depth<450){
                    var topFaces=new List<object>();
                    foreach(Body2 body in (Array)((PartDoc)master).GetBodies2(0,false)){
                        double[] b=body.GetBodyBox() as double[];if(b==null||b[1]<1.8||b[4]<1.91)continue;
                        foreach(Face2 face in (Array)body.GetFaces()){
                            double[] box=face.GetBox() as double[];if(box==null||box[2]>-(depth-70)/1000||box[4]<1.89)continue;
                            Feature f=face.GetFeature() as Feature;topFaces.Add(new{owner=f==null?null:f.Name,box=box,area=face.GetArea()});
                        }
                    }
                    records.Add(new{inspection="shallow_master_top_faces",faces=topFaces});
                }
                if(depth<550){
                    Feature locator=FindFeature(master,"草图126");
                    var measured=new List<object>();DisplayDimension dd=locator.GetFirstDisplayDimension() as DisplayDimension;
                    while(dd!=null){Dimension dim=dd.GetDimension2(0);measured.Add(new{name=dim.FullName,valueMm=dim.SystemValue*1000});dd=locator.GetNextDisplayDimension(dd) as DisplayDimension;}
                    var relations=new List<object>();Sketch sketch=locator.GetSpecificFeature2() as Sketch;
                    foreach(SketchRelation relation in (Array)sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll)){
                        var points=new List<object>();Array entities=relation.GetEntities() as Array;
                        if(entities!=null)foreach(object entity in entities){SketchPoint p=entity as SketchPoint;if(p!=null)points.Add(new[]{p.X*1000,p.Y*1000,p.Z*1000});}
                        relations.Add(new{type=relation.GetRelationType(),points=points});
                    }
                    records.Add(new{inspection="shallow_locator_after",sketch=CaptureDerivedFailureSketch(locator),dimensions=measured,relations=relations});
                    var states=new List<object>();
                    foreach(swSketchRelationFilterType_e filter in new[]{swSketchRelationFilterType_e.swDangling,swSketchRelationFilterType_e.swOverDefining,swSketchRelationFilterType_e.swExternal,swSketchRelationFilterType_e.swBroken}){
                        Array filtered=sketch.RelationManager.GetRelations((int)filter) as Array;
                        states.Add(new{filter=filter.ToString(),types=filtered==null?new int[0]:filtered.Cast<SketchRelation>().Select(r=>r.GetRelationType()).ToArray()});
                    }
                    records.Add(new{inspection="shallow_relation_states",states=states});
                }
                if(!rebuilt||!PartHealthGate(after,false))records.Add(new{inspection="shallow_master_failure",details=CaptureDerivedRebuildFailure(master)});
                if(height!=1917&&depth<450)CaptureMasterHeightLinks(master,records,"before-master-gate");
                records.Add(new{diagnosticOnly=true,file="标准寄存柜 模型.SLDPRT",dimension=dimensionName,beforeMm=before,targetMm=target,readbackMm=dimension.SystemValue*1000,beforeGeometry=baseline["标准寄存柜 模型.SLDPRT"],afterGeometry=after,rebuilt=rebuilt,healthy=PartHealthGate(after,false),depthHoleSpanMm=((Dimension)master.Parameter("D4@草图130")).SystemValue*1000});
                ParametricContext.Require(rebuilt&&Math.Abs(dimension.SystemValue*1000-target)<.001&&PartHealthGate(after,false)&&after.body_count==baseline["标准寄存柜 模型.SLDPRT"].body_count,"ENVELOPE_MASTER_DRIVE_FAILED");
                ParametricContext.Require(Math.Abs(((Dimension)master.Parameter("D2@草图1")).SystemValue*1000-height)<.001&&Math.Abs(((Dimension)master.Parameter("D1@凸台-拉伸1")).SystemValue*1000-depth)<.001,"ENVELOPE_COMBINED_DRIVER_READBACK_FAILED");
                if(height!=1917)VerifyMasterFramePatternHeight(master,records,height,"before-save");
                NativeParametricAssembly.Save(master);
                if(height!=1917){
                    sw.CloseDoc(master.GetTitle());Marshal.ReleaseComObject(master);master=null;
                    master=NativeParametricAssembly.Open(sw,masterPath,true);bool reopened=master.ForceRebuild3(false);PartSnapshot snapshot=CapturePartSnapshot(master);
                    VerifyMasterFramePatternHeight(master,records,height,"saved-reopened");
                    records.Add(new{inspection="master_frame_pattern_saved_reopened",rebuilt=reopened,snapshot=snapshot});
                    ParametricContext.Require(reopened&&PartHealthGate(snapshot,false)&&snapshot.body_count==after.body_count&&snapshot.part_box_m.Zip(after.part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v),"MASTER_FRAME_PATTERN_REOPEN_CHANGED");
                }
            }finally{if(master!=null){sw.CloseDoc(master.GetTitle());Marshal.ReleaseComObject(master);}}
            foreach(DerivedPlan plan in plans)
            {
                string path=ExactFile(folder,plan.file_name);
                ModelDoc2 source=null,model=null,contextRoot=null;
                try{
                    if(!string.IsNullOrEmpty(plan.source_file_name))source=NativeParametricAssembly.Open(sw,ExactFile(folder,plan.source_file_name),true);
                    model=NativeParametricAssembly.Open(sw,path,false);
                    int activeError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activeError);
                    if(depth<450&&plan.file_name=="上盖壳体底板.sldprt"){
                        Dimension pitch=model.Parameter("D2@草图2") as Dimension;
                        ParametricContext.Require(pitch!=null&&Math.Abs(pitch.SystemValue*1000-190)<.001,"SHALLOW_TOP_BASE_PITCH_DRIFT");
                        pitch.SystemValue=Math.Min(190,depth-170)/1000;
                        ((Dimension)model.Parameter("D4@草图7")).SystemValue=Math.Min(160,depth-170)/1000;
                        Feature holeSketch=FindFeature(model,"草图7");Sketch hs=holeSketch.GetSpecificFeature2() as Sketch;
                        Array external=hs.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                        if(external!=null)foreach(SketchRelation relation in external)ParametricContext.Require(hs.RelationManager.DeleteRelation(relation),"SHALLOW_BASE_HOLE_LINK_DETACH_FAILED");
                        model.ClearSelection2(true);holeSketch.Select2(false,0);model.EditSketch();
                        int moved=0;
                        foreach(SketchArc arc in ((Array)hs.GetSketchSegments()).Cast<object>().OfType<SketchArc>()){
                            SketchPoint center=arc.IGetCenterPoint2();double original=center.Y*1000,target=original;
                            if(Math.Abs(original-500)<.001)target=depth-50;
                            else if(Math.Abs(original-290)<.001)target=(depth+30)/2;
                            else if(Math.Abs(original-453)<.001)target=depth-97;
                            if(Math.Abs(target-original)>.001){ParametricContext.Require(center.SetCoords(center.X,target/1000,center.Z),"SHALLOW_BASE_HOLE_POINT_FAILED");moved++;}
                        }
                        model.SketchManager.InsertSketch(true);records.Add(new{inspection="shallow_base_cut_layout",movedHoles=moved});
                    }
                    if(height!=1917&&plan.file_name=="上盖壳体底板.sldprt"){
                        int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);
                        Feature notch=FindFeature(model,"草图8");Sketch sketch=notch.GetSpecificFeature2() as Sketch;
                        double[] beforeTransform=(double[])sketch.ModelToSketchTransform.ArrayData;
                        model.ClearSelection2(true);ParametricContext.Require(notch.Select2(false,0),"TOP_PANEL_NOTCH_SKETCH_SELECT_FAILED");
                        model.SketchModifyTranslate(0,0,0,(height-1917)/1000);
                        records.Add(new{file=plan.file_name,sketch="草图8",heightTranslationMm=height-1917,beforeTransform=beforeTransform,afterTransform=sketch.ModelToSketchTransform.ArrayData});
                    }
                    if(depth<450&&plan.file_name=="上盖 模型.sldprt"){
                        Dimension reliefSpan=model.Parameter("D1@草图7") as Dimension;
                        records.Add(new{inspection="shallow_top_relief_driver",path=model.GetPathName(),missing=reliefSpan==null,valueMm=reliefSpan==null?(double?)null:reliefSpan.SystemValue*1000});
                        ParametricContext.Require(reliefSpan!=null&&Math.Abs(reliefSpan.SystemValue*1000-300)<.001,"SHALLOW_TOP_RELIEF_SOURCE_DRIFT");
                        reliefSpan.SystemValue=Math.Min(300,depth-100)/1000;
                        foreach(var entry in shallowTopPoints){
                            Feature f=FindFeature(model,entry.Key);Sketch sketch=f.GetSpecificFeature2() as Sketch;
                            var points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
                            ParametricContext.Require(points.Length==entry.Value.Length,"SHALLOW_TOP_POINT_COUNT_CHANGED");
                            model.ClearSelection2(true);f.Select2(false,0);model.EditSketch();
                            for(int i=0;i<points.Length;i++){double[] p=entry.Value[i];ParametricContext.Require(points[i].SetCoords(p[0],p[1],p[2]),"SHALLOW_TOP_POINT_MOVE_FAILED");}
                            model.SketchManager.InsertSketch(true);
                        }
                        if(height!=1917)UpdateShallowSketchHeight(sw,model,records,"草图10",2,1906,1911.1);
                    }
                    var links=new List<object>();
                    Feature feature=model.FirstFeature() as Feature;
                    while(feature!=null){
                        string type=feature.GetTypeName2();
                        if(type=="SplitBody"||type=="MirrorStock"){
                            object files,parts,features,types,statuses,entities,components;int option;string config;
                            feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out option,out config);
                            Array references=files as Array;
                            if(references!=null)foreach(object file in references)ParametricContext.CheckPath(Convert.ToString(file),folder);
                            bool broken=statuses is Array&&((Array)statuses).Cast<object>().Any(value=>Convert.ToInt32(value)==(int)swExternalReferenceStatus_e.swExternalReferenceBroken);
                            bool locked=statuses is Array&&((Array)statuses).Cast<object>().Any(value=>Convert.ToInt32(value)==(int)swExternalReferenceStatus_e.swExternalReferenceLocked);
                            if(!broken){
                                if(locked)feature.UpdateExternalFileReferences(option,config,(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesunlockAll);
                                feature.UpdateExternalFileReferences(option,config,(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone);
                                model.ForceRebuild3(false);
                                if(locked)feature.UpdateExternalFileReferences(option,config,(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll);
                            }
                            links.Add(new{feature=feature.Name,type=type,referenceFiles=files,sourceStatuses=statuses,blockedByBrokenReference=broken});
                        }
                        feature=feature.GetNextFeature() as Feature;
                    }
                    if(depth<450&&plan.file_name=="箱体横层板L.sldprt"){
                        Dimension spacing=model.Parameter("D3@阵列(线性)1") as Dimension;
                        ParametricContext.Require(spacing!=null&&Math.Abs(spacing.SystemValue*1000-250)<.001,"SHALLOW_SHELF_PATTERN_SOURCE_DRIFT");
                        spacing.SystemValue=ShallowShelfPitch(depth)/1000;
                        records.Add(new{inspection="shallow_shelf_pattern_driver",dimension="D3@阵列(线性)1",targetPitchMm=ShallowShelfPitch(depth)});
                    }
                    if(height!=1917&&plan.file_name=="箱体侧板加强筋1.sldprt"){
                        CaptureShallowVerticalFeatures(model,records,"height-pattern-before");
                        UpdateVerticalFormingPatternHeight(model,records,height);
                    }
                    if(height!=1917&&plan.file_name=="箱体右侧板.sldprt")UpdateRightRearHolePatternHeight(model,records,height);
                    if(height!=1917&&plan.file_name=="箱体左侧板.sldprt")UpdateLeftRearHolePatternHeight(sw,model,records,height,folder,out contextRoot);
                    if(height!=1917&&plan.file_name=="箱体竖隔板L.sldprt"){
                        Dimension datum=model.Parameter("D5@草图19") as Dimension;
                        double target=360+height-1917;
                        ParametricContext.Require(datum!=null&&datum.DrivenState==2&&(Math.Abs(datum.SystemValue*1000-360)<.001||Math.Abs(datum.SystemValue*1000-target)<.001),"ENVELOPE_PARTITION_HEIGHT_DATUM_DRIFT");
                        datum.SystemValue=target/1000;
                        records.Add(new{inspection="envelope_partition_height_datum",targetMm=target,readbackMm=datum.SystemValue*1000});
                        RefreshPartAssemblyContext(sw,model,records,folder,"草图21",out contextRoot);
                    }
                    bool rebuilt=model.ForceRebuild3(false);PartSnapshot after=CapturePartSnapshot(model);
                    if(height!=1917&&(depth<450||plan.file_name=="箱体右侧板.sldprt")){
                        if(plan.file_name=="箱体竖隔板L.sldprt")UpdateShallowSketchHeight(sw,model,records,"草图9",4,1702.25,1703.75,false,FrameGridHeightTranslation(height));
                        if(plan.file_name=="箱体左侧板.sldprt")UpdateShallowSketchHeight(sw,model,records,"草图19",3,1542.6960116849175,1818.7429580306243);
                        if(plan.file_name=="箱体右侧板.sldprt")UpdateShallowSketchHeight(sw,model,records,"草图19",2,1787.4,1787.4);
                        rebuilt=model.ForceRebuild3(false);after=CapturePartSnapshot(model);
                        if(plan.file_name=="箱体横层板L.sldprt"){
                            Sketch shelf=FindFeature(model,"草图17").GetSpecificFeature2() as Sketch;
                            double[] origin=(double[])((MathPoint)((MathPoint)((MathUtility)sw.GetMathUtility()).CreatePoint(new[]{0.0,0.0,0.0})).MultiplyTransform(shelf.ModelToSketchTransform.Inverse() as MathTransform)).ArrayData;
                            records.Add(new{inspection="shallow_shelf_mount_plane",worldYmm=origin[1]*1000,targetYmm=1716.7+FrameGridHeightTranslation(height)});
                            ParametricContext.Require(Math.Abs(origin[1]*1000-(1716.7+FrameGridHeightTranslation(height)))<.001,"SHALLOW_SHELF_MOUNT_HEIGHT_FAILED");
                        }
                    }
                    if(depth<450&&plan.file_name=="箱体横层板L.sldprt")CaptureShallowShelfDefinitions(model,records,"after");
                    if(depth<450&&plan.file_name=="上盖壳体左侧板.sldprt"){
                        RebuildShallowTopSide(sw,model,records,height,depth);
                        rebuilt=model.ForceRebuild3(false);after=CapturePartSnapshot(model);
                    }
                    if((depth<450||height!=1917)&&plan.file_name=="箱体侧板加强筋1.sldprt"){
                        // Deeper cabinets retain this stiffener's original front depth; its forming seed still follows the height grid.
                        RelocateShallowVerticalTool(sw,model,records,depth<450?depth:550);
                        if(depth<450)RelocateShallowVerticalHoles(sw,model,records,depth);
                        rebuilt=model.ForceRebuild3(false);after=CapturePartSnapshot(model);
                    }
                    if(depth<450&&plan.file_name=="上盖 模型.sldprt")records.Add(new{inspection="shallow_top_bodies_after",bodies=((Array)((PartDoc)model).GetBodies2(0,false)).Cast<Body2>().Select(b=>new{name=b.Name,box=b.GetBodyBox(),mass=b.GetMassProperties(1)}).ToArray()});
                    if(depth<550)CaptureShallowDeleteFaces(model,records,"after");
                    bool healthy=DerivedPartHealthGate(after,baseline[plan.file_name]);
                    if(healthy&&height!=1917&&(plan.file_name=="箱体右侧板.sldprt"||plan.file_name=="箱体左侧板.sldprt")){
                        CaptureMasterHeightLinks(model,records,"side-height-driven",new[]{"草图18","切除-拉伸5","阵列(线性)1","草图19","切除-拉伸6"});
                        VerifyRearHolePatternHeight(model,records,height,"before-save",plan.file_name=="箱体左侧板.sldprt");
                        VerifySideMountHeight(sw,model,records,height,depth,"before-save",plan.file_name=="箱体左侧板.sldprt");
                    }
                    if(depth<450&&plan.file_name=="箱体侧板加强筋1.sldprt")CaptureShallowVerticalFeatures(model,records,"envelope-after");
                    if(!healthy)records.Add(new{inspection="envelope_derived_failure",file=plan.file_name,details=CaptureDerivedRebuildFailure(model)});
                    if(!healthy&&(plan.file_name=="箱体右侧板.sldprt"||plan.file_name=="箱体左侧板.sldprt"))CaptureMasterHeightLinks(model,records,"side-height-failure",new[]{"草图18","切除-拉伸5","阵列(线性)1","草图19","切除-拉伸6"});
                    if(height!=1917&&plan.file_name=="箱体竖隔板L.sldprt")CaptureMasterHeightLinks(model,records,"partition-height-driven",new[]{"草图19","草图20","凸台-拉伸3","草图21","切除-拉伸8"});
                    if(apply&&plan.file_name!="箱体竖隔板R.SLDPRT"){
                        double[] expected=(double[])baseline[plan.file_name].part_box_m.Clone();
                        bool fixedHeight=plan.file_name.StartsWith("底座")||plan.file_name=="门框 下.sldprt"||plan.file_name=="箱体竖隔板加强件.sldprt";
                        bool translatedHeight=plan.file_name.StartsWith("上盖")||plan.file_name.StartsWith("箱体横层板")||plan.file_name.StartsWith("门框 横隔板")||plan.file_name=="门框 上.sldprt";
                        double heightTranslation=plan.file_name.StartsWith("箱体横层板")||plan.file_name.StartsWith("门框 横隔板")?FrameGridHeightTranslation(height):height-1917;
                        if(!fixedHeight)expected[4]+=heightTranslation/1000;
                        if(translatedHeight)expected[1]+=heightTranslation/1000;
                        bool extendsDepth=new[]{"底座 模型.sldprt","底座底板.sldprt","底座外框.sldprt","上盖 模型.sldprt","上盖壳体底板.sldprt","上盖壳体后侧板.sldprt","上盖壳体左侧板.sldprt","上盖壳体右侧板.SLDPRT","箱体横层板L.sldprt","箱体横层板R.SLDPRT","箱体右侧板.sldprt","箱体左侧板.sldprt","箱体竖隔板L.sldprt","箱体侧板加强筋2.sldprt"}.Contains(plan.file_name);
                        if(extendsDepth)expected[2]-=(depth-550)/1000;
                        if(plan.file_name=="箱体侧板加强筋2.sldprt"||(depth<550&&plan.file_name=="上盖壳体后侧板.sldprt"))expected[5]-=(depth-550)/1000;
                        if(depth<450&&plan.file_name=="箱体竖隔板加强件.sldprt"){
                            double length=ShallowBraceLength(depth),front=ShallowShelfFront(depth),rear=ShallowShelfBack(depth);
                            expected[2]=-rear/1000;expected[5]=-front/1000;
                        }
                        if(depth<450&&plan.file_name=="箱体侧板加强筋1.sldprt"){
                        double front=ShallowShelfFront(depth);
                            expected[2]=-(front+25)/1000;expected[5]=-(front-25)/1000;
                        }
                        records.Add(new{inspection="envelope_target_boundary",file=plan.file_name,healthy=healthy,beforeGeometry=baseline[plan.file_name],afterGeometry=after,expectedBox=expected});
                        ParametricContext.Require(healthy&&after.part_box_m.Zip(expected,(a,b)=>Math.Abs(a-b)<.0002).All(x=>x),"ENVELOPE_NATIVE_TARGET_BOUNDARY_FAILED "+plan.file_name);
                    }
                    var issues=new List<object>();
                    feature=model.FirstFeature() as Feature;
                    while(feature!=null){
                        if(!feature.IsSuppressed()&&feature.GetErrorCode()>0)issues.Add(new{name=feature.Name,type=feature.GetTypeName2(),code=feature.GetErrorCode()});
                        feature=feature.GetNextFeature() as Feature;
                    }
                    if(!healthy){
                        var progression=new List<object>();
                        foreach(string step in new[]{"基体零件-标准寄存柜 模型-1","删除面1","切除-拉伸9","切除-拉伸5","切除-拉伸8","凸台-拉伸1","凸台-拉伸2","切除-拉伸6"}){
                            bool rolled=model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToAfterFeature,step);
                            var bodies=new List<object>();Array actual=((PartDoc)model).GetBodies2(0,false) as Array;
                            if(actual!=null)foreach(Body2 body in actual)bodies.Add(new{name=body.Name,box=body.GetBodyBox()});
                            progression.Add(new{afterFeature=step,rolled=rolled,bodies=bodies});
                        }
                        model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,"");model.ForceRebuild3(false);
                        records.Add(new{file=plan.file_name,diagnosticBodyProgression=progression});
                        var extrusions=new List<object>();feature=model.FirstFeature() as Feature;
                        while(feature!=null){
                            ExtrudeFeatureData2 extrusion=feature.GetDefinition() as ExtrudeFeatureData2;
                            if(extrusion!=null&&extrusion.AccessSelections(model,null)){
                                try{Array scopes=extrusion.FeatureScopeBodies as Array;extrusions.Add(new{name=feature.Name,reverse=extrusion.ReverseDirection,both=extrusion.BothDirections,end1=extrusion.GetEndCondition(true),depth1=extrusion.GetDepth(true),end2=extrusion.GetEndCondition(false),depth2=extrusion.GetDepth(false),from=extrusion.FromType,fromOffset=extrusion.FromOffsetDistance,featureScope=extrusion.FeatureScope,autoSelect=extrusion.AutoSelect,bodyScopeCount=scopes==null?0:scopes.Length,merge=extrusion.Merge});}finally{extrusion.ReleaseSelectionAccess();}
                            }
                            feature=feature.GetNextFeature() as Feature;
                        }
                        records.Add(new{file=plan.file_name,diagnosticExtrusions=extrusions});
                        var sketches=new List<object>();feature=model.FirstFeature() as Feature;
                        while(feature!=null){
                            Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                            if(sketch!=null){
                                var dimensions=new List<object>();DisplayDimension display=feature.GetFirstDisplayDimension() as DisplayDimension;
                                while(display!=null){Dimension d=display.GetDimension2(0);dimensions.Add(new{name=d.FullName,value=d.SystemValue*1000,driven=d.DrivenState});display=feature.GetNextDisplayDimension(display) as DisplayDimension;}
                                var relations=new List<object>();Array all=sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swAll) as Array;
                                if(all!=null)foreach(SketchRelation relation in all){var points=new List<object>();Array entities=relation.GetEntities() as Array;if(entities!=null)foreach(object entity in entities){SketchPoint point=entity as SketchPoint;if(point!=null)points.Add(new[]{point.X,point.Y,point.Z});}relations.Add(new{type=relation.GetRelationType(),points=points});}
                                sketches.Add(new{name=feature.Name,error=feature.GetErrorCode(),transform=sketch.ModelToSketchTransform.ArrayData,dimensions=dimensions,relations=relations,externalRelationCount=sketch.RelationManager.GetRelationsCount((int)swSketchRelationFilterType_e.swExternal)});
                            }
                            feature=feature.GetNextFeature() as Feature;
                        }
                        records.Add(new{file=plan.file_name,diagnosticSketches=sketches});
                    }
                    if(rebuilt&&healthy){
                        if(depth<450&&plan.file_name=="箱体侧板加强筋1.sldprt")VerifyShallowVerticalFlat(model,records);
                        if(depth<450&&plan.file_name=="上盖壳体右侧板.SLDPRT")VerifyShallowTopSideFlat(model,records);
                        NativeParametricAssembly.Save(model);
                        if(depth<450&&(plan.file_name=="上盖壳体左侧板.sldprt"||plan.file_name=="上盖壳体右侧板.SLDPRT")){
                            sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);model=null;
                            model=NativeParametricAssembly.Open(sw,path,true);ParametricContext.Require(model.ForceRebuild3(false),"SHALLOW_TOP_SIDE_REOPEN_REBUILD_FAILED");
                            PartSnapshot reopened=CapturePartSnapshot(model);
                            records.Add(new{inspection="shallow_top_side_reopened",file=plan.file_name,snapshot=reopened,cylinders=ShallowTopMountCylinders(model)});
                            ParametricContext.Require(DerivedPartHealthGate(reopened,baseline[plan.file_name])&&reopened.part_box_m.Zip(after.part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v),"SHALLOW_TOP_SIDE_REOPEN_CHANGED");
                        }
                        if(height!=1917&&(plan.file_name=="箱体右侧板.sldprt"||plan.file_name=="箱体左侧板.sldprt")){
                            string title=model.GetTitle();
                            if(source!=null){sw.CloseDoc(source.GetTitle());Marshal.ReleaseComObject(source);source=null;}
                            if(contextRoot!=null){sw.CloseDoc(contextRoot.GetTitle());Marshal.ReleaseComObject(contextRoot);contextRoot=null;}
                            sw.CloseDoc(title);Marshal.ReleaseComObject(model);model=null;
                            model=NativeParametricAssembly.Open(sw,path,true);ParametricContext.Require(model.ForceRebuild3(false),"RIGHT_REAR_HOLE_REOPEN_REBUILD_FAILED");
                            PartSnapshot reopened=CapturePartSnapshot(model);
                            VerifyRearHolePatternHeight(model,records,height,"saved-reopened",plan.file_name=="箱体左侧板.sldprt");
                            VerifySideMountHeight(sw,model,records,height,depth,"saved-reopened",plan.file_name=="箱体左侧板.sldprt");
                            CaptureMasterHeightLinks(model,records,"side-saved-reopened",new[]{"草图18","切除-拉伸5","阵列(线性)1","草图19","切除-拉伸6"});
                            records.Add(new{inspection="rear_hole_saved_reopened",file=plan.file_name,snapshot=reopened});
                            ParametricContext.Require(DerivedPartHealthGate(reopened,baseline[plan.file_name])&&reopened.body_count==after.body_count&&reopened.part_box_m.Zip(after.part_box_m,(a,b)=>Math.Abs(a-b)<.000001).All(v=>v),"RIGHT_REAR_HOLE_REOPEN_CHANGED");
                        }
                    }
                    records.Add(new{diagnosticOnly=true,file=plan.file_name,beforeGeometry=baseline[plan.file_name],afterGeometry=after,links=links,issues=issues,rebuilt=rebuilt,healthy=healthy,saved=rebuilt&&healthy});
                    Console.WriteLine("ENVELOPE_LINK_MEASURED "+plan.file_name+" healthy="+healthy);
                }finally{
                    if(model!=null){sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
                    if(source!=null){sw.CloseDoc(source.GetTitle());Marshal.ReleaseComObject(source);}
                    if(contextRoot!=null){sw.CloseDoc(contextRoot.GetTitle());Marshal.ReleaseComObject(contextRoot);}
                }
            }
            if(apply&&height!=1917){
                foreach(string side in new[]{"L","R"}){
                    ModelDoc2 assembly=NativeParametricAssembly.Open(sw,ExactFile(folder,"箱体横层板"+side+"焊接.SLDASM"),false);
                    try{
                        int activationError=0;sw.ActivateDoc3(assembly.GetTitle(),false,0,ref activationError);
                        foreach(Component2 component in (Array)((AssemblyDoc)assembly).GetComponents(true)){
                            if(!Path.GetFileName(component.GetPathName()).Equals("箱体横层板加强筋.SLDPRT",StringComparison.OrdinalIgnoreCase))continue;
                            double[] transform=(double[])component.Transform2.ArrayData;
                            NativeParametricAssembly.SetPlacement(sw,assembly,component,transform.Take(9).ToArray(),transform[9]*1000,transform[10]*1000+FrameGridHeightTranslation(height),transform[11]*1000);
                            records.Add(new{assembly="箱体横层板"+side+"焊接",component=component.Name2,heightTranslationMm=FrameGridHeightTranslation(height)});
                        }
                        NativeParametricAssembly.Save(assembly);
                    }finally{sw.CloseDoc(assembly.GetTitle());Marshal.ReleaseComObject(assembly);}
                }
            }
            records.Add(new{diagnosticOnly=!apply,targetHeightMm=height,targetDepthMm=depth,deliveryEligible=false,reason="Assembly interfaces and physical acceptance remain required."});
        }
    }
}
