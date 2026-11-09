"""Add the measured left rear-rib distance to the narrow native generators."""
import shutil
import subprocess
from generate_model import STAGE,sha,write

builds=[]
for old,revision in [(396,408),(397,409)]:
    target=STAGE/f'scripts/width600_native_r{revision}';assert not target.exists()
    shutil.copytree(STAGE/f'scripts/width600_native_r{old}',target)
    shutil.copy2(STAGE/'scripts/LeftRearRibLinkR407.cs',target/'LeftRearRibLinkR407.cs')
    p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
    key='if(persist){ReanchorRightRibMate(dims[0],dims[1],row);'
    assert t.count(key)==1
    t=t.replace(key,'if(persist){SyncLeftRearRib(dims[1],(List<object>)row["input_changes"],row);ReanchorRightRibMate(dims[0],dims[1],row);')
    p.write_text(t,encoding='utf-8')
    tool=STAGE/f'OutdoorWidth600R{revision}.exe'
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
write(STAGE/'evidence/width600_native_r408_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','builds':builds})
