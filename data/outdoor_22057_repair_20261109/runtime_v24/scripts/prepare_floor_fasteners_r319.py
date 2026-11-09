import shutil
import subprocess
from generate_model import STAGE,sha,write
builds=[]
for base,revision in [('width600_native_r317',319),('width600_native_r318',320)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    p=target/'FloorFastenersR317.cs';text=p.read_text(encoding='utf-8')
    text=text.replace('Need(Math.Abs(Math.Abs(b.transform[9])-.44)<1e-8,"Unexpected floor nut source location");var p=(double[])b.position.Clone();p[0]+=Math.Sign(b.transform[9])*delta;', 'double wanted=Math.Sign(b.transform[9])*(width/2000-.035);Need(Math.Min(Math.Abs(Math.Abs(b.transform[9])-.44),Math.Abs(b.transform[9]-wanted))<1e-8,"Unexpected floor nut source location");var p=(double[])b.position.Clone();p[0]+=wanted-b.transform[9];')
    p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs';text=p.read_text(encoding='utf-8').replace('OutdoorWidth600R318.exe','OutdoorWidth600R320.exe').replace('OutdoorWidth600R317.exe','OutdoorWidth600R319.exe');p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_floor_fasteners_r319_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds})
