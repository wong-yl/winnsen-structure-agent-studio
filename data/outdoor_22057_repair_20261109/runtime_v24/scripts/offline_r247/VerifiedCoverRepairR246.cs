using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
internal static partial class OutdoorShadeR145
{
    static void ValidateCoverRepairInput(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(((IEnumerable)c["dimensions_mm"]).Cast<object>().Select(Convert.ToDouble).SequenceEqual(new[]{1700.0,900,450}),"Cover-only continuation is bounded to verified low case");
        var layout=LayoutRows(c);Need(layout.Count==2&&layout.All(r=>Convert.ToDouble(r["height_mm"])==1579),"Verified two-door layout required");
        var source=(Dictionary<string,object>)c["cover_span_repair_source"];
        string saved=(string)source["saved"],reopened=(string)source["reopened"],sourceRoot=(string)source["root"];
        Need(Within(saved,Stage)&&Within(reopened,Stage)&&Within(sourceRoot,Stage),"Verified continuation must remain isolated");
        Need(Hash(saved)==(string)source["saved_sha256"]&&Hash(reopened)==(string)source["reopened_sha256"],"Verified input evidence changed");
        var save=Read(saved);var reopen=Read(reopened);Need((string)save["status"]=="generate_finished"&&(string)reopen["status"]=="verify_finished","Verified native input required");
        var savedCase=((IEnumerable)save["cases"]).Cast<Dictionary<string,object>>().Single();var reopenedCase=((IEnumerable)reopen["cases"]).Cast<Dictionary<string,object>>().Single();
        Need(Convert.ToBoolean(savedCase["health_pass"])&&Convert.ToBoolean(reopenedCase["health_pass"]),"Unhealthy continuation input");
        var identity=((Dictionary<string,object>)savedCase["final_identity"]).ToDictionary(p=>p.Key,p=>(string)p.Value);
        CheckIdentity(sourceRoot,identity);CheckIdentity(Root,identity);row["verified_cover_repair_input"]=source;
    }
}
