using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static List<RmAxis> RightRibRearAxes(Component2 component) {
        object info;var raw=component.GetBodies3(0,out info) as Array;Need(raw!=null,"Right rib geometry missing");var rows=new List<RmAxis>();
        foreach(Body2 body in raw)foreach(Face2 face in (Array)body.GetFaces()) {
            var surface=(Surface)face.GetSurface();if(!surface.IsCylinder())continue;var v=(double[])surface.CylinderParams;
            if(Math.Abs(v[6]-.001)>1e-7)continue;
            var p=RmPoint(v.Take(3).ToArray(),component.Transform2);var axis=RmVector(v.Skip(3).Take(3).ToArray(),component.Transform2);
            if(Math.Abs(axis[0])+Math.Abs(axis[1])>1e-7||Math.Abs(Math.Abs(axis[2])-1)>1e-7)continue;
            if(!rows.Any(r=>Math.Abs(r.Point[0]-p[0])+Math.Abs(r.Point[1]-p[1])<1e-7))rows.Add(new RmAxis{Face=face,Point=p,Direction=axis,Radius=v[6]});
        }
        return rows;
    }
    static void ReanchorRightRibMate(double height,double width,Dictionary<string,object> row) {
        var right=Doc("箱体右侧板焊接.SLDASM");RmActivate(right,"right_rear_rib_mate_check");right.ForceRebuild3(false);
        var old=RmFeature(right,"同心3");bool warning;int error=old.GetErrorCode2(out warning);
        row["right_rear_rib_mate_before"]=new{error=error,warning=warning,mates=LayoutMates(right),states=ShStates(right),health=LayoutFeatureHealth(right)};Flush("right_rear_rib_mate_before");
        if(error==0&&!warning){row["right_rear_rib_mate_replaced"]=false;return;}
        Need(error==51,"Unexpected right rear rib mate failure");
        var components=((Array)((AssemblyDoc)right).GetComponents(true)).Cast<Component2>().ToArray();
        var panel=components.Single(c=>c.Name2=="箱体右侧板-1");var rib=components.Single(c=>c.Name2=="OUT_RightSideRib-3");
        var panelAxes=RightRibRearAxes(panel);var ribAxes=RightRibRearAxes(rib);
        double x=Math.Max(75,width/2-300)/1000,y=(height-118.8)/1000;
        row["right_rear_rib_actual_pilots"]=new{expected_xy_m=new[]{x,y},panel=panelAxes.Select(a=>new{point=a.Point,axis=a.Direction,radius=a.Radius}).ToArray(),rib=ribAxes.Select(a=>new{point=a.Point,axis=a.Direction,radius=a.Radius}).ToArray()};Flush("right_rear_rib_actual_pilots");
        var pa=panelAxes.Where(a=>Math.Abs(a.Point[0]-x)+Math.Abs(a.Point[1]-y)<1e-7).ToArray();
        var ra=ribAxes.Where(a=>Math.Abs(a.Point[0]-x)+Math.Abs(a.Point[1]-y)<1e-7).ToArray();
        Need(pa.Length==1&&ra.Length==1,"Unique original right rear rib pilot pair must physically remain");
        var before=(double[])rib.Transform2.ArrayData;right.ClearSelection2(true);Need(old.Select2(false,0)&&right.Extension.DeleteSelection2(0),"Cannot replace dangling right rear rib mate");
        row["right_rear_rib_new_mate"]=RmMate(right,panel,pa[0].Face,rib,ra[0].Face,1,"布局_右后筋顶部辅助孔同轴");
        bool rebuilt=right.ForceRebuild3(false);var after=(double[])rib.Transform2.ArrayData;
        row["right_rear_rib_mate_after"]=new{rebuilt=rebuilt,issues=right.Extension.GetWhatsWrongCount(),before=before,after=after,constrained=rib.GetConstrainedStatus(),mates=LayoutMates(right),health=LayoutFeatureHealth(right)};row["right_rear_rib_mate_replaced"]=true;Flush("right_rear_rib_mate_after");
        Need(rebuilt&&right.Extension.GetWhatsWrongCount()==0,"Reanchored right rear rib mate is unhealthy");
        Need(before.Zip(after,(a,b)=>Math.Abs(a-b)).Max()<1e-7,"Reanchoring changed the physical right rib position");
        Need(rib.GetConstrainedStatus()==3,"Reanchored right rib must be fully constrained");
    }
}
