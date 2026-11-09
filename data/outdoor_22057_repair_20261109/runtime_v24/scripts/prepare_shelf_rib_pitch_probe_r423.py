"""Link the original shelf-rib pilot dimension while retaining all mates."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/shelf_rib_pitch_probe_r423';assert not target.exists()
shutil.copytree(STAGE/'scripts/shelf_rib_probe_r421',target)
p=target/'ShelfContextProbeR416.cs';t=p.read_text(encoding='utf-8')
key='Set(rib,"D1@草图1",dims[1]/2-7,notchChanges);';assert t.count(key)==1
t=t.replace(key,key+'Set(rib,"D4@草图14",dims[1]/2-175,notchChanges);')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorShelfRibPitchProbeR423.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_shelf_rib_pitch_probe_r423.mjs').write_text((STAGE/'scripts/run_shelf_rib_probe_r421.mjs').read_text(encoding='utf-8').replace('OutdoorShelfRibProbeR421.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/shelf_rib_r421_contract.json');c['cases'][0]['id']='shelf_rib_pitch_r423'
write(STAGE/'evidence/shelf_rib_pitch_r423_contract.json',c)
write(STAGE/'evidence/shelf_rib_pitch_r423_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
