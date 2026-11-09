// Bounded reference-mount candidate. Called only by the R20 isolated-copy host.
// No native Save call belongs here; the host owns probe/export/persistence gates.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    const double RmSin = .034899496702496716, RmCos = .9993908270190959;
    static readonly double[] RmNormal = { 0, RmCos, -RmSin };
    static readonly double[] RmDepth = { 0, RmSin, RmCos };
    static Dictionary<string,object> RmEvidence;
    sealed class RmAxis { public Face2 Face; public double[] Point, Direction; public double Radius; }
    sealed class RmPlane { public Face2 Face; public double[] Point, Normal; public double Q, Area; }
    static double RmDot(double[] a, double[] b) { return a.Zip(b, (x,y) => x*y).Sum(); }
    static double[] RmPoint(double[] p, MathTransform t) { var u = (MathUtility)Sw.GetMathUtility(); return (double[])((MathPoint)((MathPoint)u.CreatePoint(p)).MultiplyTransform(t)).ArrayData; }
    static double[] RmVector(double[] p, MathTransform t) { var u = (MathUtility)Sw.GetMathUtility(); return (double[])((MathVector)((MathVector)u.CreateVector(p)).MultiplyTransform(t)).ArrayData; }
    static double RmAxisDistance(double[] a, double[] b) { double[] d = a.Zip(b,(x,y)=>x-y).ToArray(); double q = RmDot(d,RmNormal); return Math.Sqrt(Math.Max(0,RmDot(d,d)-q*q)); }
    static void RmActivate(ModelDoc2 d, string operation) { Flush("mount_r20/" + operation); int e=0; Need(Sw.ActivateDoc3(d.GetTitle(),false,0,ref e)!=null,"Cannot activate mount target: " + d.GetPathName()); d.ClearSelection2(true); }
    static void RmConfiguration(ModelDoc2 d, string name) { if (((Configuration)d.GetActiveConfiguration()).Name!=name) Need(d.ShowConfiguration2(name),"Cannot activate mount configuration " + name); Need(((Configuration)d.GetActiveConfiguration()).Name==name,"Unexpected mount configuration"); }
    static Feature RmFeature(ModelDoc2 d, string name) { var fs=Features(d).Where(f=>f.Name==name).ToArray(); Need(fs.Length==1,"Expected exact mount feature: " + name); return fs[0]; }
    static Feature RmSketchFeature(ModelDoc2 d,Sketch sketch) { IntPtr target=Marshal.GetIUnknownForObject(sketch);Marshal.Release(target);var found=new List<Feature>();foreach(var feature in Features(d).Where(f=>f.GetTypeName2()=="ProfileFeature")){var candidate=feature.GetSpecificFeature2() as Sketch;if(candidate==null)continue;IntPtr identity=Marshal.GetIUnknownForObject(candidate);Marshal.Release(identity);if(identity==target)found.Add(feature);}Need(found.Count==1,"Cannot identify the new native sketch feature by COM identity");return found[0]; }
    static Body2[] RmBodies(ModelDoc2 d) { var a=((PartDoc)d).GetBodies2(0,false) as Array; Need(a!=null&&a.Length>0,"Missing mount solids " + d.GetPathName()); var bs=a.Cast<Body2>().ToArray(); Need(bs.All(b=>b.Check2()==0),"Invalid mount solid " + d.GetPathName()); return bs; }
    static Component2[] RmComponents(ModelDoc2 top) { return ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().ToArray(); }
    static Component2 RmComponent(ModelDoc2 top,string name) { var cs=RmComponents(top).Where(c=>c.Name2==name).ToArray(); Need(cs.Length==1,"Expected exact mount instance " + name); return cs[0]; }
    static Component2 RmBracket(ModelDoc2 top,bool left) { var cs=RmComponents(top).Where(c=>!c.IsSuppressed()&&Path.GetFileName(c.GetPathName())=="箱体雨棚安装架.SLDPRT").ToArray(); Need(cs.Length==2,"Exactly two bracket instances required"); return cs.Single(c=>(c.ReferencedConfiguration=="950_RIGHT_NATIVE")!=left); }
    static List<RmAxis> RmAxes(Component2 c,double radius) {
        object info; var raw=c.GetBodies3(0,out info) as Array; Need(raw!=null,"Missing component solids " + c.Name2); var rows=new List<RmAxis>();
        foreach(Body2 b in raw) foreach(Face2 f in (Array)b.GetFaces()) { var s=(Surface)f.GetSurface(); if(!s.IsCylinder())continue; var v=(double[])s.CylinderParams; if(Math.Abs(v[6]-radius)>1e-7)continue;
            double[] p=RmPoint(v.Take(3).ToArray(),c.Transform2),n=RmVector(v.Skip(3).Take(3).ToArray(),c.Transform2); if(Math.Abs(Math.Abs(RmDot(n,RmNormal))-1)>1e-7)continue;
            if(!rows.Any(r=>RmAxisDistance(r.Point,p)<1e-7))rows.Add(new RmAxis{Face=f,Point=p,Direction=n,Radius=v[6]}); }
        return rows.OrderByDescending(a=>RmDot(a.Point,RmDepth)).ToList();
    }
    static List<RmPlane> RmPlanes(Component2 c) {
        object info; var raw=c.GetBodies3(0,out info) as Array; Need(raw!=null,"Missing component planes"); var rows=new List<RmPlane>();
        foreach(Body2 b in raw)foreach(Face2 f in (Array)b.GetFaces()){var s=(Surface)f.GetSurface();if(!s.IsPlane())continue;var p=(double[])s.PlaneParams;
            double[] n=RmVector(p.Take(3).ToArray(),c.Transform2),o=RmPoint(p.Skip(3).Take(3).ToArray(),c.Transform2);if(Math.Abs(Math.Abs(RmDot(n,RmNormal))-1)<1e-7)rows.Add(new RmPlane{Face=f,Point=o,Normal=n,Q=RmDot(o,RmNormal),Area=f.GetArea()});}
        return rows;
    }
    static object RmAxisRows(List<RmAxis> rows) { return rows.Select(a=>new{point_m=a.Point,direction=a.Direction,radius_m=a.Radius,p_m=RmDot(a.Point,RmDepth)}).ToArray(); }
    static object RmPlaneRows(List<RmPlane> rows) { return rows.Select(p=>new{point_m=p.Point,normal=p.Normal,q_m=p.Q,area_mm2=p.Area*1e6}).ToArray(); }
    static void RmRebuild(ModelDoc2 d,string label) { RmActivate(d,"rebuild/"+label); bool ok=d.ForceRebuild3(false);int wrong=d.Extension.GetWhatsWrongCount(); if(!ok||wrong!=0){var bad=new List<object>();foreach(var f in Features(d)){bool warning;int error=f.GetErrorCode2(out warning);if(error!=0||warning)bad.Add(new{name=f.Name,type=f.GetTypeName2(),error=error,warning=warning,suppressed=f.IsSuppressed2(1,null)});}RmEvidence["last_rebuild_failure"]=new{document=d.GetPathName(),label=label,rebuild_return=ok,issues=wrong,features=bad};RmCheckpoint(d,"failed_"+label);Flush("mount_r20/rebuild_failure_recorded");}Need(ok&&wrong==0,"Mount native rebuild failed: " + label); }
    static void RmSuppressFeature(ModelDoc2 d,Feature f,List<object> rows,int configurations) { bool[] before=f.IsSuppressed2(1,null) as bool[]; Need(before!=null&&before.Length==1,"Unknown feature suppression " + f.Name); if(!before[0])Need(f.SetSuppression2(0,configurations,null),"Cannot suppress exact feature " + f.Name); rows.Add(new{document=Path.GetFileName(d.GetPathName()),feature=f.Name,type=f.GetTypeName2(),suppressed_before=before[0],suppressed_after=f.IsSuppressed2(1,null),configuration_scope=configurations}); }
    static double[] RmBox(ModelDoc2 d) { var bs=RmBodies(d); Need(bs.Length==1,"Single lower structure body required: " + d.GetPathName()); return (double[])bs[0].GetBodyBox(); }
    static void RmDimension(ModelDoc2 d,string name,double mm,List<object> changes) { var v=d.Parameter(name) as Dimension;Need(v!=null,"Missing reference control " + name); if(Math.Abs(v.SystemValue*1000-mm)>1e-6)Set(d,name,mm,changes);else changes.Add(new{document=Path.GetFileName(d.GetPathName()),dimension=name,actual_mm=mm,action="already_at_reference_value"}); }
    static void RmCheckpoint(ModelDoc2 d,string label)
    {
        if(!E.ContainsKey("mode")||(string)E["mode"]!="mount_probe")return;
        string clean=String.Concat(label.Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)||ch=='/'||ch=='\\'?'_':ch));
        string path=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_mount_r20_"+(string)RmEvidence["case_id"]+"_"+clean+".step");
        if(File.Exists(path))return;var exports=RmEvidence.ContainsKey("checkpoint_exports")?(List<object>)RmEvidence["checkpoint_exports"]:new List<object>();RmEvidence["checkpoint_exports"]=exports;
        try{int er=0,wa=0;bool ok=d.Extension.SaveAs(path,0,3,null,ref er,ref wa);exports.Add(new{document=d.GetPathName(),label=label,path=path,saved=ok,errors=er,warnings=wa,sha256=File.Exists(path)?Hash(path):null});}catch(Exception ex){exports.Add(new{document=d.GetPathName(),label=label,error=ex.Message});}
        Flush("mount_r20/checkpoint/"+label);
    }

    static int RmRemoveOldJoint(ModelDoc2 top,List<RmAxis>[] footAxes,List<RmAxis>[] bracketAxes,Dictionary<string,object> m,List<object> suppressed)
    {
        var source=Json.Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(Path.Combine(Root,"hardware_identity.json"),System.Text.Encoding.UTF8));
        Need(source.Count==36&&source.Select(x=>(string)x["name"]).Distinct().Count()==36,"The 36-instance source hardware manifest is required");
        var audit=new List<object>();m["old_joint_instance_identity"]=audit;var remove=new List<Component2>();
        foreach(var h in source){string name=(string)h["name"],file=(string)h["file"],logical=(string)h["layout_name"];var instance=RmComponent(top,name);
            Need(name.IndexOf('/')<0&&!instance.IsSuppressed()&&!instance.IsHidden(true)&&Path.GetFileName(instance.GetPathName())==file,"Old joint manifest/reference mismatch: "+name);
            Need(new[]{"垫圈8.SLDPRT","螺母M8.SLDPRT","窄柜雨棚螺钉M8x16候选.SLDPRT"}.Contains(file),"Unexpected manifest hardware document");
            string[] token=logical.Split('_');Need(token.Length>=4&&(token[0]=="L"||token[0]=="R")&&(token[1]=="foot"||token[1]=="roof"),"Unexpected joint layout identity: "+logical);
            int side=token[0]=="L"?0:1,index=int.Parse(token[2])-(token[1]=="foot"?0:3);Need(index>=0&&index<3,"Old joint station outside range");
            double[] transform=(double[])instance.Transform2.ArrayData,expected=Json.Deserialize<double[]>(Json.Serialize(h["transform"]));Need(expected.Length==16&&transform.Zip(expected,(a,b)=>Math.Abs(a-b)).Max()<1e-7,"Manifest transform differs from live instance: "+name);
            RmAxis axis=(token[1]=="foot"?footAxes:bracketAxes)[side][index];double residual=RmAxisDistance(transform.Skip(9).Take(3).ToArray(),axis.Point);
            Need(residual<1e-7,"Manifest hardware is not on its old joint axis: "+name);object info;Array bodies=instance.GetBodies3(0,out info) as Array;Need(bodies!=null&&bodies.Length==1,"Old joint hardware must be one solid");
            audit.Add(new{name=name,file=file,layout_name=logical,transform=transform,matched_old_axis=axis.Point,axis_residual_mm=residual*1000,solid_count=1});remove.Add(instance);
        }
        var plates=new[]{RmComponent(top,"窄柜雨棚独立转接板L-1"),RmComponent(top,"窄柜雨棚独立转接板R-1")};
        for(int i=0;i<2;i++){var p=plates[i];Need(Path.GetFileName(p.GetPathName())=="窄柜雨棚独立转接板"+(i==0?"L":"R")+".SLDPRT"&&!p.IsSuppressed(),"Adapter instance reference changed");
            var planes=RmPlanes(p);Need(planes.Count>=2&&Math.Abs(planes.Max(a=>a.Q)-planes.Min(a=>a.Q)-.0015)<1e-7,"Measured adapter is not 1.5 mm thick along the roof normal");
            var axes=RmAxes(p,.004);Need(axes.Count==6,"Each source adapter must have exactly six complete holes");
            foreach(var a in footAxes[i].Concat(bracketAxes[i]))Need(axes.Any(b=>RmAxisDistance(a.Point,b.Point)<1e-7),"Adapter source bore identity differs");
            object info;Array bs=p.GetBodies3(0,out info) as Array;Need(bs!=null&&bs.Length==1,"Source adapter must be one solid");
            audit.Add(new{name=p.Name2,file=Path.GetFileName(p.GetPathName()),configuration=p.ReferencedConfiguration,transform=p.Transform2.ArrayData,normal_thickness_mm=1.5,solid_count=1});remove.Add(p);
        }
        Flush("mount_r20/old_joint_identity_complete");RmActivate(top,"suppress_old_joint_mates");
        var plateNames=new HashSet<string>(plates.Select(p=>p.Name2),StringComparer.Ordinal);var mateRows=new List<object>();m["old_plate_mates"]=mateRows;
        foreach(Feature f in Features(top).Where(f=>f.GetTypeName2().StartsWith("Mate",StringComparison.Ordinal))){var mate=f.GetSpecificFeature2() as Mate2;if(mate==null)continue;var refs=new List<string>();
            for(int j=0;j<mate.GetMateEntityCount();j++){var ent=mate.MateEntity(j);var component=ent==null?null:ent.ReferenceComponent;if(component!=null)refs.Add(component.Name2);}
            if(!refs.Any(plateNames.Contains))continue;mateRows.Add(new{name=f.Name,type=f.GetTypeName2(),reference_components=refs});RmSuppressFeature(top,f,suppressed,1);
        }
        var instances=new List<object>();m["suppressed_old_joint_instances"]=instances;
        foreach(var instance in remove){Flush("mount_r20/suppress_instance/"+instance.Name2);int result=instance.SetSuppression2(0);Need(instance.IsSuppressed(),"Cannot suppress exact old joint instance: "+instance.Name2);instances.Add(new{name=instance.Name2,file=Path.GetFileName(instance.GetPathName()),suppression_return=result,suppressed=instance.IsSuppressed(),removed_visible_solids=1});}
        Need(remove.Count==38,"Exactly 36 old hardware and two adapters must be removed");return remove.Count;
    }
    static void RmRestoreFlange(ModelDoc2 part,double oldMm,Dictionary<string,object> m)
    {
        RmActivate(part,"restore_outer_flange");RmConfiguration(part,"Default");Feature f=RmFeature(part,"草图12");Need(f.Select2(false,0),"Cannot select the native outer-flange sketch");part.EditSketch();
        Sketch sketch=part.SketchManager.ActiveSketch;Need(sketch!=null,"Outer flange edit failed");var points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Where(p=>p.Type==0).ToArray();Need(points.Length==4,"Expected the four source flange corners");
        var before=points.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray();var free=points.Where(p=>Math.Abs(p.Y-oldMm/1000)<1e-7).ToArray();
        if(free.Length==0)Need(points.Count(p=>Math.Abs(p.Y-.020)<1e-7)==2,"Unexpected source outer-flange free-end width");else{Need(free.Length==2,"Exactly two free outer-flange endpoints required");foreach(var p in free)Need(p.SetCoords(p.X,.020,p.Z),"Cannot restore the native outer flange to 20 mm");}
        m["outer_flange_restore"]=new{document=Path.GetFileName(part.GetPathName()),sketch="草图12",before_m=before,after_m=points.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),constrained_status=sketch.GetConstrainedStatus(),requested_free_y_mm=20};
        part.SketchManager.InsertSketch(true);RmRebuild(part,"outer_flange_reference");
    }
    static string RmProtectedCanopyState(ModelDoc2 master)
    {
        var eq=(EquationMgr)master.GetEquationMgr();Need(eq.GetCount()==3,"Keep the three healthy canopy master equations");
        double upper=((Dimension)master.Parameter("D1@凸台-拉伸1")).SystemValue,lower=((Dimension)master.Parameter("D2@草图3")).SystemValue,holes=((Dimension)master.Parameter("D1@草图14")).SystemValue;
        Need(Math.Abs(upper-1)<1e-9&&Math.Abs(lower-.920)<1e-9&&Math.Abs(holes-.920)<1e-9,"Keep the healthy 1000/920/920 mm canopy master controls");
        Feature profile=RmFeature(master,"草图10");Sketch sketch=profile.GetSpecificFeature2() as Sketch;Need(sketch!=null,"Missing protected source frame sketch");Dimension control=master.Parameter("D1@草图10") as Dimension;
        return Json.Serialize(new{equations=Enumerable.Range(0,eq.GetCount()).Select(i=>eq.Equation[i]).ToArray(),upper_m=upper,lower_m=lower,holes_m=holes,frame_control_m=control==null?(double?)null:control.SystemValue,
            source_profile_points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new{id=p.GetID(),xyz_m=new[]{p.X,p.Y,p.Z}}).ToArray(),source_profile_status=sketch.GetConstrainedStatus()});
    }
    static Dictionary<string,double[]> RmFrameBendControls(ModelDoc2 d)
    {
        var values=new Dictionary<string,double[]>();foreach(var f in Features(d)){
            var bend=f.GetDefinition() as OneBendFeatureData;var sheet=f.GetDefinition() as SheetMetalFeatureData;
            if(bend!=null)values.Add(f.Name,new[]{bend.BendRadius,bend.BendAngle,bend.BendAllowanceType,bend.KFactor,bend.BendAllowance,bend.BendDirection,bend.BendDown?1.0:0.0,bend.UseDefaultBendRadius?1.0:0.0,bend.UseDefaultBendAllowance?1.0:0.0});
            else if(sheet!=null)values.Add(f.Name,new[]{sheet.Thickness,sheet.BendRadius,sheet.BendAllowanceType,sheet.KFactor,sheet.BendAllowance});
        }Need(values.Count==5,"Exactly the original sheet-metal definition and four bends required");return values;
    }
    static void RmFrameHealth(ModelDoc2 d,Dictionary<string,object> record,string stage)
    {
        var rows=new List<object>();int bad=0;foreach(var f in Features(d)){bool warning;int error=f.GetErrorCode2(out warning);if(error!=0||warning){bad++;rows.Add(new{name=f.Name,type=f.GetTypeName2(),error=error,warning=warning,suppressed=f.IsSuppressed2(1,null)});}}
        record[stage+"_health"]=new{issues=d.Extension.GetWhatsWrongCount(),feature_problem_count=bad,problems=rows};Flush("mount_r20/frame_health/"+stage);Need(bad==0&&d.Extension.GetWhatsWrongCount()==0,"Native frame feature errors/warnings at "+stage);
    }
    static void RmAdjustFinishedFrame(double shift,Dictionary<string,object> m)
    {
        ModelDoc2 frame=Doc("立柱连接架.SLDPRT");double[] before=RmBox(frame);double expected=879-shift*2000;
        var row=new Dictionary<string,object>{{"before_bounds_m",before},{"before_width_mm",(before[3]-before[0])*1000},{"required_width_mm",expected},{"required_end_translation_m",shift},
            {"action",shift==0?"B_healthy_finished_frame_retained":"A_local_frame_end_moves_before_native_sheetmetal_recognition"},{"master_source_edit_performed",false}};m["finished_frame_adjustment"]=row;Flush("mount_r20/finished_frame_interface_recorded");
        Need(Math.Abs(before[0]+.4395)<1e-7&&Math.Abs(before[3]-.4395)<1e-7,"Expected the healthy centered 879 mm finished frame");
        if(shift==0){row["final_bounds_m"]=before;row["final_width_mm"]=(before[3]-before[0])*1000;return;}
        Need(Math.Abs(shift-.025)<1e-9,"Only the approved 25 mm frame-end translation is permitted");RmActivate(frame,"move_finished_frame_end_regions");
        var protectedBends=RmFrameBendControls(frame);row["protected_bend_controls_before"]=protectedBends;Feature sheet=Features(frame).SingleOrDefault(f=>f.GetTypeName2()=="SheetMetal");Need(sheet!=null,"Original frame sheet-metal recognition required");
        var ends=new List<object>();row["end_region_moves"]=ends;Need(frame.FeatureManager.EditRollback(3,sheet.Name),"Cannot insert frame-end translations before native sheet-metal recognition");
        try{
            before=RmBox(frame);row["raw_source_bounds_m"]=before;row["native_insertion_before"]=sheet.Name;
            foreach(int side in new[]{-1,1}){
                frame.ClearSelection2(true);Body2[] bs=RmBodies(frame);Need(bs.Length==1,"Exactly one finished frame solid required");
                var faces=((Array)bs[0].GetFaces()).Cast<Face2>().Select(f=>new{face=f,box=(double[])f.GetBox()}).Where(x=>side<0?x.box[3]<-.420:x.box[0]>.420).ToArray();
                Need(faces.Length>=6,"Missing complete frame end-wall, bend and receiver face group");var selected=faces.Select(x=>new{bounds_m=x.box,area_m2=x.face.GetArea()}).ToArray();
                var sd=((SelectionMgr)frame.SelectionManager).CreateSelectData() as SelectData;sd.Mark=1;foreach(var f in faces)Need(((Entity)f.face).Select4(true,sd),"Cannot select complete frame-end face group");
                double[] translation={-side*shift,0,0};var end=new Dictionary<string,object>{{"side",side},{"selected_faces",selected},{"selection_mark",1},{"translation_m",translation},{"reverse_direction",side>0}};ends.Add(end);
                Feature move=frame.FeatureManager.InsertMoveFace2(1,side>0,0,shift,translation,new double[6]);end["feature_created"]=move!=null;Flush("mount_r20/frame_end_created/"+side);Need(move!=null,"Native frame-end translation was not created");
                move.Name=side<0?"参考安装_左端区内移25":"参考安装_右端区内移25";end["created_feature"]=move.Name;var definition=move.GetDefinition() as MoveFaceFeatureData;Need(definition!=null,"Cannot inspect native frame-end move definition");
                end["definition"]=new{move_type=definition.MoveType,reverse_direction=definition.ReverseDirection,translation=definition.TriadTranslationParameters};
                double[] now=RmBox(frame),required=(double[])before.Clone();required[0]+=shift;if(side>0)required[3]-=shift;end["after_bounds_m"]=now;end["required_bounds_m"]=required;Flush("mount_r20/frame_end_measured/"+side);
                Need(now.Zip(required,(a,b)=>Math.Abs(a-b)).Max()<1e-7,"Finished frame end did not move inward by the required 25 mm");
            }
        }finally{Need(frame.FeatureManager.EditRollback(1,""),"Cannot restore the frame rollback bar");}
        RmRebuild(frame,"moved_finished_frame_end_regions");double[] after=RmBox(frame);row["final_bounds_m"]=after;row["final_width_mm"]=(after[3]-after[0])*1000;row["final_bodies"]=Bodies(frame);Flush("mount_r20/finished_frame_geometry_recorded");
        Need(Math.Abs(after[0]+.4145)<1e-7&&Math.Abs(after[3]-.4145)<1e-7&&Math.Abs((after[3]-after[0])*1000-expected)<1e-4,"Expected centered 829 mm finished frame was not produced");
        var retainedBends=RmFrameBendControls(frame);row["protected_bend_controls_after"]=retainedBends;Need(protectedBends.Count==retainedBends.Count&&protectedBends.All(p=>retainedBends.ContainsKey(p.Key)&&p.Value.Zip(retainedBends[p.Key],(a,b)=>Math.Abs(a-b)).Max()<1e-10),"Original frame sheet-metal/bend controls changed");
        RmFrameHealth(frame,row,"folded");row["native_flat_cycle_stage"]="Required final independent R23 read-only session; do not interleave flat configuration changes with assembly editing";
    }
    static void RmAdjustLowerAtFinishedParts(double width,Dictionary<string,double[]> lowerBefore,Dictionary<string,object> m)
    {
        double shift=(1000-width)/2000;var rows=new List<object>();m["lower_body_moves"]=rows;m["lower_adjustment_route"]="Keep healthy master equations and source topology; adjust only finished native lower parts";
        var rightNames=new[]{"右立柱焊件2.SLDPRT","右立柱焊件3.SLDPRT","右立柱焊件4.SLDPRT"};var rightBefore=rightNames.ToDictionary(n=>n,n=>RmBox(Doc(n)));var mirrors=new List<object>();m["right_lower_mirror_follow"]=mirrors;
        if(shift==0)rows.Add(new{action="B_finished_lower_parts_retained",translation_m=0});
        else{
            Need(Math.Abs(shift-.025)<1e-9,"Only the approved 25 mm A lower-part translation is permitted");
            foreach(var source in lowerBefore){ModelDoc2 d=Doc(source.Key);RmRebuild(d,"healthy_finished_part/"+source.Key);RmMoveLower(d,source.Value,shift,rows);}
        }
        foreach(string name in rightNames){ModelDoc2 d=Doc(name);RmRebuild(d,"finished_right_mirror/"+name);double[] before=rightBefore[name],after=RmBox(d),required=before.Select((v,i)=>v-((i==0||i==3)?shift:0)).ToArray();
            mirrors.Add(new{document=name,before_bounds_m=before,after_bounds_m=after,required_bounds_m=required,required_translation_m=new[]{-shift,0,0},observed_x_translation_m=new[]{after[0]-before[0],after[3]-before[3]}});Flush("mount_r20/right_lower_mirror_measured/"+name);
            Need(after.Zip(required,(a,b)=>Math.Abs(a-b)).Max()<1e-6,"Right finished lower part did not inherit the measured inward translation: "+name);}
        RmAdjustFinishedFrame(shift,m);
    }
    static void RmMoveLower(ModelDoc2 d,double[] before,double shift,List<object> rows)
    {
        double[] now=RmBox(d);double expected=before[0]+shift;double observed=now[0]-before[0];
        var row=new Dictionary<string,object>{{"document",Path.GetFileName(d.GetPathName())},{"before_bounds_m",before},{"after_master_bounds_m",now},{"required_shift_m",shift},{"observed_shift_after_master_m",observed}};rows.Add(row);Flush("mount_r20/lower_inspect/"+Path.GetFileName(d.GetPathName()));
        Need(Math.Abs((now[3]-now[0])-(before[3]-before[0]))<1e-6,"Lower body width changed instead of translating");
        if(Math.Abs(now[0]-expected)<1e-6){row["action"]="native_source_already_followed";return;}
        Need(shift>.0&&Math.Abs(observed)<1e-6,"Unexpected lower-body datum after master update");RmActivate(d,"move_only_lower_body/"+Path.GetFileName(d.GetPathName()));
        Feature flat=Features(d).SingleOrDefault(f=>f.GetTypeName2()=="FlatPattern");Need(flat!=null,"Native lower-part flat-pattern insertion point required");
        Need(d.FeatureManager.EditRollback(3,flat.Name),"Cannot insert the lower-body translation before its flat pattern");
        try{Body2[] bs=RmBodies(d);Need(bs.Length==1,"Exactly one lower body may move");d.ClearSelection2(true);var sd=((SelectionMgr)d.SelectionManager).CreateSelectData() as SelectData;sd.Mark=1;Need(bs[0].Select2(false,sd),"Cannot select only the lower body");
            Feature move=d.FeatureManager.InsertMoveCopyBody2(shift,0,0,0,0,0,0,0,0,0,false,1);Need(move!=null,"Native lower-body move was not created");move.Name="参考安装结构_下部内移25";row["created_feature"]=move.Name;}
        finally{Need(d.FeatureManager.EditRollback(1,""),"Cannot restore the lower-part rollback bar");}
        RmRebuild(d,"moved_lower_body");now=RmBox(d);row["final_bounds_m"]=now;Need(Math.Abs(now[0]-expected)<1e-6&&Math.Abs(now[3]-before[3]-shift)<1e-6,"Native lower body did not translate by the required 25 mm");
    }
    static object RmCopyDefinition(ModelDoc2 part,Feature feature)
    {
        var data=feature.GetDefinition() as MoveCopyBodyFeatureData;Need(data!=null,"Missing native right-hand MoveCopy2 definition");bool access=false;
        try{access=data.AccessSelections(part,null);Need(access,"Cannot inspect the native right-hand body transform");Array selected=data.Bodies as Array;
            return new{feature=feature.Name,body_count=data.GetBodiesCount(),copy=data.Copy,transform_type=data.TransformType,transform_x=data.TransformX,transform_y=data.TransformY,transform_z=data.TransformZ,rotation_origin=new[]{data.RotationOriginX,data.RotationOriginY,data.RotationOriginZ},selected_bodies=selected==null?null:selected.Cast<Body2>().Select(b=>new{name=b.Name,bounds_m=b.GetBodyBox()}).ToArray()};}
        finally{if(access)data.ReleaseSelectionAccess();}
    }
    static List<RmPlane> RmLocalPlanes(Body2 body)
    {
        var rows=new List<RmPlane>();foreach(Face2 face in (Array)body.GetFaces()){var surface=(Surface)face.GetSurface();if(!surface.IsPlane())continue;var p=(double[])surface.PlaneParams;double[] n=p.Take(3).ToArray(),o=p.Skip(3).Take(3).ToArray();if(Math.Abs(Math.Abs(RmDot(n,RmNormal))-1)<1e-7)rows.Add(new RmPlane{Face=face,Point=o,Normal=n,Q=RmDot(o,RmNormal),Area=face.GetArea()});}return rows;
    }
    static object RmExactReceiverCircles(Sketch sketch,List<SketchSegment> circles,List<double[]> expected)
    {
        var segments=((Array)sketch.GetSketchSegments()).Cast<SketchSegment>().ToArray();Need(segments.Length==6&&circles.Count==6&&expected.Count==6,"Exactly six receiver-ring circles required");
        var rows=new List<object>();for(int i=0;i<6;i++){var arc=circles[i] as SketchArc;Need(arc!=null,"Receiver segment is not a circle");var p=arc.GetCenterPoint2() as SketchPoint;Need(p!=null,"Receiver circle center unavailable");double[] actual={p.X,p.Y,p.Z};double error=actual.Zip(expected[i].Take(3),(a,b)=>Math.Abs(a-b)).Max(),radiusError=Math.Abs(arc.GetRadius()-expected[i][3]);rows.Add(new{index=i,requested_center_sketch_m=expected[i].Take(3).ToArray(),actual_center_sketch_m=actual,requested_radius_m=expected[i][3],actual_radius_m=arc.GetRadius(),circle=arc.IsCircle(),construction=circles[i].ConstructionGeometry,center_error_m=error,radius_error_m=radiusError});Need(arc.IsCircle()==1&&!circles[i].ConstructionGeometry&&error<1e-9&&radiusError<1e-9,"Actual native receiver circle differs from requested exact geometry at index "+i);}
        return rows;
    }
    static void RmReferenceReceiver(ModelDoc2 part,Dictionary<string,object> m,List<object> suppressed,List<object> changes)
    {
        var record=new Dictionary<string,object>();m["reference_receiver"]=record;record["visible_before"]=part.Visible;record["model_view_count_before"]=part.GetModelViewCount();record["active_view_before"]=part.ActiveView!=null;
        part.Visible=true;int viewErrors=0,viewWarnings=0;part=Sw.OpenDoc6(part.GetPathName(),1,1,"Default",ref viewErrors,ref viewWarnings) as ModelDoc2;Need(part!=null&&viewErrors==0,"Cannot explicitly open referenced bracket as its own writable document");part.Visible=true;RmActivate(part,"inspect_right_hand_transform");
        record["model_view_count_after"]=part.GetModelViewCount();record["active_view_after"]=part.ActiveView!=null;Flush("mount_r20/receiver_own_document_view_checked");Need(part.ActiveView!=null&&part.GetModelViewCount()>0,"Bracket requires its own active native model view for feature construction");RmConfiguration(part,"950_RIGHT_NATIVE");
        Feature mirror=RmFeature(part,"实体-移动/复制2");record["right_transform_before"]=RmCopyDefinition(part,mirror);record["right_bodies_before"]=Bodies(part);double[] rightSheetBox=(double[])RmBodies(part).OrderByDescending(b=>((double[])b.GetMassProperties(1))[3]).First().GetBodyBox();
        RmConfiguration(part,"Default");record["left_bodies_before"]=Bodies(part);Need(RmBodies(part).Length==4,"Source bracket must be the sheet plus its three disjoint Stock receivers");
        Dimension hole=part.Parameter("D2@草图13") as Dimension;Need(hole!=null&&hole.DrivenState==2,"Reference bracket hole must be a native driving dimension");double oldHole=hole.SystemValue;int holeResult=hole.SetSystemValue3(.0088,2,null);
        changes.Add(new{document=Path.GetFileName(part.GetPathName()),dimension="D2@草图13",before_mm=oldHole*1000,requested_mm=8.8,result=holeResult,configuration_scope="all"});Need(holeResult==0&&Math.Abs(hole.SystemValue-.0088)<1e-9,"Reference 8.8 mm bracket hole rejected");
        foreach(string name in new[]{"阵列(线性)1","实体-移动/复制1","装饰螺纹线1-焊接六角螺母M8","焊接六角螺母M8"})RmSuppressFeature(part,RmFeature(part,name),suppressed,2);
        RmRebuild(part,"remove_disjoint_stock_receivers");Need(RmBodies(part).Length==1,"Suppressing the exact Stock chain must leave one native sheet body");record["sheet_after_stock_suppression"]=Bodies(part);Flush("mount_r20/stock_suppression_recorded");RmCheckpoint(part,"sheet_without_stock");
        Need(part.FeatureManager.EditRollback(3,mirror.Name),"Cannot insert fused reference receivers before the native opposite-hand transform");
        Feature created=null,ringSketchFeature=null;Sketch sketch=null;
        try{
            Body2[] bodies=RmBodies(part);Need(bodies.Length==1,"One sheet body is required at the receiver insertion point");var planes=RmLocalPlanes(bodies[0]);Need(planes.Count>=4,"Missing native folded bracket roof planes");double largest=planes.Max(p=>p.Area);
            double topQ=planes.Where(p=>p.Area>largest*.7).Max(p=>p.Q),bottomQ=topQ-.0015;var underside=planes.Where(p=>Math.Abs(p.Q-bottomQ)<1e-7).OrderByDescending(p=>p.Area).FirstOrDefault();Need(underside!=null,"Cannot locate the actual 1.5 mm sheet underside");
            Feature source=RmFeature(part,"草图13");Sketch holes=source.GetSpecificFeature2() as Sketch;Need(holes!=null,"Missing source mounting-hole sketch");
            var arcs=((Array)holes.GetSketchSegments()).Cast<SketchSegment>().Where(s=>!s.ConstructionGeometry&&s is SketchArc).Cast<SketchArc>().ToArray();Need(arcs.Length==3,"Exactly three original mounting circles required");
            var centers=arcs.Select(a=>{SketchPoint p=a.GetCenterPoint2() as SketchPoint;Need(p!=null,"Missing original hole center");return RmPoint(new[]{p.X,p.Y,p.Z},holes.ModelToSketchTransform.IInverse());}).OrderByDescending(p=>RmDot(p,RmDepth)).ToArray();
            Need(centers.All(p=>Math.Abs(RmDot(p,RmNormal)-topQ)<1e-7),"Native mounting circles are not on the measured bracket top");
            record["receiver_geometry"]=new{sheet_top_q_m=topQ,sheet_underside_q_m=bottomQ,outer_diameter_mm=11,inner_diameter_mm=6,height_mm=4,axis_centers_on_sheet_top_m=centers,join="annulus outer radius 5.5 overlaps the 4.4-radius sheet hole; extrusion starts at sheet underside and extends inward along -N",thread="UNVERIFIED: no cosmetic or modeled thread added"};Flush("mount_r20/receiver_geometry_recorded");
            part.ClearSelection2(true);Need(((Entity)underside.Face).Select4(false,null),"Cannot select the actual bracket underside");part.SketchManager.InsertSketch(true);sketch=part.SketchManager.ActiveSketch;Need(sketch!=null,"Reference receiver sketch did not open");ringSketchFeature=RmSketchFeature(part,sketch);ringSketchFeature.Name="参考接收凸台_3x外11内6草图";
            var circles=new List<SketchSegment>();var requested=new List<double[]>();bool priorAddToDB=part.SketchManager.AddToDB;record["add_to_db_before"]=priorAddToDB;
            try{part.SketchManager.AddToDB=true;Need(part.SketchManager.AddToDB,"Cannot disable native sketch inferencing for exact receiver rings");foreach(double[] p in centers){double[] at=p.Zip(RmNormal,(v,n)=>v-n*.0015).ToArray();double[] center=RmPoint(at,sketch.ModelToSketchTransform);Need(Math.Abs(center[2])<1e-7,"Receiver center is off the underside sketch plane");foreach(double radius in new[]{.0055,.003}){var circle=part.SketchManager.CreateCircleByRadius(center[0],center[1],0,radius);Need(circle!=null,"Reference receiver ring circle failed");circles.Add(circle);requested.Add(new[]{center[0],center[1],0.0,radius});}}}
            finally{part.SketchManager.AddToDB=priorAddToDB;}
            record["circles_after_creation"]=RmExactReceiverCircles(sketch,circles,requested);Flush("mount_r44/exact_circles_created");
            foreach(SketchSegment segment in circles){part.ClearSelection2(true);Need(segment.Select4(false,null),"Cannot constrain a reference receiver circle");part.SketchAddConstraints("sgFIXED");}record["sketch_constrained_status"]=sketch.GetConstrainedStatus();Need(sketch.GetConstrainedStatus()==3,"Reference receiver circles must be fully constrained");
            record["circles_after_fixing"]=RmExactReceiverCircles(sketch,circles,requested);Flush("mount_r44/exact_circles_fixed");
            MathTransform transform=sketch.ModelToSketchTransform;double[] normal=RmVector(new[]{0.0,0.0,1.0},transform.IInverse());bool reverse=RmDot(normal,RmNormal)>0;
            part.ClearSelection2(true);part.SketchManager.InsertSketch(true);Need(ringSketchFeature.Select2(false,0),"Cannot select native receiver ring sketch");
            record["extrusion_reverse"]=reverse;record["sketch_normal_model"]=normal;record["sketch_feature_name"]=ringSketchFeature.Name;record["active_document_before_extrusion"]=((ModelDoc2)Sw.ActiveDoc).GetPathName();
            var scopeBody=RmBodies(part).Single();var bodySelect=((SelectionMgr)part.SelectionManager).CreateSelectData() as SelectData;bodySelect.Mark=8;Need(scopeBody.Select2(true,bodySelect),"Cannot explicitly select the receiving sheet body");
            var selection=(SelectionMgr)part.SelectionManager;record["extrusion_selection"]=Enumerable.Range(1,selection.GetSelectedObjectCount2(-1)).Select(i=>new{index=i,type=selection.GetSelectedObjectType3(i,-1),mark=selection.GetSelectedObjectMark(i)}).ToArray();Flush("mount_r20/receiver_explicit_scope_selected");
            object priorMessages,priorIds,priorTypes;int priorCount=Sw.GetErrorMessages(out priorMessages,out priorIds,out priorTypes);record["native_error_history_before_extrusion"]=new{count=priorCount,messages=priorMessages,ids=priorIds,types=priorTypes};Flush("mount_r44/pre_extrusion_messages_cleared");
            created=part.FeatureManager.FeatureExtrusion3(true,false,reverse,0,0,.004,0,false,false,false,false,0,0,false,false,false,false,true,true,false,0,0,false);
            if(created==null){object messages,ids,types;int count=Sw.GetErrorMessages(out messages,out ids,out types);record["native_error_messages"]=new{count=count,messages=messages,ids=ids,types=types};Flush("mount_r20/receiver_native_error_messages");}Need(created!=null,"Fused native reference receiver extrusion failed");created.Name="参考接收凸台_3x外11内6高4_融合";record["created_feature"]=created.Name;record["extrusion_reverse"]=reverse;
            Need(RmBodies(part).Length==1,"All reference receiver bosses must merge into the sheet body");
        }finally{Need(part.FeatureManager.EditRollback(1,""),"Cannot restore the bracket rollback bar");}
        Need(created!=null&&sketch!=null&&ringSketchFeature!=null,"Incomplete native receiver feature construction");Need(ringSketchFeature.SetSuppression2(1,2,null)&&created.SetSuppression2(1,2,null),"Reference receiver feature must exist in both native configurations");
        RmConfiguration(part,"950_RIGHT_NATIVE");RmActivate(part,"repair_right_hand_body_selection");mirror=RmFeature(part,"实体-移动/复制2");var data=mirror.GetDefinition() as MoveCopyBodyFeatureData;Need(data!=null,"Cannot read opposite-hand transform definition after Stock suppression");bool access=false,modified=false;
        try{access=data.AccessSelections(part,null);Need(access,"Cannot access right-hand mirror input selections");Body2[] current=RmBodies(part);record["right_input_bodies_before_selection_repair"]=current.Select(b=>new{name=b.Name,bounds_m=b.GetBodyBox()}).ToArray();Need(current.Length==1,"The right-hand transform must receive exactly the new fused sheet body");
            data.Bodies=new[]{new DispatchWrapper(current[0])};record["right_selected_body_count_requested"]=1;modified=mirror.ModifyDefinition(data,part,null);Need(modified,"Right-hand native body-selection repair rejected");}
        finally{if(access&&!modified)data.ReleaseSelectionAccess();}
        record["right_folder_unsuppress_return"]=RmFeature(part,"镜向零部件特征1").SetSuppression2(1,1,null);record["right_mirror_unsuppress_return"]=RmFeature(part,"实体-移动/复制2").SetSuppression2(1,1,null);RmRebuild(part,"right_reference_receiver");record["right_bodies_before_final_gate"]=Bodies(part);record["stock_suppression_after_right_restore"]=new[]{"焊接六角螺母M8","实体-移动/复制1","阵列(线性)1"}.Select(n=>new{name=n,suppressed=RmFeature(part,n).IsSuppressed2(1,null)}).ToArray();Flush("mount_r20/right_unsuppression_recorded");Need(RmBodies(part).Length==1,"Right bracket must contain one fused native solid");Need(Math.Abs(((Dimension)part.Parameter("D2@草图13")).SystemValue-.0088)<1e-9,"Right bracket did not inherit the 8.8 mm hole control");record["right_bodies_after"]=Bodies(part);Need(RmBox(part).Zip(rightSheetBox,(a,b)=>Math.Abs(a-b)).Max()<1e-7,"Right native mirror did not retain its actual handed sheet placement");foreach(string old in new[]{"焊接六角螺母M8","实体-移动/复制1","阵列(线性)1"})Need(((bool[])RmFeature(part,old).IsSuppressed2(1,null)).All(v=>v),"Restoring right mirror re-enabled old independent receivers");mirror=RmFeature(part,"实体-移动/复制2");record["right_transform_after"]=RmCopyDefinition(part,mirror);RmCheckpoint(part,"right_reference_receiver");
        RmConfiguration(part,"Default");RmRebuild(part,"left_reference_receiver");Need(RmBodies(part).Length==1,"Left bracket must contain one fused native solid");record["left_bodies_after"]=Bodies(part);RmCheckpoint(part,"left_reference_receiver");
        record["flat_pattern_status"]="UNVERIFIED: host must unfold/refold the candidate and report how the fused non-sheet receiver regions are represented";
    }
    static object RmMate(ModelDoc2 top,Component2 a,Face2 fa,Component2 b,Face2 fb,int type,string name)
    {
        top.ClearSelection2(true);var ea=a.GetCorrespondingEntity(fa) as Entity;var eb=b.GetCorrespondingEntity(fb) as Entity;Need(ea!=null&&eb!=null,"Cannot map the actual direct-connection mate entities into the top assembly");Need(ea.Select4(false,null)&&eb.Select4(true,null),"Cannot select the actual direct-connection mate entities");
        int error=0;Mate2 mate=((AssemblyDoc)top).AddMate5(type,2,false,0,0,0,0,0,0,0,0,false,false,0,out error);Need(mate!=null&&error==1,"Direct native mount mate rejected: "+name+" code="+error);var feature=Features(top).LastOrDefault(f=>f.GetTypeName2()==(type==0?"MateCoincident":"MateConcentric"));Need(feature!=null,"Cannot identify the created direct-connection mate");feature.Name=name;top.ClearSelection2(true);return new{name=name,type=type,error=error,component_a=a.Name2,component_b=b.Name2};
    }
    static void RmPositionAndMate(ModelDoc2 top,Component2[] feet,Component2[] brackets,double width,Dictionary<string,object> m)
    {
        RmActivate(top,"measure_direct_mount_before_position");var before=new List<object>();m["direct_mount_before_position"]=before;double gap=0;double targetX=(width-80)/2000;
        for(int i=0;i<2;i++){var bp=RmPlanes(brackets[i]);var fp=RmPlanes(feet[i]);double bmax=bp.Max(p=>p.Area),fmax=fp.Max(p=>p.Area);double bq=bp.Where(p=>p.Area>bmax*.7).Max(p=>p.Q),fq=fp.Where(p=>p.Area>fmax*.98).Min(p=>p.Q),g=fq-bq;
            var ba=RmAxes(brackets[i],.0044);var fa=RmAxes(feet[i],.004);Need(ba.Count==3&&fa.Count==3,"Three native direct-connection bores required per side");
            Need(ba.Concat(fa).All(a=>Math.Abs(Math.Abs(a.Point[0])-targetX)<1e-7),"Reference hole rows must be +/-435 or +/-460 mm");
            for(int j=0;j<3;j++)Need(RmAxisDistance(ba[j].Point,fa[j].Point)<1e-7,"Native bracket and foot bore axes differ");
            before.Add(new{side=i==0?"L":"R",bracket_top_q_m=bq,foot_bearing_bottom_q_m=fq,measured_normal_gap_m=g,bracket_planes=RmPlaneRows(bp),foot_planes=RmPlaneRows(fp),bracket_axes=RmAxisRows(ba),foot_axes=RmAxisRows(fa)});
            Need(g>0&&Math.Abs(g-.0015)<1e-7,"Runtime normal gap must independently reproduce the surveyed 1.5 mm adapter thickness");if(i==0)gap=g;else Need(Math.Abs(g-gap)<1e-7,"Left and right direct-mount normal gaps differ");}
        Component2 upper=RmComponent(top,"太阳能雨棚装配-1");double[] original=(double[])upper.Transform2.ArrayData,requested=(double[])original.Clone(),delta=RmNormal.Select(n=>-gap*n).ToArray();for(int i=0;i<3;i++)requested[9+i]+=delta[i];m["upper_displacement_m"]=delta;m["upper_pose_before"]=original;m["upper_pose_requested"]=requested;Flush("mount_r20/direct_pose_measured");
        var utility=(MathUtility)Sw.GetMathUtility();upper.Transform2=utility.CreateTransform(requested) as MathTransform;
        var rightBracketPlanes=RmPlanes(brackets[1]);double maxArea=rightBracketPlanes.Max(p=>p.Area);var contact=rightBracketPlanes.Where(p=>p.Area>maxArea*.7).OrderByDescending(p=>p.Q).First();
        var footContact=RmPlanes(feet[1]).Where(p=>Math.Abs(p.Q-contact.Q)<1e-7).OrderByDescending(p=>p.Area).FirstOrDefault();Need(footContact!=null,"Actual Z-foot bearing surface did not reach the bracket top");
        var mates=new List<object>();m["new_direct_mates"]=mates;mates.Add(RmMate(top,brackets[1],contact.Face,feet[1],footContact.Face,0,"参考安装_Z脚直接贴合帽形架"));
        var bAxis=RmAxes(brackets[1],.0044)[0];var fAxis=RmAxes(feet[1],.004).Single(a=>RmAxisDistance(a.Point,bAxis.Point)<1e-7);mates.Add(RmMate(top,brackets[1],bAxis.Face,feet[1],fAxis.Face,1,"参考安装_三孔列基准同轴"));
        var lastBracketAxis=RmAxes(brackets[1],.0044)[2];var lastFootAxis=RmAxes(feet[1],.004).Single(a=>RmAxisDistance(a.Point,lastBracketAxis.Point)<1e-7);mates.Add(RmMate(top,brackets[1],lastBracketAxis.Face,feet[1],lastFootAxis.Face,1,"参考安装_后端孔同轴锁定绕法向转动"));
        RmRebuild(top,"direct_reference_mount");double[] actual=(double[])upper.Transform2.ArrayData;m["upper_pose_actual"]=actual;Need(actual.Zip(requested,(a,b)=>Math.Abs(a-b)).Max()<1e-7,"Native solver changed the verified upper canopy pose");
        var checks=new List<object>();m["native_direct_interface_checks"]=checks;
        for(int i=0;i<2;i++){var ba=RmAxes(brackets[i],.0044);var fa=RmAxes(feet[i],.004);var inside=RmAxes(brackets[i],.003);var outside=RmAxes(brackets[i],.0055);Need(ba.Count==3&&fa.Count==3&&inside.Count==3&&outside.Count==3,"Each side requires three sheet holes, foot holes and fused receiver inner/outer cylinders");
            for(int j=0;j<3;j++){double ef=RmAxisDistance(ba[j].Point,fa[j].Point),ei=RmAxisDistance(ba[j].Point,inside[j].Point),eo=RmAxisDistance(ba[j].Point,outside[j].Point);Need(ef<1e-7&&ei<1e-7&&eo<1e-7,"Reference receiver/foot/sheet axes do not coincide");checks.Add(new{side=i==0?"L":"R",station=j+1,x_mm=ba[j].Point[0]*1000,foot_axis_error_mm=ef*1000,receiver_inner_axis_error_mm=ei*1000,receiver_outer_axis_error_mm=eo*1000});}
            var bp=RmPlanes(brackets[i]);var fp=RmPlanes(feet[i]);double largest=bp.Max(p=>p.Area),q=bp.Where(p=>p.Area>largest*.7).Max(p=>p.Q);Need(fp.Any(p=>Math.Abs(p.Q-q)<1e-7&&p.Area>.005),"No finite-area native foot bearing face exists at the direct bracket plane");
            object info;Array bodies=brackets[i].GetBodies3(0,out info) as Array;Need(bodies!=null&&bodies.Length==1,"Exactly one fused bracket solid required in each actual assembly configuration");}
    }

    static void ApplyReferenceMount(Dictionary<string,object> c,Dictionary<string,object> row,ModelDoc2 top)
    {
        var dimensions=(System.Collections.IList)c["dimensions_mm"]; double width=Convert.ToDouble(dimensions[1]),depth=Convert.ToDouble(dimensions[2]);
        Need((width==950&&depth==550)||(width==1000&&depth==540),"Reference mount is bounded to the two approved cases");
        Need(Root.IndexOf("22057_两组安装修正版_20261016_r3",StringComparison.OrdinalIgnoreCase)>=0,"Mount may modify only the r3 candidate");
        Need(ExpectedVisibleSolids==431,"Reference mount requires the unmodified 431-solid source candidate");
        var m=new Dictionary<string,object>();row["reference_mount_r20"]=m;RmEvidence=m;m["case_id"]=c["id"];
        m["status"]="inspection";m["scope"]="Current A/B native candidates only; reference bosses carry no inferred thread or fastener specification";
        m["dimensions_mm"]=new[]{width,depth};m["normal"]=RmNormal;m["native_save_owned_by_host"]=true;
        var changes=new List<object>();m["controls"]=changes;var suppressed=new List<object>();m["suppressed_features"]=suppressed;
        var lowerNames=new[]{"左立柱焊件2.SLDPRT","左立柱焊件3.SLDPRT","左立柱焊件4.SLDPRT","立柱固定焊件.SLDPRT"};
        var lowerBefore=lowerNames.ToDictionary(n=>n,n=>RmBox(Doc(n)));
        var protectedNames=new[]{"左立柱焊件1.SLDPRT","右立柱焊件1.SLDPRT","太阳能板(1140×840).SLDPRT"};
        var protectedBoxes=protectedNames.ToDictionary(n=>n,n=>RmBox(Doc(n)));
        string purchaseHash=Hash(Path.Combine(Root,"太阳能板(1140×840).SLDPRT"));
        var protectedInstances=new[]{"太阳能雨棚装配-1/左立柱-1","太阳能雨棚装配-1/右立柱-1"}.ToDictionary(n=>n,n=>(double[])RmComponent(top,n).Transform2.ArrayData);
        var feet=new[]{RmComponent(top,"太阳能雨棚装配-1/左立柱-1/左立柱焊件4-1"),RmComponent(top,"太阳能雨棚装配-1/右立柱-1/右立柱焊件4-1")};
        var brackets=new[]{RmBracket(top,true),RmBracket(top,false)};
        var oldFootAxes=feet.Select(f=>RmAxes(f,.004)).ToArray();var oldBracketAxes=brackets.Select(b=>RmAxes(b,.0053)).ToArray();
        Need(oldFootAxes.All(a=>a.Count==3)&&oldBracketAxes.All(a=>a.Count==3),"Expected old three-hole mounting interfaces");
        m["before"]=new{canopy_equations=Eqs(Doc("太阳能雨棚 模型.SLDPRT")),canopy_dimensions=Dims(Doc("太阳能雨棚 模型.SLDPRT")),bracket_sketches=Sketches(Doc("箱体雨棚安装架.SLDPRT")),lower_part_boxes_m=lowerBefore,protected_boxes_m=protectedBoxes,
            feet=feet.Select((f,i)=>new{name=f.Name2,configuration=f.ReferencedConfiguration,axes=RmAxisRows(oldFootAxes[i]),planes=RmPlaneRows(RmPlanes(f))}).ToArray(),
            brackets=brackets.Select((b,i)=>new{name=b.Name2,configuration=b.ReferencedConfiguration,axes=RmAxisRows(oldBracketAxes[i]),planes=RmPlaneRows(RmPlanes(b))}).ToArray()};Flush("mount_r20/inspection_recorded");
        var deleted=RmRemoveOldJoint(top,oldFootAxes,oldBracketAxes,m,suppressed);
        RmDimension(Doc("防水寄存柜 模型.SLDPRT"),"D3@草图119",20,changes);RmRebuild(Doc("防水寄存柜 模型.SLDPRT"),"cabinet_master_reference_offset");
        RmRestoreFlange(Doc("箱体雨棚安装架.SLDPRT"),width==950?35:60,m);
        RmReferenceReceiver(Doc("箱体雨棚安装架.SLDPRT"),m,suppressed,changes);
        var canopy=Doc("太阳能雨棚 模型.SLDPRT");string protectedCanopy=RmProtectedCanopyState(canopy);m["protected_canopy_master_before"]=Json.DeserializeObject(protectedCanopy);Flush("mount_r20/healthy_master_preservation_recorded");
        RmAdjustLowerAtFinishedParts(width,lowerBefore,m);
        string retainedCanopy=RmProtectedCanopyState(canopy);m["protected_canopy_master_after"]=Json.DeserializeObject(retainedCanopy);Need(retainedCanopy==protectedCanopy,"Finished-part adaptation changed a protected canopy master equation, control or source-frame sketch");
        double[] frameBox=RmBox(Doc("立柱连接架.SLDPRT"));double expectedFrame=width-121;m["frame_actual"]=new{bounds_m=frameBox,required_width_mm=expectedFrame,actual_width_mm=(frameBox[3]-frameBox[0])*1000};Need(Math.Abs((frameBox[3]-frameBox[0])*1000-expectedFrame)<1e-4,"Finished frame width differs from the 879/829 mm rule");
        RmRebuild(top,"after_local_parts");
        RmPositionAndMate(top,feet,brackets,width,m);
        foreach(string name in protectedNames){double[] now=RmBox(Doc(name));Need(now.Zip(protectedBoxes[name],(a,b)=>Math.Abs(a-b)).Max()<1e-6,"Protected upper/purchased part geometry changed: "+name);}
        Need(Hash(Path.Combine(Root,"太阳能板(1140×840).SLDPRT"))==purchaseHash,"Purchased solar panel file changed");
        foreach(var p in protectedInstances){double[] now=(double[])RmComponent(top,p.Key).Transform2.ArrayData;double[] upperShift=(double[])m["upper_displacement_m"];double[] expected=(double[])p.Value.Clone();for(int i=0;i<3;i++)expected[9+i]+=upperShift[i];Need(now.Zip(expected,(a,b)=>Math.Abs(a-b)).Max()<1e-7,"Whole side-column assembly moved independently: "+p.Key);}
        int receiverDelta=2*(4-1);ExpectedVisibleSolids=431-deleted-receiverDelta;
        m["solid_accounting"]=new{source_visible_solids=431,removed_independent_joint_solids=deleted,removed_disjoint_stock_receiver_solids=receiverDelta,new_receiver_solids=0,reason="Three reference receiver bosses merge into each existing bracket sheet body",expected_visible_solids=ExpectedVisibleSolids};
        m["unverified"]=new[]{"native folded/flat/refold acceptance by the host","independent STEP contact/interference and exact hole/section acceptance","thread, fastener, roof joining process and strength data"};m["status"]="native_operations_returned_for_host_acceptance";Flush("mount_r20/native_operations_finished");
    }
}
