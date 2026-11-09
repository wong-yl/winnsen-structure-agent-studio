import shutil
import subprocess
from generate_model import STAGE,sha,write
target=STAGE/'scripts/narrow_parts_probe_r337';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r331',target)
p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8').replace('else if(mode=="layout_probe")LayoutProbe(c,row);','else if(mode=="layout_probe")Width600Probe(c,row);');p.write_text(text,encoding='utf-8')
p=target/'Width600ProbeR311.cs';text=(STAGE/'scripts/Width600ProbeR311.cs').read_text(encoding='utf-8')
text=text.replace('"箱体内顶板.SLDPRT","盖板.SLDPRT"','"箱体顶板.SLDPRT","OUT_L01_PanelL.SLDPRT","箱体横层板L.SLDPRT"')
text=text.replace('row["after"]=new[]{','var panel=Doc("OUT_L01_PanelL.SLDPRT");var changes=new List<object>();Set(panel,"D1@草图1",274,changes);Set(panel,"D2@草图1",1579,changes);row["panel_rebuild"]=panel.ForceRebuild3(false);row["after"]=new[]{')
p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorNarrowPartsProbeR337.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
p=STAGE/'scripts/run_narrow_parts_probe_r337.mjs';p.write_text((STAGE/'scripts/run_width600_probe_r311.mjs').read_text(encoding='utf-8').replace('OutdoorWidth600ProbeR311.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_narrow_parts_probe_r337_build.json',{'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
