using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void SyncShelfNotchHeight(ModelDoc2 d,List<object> changes,Dictionary<string,object> row) {
        RmActivate(d,"shelf_notch_refresh");bool initialRebuilt=d.ForceRebuild3(false);row["shelf_notch_before_alignment"]=new{rebuild=initialRebuilt,health=LayoutFeatureHealth(d)};
        var baseSketch=RmFeature(d,"草图1").GetSpecificFeature2() as Sketch;
        var profile=RmFeature(d,"草图33").GetSpecificFeature2() as Sketch;
        var basePoints=((Array)baseSketch.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>RmPoint(new[]{p.X,p.Y,p.Z},baseSketch.ModelToSketchTransform.IInverse())).ToArray();
        double targetY=basePoints.Min(p=>p[1])*1000,oldY=LayoutSketchCenterY(d,"草图33"),delta=targetY-oldY;
        var before=((Array)profile.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>RmPoint(new[]{p.X,p.Y,p.Z},profile.ModelToSketchTransform.IInverse())).ToArray();
        var lengths=((Array)profile.GetSketchSegments()).Cast<SketchSegment>().Select(s=>s.GetLength()).OrderBy(v=>v).ToArray();
        Need(before.Length==12&&lengths.Length==12&&lengths.Take(6).All(v=>Math.Abs(v-.0024)<1e-8)&&lengths.Skip(6).All(v=>Math.Abs(v-.0054)<1e-8),"Three standard 5.4 by 2.4 shelf notches required");
        if(Math.Abs(delta)>.000001)LayoutMoveSketchY(d,"草图33",delta,changes);
        bool rebuilt=d.ForceRebuild3(false);var after=((Array)profile.GetSketchPoints2()).Cast<SketchPoint>().Select(p=>RmPoint(new[]{p.X,p.Y,p.Z},profile.ModelToSketchTransform.IInverse())).ToArray();
        Need(rebuilt&&d.Extension.GetWhatsWrongCount()==0,"Shelf notch linkage failed native rebuild");
        Need(after.Length==before.Length&&before.Zip(after,(a,b)=>Math.Max(Math.Abs(a[0]-b[0]),Math.Max(Math.Abs(a[2]-b[2]),Math.Abs(a[1]+delta/1000-b[1])))).Max()<1e-8,"Shelf notch change must be a pure height translation");
        row["shelf_notch_height_linkage"]=new{delta_y_mm=delta,target_y_mm=targetY,notch_count=3,width_mm=5.4,height_mm=2.4,before_m=before,after_m=after,preserved_shelf_channel_hole_z_mm=-96.35};
        Flush("shelf_notch_height_linkage");
    }
}
