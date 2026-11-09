using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void SyncSideTopTrim(ModelDoc2 d,double height,Dictionary<string,object> row) {
        RmActivate(d,"side_top_trim_closure");var feature=RmFeature(d,"草图61");d.ClearSelection2(true);Need(feature.Select2(false,0),"Cannot edit side top trimming profile");d.EditSketch();
        var sketch=d.GetActiveSketch2() as Sketch;Need(sketch!=null,"Side trim sketch required");
        var points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new{point=p,xyz=new[]{p.X,p.Y,p.Z}}).ToArray();
        var outside=points.Where(p=>p.xyz[1]>1.99&&p.xyz[1]<2.05).ToArray();Need(outside.Length==2,"Expected two original external trim closure vertices");
        var before=points.Select(p=>p.xyz).ToArray();sketch.RelationManager.DeleteAllRelations();LayoutDropSketchDimensions(d,feature);
        sketch=d.GetActiveSketch2() as Sketch;var live=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().ToArray();
        int moved=0;foreach(var p in live.Where(p=>p.Y>1.99&&p.Y<2.05)){Need(p.SetCoords(p.X,p.Y+(height-1939)/1000,p.Z),"Cannot move external top trim closure");moved++;}Need(moved==2,"Two trim closure vertices required");
        foreach(SketchSegment segment in sketch.GetSketchSegments() as Array??new object[0]){d.ClearSelection2(true);Need(segment.Select4(false,null),"Cannot fix top trimming profile");d.SketchAddConstraints("sgFIXED");}
        foreach(var p in live.Where(p=>p.Type==1)){d.ClearSelection2(true);Need(p.Select4(false,null),"Cannot fix trimming reference point");d.SketchAddConstraints("sgFIXED");}
        int constrained=sketch.GetConstrainedStatus();row["side_top_trim_closure"]=new{before=before,after=live.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray(),fully_constrained=constrained==3,constrained_status=constrained};Flush("side_top_trim_closure");
        Need(constrained==3,"Top trimming profile must be fully constrained");d.SketchManager.InsertSketch(true);d.ClearSelection2(true);
        bool rebuilt=d.ForceRebuild3(false);row["side_top_trim_rebuild"]=new{rebuild=rebuilt,health=LayoutFeatureHealth(d)};Flush("side_top_trim_rebuilt");Need(rebuilt&&d.Extension.GetWhatsWrongCount()==0,"Side top trim repair failed");
    }
}
