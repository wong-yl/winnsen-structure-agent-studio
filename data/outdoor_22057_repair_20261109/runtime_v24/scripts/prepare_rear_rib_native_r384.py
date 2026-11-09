"""Integrate the narrow rear pilot linkage, preserving both wide generators."""
import shutil
import subprocess
from generate_model import STAGE,sha,write

helper=(STAGE/'scripts/RearRibLinkR383.cs').read_text(encoding='utf-8')
assert helper.count('OrderByDescending(a=>((SketchPoint)a.GetCenterPoint2()).Y)')==1
helper=helper.replace('OrderByDescending(a=>((SketchPoint)a.GetCenterPoint2()).Y)','OrderByDescending(a=>Math.Round(((SketchPoint)a.GetCenterPoint2()).Y,8))')
(STAGE/'scripts/RearRibLinkR384.cs').write_text(helper,encoding='utf-8')
builds=[]
for revision,baseline in [(384,379),(385,380)]:
    target=STAGE/f'scripts/width600_native_r{revision}';assert not target.exists()
    shutil.copytree(STAGE/f'scripts/width600_native_r{baseline}',target)
    (target/'RearRibLinkR384.cs').write_text(helper,encoding='utf-8')
    p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
    key='Set(master,"D1@草图1",w,changes);'
    assert t.count(key)==1
    t=t.replace(key,'SyncRearRibPilots(h,w,row);'+key)
    p.write_text(t,encoding='utf-8')
    p=target/'RightRibMateR379.cs';t=p.read_text(encoding='utf-8')
    assert t.count('double x=(width/2-125)/1000,')==1
    t=t.replace('double x=(width/2-125)/1000,','double x=Math.Max(75,width/2-300)/1000,')
    p.write_text(t,encoding='utf-8')
    tool=STAGE/f'OutdoorWidth600R{revision}.exe'
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
runner=STAGE/'scripts/run_native.mjs';t=runner.read_text(encoding='utf-8')
assert "hasShelves?'OutdoorWidth600R380.exe':'OutdoorWidth600R379.exe'" in t
runner.write_text(t.replace("hasShelves?'OutdoorWidth600R380.exe':'OutdoorWidth600R379.exe'","hasShelves?'OutdoorWidth600R385.exe':'OutdoorWidth600R384.exe'"),encoding='utf-8')
write(STAGE/'evidence/rear_rib_native_r384_build.json',{'status':'BUILT_NOT_ACCEPTED','scope':'All three rear locating bores remain in sheet at x=+-max(75,W/2-300), 2mm diameter and 30mm bottom pitch preserved; original rear ribs follow real holes. Sort bottom centers at model precision so sub-nanometer Y differences cannot swap hole identities. Wide route untouched.','builds':builds,'runner_sha256':sha(runner)})
