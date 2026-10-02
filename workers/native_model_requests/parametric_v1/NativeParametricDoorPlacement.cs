using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private static void RepairBaseMates(ISldWorks sw,List<object> records)
        {
            ModelDoc2 model=Open(sw,Find("底座焊接.SLDASM"),false);
            try{
                int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);model.ForceRebuild3(false);
                var components=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().ToList();
                var positions=components.ToDictionary(c=>c.Name2,c=>(double[])c.Transform2.ArrayData);
                Feature group=model.FirstFeature() as Feature;while(group!=null&&group.GetTypeName2()!="MateGroup")group=group.GetNextFeature() as Feature;
                ParametricContext.Require(group!=null,"BASE_MATE_GROUP_MISSING");
                var broken=new List<Feature>();Feature feature=group.GetFirstSubFeature() as Feature;
                while(feature!=null){if(!feature.IsSuppressed()&&feature.GetErrorCode()>0)broken.Add(feature);feature=feature.GetNextSubFeature() as Feature;}
                foreach(Feature mateFeature in broken)ParametricContext.Require(new[]{"重合1","同心7"}.Contains(mateFeature.Name),"BASE_MATE_REPAIR_SCOPE_CHANGED "+mateFeature.Name);
                var alignments=new Dictionary<string,int>();
                foreach(Feature mateFeature in broken){
                    Mate2 original=mateFeature.GetSpecificFeature2() as Mate2;
                    double[] a=(double[])original.MateEntity(0).EntityParams,b=(double[])original.MateEntity(1).EntityParams;
                    double dot=a[3]*b[3]+a[4]*b[4]+a[5]*b[5];
                    ParametricContext.Require(Math.Abs(Math.Abs(dot)-1)<.000001,"BASE_MATE_AXES_NOT_PARALLEL");
                    alignments[mateFeature.Name]=dot>0?(int)SolidWorks.Interop.swconst.swMateAlign_e.swMateAlignALIGNED:(int)SolidWorks.Interop.swconst.swMateAlign_e.swMateAlignANTI_ALIGNED;
                }
                foreach(Feature mateFeature in broken)ParametricContext.Require(mateFeature.SetSuppression2(0,1,null),"BASE_MATE_SUPPRESSION_FAILED");
                foreach(Feature mateFeature in broken){
                    Mate2 mate=mateFeature.GetSpecificFeature2() as Mate2;ParametricContext.Require(mate!=null&&mate.GetMateEntityCount()==2,"BASE_MATE_ENTITIES_CHANGED");
                    Entity a=mate.MateEntity(0).Reference as Entity,b=mate.MateEntity(1).Reference as Entity;
                    model.ClearSelection2(true);SelectData data=((SelectionMgr)model.SelectionManager).CreateSelectData();data.Mark=1;
                    ParametricContext.Require(a!=null&&b!=null&&a.Select4(false,data)&&b.Select4(true,data),"BASE_MATE_REFERENCE_SELECT_FAILED");
                    int alignment=alignments[mateFeature.Name];
                    int error;Mate2 replacement=((AssemblyDoc)model).AddMate5(mate.Type,alignment,false,0,0,0,1,1,0,0,0,false,false,0,out error);
                    records.Add(new{mate=mateFeature.Name,alignment=alignment,created=replacement!=null,error=error});
                    ParametricContext.Require(replacement!=null&&error==(int)SolidWorks.Interop.swconst.swAddMateError_e.swAddMateError_NoError,"BASE_MATE_RECREATE_FAILED");
                    model.ClearSelection2(true);ParametricContext.Require(mateFeature.Select2(false,0)&&model.Extension.DeleteSelection2(0),"BASE_OLD_MATE_DELETE_FAILED");
                }
                Save(model);feature=group.GetFirstSubFeature() as Feature;
                while(feature!=null){ParametricContext.Require(feature.IsSuppressed()||feature.GetErrorCode()==0,"BASE_MATE_REBUILD_FAILED "+feature.Name);feature=feature.GetNextSubFeature() as Feature;}
                foreach(Component2 component in components){
                    double[] before=positions[component.Name2],after=(double[])component.Transform2.ArrayData;
                    if(!before.Zip(after,(a,b)=>Math.Abs(a-b)<.000001).All(x=>x)){
                        records.Add(new{component=component.Name2,beforeTransform=before,afterTransform=after});
                        SetPlacement(sw,model,component,before.Take(9).ToArray(),before[9]*1000,before[10]*1000,before[11]*1000);
                    }
                    ParametricContext.Require(before.Zip((double[])component.Transform2.ArrayData,(a,b)=>Math.Abs(a-b)<.000001).All(x=>x),"BASE_MATE_REPAIR_MOVED_COMPONENT "+component.Name2);
                }
                Save(model);
                records.Add(new{repairedCount=broken.Count,componentTransformsUnchanged=true});
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }
        private static void RepairDepthPlacements(ISldWorks sw,List<object> records)
        {
            double delta=ParametricContext.DepthMm-550;if(Math.Abs(delta)<.001)return;
            ModelDoc2 bottom=Open(sw,Find("底座底板.sldprt"),false);
            try{
                int activationError=0;sw.ActivateDoc3(bottom.GetTitle(),false,0,ref activationError);
                Feature feature=bottom.FirstFeature() as Feature;while(feature!=null&&feature.Name!="草图9")feature=feature.GetNextFeature() as Feature;
                ParametricContext.Require(feature!=null,"DEPTH_KNOCKOUT_SKETCH_MISSING");Sketch sketch=feature.GetSpecificFeature2() as Sketch;
                bottom.ClearSelection2(true);ParametricContext.Require(feature.Select2(false,0),"DEPTH_KNOCKOUT_SKETCH_SELECT_FAILED");bottom.EditSketch();
                MathUtility math=(MathUtility)sw.GetMathUtility();int moved=0;
                foreach(SketchBlockInstance block in (Array)sketch.GetSketchBlockInstances()){
                    double[] p=(double[])block.InstancePosition.ArrayData;
                    if(Math.Abs(p[1]*1000-74.5)<.001)continue;
                    ParametricContext.Require(Math.Abs(p[1]*1000-500.5)<.001||Math.Abs(p[1]*1000-(500.5+delta))<.001,"DEPTH_KNOCKOUT_ROW_DRIFT");
                    block.InstancePosition=(MathPoint)math.CreatePoint(new[]{p[0],(500.5+delta)/1000,p[2]});moved++;
                }
                bottom.SketchManager.InsertSketch(true);ParametricContext.Require(moved==2,"DEPTH_REAR_KNOCKOUT_COUNT_CHANGED");Save(bottom);
                var measured=((Array)sketch.GetSketchBlockInstances()).Cast<SketchBlockInstance>().Select(b=>((double[])b.InstancePosition.ArrayData)[1]*1000).OrderBy(y=>y).ToArray();
                ParametricContext.Require(measured.Length==4&&Math.Abs(measured[0]-74.5)<.001&&Math.Abs(measured[1]-74.5)<.001&&Math.Abs(measured[2]-500.5-delta)<.001&&Math.Abs(measured[3]-500.5-delta)<.001,"DEPTH_KNOCKOUT_READBACK_FAILED");
                records.Add(new{part="底座底板.sldprt",measuredHoleDepthCentersMm=measured});
            }finally{sw.CloseDoc(bottom.GetTitle());Marshal.ReleaseComObject(bottom);}
            var assemblies=new Dictionary<string,Dictionary<string,double>>{
                {"底座焊接.SLDASM",new Dictionary<string,double>{{"底座加强筋-2",-575},{"螺母M12-3",-500.5},{"螺母M12-4",-500.5}}},
                {"标准寄存柜1917×1000×550(总装配).SLDASM",new Dictionary<string,double>{{"调整脚 M12X60(模型)-7",-500.5},{"调整脚 M12X60(模型)-8",-500.5}}},
            };
            foreach(var entry in assemblies){
                ModelDoc2 model=Open(sw,Find(entry.Key),false);
                try{
                    int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);
                    model.ForceRebuild3(false);
                    var components=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().ToList();
                    foreach(var placement in entry.Value){
                        Component2 component=components.Single(c=>c.Name2==placement.Key);double[] t=(double[])component.Transform2.ArrayData;
                        double target=placement.Value-delta;
                        ParametricContext.Require(Math.Abs(t[11]*1000-placement.Value)<.001||Math.Abs(t[11]*1000-target)<.001,"DEPTH_COMPONENT_POSITION_DRIFT "+component.Name2);
                        SetPlacement(sw,model,component,t.Take(9).ToArray(),t[9]*1000,t[10]*1000,target);
                        records.Add(new{assembly=entry.Key,component=component.Name2,sourceZmm=placement.Value,targetZmm=target});
                    }
                    Save(model);
                }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
            if(ParametricContext.DepthMm<450)RepairShallowShelfBraces(sw,records);
        }
        private static void RepairShallowShelfBraces(ISldWorks sw,List<object> records)
        {
            double front=NativeParametricWidth.ShallowShelfFront(ParametricContext.DepthMm),pitch=NativeParametricWidth.ShallowShelfPitch(ParametricContext.DepthMm);
            double targetY=1705.7+NativeParametricWidth.FrameGridHeightTranslation(ParametricContext.HeightMm);
            foreach(string side in new[]{"L","R"}){
                string name="箱体横层板"+side+"焊接.SLDASM";
                ModelDoc2 model=Open(sw,Find(name),false);
                try{
                    int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);
                    var components=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().ToList();
                    var before=components.ToDictionary(c=>c.Name2,c=>(double[])c.Transform2.ArrayData);
                    var braces=components.Where(c=>Path.GetFileName(c.GetPathName()).Equals("箱体横层板加强筋.SLDPRT",StringComparison.OrdinalIgnoreCase)).ToList();
                    ParametricContext.Require(components.Count==3&&braces.Count==2,"SHALLOW_SHELF_COMPONENT_COUNT_CHANGED "+name);
                    foreach(Component2 component in braces){
                        bool first=component.Name2=="箱体横层板加强筋-1";
                        ParametricContext.Require(first||component.Name2=="箱体横层板加强筋-2","SHALLOW_SHELF_BRACE_IDENTITY_CHANGED");
                        double[] t=before[component.Name2];double sourceZ=first?-148.5:-398.5,targetZ=-(front+(first?0:pitch));
                        ParametricContext.Require(component.IsFixed()&&t.Take(9).Zip(new[]{1.0,0,0,0,1,0,0,0,1},(a,b)=>Math.Abs(a-b)<.000001).All(x=>x),"SHALLOW_SHELF_BRACE_TRANSFORM_DRIFT");
                        ParametricContext.Require((Math.Abs(t[11]*1000-sourceZ)<.001||Math.Abs(t[11]*1000-targetZ)<.001)&&(Math.Abs(t[10]*1000-1705.7)<.001||Math.Abs(t[10]*1000-targetY)<.001),"SHALLOW_SHELF_BRACE_POSITION_DRIFT");
                        SetPlacement(sw,model,component,t.Take(9).ToArray(),t[9]*1000,targetY,targetZ);
                        double[] after=(double[])component.Transform2.ArrayData;
                        ParametricContext.Require(component.IsFixed()&&Enumerable.Range(0,16).Where(i=>i!=10&&i!=11).All(i=>Math.Abs(after[i]-t[i])<.000001)&&Math.Abs(after[10]*1000-targetY)<.001&&Math.Abs(after[11]*1000-targetZ)<.001,"SHALLOW_SHELF_BRACE_PLACEMENT_FAILED");
                        records.Add(new{inspection="shallow_shelf_brace_placement",assembly=name,component=component.Name2,before=t,after=after,targetYmm=targetY,targetZmm=targetZ,fixedComponent=component.IsFixed()});
                    }
                    Save(model);
                    foreach(Component2 component in components.Where(c=>!braces.Contains(c)))ParametricContext.Require(before[component.Name2].Zip((double[])component.Transform2.ArrayData,(a,b)=>Math.Abs(a-b)<.000001).All(x=>x),"SHALLOW_SHELF_PANEL_MOVED");
                }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
                model=Open(sw,Find(name),true);
                try{
                    ParametricContext.Require(model.ForceRebuild3(false),"SHALLOW_SHELF_REOPEN_REBUILD_FAILED");
                    foreach(Component2 component in ((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().Where(c=>Path.GetFileName(c.GetPathName()).Equals("箱体横层板加强筋.SLDPRT",StringComparison.OrdinalIgnoreCase))){
                        double[] t=(double[])component.Transform2.ArrayData;double targetZ=-(front+(component.Name2=="箱体横层板加强筋-1"?0:pitch));
                        ParametricContext.Require(component.IsFixed()&&Math.Abs(t[10]*1000-targetY)<.001&&Math.Abs(t[11]*1000-targetZ)<.001,"SHALLOW_SHELF_REOPEN_PLACEMENT_FAILED");
                        records.Add(new{inspection="shallow_shelf_brace_reopened",assembly=name,component=component.Name2,transform=t,fixedComponent=component.IsFixed()});
                    }
                }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
        }
        private static void RepairHeightBraces(ISldWorks sw,List<object> records)
        {
            if(ParametricContext.HeightMm==1917)return;
            double targetDepthDatumMm=ParametricContext.DepthMm<450?240+(ParametricContext.DepthMm-550)/2:240;
            double targetZmm=-(68.5+targetDepthDatumMm);
            Func<ModelDoc2,string,List<double[]>> inspectCylinders=delegate(ModelDoc2 part,string stage){
                var cylinders=new List<object>();var geometry=new List<double[]>();
                Array bodies=((PartDoc)part).GetBodies2(0,false) as Array;
                ParametricContext.Require(bodies!=null,"HEIGHT_MOUNT_BODIES_UNAVAILABLE "+part.GetPathName());
                foreach(Body2 body in bodies){
                    Array faces=body.GetFaces() as Array;ParametricContext.Require(faces!=null,"HEIGHT_MOUNT_FACES_UNAVAILABLE "+part.GetPathName());
                    foreach(Face2 face in faces){
                        Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsCylinder())continue;
                        double[] c=surface.CylinderParams as double[];ParametricContext.Require(c!=null&&c.Length>=7,"HEIGHT_MOUNT_CYLINDER_UNAVAILABLE");
                        if(Math.Abs(Math.Abs(c[3])-1)>=.000001||Math.Abs(c[6]-.002)>=.000001)continue;
                        double[] box=face.GetBox() as double[];ParametricContext.Require(box!=null&&box.Length==6,"HEIGHT_MOUNT_FACE_BOX_UNAVAILABLE");
                        geometry.Add(c.Take(7).Concat(box).ToArray());
                        cylinders.Add(new{radiusMm=c[6]*1000,originMm=new[]{c[0]*1000,c[1]*1000,c[2]*1000},axis=new[]{c[3],c[4],c[5]},faceBoxMm=box.Select(value=>value*1000).ToArray()});
                    }
                }
                records.Add(new{inspection="height_brace_mount_cylinders",stage=stage,part=Path.GetFileName(part.GetPathName()),path=part.GetPathName(),coordinateSystem="part_local",lengthUnit="mm",targetZmm=targetZmm,cylinders=cylinders});
                return geometry;
            };
            List<double[]> rightSourceCylinders=null;
            foreach(string name in new[]{"箱体竖隔板L.sldprt","箱体竖隔板R参数源.SLDPRT"}){
                ModelDoc2 part=Open(sw,Find(name),false);
                try{
                    Dimension datum=part.Parameter("D5@草图19") as Dimension;
                    Dimension depthDatum=part.Parameter("D4@草图19") as Dimension;
                    double target=360+ParametricContext.HeightMm-1917;
                    ParametricContext.Require(depthDatum!=null&&depthDatum.DrivenState==2&&Math.Abs(depthDatum.SystemValue*1000-targetDepthDatumMm)<.001,"HEIGHT_PARTITION_DEPTH_DATUM_DRIFT "+name);
                    double beforeD4mm=depthDatum.SystemValue*1000,beforeD5mm=datum==null?double.NaN:datum.SystemValue*1000;
                    records.Add(new{inspection="height_brace_mount_datums",stage="before",part=name,D4mm=beforeD4mm,D5mm=datum==null?(double?)null:beforeD5mm,targetDatumMm=target,targetZmm=targetZmm});
                    ParametricContext.Require(datum!=null&&datum.DrivenState==2&&(Math.Abs(datum.SystemValue*1000-360)<.001||Math.Abs(datum.SystemValue*1000-target)<.001),"HEIGHT_PARTITION_MOUNT_DATUM_DRIFT");
                    datum.SystemValue=target/1000;datum=null;depthDatum=null;Save(part);
                    datum=part.Parameter("D5@草图19") as Dimension;depthDatum=part.Parameter("D4@草图19") as Dimension;
                    ParametricContext.Require(datum!=null&&depthDatum!=null,"HEIGHT_PARTITION_DATUM_READBACK_MISSING "+name);
                    var cylinders=inspectCylinders(part,"after_save");
                    var mountY=cylinders.Where(c=>Math.Abs(c[2]*1000-targetZmm)<.001).Select(c=>c[1]*1000).ToList();
                    records.Add(new{part=name,datum="D5@草图19",targetDatumMm=target,beforeD4mm=beforeD4mm,afterD4mm=depthDatum.SystemValue*1000,beforeD5mm=beforeD5mm,afterD5mm=datum.SystemValue*1000,targetZmm=targetZmm,preservedMountYmm=mountY});
                    ParametricContext.Require(Math.Abs(depthDatum.SystemValue*1000-beforeD4mm)<.001&&Math.Abs(datum.SystemValue*1000-target)<.001,"HEIGHT_PARTITION_DATUM_READBACK_FAILED "+name);
                    foreach(double y in new[]{298.2,583.2,1213.2,1498.2})ParametricContext.Require(mountY.Any(actual=>Math.Abs(actual-y)<.01),"HEIGHT_MOUNT_Y_ALIGNMENT_FAILED "+name);
                    if(name=="箱体竖隔板R参数源.SLDPRT")rightSourceCylinders=cylinders;
                }finally{sw.CloseDoc(part.GetTitle());Marshal.ReleaseComObject(part);}
            }
            ModelDoc2 right=Open(sw,Find("箱体竖隔板R.SLDPRT"),false);try{Save(right);}finally{sw.CloseDoc(right.GetTitle());Marshal.ReleaseComObject(right);}
            right=Open(sw,Find("箱体竖隔板R.SLDPRT"),true);
            try{
                bool rebuilt=right.ForceRebuild3(false);var issues=new List<object>();Feature feature=right.FirstFeature() as Feature;
                while(feature!=null){
                    if(!feature.IsSuppressed()&&feature.GetErrorCode()>0)issues.Add(new{name=feature.Name,type=feature.GetTypeName2(),code=feature.GetErrorCode()});
                    Feature child=feature.GetFirstSubFeature() as Feature;
                    while(child!=null){if(!child.IsSuppressed()&&child.GetErrorCode()>0)issues.Add(new{name=child.Name,type=child.GetTypeName2(),code=child.GetErrorCode()});child=child.GetNextSubFeature() as Feature;}
                    feature=feature.GetNextFeature() as Feature;
                }
                records.Add(new{inspection="height_brace_right_reopen",path=right.GetPathName(),rebuilt=rebuilt,featureIssues=issues});
                var rightCylinders=inspectCylinders(right,"reopened");
                ParametricContext.Require(rebuilt&&issues.Count==0,"HEIGHT_RIGHT_PARTITION_REOPEN_FAILED");
                ParametricContext.Require(rightSourceCylinders!=null,"HEIGHT_RIGHT_PARTITION_SOURCE_MEASUREMENT_MISSING");
                var unmatched=rightCylinders.ToList();int matched=0;
                foreach(double[] source in rightSourceCylinders){
                    int index=unmatched.FindIndex(c=>Math.Abs(c[1]-source[1])<.000001&&Math.Abs(c[2]-source[2])<.000001&&Math.Abs(c[6]-source[6])<.000001&&
                        Math.Abs(Math.Abs(-c[3]*source[3]+c[4]*source[4]+c[5]*source[5])-1)<.000001&&
                        Math.Abs(c[7]+source[10])<.000001&&Math.Abs(c[10]+source[7])<.000001&&
                        Math.Abs(c[8]-source[8])<.000001&&Math.Abs(c[9]-source[9])<.000001&&Math.Abs(c[11]-source[11])<.000001&&Math.Abs(c[12]-source[12])<.000001);
                    if(index>=0){unmatched.RemoveAt(index);matched++;}
                }
                bool mirrorConsistent=rightSourceCylinders.Count>0&&matched==rightSourceCylinders.Count&&unmatched.Count==0;
                records.Add(new{inspection="height_brace_right_mount_mirror",sourcePart="箱体竖隔板R参数源.SLDPRT",part="箱体竖隔板R.SLDPRT",targetZmm=targetZmm,sourceCylinderCount=rightSourceCylinders.Count,rightCylinderCount=rightCylinders.Count,matchedCylinderCount=matched,mirrorConsistent=mirrorConsistent});
                ParametricContext.Require(mirrorConsistent,"HEIGHT_RIGHT_MOUNT_MIRROR_MISMATCH");
            }finally{sw.CloseDoc(right.GetTitle());Marshal.ReleaseComObject(right);}
            NativeParametricWidth.RepairMaintenanceStrip(sw,Find("锁控维护条_源钣金.SLDPRT"),records);
            ModelDoc2 model=Open(sw,Find("标准寄存柜1917×1000×550(总装配).SLDASM"),false);
            try{
                int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);
                var components=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().Where(c=>Path.GetFileName(c.GetPathName()).Equals("箱体竖隔板加强件.sldprt",StringComparison.OrdinalIgnoreCase)).ToList();
                ParametricContext.Require(components.Count==2,"HEIGHT_BRACE_COUNT_CHANGED");
                foreach(Component2 component in components){
                    double[] t=(double[])component.Transform2.ArrayData;
                    double sourceY=component.Name2.EndsWith("-1")?0:component.Name2.EndsWith("-2")?915:double.NaN;
                    double targetY=sourceY;
                    ParametricContext.Require(Math.Abs(t[10]*1000-sourceY)<.001||Math.Abs(t[10]*1000-(sourceY+ParametricContext.HeightMm-1917))<.001,"HEIGHT_BRACE_POSITION_DRIFT");
                    SetPlacement(sw,model,component,t.Take(9).ToArray(),t[9]*1000,targetY,t[11]*1000);
                    records.Add(new{component=component.Name2,beforeYmm=t[10]*1000,afterYmm=((double[])component.Transform2.ArrayData)[10]*1000});
                }
                var maintenance=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().Where(c=>Path.GetFileName(c.GetPathName()).Equals("锁控维护条_源钣金.SLDPRT",StringComparison.OrdinalIgnoreCase)).ToList();
                ParametricContext.Require(maintenance.Count==1,"MAINTENANCE_STRIP_COMPONENT_COUNT_CHANGED");
                Component2 strip=maintenance[0];double[] before=(double[])strip.Transform2.ArrayData;bool fixedBefore=strip.IsFixed();
                double maintenanceY=ParametricContext.HeightMm/2-15.5;
                ParametricContext.Require(Math.Abs(before[10]*1000-943)<.001||Math.Abs(before[10]*1000-maintenanceY)<.001,"MAINTENANCE_STRIP_POSITION_DRIFT");
                SetPlacement(sw,model,strip,before.Take(9).ToArray(),before[9]*1000,maintenanceY,before[11]*1000);
                double[] after=(double[])strip.Transform2.ArrayData;
                ParametricContext.Require(before.Where((v,i)=>i!=10).Zip(after.Where((v,i)=>i!=10),(a,b)=>Math.Abs(a-b)<.000001).All(v=>v)&&strip.IsFixed()==fixedBefore,"MAINTENANCE_STRIP_TRANSFORM_DRIFT");
                records.Add(new{inspection="maintenance_strip_placement",component=strip.Name2,beforeTransform=before,afterTransform=after,fixedComponent=strip.IsFixed(),targetYmm=maintenanceY});
                Save(model);
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }
        private static List<double> FrameSlotCenters(ModelDoc2 model)
        {
            var centers=new List<double>();
            foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                Surface surface=(Surface)face.GetSurface();if(!surface.IsPlane())continue;
                double[] box=(double[])face.GetBox();
                if(Math.Abs(box[2]*1000+16.7)<.01&&Math.Abs(box[5]*1000+16.7)<.01&&Math.Abs((box[4]-box[1])*1000-3.6)<.01)centers.Add((box[4]+box[1])*500);
            }
            return centers;
        }
        private static void RepairFrameSlots(ISldWorks sw,List<object> records)
        {
            sw.DocumentVisible(true,(int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocPART);
            var boundaries=ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan,"contract"),"boundaries");
            double unit=(ParametricContext.HeightMm-87)/12;
            double[] grid=Enumerable.Range(1,11).Select(i=>28.5+i*unit).ToArray();
            Action<ModelDoc2,string,string> verifyGrid=delegate(ModelDoc2 part,string file,string stage){
                bool rebuilt=part.ForceRebuild3(false);
                var walls=new List<double[]>();var issues=new List<object>();
                foreach(Body2 body in (Array)((PartDoc)part).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                    Surface surface=face.GetSurface() as Surface;if(surface==null||!surface.IsPlane())continue;
                    double[] box=(double[])face.GetBox();
                    if(Math.Abs(box[2]*1000+16.7)<.01&&Math.Abs(box[5]*1000+16.7)<.01&&Math.Abs((box[4]-box[1])*1000-3.6)<.01)walls.Add(box.Select(v=>v*1000).ToArray());
                }
                Feature feature=part.FirstFeature() as Feature;
                while(feature!=null){
                    if(!feature.IsSuppressed()&&feature.GetErrorCode()>0)issues.Add(new{name=feature.Name,error=feature.GetErrorCode()});
                    Feature child=feature.GetFirstSubFeature() as Feature;
                    while(child!=null){if(!child.IsSuppressed()&&child.GetErrorCode()>0)issues.Add(new{name=child.Name,error=child.GetErrorCode()});child=child.GetNextSubFeature() as Feature;}
                    feature=feature.GetNextFeature() as Feature;
                }
                double[] centers=walls.Select(b=>(b[1]+b[4])/2).OrderBy(y=>y).ToArray();
                records.Add(new{inspection="frame_native_slot_grid",file=file,stage=stage,unitPitchMm=unit,expectedCentersMm=grid,measuredCentersMm=centers,wallBoxesMm=walls,rebuilt=rebuilt,featureIssues=issues});
                ParametricContext.Require(rebuilt&&issues.Count==0&&grid.All(y=>centers.Count(x=>Math.Abs(x-y)<.01)==1)&&walls.All(b=>Math.Abs(b[3]-b[0]-1.2)<.01),"FRAME_NATIVE_SLOT_GRID_MISMATCH "+file+" "+stage);
            };
            foreach(string side in new[]{"L","R"})foreach(bool central in new[]{false,true}){
                string name=central?"门框 竖隔板"+side+(side=="L"?".sldprt":".SLDPRT"):"门框 "+(side=="L"?"左":"右")+".sldprt";
                ModelDoc2 model=Open(sw,Find(name),false);
                try{
                    int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);model.ForceRebuild3(false);
                    double[] before=BodyBox(model);
                    if(side=="L"){
                        string pattern=central?"阵列(线性)1":"阵列(线性)2";
                        Dimension pitch=model.Parameter("D3@"+pattern) as Dimension,count=model.Parameter("D1@"+pattern) as Dimension;
                        ParametricContext.Require(pitch!=null&&count!=null&&pitch.DrivenState==2&&count.DrivenState==2&&(Math.Abs(pitch.SystemValue*1000-152.5)<.001||Math.Abs(pitch.SystemValue*1000-unit)<.001)&&Math.Abs(count.SystemValue-11)<.000001,"FRAME_NATIVE_PATTERN_SOURCE_DRIFT "+name);
                        double sourcePitch=pitch.SystemValue*1000;pitch.SystemValue=unit/1000;
                        records.Add(new{inspection="frame_native_pattern_driver",file=name,pattern=pattern,beforePitchMm=sourcePitch,targetPitchMm=unit,readbackPitchMm=pitch.SystemValue*1000,instanceCount=count.SystemValue});
                        ParametricContext.Require(Math.Abs(pitch.SystemValue*1000-unit)<.001&&Math.Abs(count.SystemValue-11)<.000001,"FRAME_NATIVE_PATTERN_READBACK_FAILED "+name);
                        pitch=null;count=null;Save(model);
                    }
                    verifyGrid(model,name,"native-driven");
                    List<double> existing=FrameSlotCenters(model);
                    double[] expected=((IEnumerable)boundaries[side]).Cast<Dictionary<string,object>>().Select(row=>ParametricContext.Number(row,"doorGapCenterYmm")).ToArray();
                    double[] missing=expected.Where(y=>!existing.Any(x=>Math.Abs(x-y)<.01)).ToArray();
                    if(missing.Length>0){
                        double x=(side=="L"?-1:1)*(central?36.8:ParametricContext.WidthMm/2-20);
                        Face2 selected=null;double area=0;
                        foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                            Surface surface=(Surface)face.GetSurface();if(!surface.IsPlane())continue;double[] plane=(double[])surface.PlaneParams;
                            if(Math.Abs(Math.Abs(plane[0])-1)<.000001&&Math.Abs(plane[3]*1000-x)<.001&&face.GetArea()>area){selected=face;area=face.GetArea();}
                        }
                        model.ClearSelection2(true);ParametricContext.Require(selected!=null&&((Entity)selected).Select4(false,null),"FRAME_SLOT_FACE_MISSING "+name);
                        model.SketchManager.InsertSketch(true);Sketch sketch=model.SketchManager.ActiveSketch;
                        ParametricContext.Require(sketch!=null,"FRAME_SLOT_SKETCH_CREATE_FAILED");
                        MathUtility math=(MathUtility)sw.GetMathUtility();MathTransform transform=sketch.ModelToSketchTransform;
                        foreach(double y in missing){
                            MathPoint a=(MathPoint)math.CreatePoint(new[]{x/1000,(y-1.8)/1000,-.019});
                            MathPoint b=(MathPoint)math.CreatePoint(new[]{x/1000,(y+1.8)/1000,-.0167});
                            double[] p=(double[])((MathPoint)a.MultiplyTransform(transform)).ArrayData,q=(double[])((MathPoint)b.MultiplyTransform(transform)).ArrayData;
                            records.Add(new{file=name,slotCenterMm=y,sketchCornerA=p,sketchCornerB=q,activeSketch=model.SketchManager.ActiveSketch!=null});
                            model.SketchManager.AddToDB=true;
                            try{
                                double xmin=Math.Min(p[0],q[0]),xmax=Math.Max(p[0],q[0]),ymin=Math.Min(p[1],q[1]),ymax=Math.Max(p[1],q[1]);
                                ParametricContext.Require(model.SketchManager.CreateLine(xmin,ymin,0,xmax,ymin,0)!=null&&
                                    model.SketchManager.CreateLine(xmax,ymin,0,xmax,ymax,0)!=null&&
                                    model.SketchManager.CreateLine(xmax,ymax,0,xmin,ymax,0)!=null&&
                                    model.SketchManager.CreateLine(xmin,ymax,0,xmin,ymin,0)!=null,"FRAME_SLOT_RECTANGLE_FAILED");
                            }finally{model.SketchManager.AddToDB=false;}
                        }
                        model.SketchManager.InsertSketch(true);
                        Feature cut=model.FeatureManager.FeatureCut3(true,false,false,0,0,.003,.003,false,false,false,false,0,0,false,false,false,false,true,true,true,false,false,false,0,0,false);
                        ParametricContext.Require(cut!=null,"FRAME_SLOT_NATIVE_CUT_FAILED "+name);cut.Name="PARAMETRIC_CROSSBAR_SLOTS_"+side;
                        Save(model);
                    }
                    List<double> measured=FrameSlotCenters(model);
                    records.Add(new{file=name,existingCentersMm=existing,requestedCentersMm=expected,addedCentersMm=missing,measuredCentersMm=measured});
                    ParametricContext.Require(expected.All(y=>measured.Any(x=>Math.Abs(x-y)<.01)),"FRAME_SLOT_MEASURED_CENTER_MISMATCH "+name);
                    ParametricContext.Require(before.Zip(BodyBox(model),(a,b)=>Math.Abs(a-b)<.001).All(x=>x),"FRAME_SLOT_ENVELOPE_CHANGED "+name);
                    Save(model);
                }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
            foreach(string side in new[]{"L","R"})foreach(bool central in new[]{false,true}){
                string name=central?"门框 竖隔板"+side+(side=="L"?".sldprt":".SLDPRT"):"门框 "+(side=="L"?"左":"右")+".sldprt";
                ModelDoc2 model=Open(sw,Find(name),true);
                try{
                    verifyGrid(model,name,"saved-reopened");
                    if(side=="L"){
                        string pattern=central?"阵列(线性)1":"阵列(线性)2";
                        Dimension pitch=model.Parameter("D3@"+pattern) as Dimension,count=model.Parameter("D1@"+pattern) as Dimension;
                        ParametricContext.Require(pitch!=null&&count!=null&&Math.Abs(pitch.SystemValue*1000-unit)<.001&&Math.Abs(count.SystemValue-11)<.000001,"FRAME_NATIVE_PATTERN_PERSISTENCE_FAILED "+name);
                        records.Add(new{inspection="frame_native_pattern_persistence",file=name,pattern=pattern,readbackPitchMm=pitch.SystemValue*1000,instanceCount=count.SystemValue});
                    }
                    double[] expected=((IEnumerable)boundaries[side]).Cast<Dictionary<string,object>>().Select(row=>ParametricContext.Number(row,"doorGapCenterYmm")).ToArray();
                    List<double> measured=FrameSlotCenters(model);
                    ParametricContext.Require(expected.All(y=>measured.Any(x=>Math.Abs(x-y)<.01)),"FRAME_SLOT_REOPEN_CENTER_MISMATCH "+name);
                }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
            foreach(string name in new[]{"门框焊接.sldasm","标准寄存柜1917×1000×550(总装配).SLDASM"}){
                ModelDoc2 model=Open(sw,Find(name),false);try{Save(model);}finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
        }
        private static void RepairPartitionMates(ISldWorks sw,List<object> records)
        {
            ModelDoc2 model=Open(sw,Find("箱体竖隔板R焊接.SLDASM"),false);
            try{
                int activationError=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref activationError);
                model.ForceRebuild3(false);
                var components=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().ToList();
                var before=components.ToDictionary(c=>c.Name2,c=>(double[])c.Transform2.ArrayData);
                bool shallow=ParametricContext.DepthMm<450;
                double frontMm=NativeParametricWidth.ShallowShelfFront(ParametricContext.DepthMm),backMm=NativeParametricWidth.ShallowShelfBack(ParametricContext.DepthMm);
                records.Add(new{inspection="partition_mate_transforms",stage="before",components=before});
                Feature group=model.FirstFeature() as Feature;while(group!=null&&group.GetTypeName2()!="MateGroup")group=group.GetNextFeature() as Feature;
                ParametricContext.Require(group!=null,"PARTITION_MATE_GROUP_MISSING");
                var mates=new List<Feature>();Feature child=group.GetFirstSubFeature() as Feature;
                while(child!=null){if(!child.IsSuppressed()&&child.GetErrorCode()>0)mates.Add(child);child=child.GetNextSubFeature() as Feature;}
                var originalMateErrors=mates.ToDictionary(feature=>feature.Name,feature=>feature.GetErrorCode());
                foreach(Feature invalid in mates)ParametricContext.Require(invalid.SetSuppression2(0,1,null),"PARTITION_OLD_MATE_SUPPRESS_FAILED");
                foreach(Feature feature in mates){
                    Mate2 mate=feature.GetSpecificFeature2() as Mate2;
                    ParametricContext.Require(mate!=null&&mate.GetMateEntityCount()==2,"PARTITION_MATE_ENTITY_COUNT_CHANGED");
                    records.Add(new{inspection="partition_mate_state",stage="before",mate=feature.Name,type=feature.GetTypeName2(),error=originalMateErrors[feature.Name],suppressed=false,entities=Enumerable.Range(0,2).Select(index=>{MateEntity2 entity=mate.MateEntity(index);Component2 component=entity.ReferenceComponent;return new{component=component==null?null:component.Name2,referencePresent=entity.Reference!=null,parameters=entity.EntityParams};}).ToArray()});
                    var references=new object[2];int replaced=0;
                    for(int i=0;i<2;i++){
                        MateEntity2 entity=mate.MateEntity(i);references[i]=entity.Reference;if(references[i]!=null)continue;
                        Component2 component=entity.ReferenceComponent;
                        ParametricContext.Require(component!=null&&Path.GetFileName(component.GetPathName()).Equals("箱体竖隔板R.SLDPRT",StringComparison.OrdinalIgnoreCase),"PARTITION_MATE_UNEXPECTED_DANGLING_COMPONENT");
                        double[] transform=(double[])component.Transform2.ArrayData;
                        ParametricContext.Require(transform.Take(9).SequenceEqual(new[]{1.0,0,0,0,1,0,0,0,1})&&transform.Skip(9).Take(3).All(x=>Math.Abs(x)<.000001),"PARTITION_MATE_REFERENCE_TRANSFORM_CHANGED");
                        double[] old=(double[])entity.EntityParams;ModelDoc2 part=(ModelDoc2)component.GetModelDoc2();var matches=new List<Face2>();var matchParameters=new List<double[]>();
                        string mateType=feature.GetTypeName2();double targetY=old[1]+(old[1]>1?(ParametricContext.HeightMm-1917)/1000:0),targetZ=old[2];
                        if(shallow&&mateType=="MateConcentric"){
                            ParametricContext.Require(old.Length>=7&&Math.Abs(Math.Abs(old[3])-1)<.000001&&Math.Abs(old[4])<.000001&&Math.Abs(old[5])<.000001&&Math.Abs(old[6]-.001)<.000001,"PARTITION_MATE_SHALLOW_SOURCE_SIGNATURE_CHANGED "+feature.Name);
                            double[] sourceDepthsMm=new[]{148.5,133.5,413.5,398.5},targetDepthsMm=new[]{frontMm,frontMm-15,backMm+15,backMm};
                            int[] sourceGroups=Enumerable.Range(0,sourceDepthsMm.Length).Where(index=>Math.Abs(old[2]*1000+sourceDepthsMm[index])<.001).ToArray();
                            ParametricContext.Require(sourceGroups.Length==1,"PARTITION_MATE_SHALLOW_SOURCE_DEPTH_CHANGED "+feature.Name);
                            targetZ=-targetDepthsMm[sourceGroups[0]]/1000;
                        }
                        foreach(Body2 body in (Array)((PartDoc)part).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces()){
                            Surface surface=(Surface)face.GetSurface();
                            if(mateType=="MateConcentric"&&surface.IsCylinder()){
                                double[] p=(double[])surface.CylinderParams;
                                if(Math.Abs(Math.Abs(p[3])-1)<.000001&&Math.Abs(p[1]-targetY)<.000001&&Math.Abs(p[2]-targetZ)<.000001&&Math.Abs(p[6]-old[6])<.000001){matches.Add(face);matchParameters.Add(p);}
                            }else if(mateType=="MateDistanceDim"&&surface.IsPlane()){
                                double[] p=(double[])surface.PlaneParams;
                                if(Math.Abs(Math.Abs(p[0])-1)<.000001&&Math.Abs(p[3]-old[0])<.000001){matches.Add(face);matchParameters.Add(p);}
                            }
                        }
                        object targetSignature=mateType=="MateConcentric"?(object)new{absoluteAxisX=1.0,yMm=targetY*1000,zMm=targetZ*1000,radiusMm=old[6]*1000}:new{absoluteNormalX=1.0,pointXmm=old[0]*1000};
                        records.Add(new{inspection="partition_mate_face_match",mate=feature.Name,component=component.Name2,type=mateType,oldEntityParameters=old,targetSignature=targetSignature,actualFaceParameters=matchParameters.ToArray(),uniqueCount=matches.Count,shallowDepthMapped=shallow&&mateType=="MateConcentric"});
                        ParametricContext.Require(matches.Count==1,"PARTITION_MATE_FACE_NOT_UNIQUE "+feature.Name+" "+matches.Count);
                        references[i]=component.GetCorrespondingEntity(matches[0]);ParametricContext.Require(references[i]!=null,"PARTITION_MATE_FACE_CONTEXT_MISSING");replaced++;
                    }
                    ParametricContext.Require(replaced==1,"PARTITION_MATE_REPAIR_SCOPE_CHANGED");
                    object definition=feature.GetDefinition();
                    if(feature.GetTypeName2()=="MateDistanceDim"){
                        DistanceMateFeatureData previous=(DistanceMateFeatureData)definition;
                        records.Add(new{mate=feature.Name,preservedDistanceMm=previous.Distance*1000,preservedAlignment=previous.MateAlignment,preservedFlip=previous.FlipDimension});
                    }
                    else ParametricContext.Require(feature.GetTypeName2()=="MateConcentric","PARTITION_MATE_TYPE_UNSUPPORTED");
                    model.ClearSelection2(true);
                    SelectData selection=((SelectionMgr)model.SelectionManager).CreateSelectData();selection.Mark=1;
                    ParametricContext.Require(((Entity)references[0]).Select4(false,selection)&&((Entity)references[1]).Select4(true,selection),"PARTITION_MATE_REFERENCE_SELECT_FAILED");
                    double distance=feature.GetTypeName2()=="MateDistanceDim"?((DistanceMateFeatureData)definition).Distance:0;
                    bool flip=feature.GetTypeName2()=="MateDistanceDim"&&((DistanceMateFeatureData)definition).FlipDimension;
                    bool lockRotation=feature.GetTypeName2()=="MateConcentric"&&((ConcentricMateFeatureData)definition).LockRotation;
                    int mateError=0;
                    Mate2 replacement=((AssemblyDoc)model).AddMate5(mate.Type,mate.Alignment,flip,distance,distance,distance,1,1,0,0,0,false,lockRotation,0,out mateError);
                    records.Add(new{mate=feature.Name,replacementCreated=replacement!=null,replacedFaceReferenceCount=replaced,mateError=mateError,preservedType=mate.Type,preservedAlignment=mate.Alignment,preservedDistanceMm=distance*1000,preservedFlip=flip,preservedLockRotation=lockRotation});
                    ParametricContext.Require(replacement!=null&&mateError==(int)SolidWorks.Interop.swconst.swAddMateError_e.swAddMateError_NoError,"PARTITION_MATE_CREATE_FAILED "+feature.Name+" "+mateError);
                    model.ClearSelection2(true);ParametricContext.Require(feature.Select2(false,0)&&model.Extension.DeleteSelection2(0),"PARTITION_OLD_MATE_DELETE_FAILED");
                }
                Save(model);
                child=group.GetFirstSubFeature() as Feature;while(child!=null){
                    Mate2 mate=child.GetSpecificFeature2() as Mate2;
                    if(mate!=null){
                        var entities=Enumerable.Range(0,mate.GetMateEntityCount()).Select(index=>{MateEntity2 entity=mate.MateEntity(index);Component2 component=entity.ReferenceComponent;return new{component=component==null?null:component.Name2,referencePresent=entity.Reference!=null,parameters=entity.EntityParams};}).ToArray();
                        records.Add(new{inspection="partition_mate_state",stage="after",mate=child.Name,type=child.GetTypeName2(),error=child.GetErrorCode(),suppressed=child.IsSuppressed(),entities=entities});
                        ParametricContext.Require(child.IsSuppressed()||entities.All(entity=>entity.referencePresent),"PARTITION_MATE_REFERENCE_MISSING "+child.Name);
                    }
                    ParametricContext.Require(child.IsSuppressed()||child.GetErrorCode()==0,"PARTITION_MATE_REBUILD_ERROR "+child.Name);child=child.GetNextSubFeature() as Feature;
                }
                bool transformsUnchanged=true;int verifiedShallowBraces=0;
                foreach(Component2 component in components){
                    double[] original=before[component.Name2],after=(double[])component.Transform2.ArrayData;
                    bool unchanged=original.Length==after.Length&&original.Zip(after,(a,b)=>Math.Abs(a-b)<.000001).All(x=>x);transformsUnchanged&=unchanged;
                    bool shallowBrace=shallow&&Path.GetFileName(component.GetPathName()).Equals("箱体侧板加强筋1.sldprt",StringComparison.OrdinalIgnoreCase)&&(component.Name2=="箱体侧板加强筋1-1"||component.Name2=="箱体侧板加强筋1-2");
                    double? targetZMm=shallowBrace?(double?)(component.Name2=="箱体侧板加强筋1-1"?0:-(backMm-frontMm)):null;
                    records.Add(new{inspection="partition_mate_transform_result",component=component.Name2,before=original,after=after,unchanged=unchanged,targetZMm=targetZMm});
                    if(shallowBrace){
                        double[] identity=new[]{1.0,0,0,0,1,0,0,0,1};
                        ParametricContext.Require(original.Length==16&&after.Length==16&&Enumerable.Range(0,9).All(index=>Math.Abs(original[index]-identity[index])<.000001)&&Enumerable.Range(0,16).Where(index=>index!=11).All(index=>Math.Abs(after[index]-original[index])<.000001)&&Math.Abs(after[11]-targetZMm.Value/1000)<.000001,"PARTITION_MATE_SHALLOW_BRACE_TRANSFORM_MISMATCH "+component.Name2);
                        verifiedShallowBraces++;
                    }else ParametricContext.Require(unchanged,"PARTITION_MATE_REPAIR_MOVED_COMPONENT "+component.Name2);
                }
                ParametricContext.Require(!shallow||verifiedShallowBraces==2,"PARTITION_MATE_SHALLOW_BRACE_COMPONENT_COUNT_CHANGED");
                records.Add(new{repairedMates=mates.Count,componentTransformsUnchanged=transformsUnchanged,verifiedShallowBraceCount=verifiedShallowBraces,shallowBracePositionsVerified=shallow});
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }
        private static void ProbePartitionMates(ISldWorks sw,List<object> records,string fileName="箱体竖隔板R焊接.SLDASM")
        {
            ModelDoc2 model=Open(sw,Find(fileName),true);
            try{
                model.ForceRebuild3(false);
                foreach(Component2 component in (Array)((AssemblyDoc)model).GetComponents(true))records.Add(new{component=component.Name2,path=component.GetPathName(),fixedComponent=component.IsFixed(),transform=component.Transform2.ArrayData});
                Feature feature=model.FirstFeature() as Feature;
                while(feature!=null){
                    if(feature.GetTypeName2()=="MateGroup"){
                        Feature mateFeature=feature.GetFirstSubFeature() as Feature;
                        while(mateFeature!=null){
                            Mate2 mate=mateFeature.GetSpecificFeature2() as Mate2;var entities=new List<object>();
                            if(mate!=null)for(int i=0;i<mate.GetMateEntityCount();i++){
                                MateEntity2 entity=mate.MateEntity(i);Component2 component=entity.ReferenceComponent;
                                entities.Add(new{component=component==null?null:component.Name2,referenceType=entity.ReferenceType2,referencePresent=entity.Reference!=null,parameters=entity.EntityParams});
                            }
                            DisplayDimension display=mate==null?null:mate.get_DisplayDimension2(0);Dimension dimension=display==null?null:display.GetDimension2(0);
                            records.Add(new{mate=mateFeature.Name,type=mateFeature.GetTypeName2(),dimension=dimension==null?null:dimension.FullName,valueMm=dimension==null?0:dimension.SystemValue*1000,error=mateFeature.GetErrorCode(),suppressed=mateFeature.IsSuppressed(),entities=entities});
                            mateFeature=mateFeature.GetNextSubFeature() as Feature;
                        }
                    }
                    feature=feature.GetNextFeature() as Feature;
                }
            }finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
        }
        internal static void PrepareBaseFootSketch(ISldWorks sw,ModelDoc2 model,List<object> records)
        {
                double target=ParametricContext.WidthMm/2-35;
                Feature footSketch=model.FirstFeature() as Feature;
                while(footSketch!=null&&footSketch.Name!="草图_调整脚Ø15_X335_V33")footSketch=footSketch.GetNextFeature() as Feature;
                ParametricContext.Require(footSketch!=null,"BASE_FOOT_NATIVE_SKETCH_MISSING");
                Sketch sketch=(Sketch)footSketch.GetSpecificFeature2();
                var circles=((Array)sketch.GetSketchSegments()).Cast<object>().OfType<SketchArc>().Where(arc=>arc.IsCircle()!=0&&Math.Abs(arc.GetRadius()-.0075)<.000001).ToList();
                ParametricContext.Require(circles.Count==2,"BASE_FOOT_NATIVE_CIRCLE_COUNT_MISMATCH");
                MathUtility math=(MathUtility)sw.GetMathUtility();
                MathTransform toSketch=sketch.ModelToSketchTransform;
                MathTransform toModel=(MathTransform)toSketch.Inverse();
                model.ClearSelection2(true);footSketch.Select2(false,0);model.EditSketch();
                foreach(SketchArc arc in circles)
                {
                    SketchPoint center=arc.IGetCenterPoint2();
                    MathPoint point=(MathPoint)math.CreatePoint(new[]{center.X,center.Y,center.Z});
                    double[] world=(double[])((MathPoint)point.MultiplyTransform(toModel)).ArrayData;
                    ParametricContext.Require(Math.Abs(Math.Abs(world[0])*1000-335)<.001||Math.Abs(Math.Abs(world[0])*1000-target)<.001,"BASE_FOOT_CIRCLE_POSITION_DRIFT");
                    double before=world[0]*1000;world[0]=Math.Sign(world[0])*target/1000;
                    MathPoint targetPoint=(MathPoint)math.CreatePoint(world);
                    double[] coordinates=(double[])((MathPoint)targetPoint.MultiplyTransform(toSketch)).ArrayData;
                    ParametricContext.Require(center.SetCoords(coordinates[0],coordinates[1],coordinates[2]),"BASE_FOOT_CIRCLE_MOVE_FAILED");
                    records.Add(new{sketch=footSketch.Name,beforeXmm=before,targetXmm=world[0]*1000});
                }
                model.SketchManager.InsertSketch(true);
        }
        private static void RepairBaseFootHoles(ISldWorks sw,List<object> records)
        {
            ModelDoc2 model=Open(sw,Find("底座加强筋.sldprt"),false);
            try
            {
                PrepareBaseFootSketch(sw,model,records);
                Save(model);
                var axes=new List<double>();
                foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces())
                {
                    Surface surface=(Surface)face.GetSurface();if(!surface.IsCylinder())continue;
                    double[] values=(double[])surface.CylinderParams;
                    if(Math.Abs(values[6]-.0075)<.000001&&Math.Abs(values[4])>.999)axes.Add(values[0]*1000);
                }
                double expected=ParametricContext.WidthMm/2-35;
                records.Add(new{measuredCylinderXmm=axes,expectedFootXmm=new[]{-expected,expected}});
                ParametricContext.Require(axes.Any(x=>Math.Abs(x-expected)<.001)&&axes.Any(x=>Math.Abs(x+expected)<.001),"BASE_FOOT_REAL_CYLINDER_ALIGNMENT_FAILED");
                records.Add(new{actualCylinderXmm=axes,expectedFootXmm=new[]{-expected,expected}});
            }
            finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            foreach(string name in new[]{"底座焊接.SLDASM","标准寄存柜1917×1000×550(总装配).SLDASM"})
            {
                model=Open(sw,Find(name),false);try{Save(model);}finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
        }
        private static void ProbeBaseHoles(ISldWorks sw,List<object> records)
        {
            foreach(string path in new[]{Find("底座加强筋.sldprt"),Path.Combine(ParametricContext.AttemptDirectory,"door_sources","base_stiffener_source.SLDPRT")})
            {
                ModelDoc2 model=Open(sw,path,true);
                try
                {
                    var cylinders=new List<object>();
                    foreach(Body2 body in (Array)((PartDoc)model).GetBodies2(0,false))foreach(Face2 face in (Array)body.GetFaces())
                    {
                        Surface surface=(Surface)face.GetSurface();if(surface.IsCylinder())cylinders.Add(surface.CylinderParams);
                    }
                    Feature feature=model.FirstFeature() as Feature;
                    while(feature!=null&&feature.Name!="草图12")feature=feature.GetNextFeature() as Feature;
                    Sketch sketch=(Sketch)feature.GetSpecificFeature2();var circles=new List<object>();
                    foreach(object segment in (Array)sketch.GetSketchSegments())
                    {
                        SketchArc arc=segment as SketchArc;if(arc==null||arc.IsCircle()==0)continue;
                        SketchPoint center=arc.IGetCenterPoint2();circles.Add(new{x=center.X,y=center.Y,z=center.Z,radius=arc.GetRadius()});
                    }
                    records.Add(new{file=path,cylinders=cylinders,sketchCircles=circles});
                }
                finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
        }
        private static void RepairBoundaries(ISldWorks sw, List<object> records)
        {
            var map=ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan,"contract"),"boundaries");
            foreach(bool frame in new[]{true,false})
            {
                ModelDoc2 model=Open(sw,Find(frame?"门框焊接.sldasm":"标准寄存柜1917×1000×550(总装配).SLDASM"),false);
                try
                {
                    int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
                    var components=((Array)((AssemblyDoc)model).GetComponents(true)).Cast<Component2>().ToList();
                    foreach(string side in new[]{"L","R"})
                    {
                        string leaf=frame?"门框 横隔板"+(side=="R"?"R":"")+".SLDPRT":"箱体横层板"+side+"焊接.SLDASM";
                        var targets=components.Where(c=>Path.GetFileName(c.GetPathName()).Equals(leaf,StringComparison.OrdinalIgnoreCase)&&!c.IsSuppressed()).OrderBy(c=>((double[])c.Transform2.ArrayData)[10]).ToList();
                        var boundaries=((IEnumerable)map[side]).Cast<Dictionary<string,object>>().ToList();
                        ParametricContext.Require(targets.Count==boundaries.Count,"BOUNDARY_COUNT_MISMATCH");
                        for(int i=0;i<targets.Count;i++)
                        {
                            var t=(double[])targets[i].Transform2.ArrayData;
                            double y=ParametricContext.Number(boundaries[i],frame?"crossbarCenterYmm":"shelfCenterYmm")-(frame?1700:1705);
                            SetPlacement(sw,model,targets[i],t.Take(9).ToArray(),t[9]*1000,y,t[11]*1000);
                            records.Add(new{name=targets[i].Name2,side=side,yMm=y});
                        }
                    }
                    Save(model);
                }
                finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
            }
        }
        internal static void SetPlacement(ISldWorks sw, ModelDoc2 model, Component2 component, double[] rotation, double x, double y, double z)
        {
            ParametricContext.AssertMutation(model.GetPathName());
            AssemblyDoc assembly = (AssemblyDoc)model;
            model.ClearSelection2(true);
            ParametricContext.Require(component.Select4(false, null, false), "DOOR_PLACEMENT_SELECT_FAILED");
            bool fixedBefore = component.IsFixed();
            if (fixedBefore) assembly.UnfixComponent();
            MathUtility math = (MathUtility)sw.GetMathUtility();
            MathTransform target = (MathTransform)math.CreateTransform(rotation.Concat(new[] { x/1000, y/1000, z/1000, 1.0,0,0,0 }).ToArray());
            ParametricContext.Require(component.SetTransformAndSolve2(target), "DOOR_PLACEMENT_SET_FAILED " + component.Name2);
            double[] actual=(double[])component.Transform2.ArrayData;
            ParametricContext.Require(Math.Abs(actual[9]*1000-x)<.001&&Math.Abs(actual[10]*1000-y)<.001&&Math.Abs(actual[11]*1000-z)<.001,"COMPONENT_PLACEMENT_READBACK_FAILED "+component.Name2);
            if (fixedBefore) { component.Select4(false, null, false); assembly.FixComponent(); }
            model.ClearSelection2(true);
            Marshal.ReleaseComObject(target); Marshal.ReleaseComObject(math);
        }

        private static void RepairDoorPlacements(ISldWorks sw, List<object> records)
        {
            double[] identity = {1,0,0,0,1,0,0,0,1};
            double[] flipXZ = {-1,0,0,0,1,0,0,0,-1};
            double[] flipYZ = {1,0,0,0,-1,0,0,0,-1};
            double[] bottomLatch = {-1,0,0,0,0,-1,0,-1,0};
            var sources = ParametricContext.Read(Path.Combine(ParametricContext.AttemptDirectory, "door_sources.json"));
            foreach (string directory in Directory.GetDirectories(Path.Combine(ParametricContext.AttemptDirectory, "doors")))
            {
                double height = double.Parse(Path.GetFileName(directory).Substring(1).Replace('p','.'), CultureInfo.InvariantCulture);
                foreach (string side in new[] {"left","right"})
                {
                    string token = "W" + ParametricContext.DoorWidthMm.ToString("0.######", CultureInfo.InvariantCulture).Replace('.','p') + "_H" + height.ToString("0.######", CultureInfo.InvariantCulture).Replace('.','p');
                    string weld = Path.Combine(directory,"assemblies","door_weld_"+side+"_"+token+".SLDASM");
                    string top = Path.Combine(directory,"parts","top_latch_"+side+"_"+token+".SLDPRT");
                    string source = ParametricContext.Text(ParametricContext.Object(sources,"top_latch_"+side),"path");
                    ParametricContext.AssertMutation(top);
                    if (!File.Exists(top)) File.Copy(source,top,false);
                    ModelDoc2 model = Open(sw,weld,false);
                    try
                    {
                        int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
                        var assembly=(AssemblyDoc)model;
                        var components=((Array)assembly.GetComponents(true)).Cast<Component2>().ToList();
                        foreach(Component2 component in components)
                        {
                            double[] current=(double[])component.Transform2.ArrayData;
                            double[] rotation=null;
                            if(component.Name2.StartsWith("door_stiffener")) rotation=flipXZ;
                            else if(component.Name2.StartsWith("hinge_latch_plate_bottom")) rotation=bottomLatch;
                            else if(component.Name2.StartsWith("u_hook_pad")) rotation=side=="left"?flipXZ:flipYZ;
                            if(rotation!=null) SetPlacement(sw,model,component,rotation,current[9]*1000,current[10]*1000,current[11]*1000);
                        }
                        Component2 oldTop=components.Single(component=>component.Name2.StartsWith("hinge_latch_plate_top"));
                        model.ClearSelection2(true);oldTop.Select4(false,null,false);
                        ParametricContext.Require(assembly.ReplaceComponents2(top,"",false,0,false),"TOP_LATCH_REPLACEMENT_FAILED");
                        Component2 newTop=((Array)assembly.GetComponents(true)).Cast<Component2>().Single(component=>component.GetPathName().Equals(top,StringComparison.OrdinalIgnoreCase));
                        double sign=side=="left"?-1:1;
                        SetPlacement(sw,model,newTop,identity,sign*(ParametricContext.DoorWidthMm-307)/2,(height-298)/2,0);
                        Save(model);
                        records.Add(new {assembly=weld,method="V35 verified orientations and parameter-translated original top latch",heightMm=height});
                    }
                    finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
                    string ordinary=Path.Combine(directory,"assemblies","ordinary_door_"+side+"_"+token+".SLDASM");
                    model=Open(sw,ordinary,false);
                    try
                    {
                        int error=0;sw.ActivateDoc3(model.GetTitle(),false,0,ref error);
                        foreach(Component2 component in (Array)((AssemblyDoc)model).GetComponents(true))
                        {
                            if(component.Name2.StartsWith("door_weld"))continue;
                            double[] current=(double[])component.Transform2.ArrayData;
                            double[] rotation=component.Name2.StartsWith("plastic_bushing_top")?flipYZ:component.Name2.StartsWith("mechanical_lock_tongue")?flipXZ:identity;
                            SetPlacement(sw,model,component,rotation,current[9]*1000,current[10]*1000,current[11]*1000);
                        }
                        Save(model);
                    }
                    finally{sw.CloseDoc(model.GetTitle());Marshal.ReleaseComObject(model);}
                }
            }
            ModelDoc2 root=Open(sw,Find("标准寄存柜1917×1000×550(总装配).SLDASM"),false);
            try{Save(root);}finally{sw.CloseDoc(root.GetTitle());Marshal.ReleaseComObject(root);}
        }
    }
}
