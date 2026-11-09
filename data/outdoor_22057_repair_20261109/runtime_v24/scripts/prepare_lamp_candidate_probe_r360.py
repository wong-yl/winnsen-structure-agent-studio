import shutil
import subprocess
from generate_model import STAGE, sha, write

target = STAGE / 'scripts/lamp_candidate_probe_r360'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/lamp_probe_r359', target)
shutil.copy2(STAGE / 'scripts/LampWidthLinkR360.cs', target / 'LampWidthLinkR360.cs')
p=target/'OutdoorShadeR145.cs'
t=p.read_text(encoding='utf-8')
key='var shade=Doc("遮阳板.SLDPRT");Set(shade,"D1@草图1",w-160,changes);'
assert t.count(key)==1
t=t.replace(key,'SyncNarrowLamp(w,changes,row);'+key)
p.write_text(t,encoding='utf-8')
p=target/'NarrowFitProbeR350.cs'
t=p.read_text(encoding='utf-8')
key='var records=new List<object>();row["lamp_parts"]=records;'
t=t.replace(key,'''var canopy=Doc("太阳能雨棚装配.SLDASM");row["canopy_states"]=ShStates(canopy);row["canopy_mates"]=LayoutMates(canopy);
            var ledInstances=new List<object>();row["led_instances"]=ledInstances;
            foreach(Component2 instance in (Array)((AssemblyDoc)top).GetComponents(false))if(instance.GetPathName().IndexOf("LED硬灯条",StringComparison.OrdinalIgnoreCase)>=0){
                object info;var bodies=instance.GetBodies3(0,out info) as Array;var bodyRows=new List<object>();
                if(bodies!=null)foreach(Body2 body in bodies)bodyRows.Add(new{name=body.Name,bounds_m=body.GetBodyBox(),visible=body.Visible});
                ledInstances.Add(new{name=instance.Name2,hidden=instance.IsHidden(true),suppressed=instance.IsSuppressed(),configuration=instance.ReferencedConfiguration,transform=instance.Transform2.ArrayData,bodies=bodyRows});
            }
            '''+key)
t=t.replace('TrExport(Doc("雨棚LED灯罩壳.SLDPRT"),"shell",row);','TrExport(Doc("太阳能雨棚装配.SLDASM"),"canopy",row);TrExport(Doc("雨棚LED灯罩壳.SLDPRT"),"shell",row);')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorLampCandidateProbeR360.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_lamp_candidate_probe_r360.mjs').write_text((STAGE/'scripts/run_lamp_probe_r359.mjs').read_text(encoding='utf-8').replace('OutdoorLampProbeR359.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/lamp_candidate_probe_r360_build.json',{'status':'BUILT_NOT_ACCEPTED','user_approved':'600-wide lamp opening 130, plastic cover 157, retainer 160; fixed standard columns and short solar strips','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
