"""Versioned narrow generators; wide v15 executables remain frozen."""
import shutil
import subprocess
from generate_model import STAGE,sha,write,read

assert read(STAGE/'evidence/short_led_geometry_r365.json')['status']=='PASS_LOCAL_NOT_SAVED'
builds=[]
for revision,baseline in [(366,348),(367,344)]:
    target=STAGE/f'scripts/width600_native_r{revision}'
    assert not target.exists()
    shutil.copytree(STAGE/f'scripts/width600_native_r{baseline}',target)
    for name in ['LampWidthLinkR360.cs','ShortLedLinkR364.cs']:
        shutil.copy2(STAGE/'scripts'/name,target/name)
    p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
    key='var shade=Doc("遮阳板.SLDPRT");Set(shade,"D1@草图1",w-160,changes);'
    assert t.count(key)==1
    t=t.replace(key,'SyncNarrowLamp(w,changes,row);SyncShortLed(w,changes,row);'+key)
    key='ExportCurrent(top,row,(string)c["id"]);'
    assert t.count(key)==1
    t=t.replace(key,'''row["narrow_lamp_saved_parts"]=new[]{"雨棚LED灯罩壳.SLDPRT","塑料灯罩.SLDPRT","塑料板压条焊件.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) L.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) R.SLDPRT"}.Select(n=>new{name=n,health=LayoutFeatureHealth(Doc(n)),details=ShDetails(Doc(n))}).ToArray();
            row["canopy_mates"]=LayoutMates(Doc("太阳能雨棚装配.SLDASM"));row["canopy_states"]=ShStates(Doc("太阳能雨棚装配.SLDASM"));
            '''+key)
    assert 'layout.Count==0?387:129+19*layout.Count' in t
    t=t.replace('layout.Count==0?387:129+19*layout.Count','layout.Count==0?367:109+19*layout.Count')
    p.write_text(t,encoding='utf-8')
    p=target/'LayoutBuildR170.cs';t=p.read_text(encoding='utf-8')
    assert 'ExpectedVisibleSolids=129+19*rows.Count' in t
    p.write_text(t.replace('ExpectedVisibleSolids=129+19*rows.Count','ExpectedVisibleSolids=109+19*rows.Count'),encoding='utf-8')
    tool=STAGE/f'OutdoorWidth600R{revision}.exe'
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':baseline,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
runner=STAGE/'scripts/run_native.mjs';text=runner.read_text(encoding='utf-8')
assert "hasShelves?'OutdoorWidth600R344.exe':'OutdoorWidth600R343.exe'" in text
runner.write_text(text.replace("hasShelves?'OutdoorWidth600R344.exe':'OutdoorWidth600R343.exe'","hasShelves?'OutdoorWidth600R367.exe':'OutdoorWidth600R366.exe'"),encoding='utf-8')
write(STAGE/'evidence/short_led_native_r366_build.json',{'status':'BUILT_NOT_ACCEPTED','local_evidence':str(STAGE/'evidence/short_led_geometry_r365.json'),'local_evidence_sha256':sha(STAGE/'evidence/short_led_geometry_r365.json'),'solid_accounting':{'original_led_bodies':22,'short_led_bodies':2,'delta':-20,'reason':'Two 11-body long-strip models become two actual single-body short strips; no hidden surplus stock'},'builds':builds,'runner_sha256':sha(runner)})
