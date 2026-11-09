import shutil
import subprocess
from generate_model import STAGE, sha, write
builds=[]
for base,revision in [('expanded_native_r301',312),('expanded_native_r303',313)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision))
    assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    shutil.copy2(STAGE/'scripts/CoverFastenersR312.cs',target/'CoverFastenersR312.cs')
    p=target/'OutdoorShadeR145.cs'
    text=p.read_text(encoding='utf-8-sig')
    needle='Set(master,"D1@草图1",w,changes);'
    assert text.count(needle)==1
    text=text.replace(needle,'SyncCoverFasteners(w,changes,row);SyncCoverSpan(w,changes,row);\n        '+needle)
    p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs'
text=p.read_text(encoding='utf-8')
start=text.index('const narrow=');end=text.index('\nconst seed=',start)
text=text[:start]+"const tool=resolve(stage,legacyPilot?'OutdoorLayoutR188.exe':cases[0].columns?.reduce((n,c)=>n+c.doors.length,0)>2?'OutdoorWidth600R313.exe':'OutdoorWidth600R312.exe');"+text[end:]
p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_cover_fasteners_r312_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds})
