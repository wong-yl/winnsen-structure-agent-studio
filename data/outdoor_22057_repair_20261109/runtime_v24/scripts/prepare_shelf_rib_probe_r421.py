"""Restore the unloaded shared shelf rib and verify original mates recover."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/shelf_rib_probe_r421';assert not target.exists()
shutil.copytree(STAGE/'scripts/shelf_mates_probe_r420',target)
p=target/'ShelfContextProbeR416.cs';t=p.read_text(encoding='utf-8')
key='try{SyncNarrowShelfWidth(shelf,dims[1],notchChanges,row);'
assert t.count(key)==1
t=t.replace(key,'try{var rib=Doc("箱体横层板加强筋.SLDPRT");Set(rib,"D1@草图1",dims[1]/2-7,notchChanges);Need(rib.ForceRebuild3(false)&&rib.Extension.GetWhatsWrongCount()==0,"Reloaded shelf rib rebuild failed");SyncNarrowShelfWidth(shelf,dims[1],notchChanges,row);')
key='row["native_save_performed"]=false;'
t=t.replace(key,'Need(rightWeld.Extension.GetWhatsWrongCount()==0,"Original right shelf mates did not recover");row["native_save_performed"]=false;')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorShelfRibProbeR421.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_shelf_rib_probe_r421.mjs').write_text((STAGE/'scripts/run_shelf_mates_probe_r420.mjs').read_text(encoding='utf-8').replace('OutdoorShelfMatesProbeR420.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/shelf_mates_r420_contract.json');c['cases'][0]['id']='shelf_rib_r421'
write(STAGE/'evidence/shelf_rib_r421_contract.json',c)
write(STAGE/'evidence/shelf_rib_r421_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
