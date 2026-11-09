"""The cover slots already follow their edge dimension; link only the inner-sheet holes and nuts."""
import shutil
import subprocess
from generate_model import STAGE, sha, write
builds=[]
for base,revision in [('width600_native_r312',314),('width600_native_r313',315)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    p=target/'CoverFastenersR312.cs'
    text=p.read_text(encoding='utf-8').replace('        var cover=Doc("盖板.SLDPRT");\n        MoveCoverProfile(cover,"草图10",x=>Math.Abs(x)<.001?x:x+Math.Sign(x)*delta/2,log);\n','')
    text=text.replace('        foreach(var p in points)Need(Math.Abs(p.point.X-map(p.xyz[0]))<1e-8&&Math.Abs(p.point.Y-p.xyz[1])<1e-8,"Cover fastener profile target mismatch");','        log.Add(new{phase="before_cover_profile_target_check",document=Path.GetFileName(d.GetPathName()),sketch=name,points=points.Select(p=>new{before=p.xyz,target_x=map(p.xyz[0]),after=new[]{p.point.X,p.point.Y,p.point.Z}}).ToArray()});Flush("cover_profile_targets");\n        foreach(var p in points)Need(Math.Abs(p.point.X-map(p.xyz[0]))<1e-8&&Math.Abs(p.point.Y-p.xyz[1])<1e-8,"Cover fastener profile target mismatch");')
    p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs';text=p.read_text(encoding='utf-8').replace('OutdoorWidth600R313.exe','OutdoorWidth600R315.exe').replace('OutdoorWidth600R312.exe','OutdoorWidth600R314.exe');p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_cover_fasteners_r314_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds})
