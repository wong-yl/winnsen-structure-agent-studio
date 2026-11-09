using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void SyncCoverSpan(double width,List<object> changes,Dictionary<string,object> row) {
        var inner=Doc("箱体内顶板.SLDPRT");RmActivate(inner,"cover_opening_span");
        Set(inner,"D2@草图5",width/2-115,changes);Set(inner,"D3@草图5",width/2+45,changes);
        Need(inner.ForceRebuild3(false)&&inner.Extension.GetWhatsWrongCount()==0,"Cover opening span rebuild failed");
        var cover=Doc("盖板.SLDPRT");RmActivate(cover,"cover_span");Set(cover,"D1@草图1",width/2-100,changes);
        Need(cover.ForceRebuild3(false)&&cover.Extension.GetWhatsWrongCount()==0,"Cover span rebuild failed");
        row["cover_span"]=new{width_mm=width/2-100,opening_width_mm=width/2-115,opening_center_pitch_mm=width/2+45,side_overlap_mm=7.5,front_relief_mm=25,nominal_roof_gap_mm=2.8,native_centering_mate_preserved=true};
    }
}
