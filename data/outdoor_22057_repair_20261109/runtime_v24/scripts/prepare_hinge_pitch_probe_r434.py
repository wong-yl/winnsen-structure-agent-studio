"""Test the original dimension controlling the three hinge support tabs."""
import shutil
import subprocess
from generate_model import STAGE,read,write,sha
target=STAGE/'scripts/hinge_pitch_probe_r434';assert not target.exists()
shutil.copytree(STAGE/'scripts/hinge_plate_probe_r433',target)
p=target/'HingePlateProbeR433.cs';t=p.read_text(encoding='utf-8')
key='TrExport(d,"hinge_plate_r433",row);'
assert t.count(key)==1
t=t.replace(key,'var changes=new List<object>();row["changes"]=changes;Set(d,"D5@草图2",Convert.ToDouble(((System.Collections.IList)c["dimensions_mm"])[1])/2-175,changes);Need(d.ForceRebuild3(false)&&d.Extension.GetWhatsWrongCount()==0,"Hinge tab pitch rebuild failed");row["after_details"]=ShDetails(d);row["after_sketches"]=Sketches(d);Need(RmBodies(d).Length==1,"Hinge plate must remain one joined body");TrExport(d,"hinge_pitch_r434",row);')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorHingePitchProbeR434.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
runner=(STAGE/'scripts/run_hinge_plate_probe_r433.mjs').read_text(encoding='utf-8')
(STAGE/'scripts/run_hinge_pitch_probe_r434.mjs').write_text(runner.replace('OutdoorHingePlateProbeR433.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/hinge_plate_r433_contract.json');c['cases'][0]['id']='hinge_pitch_r434'
write(STAGE/'evidence/hinge_pitch_r434_contract.json',c)
write(STAGE/'evidence/hinge_pitch_r434_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool)})
