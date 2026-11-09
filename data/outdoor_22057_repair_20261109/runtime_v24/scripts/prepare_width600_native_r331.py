"""Link M4 cover receivers and M6 floor pilot holes, retaining the floor nut constraints."""
import shutil
import subprocess
from generate_model import STAGE,sha,write
builds=[]
for base,revision in [('width600_native_r314',331),('width600_native_r315',332)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    shutil.copy2(STAGE/'scripts/FloorHoleLinkR330.cs',target/'FloorHoleLinkR330.cs')
    p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8').replace('SyncCoverFasteners(w,changes,row);SyncCoverSpan(w,changes,row);','SyncCoverFasteners(w,changes,row);SyncFloorFasteners(w,changes,row);SyncCoverSpan(w,changes,row);');p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs';text=p.read_text(encoding='utf-8').replace('OutdoorWidth600R325.exe','OutdoorWidth600R332.exe').replace('OutdoorWidth600R324.exe','OutdoorWidth600R331.exe');p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_width600_r331_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds,'original_floor_constraints_preserved':True})
