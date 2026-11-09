"""Integrate measured controller-post linkage; preserve every earlier tool."""
import shutil
import subprocess
from generate_model import STAGE, sha, write

builds=[]
for old, revision in [(384,396),(385,397)]:
    target=STAGE/f'scripts/width600_native_r{revision}'
    assert not target.exists()
    shutil.copytree(STAGE/f'scripts/width600_native_r{old}',target)
    shutil.copy2(STAGE/'scripts/RoofBoardLinkR395.cs',target/'RoofBoardLinkR395.cs')
    p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
    key='SyncRearRibPilots(h,w,row);Set(master'
    assert t.count(key)==1
    t=t.replace(key,'SyncRearRibPilots(h,w,row);SyncRoofBoardMounts(w,changes,row);Set(master')
    key='row["narrow_lamp_saved_parts"]='
    assert t.count(key)==1
    t=t.replace(key,'row["roof_board_saved_mates"]=LayoutMates(top);row["roof_board_saved_parts"]=new[]{"箱体顶板.SLDPRT","锁控板支架.SLDPRT"}.Select(n=>new{name=n,health=LayoutFeatureHealth(Doc(n)),details=ShDetails(Doc(n))}).ToArray();\n            '+key)
    p.write_text(t,encoding='utf-8')
    tool=STAGE/f'OutdoorWidth600R{revision}.exe'
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
write(STAGE/'evidence/width600_native_r396_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','builds':builds})
