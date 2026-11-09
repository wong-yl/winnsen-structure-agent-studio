import shutil
import subprocess
from generate_model import STAGE,sha,write

builds=[]
for revision,baseline in [(379,373),(380,374)]:
    target=STAGE/f'scripts/width600_native_r{revision}';assert not target.exists()
    shutil.copytree(STAGE/f'scripts/width600_native_r{baseline}',target)
    shutil.copy2(STAGE/'scripts/RightRibMateR379.cs',target/'RightRibMateR379.cs')
    p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8')
    key='if(persist)RefreshLayoutAssemblyGraph(top,row);'
    assert text.count(key)==1
    text=text.replace(key,'if(persist){ReanchorRightRibMate(dims[0],dims[1],row);RefreshLayoutAssemblyGraph(top,row);}')
    p.write_text(text,encoding='utf-8')
    tool=STAGE/f'OutdoorWidth600R{revision}.exe'
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
runner=STAGE/'scripts/run_native.mjs';text=runner.read_text(encoding='utf-8')
assert "hasShelves?'OutdoorWidth600R374.exe':'OutdoorWidth600R373.exe'" in text
runner.write_text(text.replace("hasShelves?'OutdoorWidth600R374.exe':'OutdoorWidth600R373.exe'","hasShelves?'OutdoorWidth600R380.exe':'OutdoorWidth600R379.exe'"),encoding='utf-8')
write(STAGE/'evidence/right_rib_mate_r379_build.json',{'status':'BUILT_NOT_ACCEPTED','previous_failure':'width600_low_r373_save_generate.json','scope':'If 同心3 dangles, reselect the physically retained unique 2mm upper auxiliary pilot pair at x=W/2-125, y=H-118.8; preserve other mates and physical pose','builds':builds,'runner_sha256':sha(runner)})
