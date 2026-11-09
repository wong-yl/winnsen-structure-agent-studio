"""Build no-save local mount candidate from the measured original CAD."""
import shutil
import subprocess
from generate_model import STAGE, sha, write, read
from prepare_layout_case import prepare

target=STAGE/'scripts/roof_board_candidate_r395'
assert not target.exists()
shutil.copytree(STAGE/'scripts/roof_mount_probe_r393',target)
shutil.copy2(STAGE/'scripts/RoofBoardLinkR395.cs',target/'RoofBoardLinkR395.cs')
p=target/'RoofPinProbeR390.cs'
t=p.read_text(encoding='utf-8')
key='var master=Doc("防水寄存柜 模型.SLDPRT");'
assert t.count(key)==1
t=t.replace(key,'SyncRoofBoardMounts(600,log,row);'+key)
key='RoofPinState("narrow",row);row["native_save_performed"]=false;'
assert t.count(key)==1
t=t.replace(key,'''RoofPinState("narrow",row);
            var roof=Doc("箱体顶板.SLDPRT");Need(RmBodies(roof).Length==1,"Roof posts must join the original sheet");
            row["roof_native_health"]=LayoutFeatureHealth(roof);Need(roof.Extension.GetWhatsWrongCount()==0,"Roof candidate unhealthy");
            row["native_save_performed"]=false;''')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorRoofBoardCandidateR395.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_roof_board_candidate_r395.mjs').write_text((STAGE/'scripts/run_roof_mount_probe_r393.mjs').read_text(encoding='utf-8').replace('OutdoorRoofMountProbeR393.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/roof_pin_r391_contract.json')['cases'][0]
prepare(dict(zip(('height','width','depth'),c['dimensions_mm']),columns=c['columns']),'roof_board_r395')
write(STAGE/'evidence/roof_board_r395_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
