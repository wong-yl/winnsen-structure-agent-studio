import shutil
import subprocess
from generate_model import STAGE,sha,write

builds=[]
for base,revision in [('width600_native_r338',343),('width600_native_r339',344)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    p=target/'NarrowFeaturesR338.cs'
    t=p.read_text(encoding='utf-8').replace('Set(panel,"D1@草图6",width/4-87.5,log);','Set(panel,"D1@草图6",width/4-87.5,log);Set(panel,"D3@阵列(线性)1",(width/2-175)/6,log);')
    p.write_text(t,encoding='utf-8')
    if revision==344:
        shutil.copy2(STAGE/'scripts/ShelfWidthLinkR341.cs',target/'ShelfWidthLinkR341.cs')
        p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
        key='SyncShelfNotchHeight(Doc("箱体横层板L.SLDPRT"),changes,row);'
        assert t.count(key)==1
        t=t.replace(key,'SyncNarrowShelfWidth(Doc("箱体横层板L.SLDPRT"),w,changes,row);'+key)
        p.write_text(t,encoding='utf-8')
    p=target/'LayoutBuildR170.cs';t=p.read_text(encoding='utf-8')
    t=t.replace('Need(panel.ForceRebuild3(false),"Door panel failed native rebuild");','bool panelRebuilt=panel.ForceRebuild3(false);if(!panelRebuilt){row["failed_door_health"]=LayoutFeatureHealth(panel);Flush("failed_door_health");}Need(panelRebuilt,"Door panel failed native rebuild");')
    p.write_text(t,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
write(STAGE/'evidence/native_narrow_r343_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds})
