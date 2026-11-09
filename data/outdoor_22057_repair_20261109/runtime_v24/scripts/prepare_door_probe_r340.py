import shutil
import subprocess
from generate_model import STAGE, sha, write

target = STAGE/'scripts/door_probe_r340'
assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r338', target)
p = target/'OutdoorShadeR145.cs'
p.write_text(p.read_text(encoding='utf-8').replace('else if(mode=="layout_probe")LayoutProbe(c,row);', 'else if(mode=="layout_probe")Width600Probe(c,row);'), encoding='utf-8')
probe = (STAGE/'scripts/narrow_parts_probe_r337/Width600ProbeR311.cs').read_text(encoding='utf-8')
probe = probe.replace('details=ShDetails(d),sketches=Sketches(d),', 'details=ShDetails(d),sketches=Sketches(d),extrusions=DoorExtrusions(d),')
probe = probe.replace('Set(panel,"D1@草图1",274,changes);', 'SyncDoorAuxiliaryHole(panel,600,changes);Set(panel,"D1@草图1",274,changes);')
probe = probe.replace('Flush("width_probe/after");', '''Flush("width_probe/after");
            RmActivate(panel,"door_scope_probe");var f=RmFeature(panel,"切除-拉伸4");var data=f.GetDefinition() as IExtrudeFeatureData2;
            Need(data.AccessSelections(panel,null),"Cannot access door cut");bool modified=false;
            try{data.FeatureScope=false;data.AutoSelect=true;modified=f.ModifyDefinition(data,panel,null);}finally{if(!modified)data.ReleaseSelectionAccess();Need(panel.FeatureManager.EditRollback(1,""),"Restore door history");}
            row["door_scope_modified"]=modified;row["door_scope_rebuild"]=panel.ForceRebuild3(false);row["door_after_scope"]=WidthProbeDetails(panel);Flush("door_probe/after_scope");''')
(target/'Width600ProbeR340.cs').write_text(probe, encoding='utf-8')
(target/'DoorExtrusionsR340.cs').write_text('''using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object DoorExtrusions(ModelDoc2 d) {
        return Features(d).Where(f=>f.GetTypeName2()=="ICE").Select(f=>{
            var a=f.GetDefinition() as IExtrudeFeatureData2;var children=new System.Collections.Generic.List<string>();
            for(var s=f.GetFirstSubFeature() as Feature;s!=null;s=s.GetNextSubFeature() as Feature)children.Add(s.Name+"/"+s.GetTypeName2());
            return new{name=f.Name,children=children,data=a==null?null:(object)new{end=a.GetEndCondition(true),depth=a.GetDepth(true),from=a.FromType,scope=a.FeatureScope,auto_select=a.AutoSelect}};
        }).ToArray();
    }
}
''',encoding='utf-8')
tool = STAGE/'OutdoorDoorProbeR340.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_door_probe_r340.mjs').write_text((STAGE/'scripts/run_narrow_parts_probe_r337.mjs').read_text(encoding='utf-8').replace('OutdoorNarrowPartsProbeR337.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_door_probe_r340_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
