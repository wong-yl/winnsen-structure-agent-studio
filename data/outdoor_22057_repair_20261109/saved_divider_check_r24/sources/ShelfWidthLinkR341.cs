using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void ShelfWidthProfile(ModelDoc2 d,string name,Func<double,double> targetX,List<object> changes) {
        RmActivate(d,"narrow_shelf_profile/"+name);var feature=RmFeature(d,name);
        d.ClearSelection2(true);Need(feature.Select2(false,0),"Cannot select narrow shelf profile");d.EditSketch();
        var sketch=d.GetActiveSketch2() as Sketch;Need(sketch!=null,"Shelf sketch required");
        var points=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new{p=p,x=p.X,y=p.Y,z=p.Z}).ToArray();
        var segments=((Array)sketch.GetSketchSegments()).Cast<SketchSegment>().ToArray();
        var radii=segments.OfType<SketchArc>().Select(a=>a.GetRadius()).ToArray();
        Need(sketch.RelationManager.DeleteAllRelations(),"Cannot release obsolete shelf external references");LayoutDropSketchDimensions(d,feature);
        foreach(var v in points)Need(v.p.SetCoords(targetX(v.x),v.y,v.z),"Shelf profile move rejected");
        foreach(var segment in segments){d.ClearSelection2(true);Need(segment.Select4(false,null),"Cannot fix shelf profile");d.SketchAddConstraints("sgFIXED");}
        Need(sketch.GetConstrainedStatus()==3,"Narrow shelf profile must be fully constrained");
        var actual=points.Select(v=>new[]{v.p.X,v.p.Y,v.p.Z}).ToArray();
        Need(points.Select((v,i)=>Math.Max(Math.Abs(actual[i][0]-targetX(v.x)),Math.Max(Math.Abs(actual[i][1]-v.y),Math.Abs(actual[i][2]-v.z)))).Max()<1e-8,"Shelf profile did not reach targets");
        Need(radii.Zip(segments.OfType<SketchArc>().Select(a=>a.GetRadius()),(a,b)=>Math.Abs(a-b)).DefaultIfEmpty(0).Max()<1e-9,"Shelf auxiliary bore radius changed");
        d.SketchManager.InsertSketch(true);changes.Add(new{document=d.GetTitle(),sketch=name,points_m=actual,fully_constrained=true});
    }
    static void SyncNarrowShelfWidth(ModelDoc2 d,double width,List<object> changes,Dictionary<string,object> row) {
        Need(width>=600&&width<900,"Narrow shelf route only");
        d.ForceRebuild3(false);
        var holes=(Sketch)RmFeature(d,"草图5").GetSpecificFeature2();
        var arcs=((Array)holes.GetSketchSegments()).Cast<SketchSegment>().OfType<SketchArc>().ToArray();
        Need(arcs.Length==4&&arcs.All(a=>Math.Abs(a.GetRadius()-.001)<1e-8),"Four standard 2 mm shelf holes required");
        double outerHole=-(width/2-87.75)/1000,innerHole=-.08725;
        var oldXs=((Array)holes.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>p.X).Distinct().OrderBy(v=>v).ToArray();Need(oldXs.Length==2,"Two shelf hole columns required");
        ShelfWidthProfile(d,"草图5",x=>Math.Abs(x-oldXs[0])<1e-7?outerHole:innerHole,changes);
        var notch=(Sketch)RmFeature(d,"草图33").GetSpecificFeature2();
        var xs=((Array)notch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>p.X).OrderBy(x=>x).ToArray();
        Need(xs.Length==12,"Three shelf notch rectangles required");
        var oldCenters=new[]{xs.Take(4).Average(),xs.Skip(4).Take(4).Average(),xs.Skip(8).Take(4).Average()};
        var targets=new[]{-(width/2-111)/1000,-(width/4-23.5)/1000,-.064};
        ShelfWidthProfile(d,"草图33",x=>{int i=Enumerable.Range(0,3).OrderBy(j=>Math.Abs(x-oldCenters[j])).First();return x-oldCenters[i]+targets[i];},changes);
        row["shelf_width_linkage"]=new{width_mm=width,notch_centers_mm=targets.Select(x=>x*1000).ToArray(),auxiliary_hole_x_mm=new[]{outerHole*1000,innerHole*1000}};
    }
}
