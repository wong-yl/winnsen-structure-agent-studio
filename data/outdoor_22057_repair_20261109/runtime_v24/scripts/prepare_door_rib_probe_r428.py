"""Bounded no-save diagnosis of rib suppression across door configurations."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target = STAGE / 'scripts/door_rib_probe_r428'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/width600_native_r424', target)
p = target / 'OutdoorShadeR145.cs'
t = p.read_text(encoding='utf-8')
key = 'LayoutProbe(c,row);'
assert t.count(key) == 1
p.write_text(t.replace(key, 'DoorRibProbe(c,row);'), encoding='utf-8')
(target / 'DoorRibProbeR428.cs').write_text('''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object DoorRibState(ModelDoc2 d){return new{document=d.GetTitle(),configuration=((Configuration)d.GetActiveConfiguration()).Name,configurations=d.GetConfigurationNames(),save_flag=d.GetSaveFlag(),components=((Array)((AssemblyDoc)d).GetComponents(false)).Cast<Component2>().Select(p=>new{name=p.Name2,document=Path.GetFileName(p.GetPathName()),configuration=p.ReferencedConfiguration,suppressed=p.IsSuppressed()}).ToArray()};}
    static void DoorRibProbe(Dictionary<string,object> c,Dictionary<string,object> row){
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);
        var records=new List<object>();row["rib_cycles"]=records;
        try{foreach(string prefix in new[]{"OUT_L01","OUT_R02"}){
            var cycle=new Dictionary<string,object>{{"prefix",prefix}};records.Add(cycle);
            var weld=Doc(prefix+"_Weld.SLDASM");cycle["before"]=DoorRibState(weld);
            var rib=((Array)((AssemblyDoc)weld).GetComponents(true)).Cast<Component2>().Single(p=>Path.GetFileName(p.GetPathName())=="门板加强筋.SLDPRT");
            cycle["suppress_return"]=rib.SetSuppression2(0);cycle["after_suppress"]=DoorRibState(weld);Flush("door_rib_suppressed/"+prefix);
            var module=Doc(prefix+"_Door.SLDASM");cycle["after_module_open"]=DoorRibState(weld);cycle["module_open"]=DoorRibState(module);
            RmConfiguration(module,prefix=="OUT_L01"?"BOTTOM_SHORT_PIN_60":"Default");module.ForceRebuild3(false);
            cycle["after_module_rebuild"]=DoorRibState(weld);cycle["module_rebuilt"]=DoorRibState(module);Flush("door_rib_module_rebuilt/"+prefix);
            foreach(string configuration in (string[])weld.GetConfigurationNames()){
                RmConfiguration(weld,configuration);rib=((Array)((AssemblyDoc)weld).GetComponents(true)).Cast<Component2>().Single(p=>Path.GetFileName(p.GetPathName())=="门板加强筋.SLDPRT");rib.SetSuppression2(0);Need(rib.IsSuppressed(),"All-config rib suppression failed");
            }
            RmActivate(module,"door_rib_module_check");module.ForceRebuild3(false);cycle["after_all_config_suppression"]=DoorRibState(module);Flush("door_rib_all_configs/"+prefix);
        }row["native_save_performed"]=false;}finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''', encoding='utf-8')
tool = STAGE / 'OutdoorDoorRibProbeR428.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
runner = (STAGE / 'scripts/run_shelf_rib_pitch_probe_r423.mjs').read_text(encoding='utf-8')
(STAGE / 'scripts/run_door_rib_probe_r428.mjs').write_text(runner.replace('OutdoorShelfRibPitchProbeR423.exe',tool.name),encoding='utf-8')
c = read(STAGE / 'evidence/width600_high_r426_contract.json')
c['cases'][0]['id'] = 'door_rib_r428'
write(STAGE / 'evidence/door_rib_r428_contract.json',c)
write(STAGE / 'evidence/door_rib_r428_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool)})
