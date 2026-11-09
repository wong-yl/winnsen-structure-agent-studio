import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/short_led_candidate_r364'
assert not target.exists()
shutil.copytree(STAGE/'scripts/lamp_candidate_probe_r360',target)
shutil.copy2(STAGE/'scripts/ShortLedLinkR364.cs',target/'ShortLedLinkR364.cs')
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='SyncNarrowLamp(w,changes,row);var shade='
assert t.count(key)==1
t=t.replace(key,'SyncNarrowLamp(w,changes,row);SyncShortLed(w,changes,row);var shade=')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorShortLedCandidateR364.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_short_led_candidate_r364.mjs').write_text((STAGE/'scripts/run_lamp_candidate_probe_r360.mjs').read_text(encoding='utf-8').replace('OutdoorLampCandidateProbeR360.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/short_led_candidate_r364_build.json',{'status':'BUILT_NOT_ACCEPTED','approval':'Suitable short strips allowed by user; no cross-section or LED package scaling','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
