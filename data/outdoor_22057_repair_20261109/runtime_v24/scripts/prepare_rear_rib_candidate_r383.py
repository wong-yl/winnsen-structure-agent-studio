"""Build a no-save candidate before changing the narrow production generators."""
import shutil
import subprocess
from generate_model import STAGE,sha,write
target=STAGE/'scripts/rear_rib_candidate_r383';assert not target.exists()
shutil.copytree(STAGE/'scripts/rib_source_probe_r382',target)
shutil.copy2(STAGE/'scripts/RearRibLinkR383.cs',target/'RearRibLinkR383.cs')
p=target/'RibPhaseProbeR381.cs';t=p.read_text(encoding='utf-8')
key='var master=Doc("防水寄存柜 模型.SLDPRT");'
assert t.count(key)==1
t=t.replace(key,'SyncRearRibPilots(1700,600,row);'+key)
key='TrExport(Doc("箱体左侧板.SLDPRT"),"left_panel",row);'
assert t.count(key)==1
t=t.replace(key,'RmActivate(Doc("箱体左侧板.SLDPRT"),"export_left");'+key)
key='TrExport(Doc("箱体右侧板.SLDPRT"),"right_panel",row);'
t=t.replace(key,'RmActivate(Doc("箱体右侧板.SLDPRT"),"export_right");'+key)
key='TrExport(Doc("OUT_RightSideRib.SLDPRT"),"right_rib",row);'
t=t.replace(key,'RmActivate(Doc("OUT_RightSideRib.SLDPRT"),"export_rib");'+key)
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorRearRibCandidateR383.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_rear_rib_candidate_r383.mjs').write_text((STAGE/'scripts/run_rib_source_probe_r382.mjs').read_text(encoding='utf-8').replace('OutdoorRibSourceProbeR382.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/rear_rib_candidate_r383_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','previous_diagnosis':'rib_source_r382_layout_probe.json','scope':'Preserve rear rib cross section; couple its three original 2mm locating holes to a center offset max(75,W/2-300) so both bottom holes and top hole remain inside the back sheet','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
