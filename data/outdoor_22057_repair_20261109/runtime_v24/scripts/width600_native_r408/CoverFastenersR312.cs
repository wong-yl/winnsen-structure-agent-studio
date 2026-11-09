using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void FixCoverProfile(ModelDoc2 d,Sketch s) {
        foreach(SketchSegment segment in s.GetSketchSegments() as Array??new object[0]){d.ClearSelection2(true);Need(segment.Select4(false,null),"Cannot constrain cover profile");d.SketchAddConstraints("sgFIXED");}
        foreach(SketchPoint point in s.GetSketchPoints2() as Array??new object[0])if(point.Type==1){d.ClearSelection2(true);Need(point.Select4(false,null),"Cannot constrain cover point");d.SketchAddConstraints("sgFIXED");}
        d.ClearSelection2(true);
    }
    static void MoveCoverProfile(ModelDoc2 d,string name,Func<double,double> map,List<object> log) {
        RmActivate(d,"cover_fastener/"+name);var f=RmFeature(d,name);d.ClearSelection2(true);Need(f.Select2(false,0),"Cannot select cover fastener profile");d.EditSketch();var s=(Sketch)d.GetActiveSketch2();
        var points=(s.GetSketchPoints2() as Array??new object[0]).Cast<SketchPoint>().Select(p=>new{point=p,xyz=new[]{p.X,p.Y,p.Z}}).ToArray();
        s.RelationManager.DeleteAllRelations();LayoutDropSketchDimensions(d,f);
        foreach(var p in points)Need(p.point.SetCoords(map(p.xyz[0]),p.xyz[1],p.xyz[2]),"Cannot move cover fastener point");
        log.Add(new{phase="before_cover_profile_target_check",document=Path.GetFileName(d.GetPathName()),sketch=name,points=points.Select(p=>new{before=p.xyz,target_x=map(p.xyz[0]),after=new[]{p.point.X,p.point.Y,p.point.Z}}).ToArray()});Flush("cover_profile_targets");
        foreach(var p in points)Need(Math.Abs(p.point.X-map(p.xyz[0]))<1e-8&&Math.Abs(p.point.Y-p.xyz[1])<1e-8,"Cover fastener profile target mismatch");
        FixCoverProfile(d,s);Need(s.GetConstrainedStatus()==3,"Cover fastener profile must be constrained");
        log.Add(new{document=Path.GetFileName(d.GetPathName()),sketch=name,points=points.Select(p=>new{before=p.xyz,after=new[]{p.point.X,p.point.Y,p.point.Z}}).ToArray()});
        d.SketchManager.InsertSketch(true);d.ClearSelection2(true);
    }
    static void SyncCoverFasteners(double width,List<object> log,Dictionary<string,object> row) {
        Need(width>=600&&width<=1200,"Cover fastener range");
        if(Math.Abs(width-950)<1e-8)return;
        double delta=(width-950)/2000;
        var inner=Doc("箱体内顶板.SLDPRT");
        MoveCoverProfile(inner,"草图20",x=>Math.Abs(x)<.25?x:x+Math.Sign(x)*delta,log);
        RmActivate(inner,"cover_nut_blocks");inner.ClearSelection2(true);Need(RmFeature(inner,"草图21").Select2(false,0),"Cannot select cover nut blocks");inner.EditSketch();var s=(Sketch)inner.GetActiveSketch2();
        var blocks=(s.GetSketchBlockInstances() as Array??new object[0]).Cast<SketchBlockInstance>().Select(b=>new{block=b,position=(double[])b.InstancePosition.ArrayData,transform=(double[])b.BlockToSketchTransform.ArrayData}).ToArray();Need(blocks.Length==4,"Four standard M4 blocks required");
        s.RelationManager.DeleteAllRelations();var math=(MathUtility)Sw.GetMathUtility();
        foreach(var b in blocks){var p=(double[])b.position.Clone();if(Math.Abs(b.transform[9])>.25)p[0]+=Math.Sign(b.transform[9])*delta;b.block.InstancePosition=(MathPoint)math.CreatePoint(p);Need(((double[])b.block.InstancePosition.ArrayData).Zip(p,(a,v)=>Math.Abs(a-v)).Max()<1e-8,"Nut block target mismatch");inner.ClearSelection2(true);Need(b.block.Select(false,null),"Cannot constrain nut block");inner.SketchAddConstraints("sgFIXED");}
        FixCoverProfile(inner,s);Need(s.GetConstrainedStatus()==3,"Cover nut blocks must be constrained");
        row["cover_fastener_width_linkage"]=new{width_mm=width,cover_slot_half_pitch_mm=width/4-140,cover_edge_inset_mm=90,inner_bores_abs_x_mm=new[]{162.5,width/2-117.5},standard_nut_geometry_preserved=true,blocks=blocks.Select(b=>new{name=b.block.Name,before=b.transform,after=b.block.BlockToSketchTransform.ArrayData}).ToArray()};
        inner.SketchManager.InsertSketch(true);inner.ClearSelection2(true);Flush("cover_fasteners_prepared");
    }
}
