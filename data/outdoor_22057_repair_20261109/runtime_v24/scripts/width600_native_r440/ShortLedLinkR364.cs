using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static int ShortLedCount(double width) {
        int count=(int)Math.Floor((width-470-53.5)/75)+1;
        return Math.Max(2,count-count%2);
    }
    static void SyncShortLed(double width,List<object> changes,Dictionary<string,object> row) {
        Need(width>=600&&width<900,"Short LED linkage range");
        int count=ShortLedCount(width);double length=75*(count-1)+30;
        var records=new List<object>();row["short_led_parts"]=records;
        foreach(string side in new[]{"L","R"}) {
            var d=Doc("LED硬灯条(蓝景13点白色930×16mm) "+side+".SLDPRT");RmActivate(d,"short_led/"+side);
            Need(RmFeature(d,"分割1").SetSuppression2(0,1,null),"Cannot disable long-strip body division");
            Need(RmFeature(d,"切除-拉伸1").SetSuppression2(0,1,null),"Cannot defer original LED mounting slots");
            var dim=d.Parameter("D1@阵列(线性)1") as Dimension;Need(dim!=null,"LED count control missing");
            int status=dim.SetSystemValue3(count,1,null);Need(status==0&&Math.Abs(dim.SystemValue-count)<1e-9,"LED count rejected");
            Set(d,"D1@草图1",length,changes);
            Set(d,"D1@草图3",20,changes);Set(d,"D2@草图3",20,changes);Set(d,"D5@草图3",length/2-20,changes);
            Need(RmFeature(d,"切除-拉伸1").SetSuppression2(1,1,null),"Cannot restore shortened LED mounting slots");
            bool rebuilt=d.ForceRebuild3(false);var bodies=RmBodies(d);Need(bodies.Length==1,"Short LED must contain exactly one physical body");
            bodies[0].HideBody(false);
            records.Add(new{side=side,rebuild=rebuilt,health=LayoutFeatureHealth(d),details=ShDetails(d),body_visible=bodies[0].Visible,led_count=count,length_mm=length});Flush("short_led/rebuilt/"+side);
            Need(rebuilt&&d.Extension.GetWhatsWrongCount()==0&&bodies[0].Visible,"Short LED native rebuild failed");
        }
        var shade=Doc("遮阳板.SLDPRT");
        Set(shade,"D2@草图47",20,changes);Set(shade,"D3@草图47",20,changes);Set(shade,"D6@草图47",1,changes);
        Need(shade.ForceRebuild3(false),"Short LED shade mounting pilots failed");
        row["short_led_linkage"]=new{cabinet_width_mm=width,strip_length_mm=length,leds_per_row=count,pitch_mm=75,base_width_mm=16,base_thickness_mm=1.8,package_diameter_mm=13.5,boss_height_mm=2,dome_height_mm=3,mount_axis_spacing_mm=20,pilot_diameter_mm=3,slot_width_mm=4,slot_arc_center_spacing_mm=8,source_split_suppressed=true};
    }
}
