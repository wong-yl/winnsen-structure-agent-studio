import shutil
import subprocess
from generate_model import STAGE, sha, write
target=STAGE/'scripts/width600_probe_r311'
assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r309',target)
shutil.copy2(STAGE/'scripts/Width600ProbeR311.cs',target/'Width600ProbeR311.cs')
p=target/'OutdoorShadeR145.cs'
text=p.read_text(encoding='utf-8').replace('else if(mode=="layout_probe")LayoutProbe(c,row);','else if(mode=="layout_probe")Width600Probe(c,row);')
p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorWidth600ProbeR311.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
p=STAGE/'scripts/run_width600_probe_r311.mjs'
text=(STAGE/'scripts/run_native.mjs').read_text(encoding='utf-8')
start=text.index('const tool=resolve(stage,');end=text.index('\nconst seed=',start)
text=text[:start]+"const tool=resolve(stage,'OutdoorWidth600ProbeR311.exe');"+text[end:]
p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_width600_probe_r311_build.json',{'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
