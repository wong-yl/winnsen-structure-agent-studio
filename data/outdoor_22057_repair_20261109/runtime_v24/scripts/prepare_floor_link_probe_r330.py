import shutil
import subprocess
from generate_model import STAGE,sha,write
target=STAGE/'scripts/floor_link_probe_r330';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r314',target)
shutil.copy2(STAGE/'scripts/FloorHoleLinkR330.cs',target/'FloorHoleLinkR330.cs')
p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8').replace('SyncCoverFasteners(w,changes,row);SyncCoverSpan(w,changes,row);','SyncCoverFasteners(w,changes,row);SyncFloorFasteners(w,changes,row);SyncCoverSpan(w,changes,row);').replace('else if(mode=="layout_probe")LayoutProbe(c,row);','else if(mode=="layout_probe")Width600Probe(c,row);');p.write_text(text,encoding='utf-8')
p=target/'Width600ProbeR311.cs';text=(STAGE/'scripts/Width600ProbeR311.cs').read_text(encoding='utf-8').replace('"箱体内顶板.SLDPRT","盖板.SLDPRT"','"箱体底板.SLDPRT","箱体内顶板.SLDPRT","盖板.SLDPRT"');p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorFloorLinkProbeR330.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
p=STAGE/'scripts/run_floor_link_probe_r330.mjs';p.write_text((STAGE/'scripts/run_width600_probe_r311.mjs').read_text(encoding='utf-8').replace('OutdoorWidth600ProbeR311.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_floor_link_probe_r330_build.json',{'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
