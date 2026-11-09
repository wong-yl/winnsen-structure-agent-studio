using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static object Cylinders(ModelDoc2 d) {
        return (((PartDoc)d).GetBodies2(0,false) as Array??new object[0]).Cast<Body2>().SelectMany(b=>(b.GetFaces() as Array??new object[0]).Cast<Face2>()).Where(f=>((Surface)f.GetSurface()).IsCylinder()).Select(f=>new{box=f.GetBox(),cylinder=((Surface)f.GetSurface()).CylinderParams,area=f.GetArea()}).ToArray();
    }
    static void SyncDividerPilotSheet(ModelDoc2 d,string sketchName,double depth,Dictionary<string,object> row) {
        RmActivate(d,"divider_sheet_pilots");d.ClearSelection2(true);Need(RmFeature(d,sketchName).Select2(false,0),"Cannot select pilot sketch");d.EditSketch();var s=(Sketch)d.GetActiveSketch2();
        int removed=0;
        while(true){var relation=(s.RelationManager.GetRelations(0) as Array??new object[0]).Cast<SketchRelation>().FirstOrDefault(r=>r.GetRelationType()==9&&(r.GetEntities() as Array??new object[0]).Cast<object>().OfType<SketchPoint>().Any(p=>Math.Abs(p.X+.0085)<1e-8));if(relation==null)break;Need(s.RelationManager.DeleteRelation(relation),"Cannot release stale divider pilot reference");removed++;Need(removed<4,"Unexpected pilot relations");}
        Need(removed==2,"Expected two divider pilot references");
        var arcs=(s.GetSketchSegments() as Array??new object[0]).Cast<SketchSegment>().OfType<SketchArc>().Where(a=>Math.Abs(a.GetRadius()-.001)<1e-8&&Math.Abs(((SketchPoint)a.GetCenterPoint2()).X+.0085)<1e-8).OrderByDescending(a=>Math.Abs(((SketchPoint)a.GetCenterPoint2()).Y)).ToArray();Need(arcs.Length==2,"Expected two left divider pilots");
        for(int i=0;i<2;i++){var point=(SketchPoint)arcs[i].GetCenterPoint2();double sign=Math.Sign(point.Y);Need(point.SetCoords(-.0085,sign*(i==0?(depth-101.2)/1000:.2488),0),"Cannot move pilot");d.ClearSelection2(true);Need(point.Select4(false,null),"Cannot select pilot center");d.SketchAddConstraints("sgFIXED");}
        Need(s.GetConstrainedStatus()==3,"Pilot sketch not fully constrained");
        var actual=(s.GetSketchSegments() as Array??new object[0]).Cast<SketchSegment>().OfType<SketchArc>().Where(a=>Math.Abs(a.GetRadius()-.001)<1e-8&&Math.Abs(Math.Abs(((SketchPoint)a.GetCenterPoint2()).X)-.0085)<1e-8).Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new[]{p.X,p.Y,a.GetRadius()};}).OrderBy(p=>Math.Sign(p[0])).ThenBy(p=>Math.Abs(p[1])).ToArray();Need(actual.Length==4,"Expected four pilot circles");
        for(int i=0;i<4;i++)Need(Math.Abs(Math.Abs(actual[i][1])-(i%2==0?.2488:(depth-101.2)/1000))<1e-8,"Pilot target mismatch");
        d.SketchManager.InsertSketch(true);bool rebuilt=d.ForceRebuild3(false);row[Path.GetFileName(d.GetPathName())]=new{constrained=3,rebuild=rebuilt,health=LayoutFeatureHealth(d),pilots=actual,cylinders=Cylinders(d)};Flush("divider_pilot_sheet/"+sketchName);Need(rebuilt&&d.Extension.GetWhatsWrongCount()==0,"Pilot sheet final rebuild failed");
    }
    static void SyncDividerDepth(double depth,List<object> changes,Dictionary<string,object> row) {
        var divider=Doc("箱体竖隔板.SLDPRT");RmActivate(divider,"divider_hole_pitch");Set(divider,"D2@草图13",depth-350,changes);Need(divider.ForceRebuild3(false)&&divider.Extension.GetWhatsWrongCount()==0,"Divider rebuild failed");
        row["divider_depth_pilots"]=new{pitch_mm=depth-350,rear_edge_offset_mm=100,front_edge_offset_mm=93.4,cylinders=Cylinders(divider)};
        SyncDividerPilotSheet(Doc("箱体内顶板.SLDPRT"),"草图5",depth,row);SyncDividerPilotSheet(Doc("箱体底板.SLDPRT"),"草图16",depth,row);
    }
}
