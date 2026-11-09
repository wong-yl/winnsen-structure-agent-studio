using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static void OfflineResolveSuppressed(ModelDoc2 top,Dictionary<string,object> row)
    {
        var rows=new List<object>();row["relocated_suppressed_reference_resolution"]=rows;
        foreach(var c in ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().Where(c=>c.IsSuppressed()).OrderBy(c=>c.Name2))
        {
            string before=c.GetPathName();if(Within(before,Root))continue;
            int original=c.GetSuppression2();Need(original==0,"Suppressed reference state required");
            string local=Path.Combine(Root,Path.GetFileName(before));Need(File.Exists(local),"Suppressed document absent from download");
            Flush("offline/resolve/"+c.Name2);int resolved=c.SetSuppression2(2);var doc=c.GetModelDoc2() as ModelDoc2;
            rows.Add(new{name=c.Name2,before=before,resolve_return=resolved,actual_document=doc==null?null:doc.GetPathName(),actual_component=c.GetPathName()});
            Need(resolved==2 && doc!=null && doc.GetPathName().Equals(local,StringComparison.OrdinalIgnoreCase),"Suppressed reference failed to resolve inside downloaded package");
            Need(c.SetSuppression2(original)==2,"Cannot restore original suppressed state");
        }
        Need(top.ForceRebuild3(false),"Offline assembly rebuild failed");LoadedInside();
        row["suppression_states_restored"]=true;
    }
}
