"""Use the installed API's named suppression actions; preserve prior failures."""
import shutil
import subprocess
from generate_model import STAGE, sha, write

native=STAGE/'scripts/offline_r307'
assert not native.exists()
shutil.copytree(STAGE/'scripts/offline_r306',native)
p=native/'OfflineCheck.cs'
text=p.read_text(encoding='utf-8').replace('using SolidWorks.Interop.sldworks;', 'using SolidWorks.Interop.sldworks;\nusing SolidWorks.Interop.swconst;')
text=text.replace('feature.SetSuppression2(2,1,null)', 'feature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null)')
text=text.replace('feature.SetSuppression2(1,1,null)', 'feature.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null)')
p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorLayoutOfflineR307.exe'
assert not tool.exists()
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in native.glob('*.cs')]],check=True)
runner=(STAGE/'scripts/run_layout_offline_r306.mjs').read_text(encoding='utf-8').replace('OutdoorLayoutOfflineR306.exe',tool.name)
(STAGE/'scripts/run_layout_offline_r307.mjs').write_text(runner,encoding='utf-8')
resume=(STAGE/'scripts/resume_download_offline_r306.py').read_text(encoding='utf-8').replace('r306','r307').replace('R306','R307')
(STAGE/'scripts/resume_download_offline_r307.py').write_text(resume,encoding='utf-8')
write(STAGE/'evidence/native_offline_r307_build.json',{'status':'COMPILED','tool':str(tool),'tool_sha256':sha(tool),'sources':{str(p):sha(p) for p in native.glob('*.cs')}})
for name in ('publish_layout_portal_r191.mjs','finalize_layout_publication_r192.mjs'):
    path=STAGE/'scripts'/name
    path.write_text(path.read_text(encoding='utf-8').replace('native_offline_r306_build.json','native_offline_r307_build.json'),encoding='utf-8')
print('COMPILED')
