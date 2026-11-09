"""Offline reopening verifier with roof-controller mounting evidence."""
import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/offline_r400'
assert not target.exists()
shutil.copytree(STAGE/'scripts/offline_r372',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='row["narrow_lamp_saved_parts"]='
assert t.count(key)==1
t=t.replace(key,'row["roof_board_saved_mates"]=LayoutMates(top);row["roof_board_saved_parts"]=new[]{"箱体顶板.SLDPRT","锁控板支架.SLDPRT"}.Select(n=>new{name=n,health=LayoutFeatureHealth(Doc(n)),details=ShDetails(Doc(n))}).ToArray();\n            '+key)
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorLayoutOfflineR400.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_layout_offline_r400.mjs').write_text((STAGE/'scripts/run_layout_offline_r372.mjs').read_text(encoding='utf-8').replace('OutdoorLayoutOfflineR372.exe',tool.name),encoding='utf-8')
p=STAGE/'scripts/verify_layout_download_r400.py'
assert not p.exists()
t=(STAGE/'scripts/verify_layout_download.py').read_text(encoding='utf-8').replace('r190','r400').replace('run_layout_offline_r189.mjs','run_layout_offline_r400.mjs')
p.write_text(t,encoding='utf-8')
write(STAGE/'evidence/offline_r400_build.json',{'status':'NOT_EXECUTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
