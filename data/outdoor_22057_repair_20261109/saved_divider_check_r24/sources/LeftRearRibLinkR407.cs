using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void SyncLeftRearRib(double width,List<object> changes,Dictionary<string,object> row) {
        var left=Doc("箱体左侧板焊接.SLDASM");RmActivate(left,"left_rear_rib_width_linkage");
        double center=Math.Max(75,width/2-300),distance=width/2-center;
        Set(left,"D1@距离3",distance,changes);
        Need(left.ForceRebuild3(false)&&left.Extension.GetWhatsWrongCount()==0,"Left rear rib mounting distance failed");
        row["left_rear_rib_width_linkage"]=new{cabinet_width_mm=width,rear_rib_center_abs_x_mm=center,side_plane_distance_mm=distance,mate="距离3",original_mates_preserved=true,mates=LayoutMates(left),states=ShStates(left)};
        Flush("left_rear_rib_width_linkage");
    }
}
