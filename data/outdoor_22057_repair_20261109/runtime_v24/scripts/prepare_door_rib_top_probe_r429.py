"""Observe a small-door rib from its actual top assembly context without saves."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target = STAGE / 'scripts/door_rib_top_probe_r429'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/door_rib_probe_r428', target)
p = target / 'DoorRibProbeR428.cs'
t = p.read_text(encoding='utf-8')
key = 'try{foreach(string prefix'
assert t.count(key) == 1
t = t.replace(key, 'try{var top=OpenTop(row);foreach(string prefix')
key = 'cycle["after_all_config_suppression"]=DoorRibState(module);Flush("door_rib_all_configs/"+prefix);'
assert t.count(key) == 1
t = t.replace(key, key + '''
            var identity=new double[]{1,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0};
            LayoutAdd(top,prefix+"_Door.SLDASM",prefix=="OUT_L01"?"BOTTOM_SHORT_PIN_60":"Default",identity,"PROBE_"+prefix);
            cycle["top_after_add"]=DoorRibState(top);cycle["module_after_add"]=DoorRibState(module);top.ForceRebuild3(false);
            cycle["top_after_rebuild"]=DoorRibState(top);cycle["weld_after_top_rebuild"]=DoorRibState(weld);Flush("door_rib_top_rebuilt/"+prefix);
''')
p.write_text(t,encoding='utf-8')
tool = STAGE / 'OutdoorDoorRibTopProbeR429.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
runner = (STAGE / 'scripts/run_door_rib_probe_r428.mjs').read_text(encoding='utf-8')
(STAGE / 'scripts/run_door_rib_top_probe_r429.mjs').write_text(runner.replace('OutdoorDoorRibProbeR428.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/door_rib_r428_contract.json');c['cases'][0]['id']='door_rib_top_r429'
write(STAGE/'evidence/door_rib_top_r429_contract.json',c)
write(STAGE/'evidence/door_rib_top_r429_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool)})
