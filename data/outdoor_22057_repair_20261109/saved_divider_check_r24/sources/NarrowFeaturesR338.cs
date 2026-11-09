using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void SyncDoorAuxiliaryHole(ModelDoc2 panel,double width,List<object> log) {
        Set(panel,"D1@草图6",width/4-87.5,log);Set(panel,"D3@阵列(线性)1",(width/2-175)/6,log);
    }
    static void SyncOriginalDoorAuxiliaryHoles(double width,List<object> log) {
        foreach(string name in new[]{"防水柜门1╱12L.SLDPRT","防水柜门2╱12L.SLDPRT","防水柜门3╱12L.SLDPRT"})SyncDoorAuxiliaryHole(Doc(name),width,log);
    }
    static void RefreshRoofEndRelief(Dictionary<string,object> row) {
        var roof=Doc("箱体顶板.SLDPRT");RmActivate(roof,"roof_end_relief_scope");var f=RmFeature(roof,"切除-拉伸12");
        var data=(IExtrudeFeatureData2)f.GetDefinition();row["roof_relief_definition_before"]=new{end=data.GetEndCondition(true),depth=data.GetDepth(true),from=data.FromType,scope=data.FeatureScope,auto_select=data.AutoSelect};
        Need(data.AccessSelections(roof,null),"Cannot access roof end relief scope");bool modified=false;
        try{data.FeatureScope=false;data.AutoSelect=true;modified=f.ModifyDefinition(data,roof,null);}finally{if(!modified)data.ReleaseSelectionAccess();Need(roof.FeatureManager.EditRollback(1,""),"Cannot restore roof feature history");}
        bool rebuilt=roof.ForceRebuild3(false);row["roof_relief_scope_refresh"]=new{modified=modified,rebuild=rebuilt,health=LayoutFeatureHealth(roof)};Flush("roof_relief_scope_refresh");
        Need(modified&&rebuilt&&roof.Extension.GetWhatsWrongCount()==0,"Roof end relief scope refresh failed");
    }
}
