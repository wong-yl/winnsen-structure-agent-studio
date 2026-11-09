"""Add an explicit, hash-bound continuation for the saved high-case hinge defect."""
import shutil
import subprocess
from generate_model import STAGE,read,write,sha
target=STAGE/'scripts/width600_native_r438';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r435',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='if(c.ContainsKey("cover_span_repair_source"))ValidateCoverRepairInput(c,row);';assert t.count(key)==1
t=t.replace(key,key+'if(c.ContainsKey("hinge_pitch_repair_source"))ValidateHingeCheckpoint(c,row);')
key='else if(persist&&!c.ContainsKey("cover_span_repair_source"))PrepareLayout(c,row);';assert t.count(key)==1
t=t.replace(key,'else if(persist&&!c.ContainsKey("cover_span_repair_source")&&!c.ContainsKey("hinge_pitch_repair_source"))PrepareLayout(c,row);')
key='else{ApplyInputs(top,dims[0],dims[1],dims[2],row);BuildLayout(top,c,row);BuildLayoutFeatures(c,row);}';assert t.count(key)==1
t=t.replace(key,'else if(c.ContainsKey("hinge_pitch_repair_source")){row["reused_saved_high_layout_checkpoint_not_previously_accepted"]=true;}'+key)
p.write_text(t,encoding='utf-8')
(target/'HingeCheckpointR438.cs').write_text('''using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
internal static partial class OutdoorShadeR145 {
    static void ValidateHingeCheckpoint(Dictionary<string,object> c,Dictionary<string,object> row){
        Need(((IEnumerable)c["dimensions_mm"]).Cast<object>().Select(Convert.ToDouble).SequenceEqual(new[]{2200.0,600,700}),"Hinge checkpoint is only the saved narrow high case");
        var source=(Dictionary<string,object>)c["hinge_pitch_repair_source"];string path=(string)source["evidence"],sourceRoot=(string)source["root"];
        Need(Within(path,Stage)&&Within(sourceRoot,Stage)&&Root!=sourceRoot&&Hash(path)==(string)source["sha256"],"Hinge checkpoint source identity changed");
        var value=Read(path);Need((string)value["status"]=="failed"&&((string)value["error"]).Contains("actual=527 invalid=0"),"Hinge continuation requires the preserved split-body failure");
        var saved=((IEnumerable)value["cases"]).Cast<Dictionary<string,object>>().Single();Need(Convert.ToBoolean(saved["preaudit_candidate_saved_not_accepted"]),"Saved native checkpoint required");
        Need(Json.Serialize(saved["dimensions_mm"])==Json.Serialize(c["dimensions_mm"]),"Checkpoint dimensions changed");
        var manifest=Read(Path.Combine(sourceRoot,"generated_layout_manifest.json"));Need(Json.Serialize(manifest["rows"])==Json.Serialize(LayoutRows(c)),"Checkpoint door layout changed");
        var identity=((Dictionary<string,object>)saved["final_identity"]).ToDictionary(p=>p.Key,p=>(string)p.Value);CheckIdentity(sourceRoot,identity);CheckIdentity(Root,identity);
        row["validated_saved_hinge_checkpoint"]=source;
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorWidth600R438.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
p=STAGE/'scripts/run_native.mjs';t=p.read_text(encoding='utf-8');assert t.count('OutdoorWidth600R435.exe')==1;p.write_text(t.replace('OutdoorWidth600R435.exe',tool.name),encoding='utf-8')
p=STAGE/'scripts/complete_narrow_high_r439.py';assert not p.exists()
t=(STAGE/'scripts/complete_narrow_high_r436.py').read_text(encoding='utf-8').replace('r436','r439')
key="cover_span_repair_source='saved_r432_hinge_pitch_repair',";assert t.count(key)==1
t=t.replace(key,"hinge_pitch_repair_source={'evidence':str(STAGE/'evidence/width600_high_r432_save_generate.json'),'sha256':sha(STAGE/'evidence/width600_high_r432_save_generate.json'),'root':str(old)},")
p.write_text(t,encoding='utf-8')
write(STAGE/'evidence/hinge_checkpoint_r438_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')},'original_cover_continuation_guard_unchanged':True,'acceptance_thresholds_unchanged':True})
