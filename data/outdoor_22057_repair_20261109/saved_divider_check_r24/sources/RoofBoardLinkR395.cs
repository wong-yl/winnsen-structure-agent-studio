using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void SyncRoofBoardMounts(double width,List<object> changes,Dictionary<string,object> row) {
        var roof=Doc("箱体顶板.SLDPRT");RmActivate(roof,"roof_board_mount_linkage");
        double center=Math.Min(260,width/2-125);
        Need(center>=175,"Roof controller mount outside investigated width");
        Set(roof,"D3@草图24",2*center,changes);
        Need(Math.Abs(((Dimension)roof.Parameter("D2@草图24")).SystemValue-.160)<1e-9,"Original 160mm mounting pitch changed");
        row["roof_board_mount_linkage"]=new{cabinet_width_mm=width,center_offset_mm=center,pair_pitch_mm=160,outer_pilot_abs_x_mm=center+80,inner_pilot_abs_x_mm=center-80,minimum_side_margin_mm=width/2-center-80,standard_bracket_geometry_preserved=true,original_mates_preserved=true};
        Flush("roof_board_mount_linkage");
    }
}
