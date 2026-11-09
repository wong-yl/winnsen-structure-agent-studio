import shutil
import subprocess
from generate_model import STAGE, sha, write

target=STAGE/'scripts/narrow_probe_r342'
assert not target.exists()
shutil.copytree(STAGE/'scripts/door_probe_r340',target)
p=target/'NarrowFeaturesR338.cs'
t=p.read_text(encoding='utf-8').replace('Set(panel,"D1@草图6",width/4-87.5,log);','Set(panel,"D1@草图6",width/4-87.5,log);Set(panel,"D3@阵列(线性)1",(width/2-175)/6,log);')
p.write_text(t,encoding='utf-8')
shutil.copy2(STAGE/'scripts/ShelfWidthLinkR341.cs',target/'ShelfWidthLinkR341.cs')
shutil.copy2(STAGE/'scripts/width600_native_r339/ShelfNotchLinkR286.cs',target/'ShelfNotchLinkR286.cs')
p=target/'OutdoorShadeR145.cs'
t=p.read_text(encoding='utf-8')
old='RefreshRoofEndRelief(row);RmActivate(top,"dimension_propagation_before_layout");row["pre_layout_rebuild_return"]=top.ForceRebuild3(false);row["pre_layout_health"]=TrAssemblyHealth();Flush("dimension_propagation_before_layout_not_accepted");'
assert old in t
t=t.replace(old,'RefreshRoofEndRelief(row);Flush("diagnostic_skip_full_assembly_rebuild");')
p.write_text(t,encoding='utf-8')
p=target/'Width600ProbeR340.cs'
t=p.read_text(encoding='utf-8')
start=t.index('            RmActivate(panel,"door_scope_probe")')
end=t.index('\n        }finally',start)
t=t[:start]+'''            var shelf=Doc("箱体横层板L.SLDPRT");SyncNarrowShelfWidth(shelf,600,changes,row);SyncShelfNotchHeight(shelf,changes,row);
            row["shelf_after_link"]=WidthProbeDetails(shelf);Flush("narrow_probe/shelf_after_link");'''+t[end:]
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorNarrowProbeR342.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_narrow_probe_r342.mjs').write_text((STAGE/'scripts/run_door_probe_r340.mjs').read_text(encoding='utf-8').replace('OutdoorDoorProbeR340.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_narrow_probe_r342_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
