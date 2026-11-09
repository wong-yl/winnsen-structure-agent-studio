using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static readonly string[] ShNames={"遮阳板.SLDPRT","太阳能板压条.SLDPRT","太阳能连接压板.SLDPRT","立柱连接架.SLDPRT","立柱固定焊件.SLDPRT","太阳能雨棚装配.SLDASM","左立柱.SLDASM","右立柱.SLDASM","左立柱焊件1.SLDPRT","左立柱焊件2.SLDPRT","左立柱焊件3.SLDPRT","左立柱焊件4.SLDPRT","右立柱焊件1.SLDPRT","右立柱焊件2.SLDPRT","右立柱焊件3.SLDPRT","右立柱焊件4.SLDPRT"};
    static object ShMates(ModelDoc2 d){return Features(d).Where(f=>f.GetTypeName2().StartsWith("Mate")).Select(f=>{bool w;int e=f.GetErrorCode2(out w);return new{name=f.Name,type=f.GetTypeName2(),error=e,warning=w,suppressed=f.IsSuppressed2(1,null)};}).ToArray();}
    static object ShDetails(ModelDoc2 d){return new{name=Path.GetFileName(d.GetPathName()),configuration=((Configuration)d.GetActiveConfiguration()).Name,dimensions=Dims(d),equations=Eqs(d),features=Features(d).Select(f=>new{name=f.Name,type=f.GetTypeName2(),suppressed=f.IsSuppressed2(1,null)}).ToArray(),bodies=d is PartDoc?Bodies(d):null,mates=d is AssemblyDoc?ShMates(d):null};}
    static void ShSurvey(Dictionary<string,object> c,Dictionary<string,object> row){Top=(string)c["top"];Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;try{var top=OpenTop(row);row["health_after_top_rebuild"]=TrAssemblyHealth();row["survey_documents"]=ShNames.Select(n=>ShDetails(Doc(n))).ToArray();}finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}}
    static void ShRestore(Dictionary<string,object> row)
    {
        var contract=Read(Path.Combine(Path.GetDirectoryName(EvidencePath),"shade_mount_contract_r114.json"));string source=(string)contract["standard_column_source_root"];
        var identity=Json.Deserialize<Dictionary<string,string>>(Json.Serialize(contract["standard_column_source_identity"]));var restored=new List<object>();row["standard_columns_restored"]=restored;
        var names=new List<string>{"左立柱.SLDASM","右立柱.SLDASM","立柱固定焊件.SLDPRT"};foreach(string side in new[]{"左","右"})for(int i=1;i<=4;i++)names.Add(side+"立柱焊件"+i+".SLDPRT");
        foreach(string name in names){string path=Path.Combine(source,name),dest=Path.Combine(Root,name);Need(Hash(path)==identity[name],"Standard column source identity changed");File.SetAttributes(dest,FileAttributes.Normal);File.Copy(path,dest,true);File.SetAttributes(dest,FileAttributes.Normal);restored.Add(new{document=name,source=path,sha256=Hash(path)});}
        var changes=new List<object>();row["standard_reference_relocation"]=changes;
        foreach(string name in names){string path=Path.Combine(Root,name);Array raw=Sw.GetDocumentDependencies2(path,false,false,false) as Array;if(raw==null)continue;var values=raw.Cast<string>().ToArray();Need(values.Length%2==0,"Invalid standard dependency list");for(int i=1;i<values.Length;i+=2){string old=values[i],target=Path.Combine(Root,Path.GetFileName(old));Need(File.Exists(target),"Local standard dependency missing: "+old);if(old.Equals(target,StringComparison.OrdinalIgnoreCase))continue;Need(old.EndsWith(".sldprt",StringComparison.OrdinalIgnoreCase)||old.EndsWith(".sldasm",StringComparison.OrdinalIgnoreCase),"Unexpected standard non-native dependency");bool changed=Sw.ReplaceReferencedDocument(path,old,target);changes.Add(new{document=name,old_path=old,new_path=target,replaced=changed});Need(changed,"Standard dependency relocation rejected");}}
        Flush("shade/standard_columns_restored");
    }
    static void ShEquation(ModelDoc2 d,string expression,List<object> log)
    {
        var q=d.GetEquationMgr() as EquationMgr;int index=q.Add2(-1,expression,true);log.Add(new{document=d.GetPathName(),expression=expression,index=index});Flush("shade/add_equation");Need(index>=0,"Native shade equation rejected: "+expression);q.EvaluateAll();
    }
    static object ShStates(ModelDoc2 d) { return ((Array)((AssemblyDoc)d).GetComponents(true)).Cast<Component2>().Select(a=>new{name=a.Name2,fixed_instance=a.IsFixed(),constrained=a.GetConstrainedStatus(),transform=a.Transform2.ArrayData}).ToArray(); }
    static void ShRelease(ModelDoc2 d,Component2 c) { RmActivate(d,"release/"+c.Name2);if(c.IsFixed()){Need(c.Select4(false,null,false),"Cannot select fixed instance");((AssemblyDoc)d).UnfixComponent();}Need(!c.IsFixed(),"Instance still fixed"); }
    static Feature ShRightMate(ModelDoc2 d,Component2 c,List<object> log) {
        ShRelease(d,c);string plane="右视@"+c.Name2+"@"+Path.GetFileNameWithoutExtension(d.GetPathName());d.ClearSelection2(true);
        Need(d.Extension.SelectByID2(plane,"PLANE",0,0,0,false,1,null,0),"Cannot select column datum");Need(Features(d).Single(f=>f.Name=="右视"&&f.GetTypeName2()=="RefPlane").Select2(true,1),"Cannot select assembly datum");
        var before=new HashSet<string>(Features(d).Select(f=>f.Name));int error;var mate=((AssemblyDoc)d).AddMate5(5,2,false,.025,.025,.025,0,0,0,0,0,false,false,0,out error);
        log.Add(new{role="right_column_only_x",mate_created=mate!=null,error=error,flip=false});Flush("shade/right_mate_diagnosis");Need(mate!=null&&error==1,"Right X locating mate rejected");
        d.EditRebuild3();var dd=mate.DisplayDimension as DisplayDimension;Need(dd!=null,"Created distance mate dimension unavailable");var dimension=dd.GetDimension2(0) as Dimension;Need(dimension!=null,"Created mate has no native dimension");log.Add(new{role="created_mate_identity",dimension=dimension.FullName,selection=dimension.GetNameForSelection(),features=Features(d).Where(f=>!before.Contains(f.Name)).Select(f=>new{name=f.Name,type=f.GetTypeName2()}).ToArray()});Flush("shade/created_mate_identity");string actual=dimension.GetNameForSelection().Split('@')[1];var feature=Features(d).Single(f=>f.Name==actual);feature.Name="遮阳板联动_右立柱偏置";d.ClearSelection2(true);return feature;
    }
    static Dictionary<string,double[][]> ShColumnBodies() { return ShNames.Where(n=>n.EndsWith(".SLDPRT")&&(n.Contains("立柱焊件")||n=="立柱固定焊件.SLDPRT")).ToDictionary(n=>n,n=>RmBodies(Doc(n)).Select(b=>((double[])b.GetBodyBox()).Concat((double[])b.GetMassProperties(1)).ToArray()).ToArray()); }
    static void ShNonWidthDatum(ModelDoc2 d,Component2 c,string datum,List<object> log) {
        RmActivate(d,"non_width_datum/"+c.Name2);string plane=datum+"@"+c.Name2+"@"+Path.GetFileNameWithoutExtension(d.GetPathName());Need(d.Extension.SelectByID2(plane,"PLANE",0,0,0,false,1,null,0),"Cannot select original column datum");Need(Features(d).Single(f=>f.Name==datum&&f.GetTypeName2()=="RefPlane").Select2(true,1),"Cannot select matching assembly datum");int error;var mate=((AssemblyDoc)d).AddMate5(0,2,false,0,0,0,0,0,0,0,0,false,false,0,out error);log.Add(new{role="retain_original_non_width_position",component=c.Name2,datum=datum,mate_created=mate!=null,error=error});Flush("shade/non_width_datum_created");Need(mate!=null&&error==1,"Original non-width datum rejected");d.ClearSelection2(true);
    }
    static void ShPressEnds(ModelDoc2 press,Dictionary<string,object> row) {
        RmActivate(press,"press_native_end_controls");var sheet=Features(press).Single(f=>f.GetTypeName2()=="SheetMetal");Need(press.FeatureManager.EditRollback(3,sheet.Name),"Cannot insert press end features before sheet recognition");var records=new List<object>();row["press_end_features"]=records;
        try{foreach(int side in new[]{-1,1}){press.ClearSelection2(true);var faces=RmBodies(press).SelectMany(b=>((Array)b.GetFaces()).Cast<Face2>()).Where(f=>{var b=(double[])f.GetBox();return side<0?b[3]<-.440:b[0]>.440;}).ToArray();Need(faces.Length==2,"Press end must include one end face and one mounting cylinder");var sd=((SelectionMgr)press.SelectionManager).CreateSelectData() as SelectData;sd.Mark=1;foreach(var f in faces)Need(((Entity)f).Select4(true,sd),"Cannot select press end face");Feature move=press.FeatureManager.InsertMoveFace2(1,side>0,0,.025,new double[]{-side*.025,0,0},new double[6]);Need(move!=null,"Native press end feature rejected");move.Name="遮阳板联动_"+(side<0?"左":"右")+"端缩短";records.Add(new{feature=move.Name,side=side,selected_face_count=faces.Length,bounds=RmBox(press)});Flush("shade/press_end_created");}}
        finally{Need(press.FeatureManager.EditRollback(1,""),"Cannot restore press feature history");}
        Need(press.ForceRebuild3(false),"Press end rebuild failed");Need(Math.Abs((RmBox(press)[3]-RmBox(press)[0])*1000-950)<.001,"Press overall width is not950");
    }
    static void ShSaveAffected(ModelDoc2 top,Dictionary<string,object> row){var allowed=new HashSet<string>(ShNames.Concat(new[]{Path.GetFileName(top.GetPathName())}));var saved=new List<object>();row["saved_documents"]=saved;var docs=new List<ModelDoc2>();for(ModelDoc2 d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2)docs.Add(d);foreach(var d in docs.Where(d=>allowed.Contains(Path.GetFileName(d.GetPathName()))).OrderBy(d=>d.GetPathName()==top.GetPathName()?2:d is AssemblyDoc?1:0)){if(!d.GetSaveFlag())continue;Need(Within(d.GetPathName(),Root)&&!d.IsOpenedReadOnly(),"Refuse external native save");int e=0,w=0;bool ok=d.Save3(1,ref e,ref w);saved.Add(new{document=Path.GetFileName(d.GetPathName()),saved=ok,errors=e,warnings=w});Flush("shade/save_affected");Need(ok&&e==0&&w==0,"Affected native save failed");}}
    static void ShPersist(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Need(Writable&&Convert.ToInt32(c["index"])==13,"Exact A shade candidate only");Root=(string)c["package_root"];Top=(string)c["top"];Need(Root.Contains("22057_950遮阳板联动修正版_20261017_r113"),"Only isolated shade candidate writable");row["input_identity"]=Identity(Root);RmEvidence=row;
        try{
            ShRestore(row);var top=OpenTop(row);var protectedColumns=ShColumnBodies();row["standard_column_bodies_before"]=protectedColumns;var changes=new List<object>();row["dimension_changes"]=changes;
            var shade=Doc("遮阳板.SLDPRT");Set(shade,"D1@草图1",790,changes);Need(shade.ForceRebuild3(false),"Shade native rebuild failed");
            var frame=Doc("立柱连接架.SLDPRT");RmActivate(frame,"shade_frame_end_features");foreach(string n in new[]{"参考安装_左端区内移25","参考安装_右端区内移25"})Need(RmFeature(frame,n).SetSuppression2(1,2,null),"Cannot enable native frame end feature");Need(frame.ForceRebuild3(false),"Shortened frame rebuild failed");
            var press=Doc("太阳能连接压板.SLDPRT");ShPressEnds(press,row);Set(press,"D1@草图7",770,changes);Need(press.ForceRebuild3(false),"Press cutout rebuild failed");
            var assembly=Doc("太阳能雨棚装配.SLDASM");var log=new List<object>();row["constraint_changes"]=log;row["states_initial"]=ShStates(assembly);
            var cs=((Array)((AssemblyDoc)assembly).GetComponents(true)).Cast<Component2>().ToArray();
            ShRelease(assembly,cs.Single(x=>x.Name2=="左立柱-1"));ShRightMate(assembly,cs.Single(x=>x.Name2=="右立柱-1"),log);foreach(string side in new[]{"左","右"})foreach(string datum in new[]{"前视","上视"})ShNonWidthDatum(assembly,cs.Single(x=>x.Name2==side+"立柱-1"),datum,log);
            var eq=new List<object>();row["equations_added"]=eq;RmActivate(assembly,"native_equations");
            ShEquation(assembly,"\"遮阳板宽度\" = \"D1@草图1@遮阳板<1>.Part\"",eq);
            foreach(string side in new[]{"左","右"})ShEquation(assembly,"\"D1@参考安装_"+side+"端区内移25@立柱连接架<1>.Part\" = (840mm - \"遮阳板宽度\") / 2",eq);
            ShEquation(assembly,"\"D1@遮阳板联动_右立柱偏置\" = (840mm - \"遮阳板宽度\") / 2",eq);
            foreach(string side in new[]{"左","右"})ShEquation(assembly,"\"D1@遮阳板联动_"+side+"端缩短@太阳能连接压板<1>.Part\" = (840mm - \"遮阳板宽度\") / 2",eq);
            ShEquation(assembly,"\"D1@草图7@太阳能连接压板<1>.Part\" = \"遮阳板宽度\" - 20mm",eq);
            var tests=new List<object>();row["actual_linkage_tests"]=tests;
            foreach(double width in new[]{790.0,800.0,790.0}){
                RmActivate(shade,"set_source_width");Set(shade,"D1@草图1",width,changes);Need(shade.ForceRebuild3(false),"Shade width rebuild failed");
                RmActivate(assembly,"evaluate_linkage");((EquationMgr)assembly.GetEquationMgr()).EvaluateAll();Need(assembly.ForceRebuild3(false),"Linked canopy rebuild failed");
                tests.Add(new{source_width_mm=width,equations=Eqs(assembly),frame_controls=Dims(frame),frame_bounds=RmBox(frame),press_bounds=RmBox(press),press_dimensions=Dims(press),states=ShStates(assembly),mates=ShMates(assembly)});Flush("shade/linkage_measured/"+width);
                double shift=(840-width)/2000;Need(Math.Abs((RmBox(frame)[3]-RmBox(frame)[0])*1000-(width+39))<.001,"Frame width did not follow source");Need(Math.Abs((RmBox(press)[3]-RmBox(press)[0])*1000-(width+160))<.001,"Press overall width did not follow source");
                foreach(string side in new[]{"左","右"}){var component=cs.Single(x=>x.Name2==side+"立柱-1");var t=(double[])component.Transform2.ArrayData;Need(Math.Abs(t[9]-(side=="左"?shift:-shift))<1e-7,"Column did not follow source with correct sign: "+side);Need(component.GetConstrainedStatus()==3,"Column remains underconstrained: "+side);}
                foreach(string name in new[]{"太阳能板压条-2","太阳能板压条-3"})Need(Math.Abs(((double[])cs.Single(x=>x.Name2==name).Transform2.ArrayData)[9]-(.495-shift))<1e-7,"Short strip did not follow right column");
            }
            var after=ShColumnBodies();row["standard_column_bodies_after"]=after;foreach(var pair in protectedColumns){Need(after[pair.Key].Length==pair.Value.Length,"Standard column body count changed");for(int i=0;i<pair.Value.Length;i++)Need(after[pair.Key][i].Zip(pair.Value[i],(x,y)=>Math.Abs(x-y)).Max()<1e-9,"Standard column local geometry changed: "+pair.Key);}row["standard_column_geometry_unchanged"]=true;
            RmActivate(top,"final_rebuild");Need(top.ForceRebuild3(false),"Top final rebuild failed");row["health"]=TrAssemblyHealth();row["snapshot"]=Snapshot(top,false);row["canopy_final_equations"]=Eqs(assembly);row["final_components"]=ShStates(assembly);row["native_save_performed"]=false;
            ShSaveAffected(top,row);row["native_save_performed"]=true;row["diagnostic_only"]=false;row["expected_visible_solids"]=387;row["final_details"]=new[]{shade,frame,press,Doc("太阳能板压条.SLDPRT")}.Select(d=>ShDetails(d)).ToArray();Flush("shade/native_candidate_saved");
        }finally{Sw.CloseAllDocuments(true);row["final_identity"]=Identity(Root);}
    }
    static void ShVerify(Dictionary<string,object> c,Dictionary<string,object> row){
        Need(!Writable,"Saved shade verification is read only");Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);row["input_identity"]=before;
        try{var top=OpenTop(row);var assembly=Doc("太阳能雨棚装配.SLDASM");var shade=Doc("遮阳板.SLDPRT");var frame=Doc("立柱连接架.SLDPRT");var press=Doc("太阳能连接压板.SLDPRT");var shortStrip=Doc("太阳能板压条.SLDPRT");var columns=ShColumnBodies();row["saved_details"]=new[]{shade,frame,press,shortStrip}.Select(d=>ShDetails(d)).ToArray();row["saved_equations"]=Eqs(assembly);row["saved_components"]=ShStates(assembly);Need(Math.Abs(((Dimension)shade.Parameter("D1@草图1")).SystemValue-.790)<1e-9,"Saved shade width differs");
            var tests=new List<object>();row["reopened_linkage_tests"]=tests;var changes=new List<object>();row["unsaved_width_controls"]=changes;
            foreach(double width in new[]{790.0,800.0,790.0}){RmActivate(shade,"reopened_source_width");Set(shade,"D1@草图1",width,changes);Need(shade.ForceRebuild3(false),"Reopened shade rebuild failed");RmActivate(assembly,"reopened_equation_test");((EquationMgr)assembly.GetEquationMgr()).EvaluateAll();Need(assembly.ForceRebuild3(false),"Reopened linked canopy rebuild failed");var states=((Array)((AssemblyDoc)assembly).GetComponents(true)).Cast<Component2>().ToArray();double shift=(840-width)/2000;foreach(string side in new[]{"左","右"}){var part=states.Single(x=>x.Name2==side+"立柱-1");var transform=(double[])part.Transform2.ArrayData;Need(part.GetConstrainedStatus()==3&&Math.Abs(transform[9]-(side=="左"?shift:-shift))<1e-7&&Math.Abs(transform[10])<1e-7&&Math.Abs(transform[11])<1e-7,"Saved column linkage or absolute location failed");}Need(Math.Abs((RmBox(frame)[3]-RmBox(frame)[0])*1000-(width+39))<.001&&Math.Abs((RmBox(press)[3]-RmBox(press)[0])*1000-(width+160))<.001,"Saved linked widths failed");tests.Add(new{source_width_mm=width,frame_bounds=RmBox(frame),press_bounds=RmBox(press),equations=Eqs(assembly),states=ShStates(assembly),mates=ShMates(assembly)});Flush("shade/reopened_linkage_measured");}
            var after=ShColumnBodies();foreach(var pair in columns)for(int i=0;i<pair.Value.Length;i++)Need(after[pair.Key][i].Zip(pair.Value[i],(x,y)=>Math.Abs(x-y)).Max()<1e-9,"Reopened standard column local body changed");row["standard_column_geometry_unchanged"]=true;row["standard_column_bodies"]=after;
            RmActivate(top,"independent_final_rebuild");row["full_rebuild_return"]=top.ForceRebuild3(false);Need(Convert.ToBoolean(row["full_rebuild_return"]),"Independent top rebuild failed");row["snapshot"]=Snapshot(top,false);row["health"]=TrAssemblyHealth();row["final_equations"]=Eqs(assembly);row["final_components"]=ShStates(assembly);row["expected_visible_solids"]=387;ExportCurrent(top,row,(string)c["id"]);row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }

    static void LedCycle(ModelDoc2 top,Dictionary<string,object> row,string label) {
        var shade=Doc("遮阳板.SLDPRT");var assembly=Doc("太阳能雨棚装配.SLDASM");var led=Doc("雨棚LED灯罩壳.SLDPRT");
        var frame=Doc("立柱连接架.SLDPRT");var press=Doc("太阳能连接压板.SLDPRT");var weld=Doc("雨棚LED灯罩焊接.SLDASM");
        var columns=ShColumnBodies();var tests=new List<object>();row[label]=tests;var changes=new List<object>();row["width_controls"]=changes;
        foreach(double width in new[]{790.0,800.0,790.0}) {
            RmActivate(shade,"led_source_width");Set(shade,"D1@草图1",width,changes);Need(shade.ForceRebuild3(false),"Source rebuild failed");
            RmActivate(assembly,"led_equation_evaluate");((EquationMgr)assembly.GetEquationMgr()).EvaluateAll();Need(assembly.ForceRebuild3(false),"Linked canopy rebuild failed");
            Need(Math.Abs(((Dimension)led.Parameter("D2@草图1")).SystemValue*1000-(width-62))<.001,"Nested LED width did not follow source");
            Need(led.ForceRebuild3(false)&&weld.ForceRebuild3(false)&&assembly.ForceRebuild3(false),"LED linked geometry rebuild failed");
            Need(Math.Abs((RmBox(led)[3]-RmBox(led)[0])*1000-(width-62))<.005,"LED body width did not follow source");
            Need(Math.Abs((RmBox(frame)[3]-RmBox(frame)[0])*1000-(width+39))<.001&&Math.Abs((RmBox(press)[3]-RmBox(press)[0])*1000-(width+160))<.001,"Existing linked widths failed");
            var cs=((Array)((AssemblyDoc)assembly).GetComponents(true)).Cast<Component2>().ToArray();double shift=(840-width)/2000;
            foreach(string side in new[]{"左","右"}) {var c=cs.Single(x=>x.Name2==side+"立柱-1");var t=(double[])c.Transform2.ArrayData;Need(c.GetConstrainedStatus()==3&&Math.Abs(t[9]-(side=="左"?shift:-shift))<1e-7&&Math.Abs(t[10])<1e-7&&Math.Abs(t[11])<1e-7,"Standard column location or constraint failed");}
            foreach(string name in new[]{"太阳能板压条-2","太阳能板压条-3"}) {var c=cs.Single(x=>x.Name2==name);Need(c.GetConstrainedStatus()==3&&Math.Abs(((double[])c.Transform2.ArrayData)[9]-(.495-shift))<1e-7,"Short strip placement failed");}
            tests.Add(new{source_width_mm=width,frame_bounds=RmBox(frame),press_bounds=RmBox(press),led_bounds=RmBox(led),led_dimensions=Dims(led),equations=Eqs(assembly),states=ShStates(assembly),led_weld_states=ShStates(weld),mates=ShMates(assembly),led_mates=ShMates(weld)});Flush("led/linkage_measured/"+width);
        }
        var after=ShColumnBodies();foreach(var pair in columns){Need(after[pair.Key].Length==pair.Value.Length,"Standard column body count changed");for(int i=0;i<pair.Value.Length;i++)Need(after[pair.Key][i].Zip(pair.Value[i],(x,y)=>Math.Abs(x-y)).Max()<1e-9,"Standard column local geometry changed");}
        row["standard_column_geometry_unchanged"]=true;row["standard_column_bodies"]=after;
        RmActivate(top,"led_final_top_rebuild");row["full_rebuild_return"]=top.ForceRebuild3(false);Need((bool)row["full_rebuild_return"],"Final top rebuild failed");
        row["snapshot"]=Snapshot(top,false);row["health"]=TrAssemblyHealth();row["final_equations"]=Eqs(assembly);row["final_components"]=ShStates(assembly);row["expected_visible_solids"]=387;
        row["led_final_details"]=ShDetails(led);row["final_details"]=new[]{shade,frame,press,Doc("太阳能板压条.SLDPRT"),Doc("塑料灯罩.SLDPRT"),Doc("塑料板压条焊件.SLDPRT")}.Select(d=>ShDetails(d)).ToArray();
    }
    static void LedPersist(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(Writable&&Convert.ToInt32(c["index"])==13,"Exact A candidate only");Root=(string)c["package_root"];Top=(string)c["top"];Need(Root.Contains("22057_950遮阳板联动修正版_20261017_r113"),"Bounded current candidate only");
        var before=Identity(Root);row["input_identity"]=before;row["native_save_performed"]=false;
        var allowed=new HashSet<string>{"雨棚LED灯罩壳.SLDPRT","雨棚LED灯罩焊接.SLDASM","太阳能雨棚装配.SLDASM",Top};
        try {
            var top=OpenTop(row);var led=Doc("雨棚LED灯罩壳.SLDPRT");row["led_initial_details"]=ShDetails(led);
            var assembly=Doc("太阳能雨棚装配.SLDASM");RmActivate(assembly,"led_add_nested_native_equation");var eq=new List<object>();row["equations_added"]=eq;
            ShEquation(assembly,"\"D2@草图1@雨棚LED灯罩壳<1>.Part@雨棚LED灯罩焊接<1>.Assembly\" = \"遮阳板宽度\" - 62mm",eq);
            LedCycle(top,row,"actual_linkage_tests");
            var docs=new List<ModelDoc2>();for(ModelDoc2 d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2)docs.Add(d);
            var saves=new List<object>();row["saved_documents"]=saves;
            foreach(var d in docs.Where(d=>allowed.Contains(Path.GetFileName(d.GetPathName()))).OrderBy(d=>d.GetPathName()==top.GetPathName()?2:d is AssemblyDoc?1:0)){
                if(!d.GetSaveFlag())continue;Need(Within(d.GetPathName(),Root)&&!d.IsOpenedReadOnly(),"Refuse external save");int e=0,w=0;bool ok=d.Save3(1,ref e,ref w);saves.Add(new{document=Path.GetFileName(d.GetPathName()),saved=ok,errors=e,warnings=w});Flush("led/save");Need(ok&&e==0&&w==0,"LED native save failed");
            }
            row["native_save_performed"]=true;row["diagnostic_only"]=false;
        } finally {
            Sw.CloseAllDocuments(true);var after=Identity(Root);row["final_identity"]=after;Need(before.Count==after.Count&&before.All(p=>after.ContainsKey(p.Key)),"Candidate file set changed");
            row["changed_files"]=before.Keys.Where(n=>before[n]!=after[n]).ToArray();Need(before.All(p=>allowed.Contains(p.Key)||after[p.Key]==p.Value),"Unrelated native document changed");
        }
    }
    static void LedVerify(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(!Writable,"Read-only independent LED verify required");Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);row["input_identity"]=before;
        try{var top=OpenTop(row);row["saved_equations"]=Eqs(Doc("太阳能雨棚装配.SLDASM"));row["saved_led_details"]=ShDetails(Doc("雨棚LED灯罩壳.SLDPRT"));LedCycle(top,row,"reopened_linkage_tests");ExportCurrent(top,row,(string)c["id"]);row["native_save_performed"]=false;}
        finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
