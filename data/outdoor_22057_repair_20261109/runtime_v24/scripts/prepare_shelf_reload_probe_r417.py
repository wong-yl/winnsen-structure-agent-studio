"""Verify that shelf-instance suppression reloads the unsaved source profiles."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/shelf_reload_probe_r417';assert not target.exists()
shutil.copytree(STAGE/'scripts/shelf_context_probe_r416',target)
p=target/'ShelfContextProbeR416.cs';t=p.read_text(encoding='utf-8')
t=t.replace('row["after_suppression_shelf"]=ShelfProbeState(shelf);','shelf=Doc("箱体横层板L.SLDPRT");row["after_suppression_shelf"]=ShelfProbeState(shelf);')
t=t.replace('row["after_replacement_shelf"]=ShelfProbeState(shelf);','shelf=Doc("箱体横层板L.SLDPRT");row["after_replacement_shelf"]=ShelfProbeState(shelf);')
t=t.replace('try{SyncShelfNotchHeight(shelf,notchChanges,row);}','try{SyncNarrowShelfWidth(shelf,dims[1],notchChanges,row);SyncShelfNotchHeight(shelf,notchChanges,row);}')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorShelfReloadProbeR417.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_shelf_reload_probe_r417.mjs').write_text((STAGE/'scripts/run_shelf_context_probe_r416.mjs').read_text(encoding='utf-8').replace('OutdoorShelfContextProbeR416.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/shelf_context_r416_contract.json');c['cases'][0]['id']='shelf_reload_r417'
write(STAGE/'evidence/shelf_reload_r417_contract.json',c)
write(STAGE/'evidence/shelf_reload_r417_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
