using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static int ExpectedNativeDocuments=122;
    static Dictionary<string,string> LayoutCheckpointTools()
    {
        return new[]{"scripts/LayoutBuildR170.cs","scripts/OutdoorShadeR145.cs","scripts/FeedbackCadBaseR145.cs","independent_size_rules.json","assembly_size_rules.json","seed_manifest.json"}.ToDictionary(p=>p,p=>Hash(Path.Combine(Stage,p)));
    }
    static void CheckLayoutPartsCheckpoint(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        var checkpoint=Read(Path.Combine(Root,"layout_parts_checkpoint.json"));Need((string)checkpoint["status"]=="PARTS_SAVED_NOT_ACCEPTED","Wrong checkpoint stage");
        Need((string)checkpoint["requested_rows"]==Json.Serialize(LayoutRows(c)),"Checkpoint layout changed");
        foreach(var pair in LayoutCheckpointTools())Need((string)((Dictionary<string,object>)checkpoint["tools"])[pair.Key]==pair.Value,"Placement inputs changed since checkpoint");
        var expected=((Dictionary<string,object>)checkpoint["files"]).ToDictionary(p=>p.Key,p=>(string)p.Value);var current=Identity(Root);current.Remove("layout_parts_checkpoint.json");
        Need(expected.Count==current.Count&&expected.All(p=>current.ContainsKey(p.Key)&&current[p.Key]==p.Value),"Saved layout checkpoint files changed");row["resumed_layout_parts_checkpoint"]=true;
    }
    static void SaveLayoutPartsCheckpoint(ModelDoc2 top,Dictionary<string,object> c,Dictionary<string,object> row)
    {
        if(LayoutRows(c).Count==0)return;
        for(ModelDoc2 doc=Sw.GetFirstDocument() as ModelDoc2;doc!=null;doc=doc.GetNext() as ModelDoc2)Need(doc.Extension.GetWhatsWrongCount()==0,"Unhealthy document before layout-parts checkpoint "+doc.GetTitle());
        SaveDocs(top,row);string path=Path.Combine(Root,"layout_parts_checkpoint.json");Need(!File.Exists(path),"Preserve prior layout checkpoint");
        File.WriteAllText(path,Json.Serialize(new{status="PARTS_SAVED_NOT_ACCEPTED",requested_rows=Json.Serialize(LayoutRows(c)),tools=LayoutCheckpointTools(),files=Identity(Root)}),new System.Text.UTF8Encoding(false));
        row["layout_parts_checkpoint"]=path;Flush("layout_parts_checkpoint_saved_not_accepted");
    }
    static List<Dictionary<string,object>> LayoutRows(Dictionary<string,object> c)
    {
        var rows=new List<Dictionary<string,object>>();
        if(!c.ContainsKey("columns"))return rows;
        double h=Convert.ToDouble(((IList)c["dimensions_mm"])[0]);int sideIndex=0;
        foreach(Dictionary<string,object> column in (IEnumerable)c["columns"]){
            string side=sideIndex++==0?"L":"R";Need((string)column["side"]==side,"Ordered L/R columns required");
            var heights=((IEnumerable)column["doors"]).Cast<Dictionary<string,object>>().Select(d=>Convert.ToDouble(d["heightMm"])).ToArray();
            Need(heights.Length>=1&&heights.Length<=24&&heights.All(v=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=100),"Invalid door heights");
            Need(heights[0]>=170,"Bottom door must be at least 170 mm to retain lock bores and their formed outer rims");
            Need(Math.Abs(heights.Sum()+heights.Length*6-(h-115))<.000001,"Outdoor column must close with 6 mm gaps");double offset=0;
            for(int i=0;i<heights.Length;i++){
                string prefix="OUT_"+side+(i+1).ToString("00");
                rows.Add(new Dictionary<string,object>{{"side",side},{"index",i+1},{"height_mm",heights[i]},{"bottom_mm",23+offset},{"center_mm",23+offset+heights[i]/2},
                    {"boundary_mm",offset+heights[i]+6},{"bottom_door",i==0},{"top_door",i==heights.Length-1},{"prefix",prefix},
                    {"panel_source",prefix+"_PanelL.SLDPRT"},{"panel",prefix+"_Panel"+side+".SLDPRT"},{"weld",prefix+"_Weld.SLDASM"},{"assembly",prefix+"_Door.SLDASM"}});
                offset+=heights[i]+6;
            }
        }
        Need(sideIndex==2&&rows.Count<=34,"Two columns and at most 34 doors required");return rows;
    }
    static void LayoutCopy(string from,string to)
    {
        string target=Path.Combine(Root,to);Need(!File.Exists(target),"Preserve generated module "+to);
        File.Copy(Path.Combine(Root,from),target);File.SetAttributes(target,FileAttributes.Normal);
    }
    static void LayoutReferences(string name,Dictionary<string,string> map,List<object> changes)
    {
        string path=Path.Combine(Root,name);var raw=Sw.GetDocumentDependencies2(path,false,false,false) as Array;if(raw==null)return;
        var deps=raw.Cast<string>().ToArray();for(int i=1;i<deps.Length;i+=2){string target;
            if(!map.TryGetValue(Path.GetFileName(deps[i]),out target))continue;
            string destination=Path.Combine(Root,target);Need(Sw.ReplaceReferencedDocument(path,deps[i],destination),"Cannot bind unique door dependency");
            changes.Add(new{document=name,source=deps[i],target=destination});
        }
    }
    static void PrepareLayout(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        var rows=LayoutRows(c);if(rows.Count==0)return;var copies=new List<object>();row["layout_copy_references"]=copies;
        foreach(var r in rows){
            string side=(string)r["side"];
            var map=new Dictionary<string,string>{{"防水柜门3╱12L.SLDPRT",(string)r["panel_source"]},{"防水柜门3╱12焊接"+side+".SLDASM",(string)r["weld"]},{"防水柜门3╱12装配"+side+".SLDASM",(string)r["assembly"]}};
            if(side=="R")map.Add("防水柜门3╱12R.SLDPRT",(string)r["panel"]);
            foreach(var pair in map)LayoutCopy(pair.Key,pair.Value);
            foreach(string n in map.Values)LayoutReferences(n,map,copies);
        }
        LayoutCopy("箱体侧板加强筋.SLDPRT","OUT_RightSideRib.SLDPRT");
        LayoutReferences("箱体右侧板焊接.SLDASM",new Dictionary<string,string>{{"箱体侧板加强筋.SLDPRT","OUT_RightSideRib.SLDPRT"}},copies);
        File.WriteAllText(Path.Combine(Root,"generated_layout_manifest.json"),Json.Serialize(new{rows=rows,native_documents=122+rows.Count*3+rows.Count(r=>(string)r["side"]=="R")+1}),new System.Text.UTF8Encoding(false));
    }
    static double LayoutExtremeY(Component2 component,bool upper)
    {
        object info;var bodies=component.GetBodies3(0,out info) as Array;Need(bodies!=null&&bodies.Length>0,"Part bodies required for placement");
        double best=upper?double.NegativeInfinity:double.PositiveInfinity;
        var inverse=component.Transform2.IInverse();var origin=RmPoint(new double[3],inverse);var point=RmPoint(new[]{0.0,upper?1.0:-1.0,0.0},inverse);var direction=point.Zip(origin,(a,b)=>a-b).ToArray();
        foreach(Body2 body in bodies){double x,y,z;Need(body.GetExtremePoint(direction[0],direction[1],direction[2],out x,out y,out z),"Exact placement extent required");
            double value=RmPoint(new[]{x,y,z},component.Transform2)[1];best=upper?Math.Max(best,value):Math.Min(best,value);}
        return best*1000;
    }
    static Component2 LayoutAdd(ModelDoc2 owner,string file,string configuration,double[] transform,string label)
    {
        string path=Path.Combine(Root,file);
        if(Sw.GetOpenDocumentByName(path)==null){int errors=0,warnings=0;var loaded=Sw.OpenDoc6(path,path.EndsWith(".SLDASM",StringComparison.OrdinalIgnoreCase)?2:1,1,"",ref errors,ref warnings) as ModelDoc2;
            Need(loaded!=null&&errors==0&&!loaded.IsOpenedReadOnly(),"Cannot reload module unloaded after original-instance suppression");LoadedInside();}
        RmActivate(owner,"layout_add/"+label);var assembly=(AssemblyDoc)owner;
        var component=assembly.AddComponent5(path,0,"",false,configuration,0,0,0) as Component2;Need(component!=null,"Cannot insert native layout module "+file);
        component.ReferencedConfiguration=configuration;component.Transform2=((MathUtility)Sw.GetMathUtility()).CreateTransform(transform) as MathTransform;
        owner.ClearSelection2(true);Need(component.Select4(false,null,false),"Cannot fix placed layout module");assembly.FixComponent();Need(component.IsFixed(),"Layout module must be fixed at its calculated installation datum");
        component.Name2=label;owner.ClearSelection2(true);return component;
    }
    static void LayoutSuppress(ModelDoc2 owner,Func<Component2,bool> matches,string[] patterns,List<object> changes)
    {
        RmActivate(owner,"layout_replace_original_rows");
        var components=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().Where(matches).ToArray();
        var roots=new HashSet<string>(components.Select(c=>c.Name2));
        foreach(var f in Features(owner).Where(f=>f.GetTypeName2().StartsWith("Mate")&&f.GetTypeName2()!="MateGroup")){
            var mate=f.GetSpecificFeature2() as Mate2;if(mate==null)continue;bool affected=false;
            for(int i=0;i<mate.GetMateEntityCount();i++){var e=mate.MateEntity(i) as MateEntity2;var comp=e==null?null:e.ReferenceComponent as Component2;if(comp!=null&&roots.Any(n=>comp.Name2==n||comp.Name2.StartsWith(n+"/")))affected=true;}
            if(affected)RmSuppressFeature(owner,f,changes,1);
        }
        foreach(string n in patterns)RmSuppressFeature(owner,RmFeature(owner,n),changes,1);
        foreach(var component in components){if(component.IsSuppressed())continue;int result=component.SetSuppression2(0);Need(component.IsSuppressed(),"Original row was not suppressed "+component.Name2);changes.Add(new{component=component.Name2,result=result});}
    }
    static void BuildLayout(ModelDoc2 top,Dictionary<string,object> c,Dictionary<string,object> row)
    {
        var rows=LayoutRows(c);if(rows.Count==0)return;row["layout_rows"]=rows;double w=Convert.ToDouble(((IList)c["dimensions_mm"])[1]);var changes=new List<object>();row["layout_changes"]=changes;
        var cabinet=Doc("箱体焊接.SLDASM");var parts=((Array)((AssemblyDoc)cabinet).GetComponents(false)).Cast<Component2>().ToArray();
        var shelfSeeds=new Dictionary<string,Tuple<double[],double>>();var hingeSeeds=new Dictionary<string,Tuple<double[],double>>();
        foreach(string side in new[]{"L","R"}){
            var seed=parts.Single(p=>p.Name2==(side=="L"?"箱体横层板L焊接-1":"箱体横层板R焊接-2"));
            var leaf=parts.Single(p=>p.Name2.StartsWith(seed.Name2+"/")&&Path.GetFileName(p.GetPathName())=="箱体横层板"+side+".SLDPRT");
            shelfSeeds[side]=Tuple.Create((double[])seed.Transform2.ArrayData,LayoutExtremeY(leaf,false));
            var hinge=parts.Single(p=>p.Name2==(side=="L"?"箱体门轴固定板-1":"箱体门轴固定板-9"));hingeSeeds[side]=Tuple.Create((double[])hinge.Transform2.ArrayData,LayoutExtremeY(hinge,false));
        }
        var topParts=((Array)((AssemblyDoc)top).GetComponents(true)).Cast<Component2>().ToArray();
        var doorTransforms=new Dictionary<string,double[]>();var lockTransforms=new Dictionary<string,double[]>();
        foreach(string side in new[]{"L","R"}){
            doorTransforms[side]=(double[])topParts.Single(p=>p.Name2=="防水柜门3╱12装配"+side+"-1").Transform2.ArrayData;
            lockTransforms[side]=(double[])topParts.Single(p=>p.Name2=="电控锁装配 "+side+"-10").Transform2.ArrayData;
        }
        LayoutSuppress(top,p=>Path.GetFileName(p.GetPathName()).StartsWith("防水柜门")&&p.GetPathName().EndsWith(".SLDASM",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p.GetPathName()).StartsWith("电控锁装配"),new[]{"柜门阵列1","柜门阵列2"},changes);
        LayoutSuppress(cabinet,p=>Path.GetFileName(p.GetPathName()).StartsWith("箱体横层板")||Path.GetFileName(p.GetPathName())=="箱体门轴固定板.SLDPRT",new[]{"局部线性阵列1","局部线性阵列2"},changes);
        foreach(var r in rows){string side=(string)r["side"],prefix=(string)r["prefix"];double height=Convert.ToDouble(r["height_mm"]),center=Convert.ToDouble(r["center_mm"]);bool bottom=(bool)r["bottom_door"];
            var panel=Doc((string)r["panel_source"]);Set(panel,"D1@草图1",w/2-26,changes);Set(panel,"D2@草图1",height,changes);Need(panel.ForceRebuild3(false),"Door panel failed native rebuild");
            if(side=="R")Need(Doc((string)r["panel"]).ForceRebuild3(false),"Mirrored right door failed native rebuild");
            var weld=Doc((string)r["weld"]);Need(weld.ForceRebuild3(false),"Door weld failed native rebuild");
            if(height<298){var rib=((Array)((AssemblyDoc)weld).GetComponents(true)).Cast<Component2>().Single(p=>Path.GetFileName(p.GetPathName())=="门板加强筋.SLDPRT");rib.SetSuppression2(0);Need(rib.IsSuppressed(),"Small-door rib suppression failed");}
            var module=Doc((string)r["assembly"]);string config=bottom?"BOTTOM_SHORT_PIN_60":"Default";RmConfiguration(module,config);Need(module.ForceRebuild3(false),"Door module failed native rebuild");
            var transform=(double[])doorTransforms[side].Clone();transform[9]=(side=="L"?-1:1)*(w/4-11)/1000;transform[10]=center/1000;transform[11]=0;
            LayoutAdd(top,(string)r["assembly"],config,transform,prefix+"_Door");
            transform=(double[])lockTransforms[side].Clone();transform[10]=(center-15)/1000;LayoutAdd(top,"电控锁装配 "+side+".SLDASM","Default",transform,prefix+"_Lock");
            if(!(bool)r["top_door"]){double boundary=Convert.ToDouble(r["boundary_mm"]);var shelf=shelfSeeds[side];transform=(double[])shelf.Item1.Clone();transform[10]+=(7+boundary-shelf.Item2)/1000;
                LayoutAdd(cabinet,"箱体横层板"+side+"焊接.SLDASM","Default",transform,prefix+"_Shelf");
                var hinge=hingeSeeds[side];transform=(double[])hinge.Item1.Clone();transform[10]+=(19+boundary-hinge.Item2)/1000;LayoutAdd(cabinet,"箱体门轴固定板.SLDPRT","Default",transform,prefix+"_HingePlate");}
        }
        ExpectedNativeDocuments=122+rows.Count*3+rows.Count(r=>(string)r["side"]=="R")+1;
        ExpectedVisibleSolids=129+19*rows.Count-rows.Count(r=>Convert.ToDouble(r["height_mm"])<298);
        row["expected_native_documents"]=ExpectedNativeDocuments;row["expected_visible_solids"]=ExpectedVisibleSolids;
    }
}
