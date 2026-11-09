using System;
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
