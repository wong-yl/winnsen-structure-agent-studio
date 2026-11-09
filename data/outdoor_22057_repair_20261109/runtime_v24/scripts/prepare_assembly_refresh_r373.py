import shutil
import subprocess
from generate_model import STAGE,sha,write

builds=[]
for revision,baseline in [(373,366),(374,367)]:
    target=STAGE/f'scripts/width600_native_r{revision}';assert not target.exists()
    shutil.copytree(STAGE/f'scripts/width600_native_r{baseline}',target)
    shutil.copy2(STAGE/'scripts/AssemblyRefreshR373.cs',target/'AssemblyRefreshR373.cs')
    p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8')
    key='row["snapshot"]=Snapshot(top,false);row["full_rebuild_return"]=true;'
    assert text.count(key)==1
    text=text.replace(key,'if(persist)RefreshLayoutAssemblyGraph(top,row);'+key)
    p.write_text(text,encoding='utf-8')
    p=target/'FeedbackCadBaseR145.cs';text=p.read_text(encoding='utf-8')
    key='int wrong=d.Extension.GetWhatsWrongCount();Need(wrong==0,"Unhealthy native document: "+d.GetPathName());'
    assert text.count(key)==1
    text=text.replace(key,'int wrong=d.Extension.GetWhatsWrongCount();if(wrong!=0){E["failed_snapshot_document"]=d.GetPathName();E["failed_snapshot_feature_health"]=TrAssemblyHealth();Flush("snapshot_failed_document_health");}Need(wrong==0,"Unhealthy native document: "+d.GetPathName());')
    p.write_text(text,encoding='utf-8')
    tool=STAGE/f'OutdoorWidth600R{revision}.exe'
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
runner=STAGE/'scripts/run_native.mjs';text=runner.read_text(encoding='utf-8')
assert "hasShelves?'OutdoorWidth600R367.exe':'OutdoorWidth600R366.exe'" in text
runner.write_text(text.replace("hasShelves?'OutdoorWidth600R367.exe':'OutdoorWidth600R366.exe'","hasShelves?'OutdoorWidth600R374.exe':'OutdoorWidth600R373.exe'"),encoding='utf-8')
write(STAGE/'evidence/native_assembly_refresh_r373_build.json',{'status':'BUILT_NOT_ACCEPTED','previous_failure':'width600_low_r366_save_generate.json','reason':'Record exact post-layout health and refresh affected nested assemblies before the top snapshot; no health thresholds changed','builds':builds,'runner_sha256':sha(runner)})
