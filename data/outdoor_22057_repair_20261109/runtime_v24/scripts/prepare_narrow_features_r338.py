import shutil
import subprocess
from generate_model import STAGE,sha,write
builds=[]
for base,revision in [('width600_native_r331',338),('width600_native_r332',339)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    shutil.copy2(STAGE/'scripts/NarrowFeaturesR338.cs',target/'NarrowFeaturesR338.cs')
    p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8').replace('SyncCoverFasteners(w,changes,row);','SyncOriginalDoorAuxiliaryHoles(w,changes);SyncCoverFasteners(w,changes,row);').replace('RmActivate(top,"dimension_propagation_before_layout");','RefreshRoofEndRelief(row);RmActivate(top,"dimension_propagation_before_layout");');p.write_text(text,encoding='utf-8')
    p=target/'LayoutBuildR170.cs';text=p.read_text(encoding='utf-8').replace('var panel=Doc((string)r["panel_source"]);Set(panel,','var panel=Doc((string)r["panel_source"]);SyncDoorAuxiliaryHole(panel,w,changes);Set(panel,');p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs';text=p.read_text(encoding='utf-8').replace('OutdoorWidth600R332.exe','OutdoorWidth600R339.exe').replace('OutdoorWidth600R331.exe','OutdoorWidth600R338.exe');p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_narrow_features_r338_build.json',{'status':'BUILT_NOT_ACCEPTED','builds':builds})
