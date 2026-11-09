import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/narrow_fit_probe_r350';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r348',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8').replace('else if(mode=="layout_probe")LayoutProbe(c,row);','else if(mode=="layout_probe")NarrowFitProbe(c,row);')
key='RefreshRoofEndRelief(row);RmActivate(top,"dimension_propagation_before_layout");row["pre_layout_rebuild_return"]=top.ForceRebuild3(false);row["pre_layout_health"]=TrAssemblyHealth();Flush("dimension_propagation_before_layout_not_accepted");'
assert key in t;t=t.replace(key,'RefreshRoofEndRelief(row);row["pre_layout_health"]=TrAssemblyHealth();Flush("narrow_fit_without_top_rebuild");')
p.write_text(t,encoding='utf-8')
(target/'NarrowFitProbeR350.cs').write_text('''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void NarrowFitProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);Writable=false;
        try {
            var top=OpenTop(row);try{ApplyInputs(top,1700,600,450,row);}catch(Exception ex){row["apply_error"]=ex.ToString();}
            var records=new List<object>();row["fit_parts"]=records;
            foreach(string n in new[]{"雨棚LED灯罩壳.SLDPRT","塑料灯罩.SLDPRT","雨棚LED灯罩焊接.SLDASM","箱体右侧板焊接.SLDASM","OUT_RightSideRib.SLDPRT"}){
                var d=Doc(n);bool rebuilt=d.ForceRebuild3(false);records.Add(new{name=n,rebuild=rebuilt,health=LayoutFeatureHealth(d),details=ShDetails(d),mates=d is AssemblyDoc?LayoutMates(d):null,states=d is AssemblyDoc?ShStates(d):null});Flush("narrow_fit/"+n);
            }
            TrExport(Doc("雨棚LED灯罩壳.SLDPRT"),"led_shell",row);TrExport(Doc("塑料灯罩.SLDPRT"),"plastic_cover",row);
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;row["native_save_performed"]=false;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorNarrowFitProbeR350.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_narrow_fit_probe_r350.mjs').write_text((STAGE/'scripts/run_narrow_probe_r342.mjs').read_text(encoding='utf-8').replace('OutdoorNarrowProbeR342.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_narrow_fit_probe_r350_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
