using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void SyncFloorFasteners(double width,List<object> log,Dictionary<string,object> row) {
        if(Math.Abs(width-950)<1e-8)return;
        double delta=(width-950)/2000;var floor=Doc("箱体底板.SLDPRT");
        MoveCoverProfile(floor,"草图19",x=>Math.Abs(x)<.001?x:x+Math.Sign(x)*delta,log);
        RmActivate(floor,"floor_original_nut_linkage");Need(floor.ForceRebuild3(false),"Floor fastener profile rebuild failed");
        var sketch=(Sketch)RmFeature(floor,"草图21").GetSpecificFeature2();
        var blocks=(sketch.GetSketchBlockInstances() as Array??new object[0]).Cast<SketchBlockInstance>().Select(b=>new{name=b.Name,transform=(double[])b.BlockToSketchTransform.ArrayData}).ToArray();
        row["floor_fastener_width_linkage"]=new{width_mm=width,abs_x_mm=width/2-35,original_block_constraints_preserved=true,blocks=blocks};Flush("floor_original_nut_linkage");
        Need(blocks.Length==2&&blocks.All(b=>Math.Abs(Math.Abs(b.transform[9])-(width/2000-.035))<1e-8&&Math.Abs(b.transform[10]-.02)<1e-8),"Original floor nut constraints did not follow their holes");
    }
}
