using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void SyncNarrowLamp(double width,List<object> changes,Dictionary<string,object> row) {
        Need(width>=600&&width<900,"Narrow lamp linkage range");
        var shell=Doc("雨棚LED灯罩壳.SLDPRT");var cover=Doc("塑料灯罩.SLDPRT");var retainer=Doc("塑料板压条焊件.SLDPRT");
        Set(shell,"D2@草图17",width-470,changes);
        Set(cover,"D2@草图1",width-443,changes);
        Set(retainer,"D1@拉伸-薄壁1",width-440,changes);
        Set(retainer,"D3@阵列(线性)1",(width-490)/4,changes);
        foreach(var part in new[]{shell,cover,retainer}) {
            bool rebuilt=part.ForceRebuild3(false);
            row["lamp_latest_part_rebuild"]=new{name=part.GetTitle(),rebuilt=rebuilt,health=LayoutFeatureHealth(part),details=ShDetails(part)};
            Flush("narrow_lamp/"+part.GetTitle());
            Need(rebuilt&&part.Extension.GetWhatsWrongCount()==0,"Narrow lamp part failed: "+part.GetTitle());
        }
        row["narrow_lamp_width_linkage"]=new{cabinet_width_mm=width,opening_mm=width-470,cover_width_mm=width-443,retainer_width_mm=width-440,retainer_pattern_spacing_mm=(width-490)/4,standard_columns_and_short_solar_strips_preserved=true};
    }
}
