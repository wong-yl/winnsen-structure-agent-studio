import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/led_transition_probe_r377';assert not target.exists()
shutil.copytree(STAGE/'scripts/led_transition_probe_r371',target)
p=target/'NarrowFitProbeR350.cs';text=p.read_text(encoding='utf-8')
assert '749.0,899.0' in text
p.write_text(text.replace('749.0,899.0','748.5,898.5'),encoding='utf-8')
tool=STAGE/'OutdoorLedTransitionProbeR377.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_led_transition_probe_r377.mjs').write_text((STAGE/'scripts/run_lamp_candidate_probe_r360.mjs').read_text(encoding='utf-8').replace('OutdoorLampCandidateProbeR360.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/led_transition_probe_r377_build.json',{'status':'BUILT_NOT_ACCEPTED','scope':'Local canopy at exact 748.5 and 898.5 mm count transitions; three decimal input precision','supersedes_unexecuted_build':'led_transition_probe_r371_build.json','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
