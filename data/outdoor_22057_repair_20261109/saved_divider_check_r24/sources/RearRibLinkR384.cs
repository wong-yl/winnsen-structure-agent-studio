using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void SyncRearRibPilots(double height,double width,Dictionary<string,object> row) {
        var d=Doc("箱体左侧板.SLDPRT");RmActivate(d,"narrow_rear_rib_pilots");var f=RmFeature(d,"草图74");
        d.ClearSelection2(true);Need(f.Select2(false,0),"Cannot select rear rib pilot sketch");d.EditSketch();var s=(Sketch)d.GetActiveSketch2();
        var segments=(s.GetSketchSegments() as Array??new object[0]).Cast<SketchSegment>().ToArray();
        var arcs=segments.OfType<SketchArc>().OrderByDescending(a=>Math.Round(((SketchPoint)a.GetCenterPoint2()).Y,8)).ThenBy(a=>((SketchPoint)a.GetCenterPoint2()).X).ToArray();
        Need(segments.Length==3&&arcs.Length==3&&arcs.All(a=>Math.Abs(a.GetRadius()-.001)<1e-8),"Three original 2mm rear rib pilots required");
        var before=arcs.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new[]{p.X,p.Y,p.Z,a.GetRadius()};}).ToArray();
        double center=Math.Max(75,width/2-300);var target=new[]{new[]{center/1000,(height-118.8)/1000,0.0},new[]{(center-15)/1000,.060,0.0},new[]{(center+15)/1000,.060,0.0}};
        s.RelationManager.DeleteAllRelations();LayoutDropSketchDimensions(d,f);
        for(int i=0;i<3;i++){var p=(SketchPoint)arcs[i].GetCenterPoint2();Need(p.SetCoords(target[i][0],target[i][1],0),"Cannot link rear rib pilot center");}
        foreach(var a in arcs){d.ClearSelection2(true);Need(((SketchSegment)a).Select4(false,null),"Cannot constrain rear rib pilot");d.SketchAddConstraints("sgFIXED");}
        Need(s.GetConstrainedStatus()==3,"Rear rib pilots must be fully constrained");
        var after=arcs.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new[]{p.X,p.Y,p.Z,a.GetRadius()};}).ToArray();
        Need(after.Zip(target,(a,b)=>a.Take(3).Zip(b,(u,v)=>Math.Abs(u-v)).Max()).Max()<1e-8,"Rear pilot targets differ");
        row["rear_rib_pilot_linkage"]=new{center_offset_mm=center,minimum_center_offset_mm=75,original_radius_mm=1,before=before,after=after,constrained=3};
        d.SketchManager.InsertSketch(true);d.ClearSelection2(true);Flush("narrow_rear_rib_pilots");
    }
}
