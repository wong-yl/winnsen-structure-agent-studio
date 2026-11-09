import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/right_rib_probe_r352';assert not target.exists()
shutil.copytree(STAGE/'scripts/narrow_fit_probe_r350',target)
p=target/'NarrowFitProbeR350.cs';t=p.read_text(encoding='utf-8')
key='TrExport(Doc("雨棚LED灯罩壳.SLDPRT"),"led_shell",row);TrExport(Doc("塑料灯罩.SLDPRT"),"plastic_cover",row);'
assert key in t
t=t.replace(key,'''BuildLayoutFeatures(c,row);var right=Doc("箱体右侧板焊接.SLDASM");
            row["right_after_layout_rebuild"]=right.ForceRebuild3(false);row["right_after_layout_health"]=LayoutFeatureHealth(right);row["right_after_layout_mates"]=LayoutMates(right);
            row["rib_after_layout_health"]=LayoutFeatureHealth(Doc("OUT_RightSideRib.SLDPRT"));Flush("right_rib_after_layout");
            TrExport(Doc("箱体右侧板.SLDPRT"),"right_panel",row);TrExport(Doc("OUT_RightSideRib.SLDPRT"),"right_rib",row);''')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorRightRibProbeR352.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_right_rib_probe_r352.mjs').write_text((STAGE/'scripts/run_narrow_fit_probe_r350.mjs').read_text(encoding='utf-8').replace('OutdoorNarrowFitProbeR350.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_right_rib_probe_r352_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
