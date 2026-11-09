"""Inspect the split hinge support on the saved, isolated narrow candidate."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/hinge_plate_probe_r433';assert not target.exists()
shutil.copytree(STAGE/'scripts/door_rib_probe_r428',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8');assert t.count('DoorRibProbe(c,row);')==1
p.write_text(t.replace('DoorRibProbe(c,row);','HingePlateProbe(c,row);'),encoding='utf-8')
(target/'HingePlateProbeR433.cs').write_text('''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void HingePlateProbe(Dictionary<string,object> c,Dictionary<string,object> row){
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);
        try{var d=Doc("箱体门轴固定板.SLDPRT");RmActivate(d,"hinge_plate_survey");row["rebuilt"]=d.ForceRebuild3(false);row["details"]=ShDetails(d);row["sketches"]=Sketches(d);row["cylinders"]=Cylinders(d);row["parents"]=Features(d).Select(f=>new{name=f.Name,type=f.GetTypeName2(),parents=(f.GetParents() as Array??new object[0]).Cast<Feature>().Select(p=>p.Name).ToArray()}).ToArray();
            TrExport(d,"hinge_plate_r433",row);row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorHingePlateProbeR433.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
runner=(STAGE/'scripts/run_door_rib_probe_r428.mjs').read_text(encoding='utf-8')
(STAGE/'scripts/run_hinge_plate_probe_r433.mjs').write_text(runner.replace('OutdoorDoorRibProbeR428.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/width600_high_r432_contract.json');c['cases'][0]['id']='hinge_plate_r433'
write(STAGE/'evidence/hinge_plate_r433_contract.json',c)
write(STAGE/'evidence/hinge_plate_r433_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool)})
