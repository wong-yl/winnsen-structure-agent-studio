using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static object LayoutFeatureHealth(ModelDoc2 d)
    {
        return Features(d).Select(f=>{bool warning;int error=f.GetErrorCode2(out warning);return new{name=f.Name,type=f.GetTypeName2(),error=error,warning=warning,suppressed=f.IsSuppressed2(1,null)};}).Where(f=>f.error!=0||f.warning).ToArray();
    }
    static void LayoutDropSketchDimensions(ModelDoc2 d,Feature feature)
    {
        var annotations=new List<Annotation>();var seen=new HashSet<string>();
        for(DisplayDimension display=feature.GetFirstDisplayDimension() as DisplayDimension;display!=null;display=feature.GetNextDisplayDimension(display) as DisplayDimension){
            var dim=display.GetDimension2(0);if(dim==null||!seen.Add(dim.FullName))break;annotations.Add(display.GetAnnotation() as Annotation);
        }
        d.ClearSelection2(true);foreach(var annotation in annotations)Need(annotation.Select3(true,null),"Cannot select obsolete row dimension");
        if(annotations.Count>0)Need(d.Extension.DeleteSelection2(0),"Cannot remove obsolete row dimensions");d.ClearSelection2(true);
    }
    static void LayoutMoveSketchY(ModelDoc2 d,string name,double deltaMm,List<object> changes)
    {
        RmActivate(d,"layout_row_sketch/"+name);var feature=RmFeature(d,name);d.ClearSelection2(true);Need(feature.Select2(false,0),"Cannot select row sketch");d.EditSketch();
        var sketch=d.GetActiveSketch2() as Sketch;Need(sketch!=null,"Row sketch not active");
        var points=(sketch.GetSketchPoints2() as Array??new object[0]).Cast<SketchPoint>().Select(p=>new{point=p,xyz=new[]{p.X,p.Y,p.Z}}).ToArray();
        var origin=RmPoint(new double[3],sketch.ModelToSketchTransform);var tip=RmPoint(new[]{0.0,deltaMm/1000,0.0},sketch.ModelToSketchTransform);var delta=tip.Zip(origin,(a,b)=>a-b).ToArray();
        sketch.RelationManager.DeleteAllRelations();LayoutDropSketchDimensions(d,feature);
        foreach(var p in points)Need(p.point.SetCoords(p.xyz[0]+delta[0],p.xyz[1]+delta[1],p.xyz[2]+delta[2]),"Native row point move failed");
        foreach(var p in points)Need(new[]{p.point.X,p.point.Y,p.point.Z}.Zip(p.xyz.Zip(delta,(a,b)=>a+b),(a,b)=>Math.Abs(a-b)).Max()<1e-8,"Native row point did not reach its target");
        foreach(SketchSegment segment in sketch.GetSketchSegments() as Array??new object[0]){d.ClearSelection2(true);Need(segment.Select4(false,null),"Cannot constrain row profile");d.SketchAddConstraints("sgFIXED");}
        d.ClearSelection2(true);foreach(var p in points.Where(p=>p.point.Type==1)){Need(p.point.Select4(false,null),"Cannot constrain row point");d.SketchAddConstraints("sgFIXED");d.ClearSelection2(true);}
        int constrained=sketch.GetConstrainedStatus();Need(constrained==3,"Edited row profile must be fully constrained");d.SketchManager.InsertSketch(true);
        changes.Add(new{document=d.GetTitle(),sketch=name,model_y_delta_mm=deltaMm,fully_constrained=true});
    }
    static double LayoutSketchCenterY(ModelDoc2 d,string name)
    {
        var sketch=RmFeature(d,name).GetSpecificFeature2() as Sketch;Need(sketch!=null,"Row sketch required");
        var points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>RmPoint(new[]{p.X,p.Y,p.Z},sketch.ModelToSketchTransform.IInverse())[1]*1000).ToArray();
        return (points.Min()+points.Max())/2;
    }
    static Feature LayoutYAxis(ModelDoc2 d)
    {
        var old=Features(d).SingleOrDefault(f=>f.Name=="布局_竖直阵列轴");if(old!=null)return old;
        d.ClearSelection2(true);Need(RmFeature(d,"前视").Select2(false,0)&&RmFeature(d,"右视").Select2(true,0),"Cannot select Y-axis reference planes");
        var before=new HashSet<string>(Features(d).Select(f=>f.Name));Need(d.InsertAxis2(true),"Cannot create native vertical layout axis");
        var axis=Features(d).Single(f=>!before.Contains(f.Name)&&f.GetTypeName2()=="RefAxis");axis.Name="布局_竖直阵列轴";return axis;
    }
    static void LayoutFeatureRows(ModelDoc2 d,string oldPattern,string[] sketches,double[] targets,double currentDatumMm,List<object> changes,bool deferHealth=false)
    {
        RmActivate(d,"layout_feature_rows/"+oldPattern);var old=RmFeature(d,oldPattern);var definition=old.GetDefinition() as LinearPatternFeatureData;Need(definition!=null&&definition.AccessSelections(d,null),"Cannot inspect original row features");
        string[] seeds=((Array)definition.PatternFeatureArray).Cast<Feature>().Select(f=>f.Name).ToArray();bool geometry=definition.GeometryPattern;definition.ReleaseSelectionAccess();
        RmSuppressFeature(d,old,changes,1);
        if(targets.Length==0){foreach(string name in seeds.Reverse())RmSuppressFeature(d,RmFeature(d,name),changes,1);return;}
        double first=targets[0];foreach(string sketch in sketches)LayoutMoveSketchY(d,sketch,first-currentDatumMm,changes);
        bool rebuilt=d.ForceRebuild3(false);Need(rebuilt||deferHealth,"Native seed row rebuild failed "+d.GetTitle());
        if(targets.Length==1)return;var axis=LayoutYAxis(d);var points=(double[])((RefAxis)axis.GetSpecificFeature2()).GetRefAxisParams();
        double axisY=points[4]-points[1];Need(Math.Abs(axisY)>1e-8&&Math.Abs(points[3]-points[0])<1e-8&&Math.Abs(points[5]-points[2])<1e-8,"Vertical native pattern axis required");
        for(int i=1;i<targets.Length;i++){
            double delta=(targets[i]-first)/1000;Need(Math.Abs(delta)>1e-8,"Duplicate layout rows");d.ClearSelection2(true);Need(axis.Select2(false,1),"Cannot select independent layout axis");
            foreach(string n in seeds)Need(RmFeature(d,n).Select2(true,4),"Cannot select source row feature");
            var feature=d.FeatureManager.FeatureLinearPattern5(2,Math.Abs(delta),1,0,delta*axisY<0,false,"NULL","NULL",geometry,false,false,false,false,false,false,false,false,false,0,0,false,false) as Feature;
            Need(feature!=null,"Cannot create native layout row");feature.Name="布局_"+oldPattern+"_"+(i+1);bool warning;Need(feature.GetErrorCode2(out warning)==0&&!warning,"Native layout pattern unhealthy");
            changes.Add(new{document=d.GetTitle(),feature=feature.Name,target_datum_mm=targets[i],delta_y_mm=delta*1000});
        }
        d.ClearSelection2(true);rebuilt=d.ForceRebuild3(false);Need(rebuilt||deferHealth,"Native row patterns failed rebuild");
    }
    static void LayoutPreserveAuxiliaryHoles(ModelDoc2 d,List<object> changes)
    {
        RmActivate(d,"layout_preserve_auxiliary_holes");var feature=RmFeature(d,"草图8");
        d.ClearSelection2(true);Need(feature.Select2(false,0),"Cannot select auxiliary-hole sketch");d.EditSketch();
        var sketch=d.GetActiveSketch2() as Sketch;var segments=((Array)sketch.GetSketchSegments()).Cast<SketchSegment>().ToArray();
        var holes=segments.OfType<SketchArc>().ToArray();Need(holes.Length==3&&holes.All(a=>Math.Abs(a.GetRadius()-.003)<1e-8),"Three standard auxiliary bores required");
        var before=holes.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new[]{p.X,p.Y,p.Z,a.GetRadius()};}).ToArray();
        sketch.RelationManager.DeleteAllRelations();LayoutDropSketchDimensions(d,feature);
        foreach(var segment in segments){d.ClearSelection2(true);Need(segment.Select4(false,null),"Cannot preserve auxiliary-hole geometry");d.SketchAddConstraints("sgFIXED");}
        Need(sketch.GetConstrainedStatus()==3,"Auxiliary holes must remain fully constrained");
        var after=holes.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new[]{p.X,p.Y,p.Z,a.GetRadius()};}).ToArray();
        Need(before.SelectMany(a=>a).Zip(after.SelectMany(a=>a),(a,b)=>Math.Abs(a-b)).Max()<1e-9,"Auxiliary-hole geometry changed");
        d.SketchManager.InsertSketch(true);d.ClearSelection2(true);changes.Add(new{document=d.GetTitle(),sketch="草图8",preserved_auxiliary_bores_m=after,fully_constrained=true});
    }
    static void LayoutLockPattern(ModelDoc2 d,string patternName,double[] centers,List<object> changes)
    {
        var feature=RmFeature(d,patternName);if(centers.Length==1){RmSuppressFeature(d,feature,changes,1);return;}
        var definition=feature.GetDefinition() as SketchPatternFeatureData;Need(definition.AccessSelections(d,null),"Cannot access independent lock pattern");
        string sketchName=((Feature)definition.Sketch).Name;var reference=(SketchPoint)definition.ReferencePoint;double x=reference.X;definition.ReleaseSelectionAccess();
        RmActivate(d,"layout_lock_points");var sketchFeature=RmFeature(d,sketchName);d.ClearSelection2(true);Need(sketchFeature.Select2(false,0),"Cannot select lock-point sketch");d.EditSketch();
        var sketch=d.GetActiveSketch2() as Sketch;Need(sketch!=null,"Lock-point sketch not active");sketch.RelationManager.DeleteAllRelations();LayoutDropSketchDimensions(d,sketchFeature);
        d.ClearSelection2(true);int selected=0;
        foreach(SketchSegment segment in (Array)sketch.GetSketchSegments()){Need(segment.Select4(true,null),"Cannot select obsolete lock construction geometry");selected++;}
        foreach(SketchPoint point in (Array)sketch.GetSketchPoints2()){Need(point.Select4(true,null),"Cannot select obsolete lock point");selected++;}
        if(selected>0)Need(d.Extension.DeleteSelection2(0),"Cannot clear obsolete lock points");
        var remaining=sketch.GetSketchPoints2() as Array;Need(remaining==null||remaining.Length==0,"Obsolete lock points remain");
        foreach(double center in centers.Skip(1)){var p=d.SketchManager.CreatePoint(x,center/1000,0);Need(p!=null,"Cannot create independent lock row point");d.ClearSelection2(true);Need(p.Select4(false,null),"Cannot fix lock-point coordinate");d.SketchAddConstraints("sgFIXED");}
        Need(sketch.GetConstrainedStatus()==3,"Independent lock-point sketch must be fully constrained");d.SketchManager.InsertSketch(true);d.ClearSelection2(true);
        changes.Add(new{document=d.GetTitle(),pattern=patternName,requested_lock_centers_mm=centers,repeat_points=centers.Length-1});
    }
    static void BuildLayoutFeatures(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        var rows=LayoutRows(c);if(rows.Count==0)return;var changes=new List<object>();row["layout_feature_changes"]=changes;var boundaries=new Dictionary<string,double[]>();
        foreach(string side in new[]{"L","R"})boundaries[side]=rows.Where(r=>(string)r["side"]==side&&!(bool)r["top_door"]).Select(r=>Convert.ToDouble(r["boundary_mm"])).OrderByDescending(v=>v).ToArray();
        var channel=Doc("箱体锁安装槽.SLDPRT");RmActivate(channel,"independent_lock_rows");LayoutPreserveAuxiliaryHoles(channel,changes);
        foreach(string side in new[]{"L","R"}){
            var centers=rows.Where(r=>(string)r["side"]==side).Select(r=>Convert.ToDouble(r["center_mm"])-15).OrderByDescending(v=>v).ToArray();
            string control=side=="L"?"D2@草图1":"D1@草图32",bores=side=="L"?"草图2":"草图33";double current=LayoutSketchCenterY(channel,bores);var dimension=channel.Parameter(control) as Dimension;
            Set(channel,control,dimension.SystemValue*1000+current-centers[0],changes);bool seedRebuild=channel.ForceRebuild3(false);
            changes.Add(new{side=side,seed_rebuild=seedRebuild,health=LayoutFeatureHealth(channel)});Flush("layout_lock_seed/"+side);
            Need(Math.Abs(LayoutSketchCenterY(channel,bores)-centers[0])<.001,"Lock seed center did not follow its control");
            LayoutLockPattern(channel,side=="L"?"草图阵列1":"草图阵列3",centers,changes);
        }
        channel.ForceRebuild3(false);row["lock_pattern_intermediate_health"]=LayoutFeatureHealth(channel);Flush("layout_lock_patterns_intermediate_health");
        foreach(string side in new[]{"L","R"}){
            bool left=side=="L";var d=Doc(left?"箱体左侧板.SLDPRT":"箱体右侧板.SLDPRT");string sketch=left?"草图66":"草图22";
            double datum=LayoutSketchCenterY(d,sketch)-20;
            LayoutFeatureRows(d,left?"阵列(线性)*152":"阵列(线性)2",left?new[]{"草图66","草图72"}:new[]{"草图22","草图23"},boundaries[side],datum,changes);
            d=Doc(left?"箱体侧板加强筋.SLDPRT":"OUT_RightSideRib.SLDPRT");var s=RmFeature(d,"草图7").GetSpecificFeature2() as Sketch;
            var p=((Array)s.GetSketchPoints2()).Cast<SketchPoint>().Single(point=>point.Type==1);datum=RmPoint(new[]{p.X,p.Y,p.Z},s.ModelToSketchTransform.IInverse())[1]*1000-17.7;
            LayoutFeatureRows(d,"阵列(线性)1",new[]{"草图7"},boundaries[side],datum,changes);
            d=channel;string profile=left?"草图45":"草图46";datum=LayoutSketchCenterY(d,profile)-25;
            LayoutFeatureRows(d,left?"阵列(线性)2":"阵列(线性)3",new[]{profile},boundaries[side],datum,changes,true);
        }
        var union=boundaries.Values.SelectMany(v=>v).Distinct().OrderByDescending(v=>v).ToArray();var stop=Doc("箱体竖门框挡边.SLDPRT");
        LayoutFeatureRows(stop,"阵列(线性)2",new[]{"草图9"},union,LayoutSketchCenterY(stop,"草图9")-20,changes);
        var frame=Doc("箱体防水门框 竖.SLDPRT");double topBudget=Convert.ToDouble(((System.Collections.IList)c["dimensions_mm"])[0])-115;
        foreach(string side in new[]{"L","R"}){string sketch=side=="L"?"草图54":"草图55";var targets=boundaries[side].Concat(new[]{topBudget}).OrderByDescending(v=>v).ToArray();
            LayoutFeatureRows(frame,side=="L"?"阵列(线性)1":"阵列(线性)2",new[]{sketch},targets,LayoutSketchCenterY(frame,sketch)-1.8,changes);}
        bool lockRebuild=channel.ForceRebuild3(false);row["lock_pattern_final_health"]=LayoutFeatureHealth(channel);row["lock_pattern_final_geometry"]=new{sketches=Sketches(channel),patterns=Features(channel).Where(f=>f.GetTypeName2()=="SketchPattern").Select(f=>LayoutPatternDetail(channel,f)).ToArray()};Flush("layout_features_final_health");
        Need(lockRebuild&&channel.Extension.GetWhatsWrongCount()==0,"Independent lock patterns failed final rebuild");
    }
}
