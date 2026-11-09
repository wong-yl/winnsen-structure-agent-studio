import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/offline_r372';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r366',target)
shutil.copy2(STAGE/'scripts/offline_r307/OfflineCheck.cs',target/'OfflineCheck.cs')
p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8')
text=text.replace('new[]{"relocate","sizing_survey","layout_survey","layout_probe","generate","verify"}.Contains(mode)','new[]{"verify"}.Contains(mode)')
key='row["snapshot"]=Snapshot(top,false);row["full_rebuild_return"]=true;'
assert text.count(key)==1
text=text.replace(key,'OfflineResolveSuppressed(top,row);'+key)
p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorLayoutOfflineR372.exe';assert not tool.exists()
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
runner=(STAGE/'scripts/run_layout_offline_r307.mjs').read_text(encoding='utf-8').replace('OutdoorLayoutOfflineR307.exe',tool.name)
(STAGE/'scripts/run_layout_offline_r372.mjs').write_text(runner,encoding='utf-8')
write(STAGE/'evidence/native_offline_r372_build.json',{'status':'BUILT_NOT_ACCEPTED','mode':'verify only; narrow packages use 20 fewer physical LED stock bodies','tool':str(tool),'tool_sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
