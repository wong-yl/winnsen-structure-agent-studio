using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
internal static partial class OutdoorShadeR145
{
    static void OfflineResolvePattern(ModelDoc2 top,Component2 target,string local,Dictionary<string,object> row) {
        var parent=target.GetParent() as Component2;
        var owner=parent==null?top:parent.GetModelDoc2() as ModelDoc2;
        Need(owner is AssemblyDoc,"Pattern owner must be loaded");
        var components=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().ToArray();
        var states=components.ToDictionary(c=>c.Name2,c=>c.GetSuppression2());
        var patterns=Features(owner).Where(f=>new[]{"局部线性阵列1","局部线性阵列2","柜门阵列1","柜门阵列2"}.Contains(f.Name)).ToArray();
        var patternStates=patterns.ToDictionary(f=>f.Name,f=>((Array)f.IsSuppressed2(1,null)).Cast<bool>().Single());
        var seeds=components.Where(c=>!c.IsPatternInstance()&&Path.GetFileName(c.GetPathName()).Equals(Path.GetFileName(local),StringComparison.OrdinalIgnoreCase)).ToArray();
        Need(seeds.Length>0&&patterns.Length>0,"Native pattern seeds and features required");
        var steps=new List<object>();row["pattern_reference_steps"]=steps;
        try {
            RmActivate(owner,"offline_pattern_seed");
            foreach(var seed in seeds){int result=seed.SetSuppression2(2);var model=seed.GetModelDoc2() as ModelDoc2;steps.Add(new{seed=seed.Name2,result=result,path=model==null?null:model.GetPathName()});Need(result==2&&model!=null&&model.GetPathName().Equals(local,StringComparison.OrdinalIgnoreCase),"Pattern seed failed local resolution");}
            foreach(var feature in patterns.Where(f=>patternStates[f.Name]))Need(feature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null),"Cannot temporarily resolve native pattern");
            string leaf=target.Name2.Split('/').Last();var resolved=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().Single(c=>c.Name2.Split('/').Last()==leaf);
            int status=resolved.SetSuppression2(2);var doc=resolved.GetModelDoc2() as ModelDoc2;
            steps.Add(new{target=target.Name2,result=status,path=doc==null?null:doc.GetPathName(),pattern_instance=target.IsPatternInstance()});Flush("offline_pattern_resolved/"+leaf);
            Need(status==2&&doc!=null&&doc.GetPathName().Equals(local,StringComparison.OrdinalIgnoreCase),"Pattern instance failed local resolution");
        }finally {
            RmActivate(owner,"offline_pattern_restore");
            foreach(var component in components.OrderByDescending(c=>c.IsPatternInstance()?1:0).ThenByDescending(c=>c.Name2.Length))if(component.GetSuppression2()!=states[component.Name2])Need(component.SetSuppression2(states[component.Name2])==2,"Cannot restore native pattern seed state");
            foreach(var feature in patterns.Where(f=>patternStates[f.Name]))Need(feature.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null),"Cannot restore suppressed native pattern");
            row["pattern_feature_restore"]=new{before=patternStates,after=patterns.ToDictionary(f=>f.Name,f=>((Array)f.IsSuppressed2(1,null)).Cast<bool>().Single())};
            var current=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().ToDictionary(c=>c.Name2,c=>c.GetSuppression2());
            row["pattern_state_restore"]=new{before=states,after=current};Flush("offline_pattern_restore_checked");
            Need(states.Count==current.Count&&states.All(p=>current.ContainsKey(p.Key)&&current[p.Key]==p.Value),"Pattern component suppression states changed");
            Need(patterns.All(f=>((Array)f.IsSuppressed2(1,null)).Cast<bool>().Single()==patternStates[f.Name]),"Pattern feature suppression states changed");
            RmActivate(top,"offline_pattern_restored");
        }
    }
    static void OfflineResolveSuppressed(ModelDoc2 top,Dictionary<string,object> row)
    {
        var rows=new List<object>();row["relocated_suppressed_reference_resolution"]=rows;
        foreach(var c in ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().Where(c=>c.IsSuppressed()).OrderBy(c=>c.Name2))
        {
            string before=c.GetPathName();if(Within(before,Root))continue;
            int original=c.GetSuppression2();Need(original==0,"Suppressed reference state required");
            string local=Path.Combine(Root,Path.GetFileName(before));Need(File.Exists(local),"Suppressed document absent from download");
            if(c.IsPatternInstance()){
                OfflineResolvePattern(top,c,local,row);rows.Add(new{name=c.Name2,before=before,pattern_seed_and_feature_resolved=true,actual_component=c.GetPathName()});
                Need(c.GetPathName().Equals(local,StringComparison.OrdinalIgnoreCase),"Restored pattern reference escaped package");continue;
            }
            Flush("offline/resolve/"+c.Name2);int resolved=c.SetSuppression2(2);var doc=c.GetModelDoc2() as ModelDoc2;
            rows.Add(new{name=c.Name2,before=before,resolve_return=resolved,actual_document=doc==null?null:doc.GetPathName(),actual_component=c.GetPathName()});
            Need(resolved==2 && doc!=null && doc.GetPathName().Equals(local,StringComparison.OrdinalIgnoreCase),"Suppressed reference failed to resolve inside downloaded package");
            Need(c.SetSuppression2(original)==2,"Cannot restore original suppressed state");
        }
        Need(top.ForceRebuild3(false),"Offline assembly rebuild failed");LoadedInside();
        row["suppression_states_restored"]=true;
    }
}
