"""Initialize the existing change journal on the bounded checkpoint branch."""
import shutil
import subprocess
from generate_model import STAGE,write,sha
target=STAGE/'scripts/width600_native_r440';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r438',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='else if(c.ContainsKey("hinge_pitch_repair_source")){row["reused_saved_high_layout_checkpoint_not_previously_accepted"]=true;}'
assert t.count(key)==1
p.write_text(t.replace(key,key[:-1]+'row["input_changes"]=new List<object>();}'),encoding='utf-8')
tool=STAGE/'OutdoorWidth600R440.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
p=STAGE/'scripts/run_native.mjs';t=p.read_text(encoding='utf-8');assert t.count('OutdoorWidth600R438.exe')==1;p.write_text(t.replace('OutdoorWidth600R438.exe',tool.name),encoding='utf-8')
p=STAGE/'scripts/complete_narrow_high_r442.py';assert not p.exists()
p.write_text((STAGE/'scripts/complete_narrow_high_r439.py').read_text(encoding='utf-8').replace('r439','r442'),encoding='utf-8')
write(STAGE/'evidence/hinge_checkpoint_r440_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')},'change':'initialize input_changes on the checkpoint branch','fresh_generation_path_unchanged':True})
