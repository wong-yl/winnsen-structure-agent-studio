import shutil
import subprocess
from generate_model import STAGE,sha,write
builds=[]
for base,revision in [('width600_native_r319',322),('width600_native_r320',323)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    p=target/'FloorFastenersR317.cs';text=p.read_text(encoding='utf-8')
    text=text.replace('b.block.InstancePosition=(MathPoint)math.CreatePoint(p);','b.block.InstancePosition=(MathPoint)math.CreatePoint(p);b.block.LockAngle=true;')
    text=text.replace('FixCoverProfile(floor,s);Need(s.GetConstrainedStatus()==3,"Floor nut blocks must be constrained");','FixCoverProfile(floor,s);row["floor_constraint_status"]=new{status=s.GetConstrainedStatus(),blocks=blocks.Select(b=>new{name=b.block.Name,angle=b.block.Angle,locked=b.block.LockAngle,position=b.block.InstancePosition.ArrayData}).ToArray()};Flush("floor_constraint_status");Need(s.GetConstrainedStatus()==3,"Floor nut blocks must be constrained");')
    p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs';text=p.read_text(encoding='utf-8').replace('OutdoorWidth600R320.exe','OutdoorWidth600R323.exe').replace('OutdoorWidth600R319.exe','OutdoorWidth600R322.exe');p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_floor_constraints_r322_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds})
