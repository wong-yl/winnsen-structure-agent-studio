"""No-save diagnosis: refresh the existing left rear-rib assembly solver."""
import shutil
import subprocess
from generate_model import STAGE,sha,write,read

target=STAGE/'scripts/left_rib_probe_r405';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r396',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='else if(mode=="layout_probe")LayoutProbe(c,row);';assert t.count(key)==1
p.write_text(t.replace(key,'else if(mode=="layout_probe")LeftRibProbe(c,row);'),encoding='utf-8')
(target/'LeftRibProbeR405.cs').write_text(r'''using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void LeftRibProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);
        ExpectedNativeDocuments=130;ExpectedVisibleSolids=147;
        try {
            var top=OpenTop(row);var left=Doc("箱体左侧板焊接.SLDASM");
            row["left_before"]=new{mates=LayoutMates(left),states=ShStates(left),health=LayoutFeatureHealth(left)};Flush("left_rib_before");
            RmActivate(left,"left_rib_rebuild");bool ok=left.ForceRebuild3(false);
            row["left_after"]=new{rebuilt=ok,mates=LayoutMates(left),states=ShStates(left),health=LayoutFeatureHealth(left)};Flush("left_rib_after");
            Need(ok&&left.Extension.GetWhatsWrongCount()==0,"Explicit left assembly refresh failed");
            RmActivate(top,"left_rib_top_rebuild");Need(top.ForceRebuild3(false),"Top refresh failed");
            row["snapshot"]=Snapshot(top,false);row["native_save_performed"]=false;ExportCurrent(top,row,(string)c["id"]);
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorLeftRibProbeR405.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_left_rib_probe_r405.mjs').write_text((STAGE/'scripts/run_roof_pin_probe_r391.mjs').read_text(encoding='utf-8').replace('OutdoorRoofPinProbeR391.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/width600_low_r399_contract.json');c['cases'][0]['id']='left_rib_r405'
write(STAGE/'evidence/left_rib_r405_contract.json',c)
write(STAGE/'evidence/left_rib_r405_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
