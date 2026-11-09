"""Link hinge tabs to the same width pitch as the three shelf notches."""
import shutil
import subprocess
from generate_model import STAGE,read,write,sha
probe=read(STAGE/'evidence/hinge_pitch_r434_layout_probe.json');case=probe['cases'][0]
assert probe['status']=='layout_probe_finished' and case['files_unchanged']
assert len(case['after_details']['bodies'])==1
target=STAGE/'scripts/width600_native_r435';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r430',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='var mirroredShelf=Doc("箱体横层板R.SLDPRT");';assert t.count(key)==1
t=t.replace(key,'var hinge=Doc("箱体门轴固定板.SLDPRT");Set(hinge,"D5@草图2",width/2-175,shelfChanges);Need(hinge.ForceRebuild3(false)&&hinge.Extension.GetWhatsWrongCount()==0&&RmBodies(hinge).Length==1,"Final hinge support must remain one joined body");'+key)
p.write_text(t,encoding='utf-8')
p=target/'RightRibMateR379.cs';t=p.read_text(encoding='utf-8')
key='var old=RmFeature(right,"同心3");bool warning;int error=old.GetErrorCode2(out warning);';assert t.count(key)==1
t=t.replace(key,'var old=Features(right).SingleOrDefault(f=>f.Name=="同心3");if(old==null){var existing=RmFeature(right,"布局_右后筋顶部辅助孔同轴");bool existingWarning;Need(existing.GetErrorCode2(out existingWarning)==0&&!existingWarning&&right.Extension.GetWhatsWrongCount()==0,"Saved right rear rib mate is unhealthy");row["right_rear_rib_saved_mate_reused"]=true;return;}bool warning;int error=old.GetErrorCode2(out warning);')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorWidth600R435.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
p=STAGE/'scripts/run_native.mjs';t=p.read_text(encoding='utf-8');assert t.count('OutdoorWidth600R430.exe')==1
p.write_text(t.replace('OutdoorWidth600R430.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/hinge_linkage_r435_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')},'local_probe':'hinge_pitch_r434','thresholds_unchanged':True})
