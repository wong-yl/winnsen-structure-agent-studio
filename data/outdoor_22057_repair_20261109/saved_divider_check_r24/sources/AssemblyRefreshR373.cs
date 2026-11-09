using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void RefreshLayoutAssemblyGraph(ModelDoc2 top,Dictionary<string,object> row) {
        row["post_layout_health_before_graph_refresh"]=TrAssemblyHealth();Flush("post_layout_health_before_graph_refresh");
        var components=((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().Where(c=>!c.IsSuppressed()).OrderByDescending(c=>c.Name2.Count(ch=>ch=='/')).ToArray();
        var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var records=new List<object>();row["layout_assembly_graph_refresh"]=records;
        foreach(var c in components) {
            var d=c.GetModelDoc2() as ModelDoc2;if(!(d is AssemblyDoc)||!visited.Add(d.GetPathName()))continue;
            int before=d.Extension.GetWhatsWrongCount();if(before==0)continue;
            RmActivate(d,"layout_graph_refresh/"+d.GetTitle());bool rebuilt=d.ForceRebuild3(false);
            records.Add(new{document=d.GetTitle(),issues_before=before,rebuild=rebuilt,issues_after=d.Extension.GetWhatsWrongCount(),health=LayoutFeatureHealth(d)});Flush("layout_graph_refresh/"+d.GetTitle());
        }
        RmActivate(top,"layout_graph_final_top");bool final=top.ForceRebuild3(false);
        row["post_layout_health_after_graph_refresh"]=TrAssemblyHealth();row["layout_graph_top_rebuild"]=final;Flush("post_layout_health_after_graph_refresh");
        Need(final,"Layout assembly graph refresh failed");
    }
}
