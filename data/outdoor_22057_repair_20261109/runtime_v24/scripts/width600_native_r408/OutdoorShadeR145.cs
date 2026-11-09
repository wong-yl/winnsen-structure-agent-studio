using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static string Stage;
    static void SyncBaseSlots(double depth,Dictionary<string,object> row)
    {
        var d=Doc("底加强筋2.SLDPRT");RmActivate(d,"base_slot_linkage");Need(d.ForceRebuild3(false),"Base rib rebuild failed");
        var f=RmFeature(d,"草图2");d.ClearSelection2(true);Need(f.Select2(false,0),"Cannot select base slot sketch");d.EditSketch();
        var s=d.GetActiveSketch2() as Sketch;Need(s!=null,"Base slot sketch not active");
        var relations=((Array)s.RelationManager.GetRelations(0)).Cast<SketchRelation>().Where(r=>r.GetRelationType()==9).Where(r=>{
            var points=(r.GetEntities() as Array ?? new object[0]).Cast<object>().OfType<SketchPoint>().ToArray();
            return points.Length==2 && points.All(p=>Math.Abs(p.X+.026)<1e-8 && Math.Abs(p.Y+.1698)<1e-8);
        }).ToArray();Need(relations.Length==1,"Exact base slot location relation required");
        Need(s.RelationManager.DeleteRelation(relations[0]),"Cannot release fixed standard slot location");
        var point=((Array)s.GetSketchPoints2()).Cast<SketchPoint>().Single(p=>Math.Abs(p.X+.026)<1e-8 && Math.Abs(p.Y+.1698)<1e-8);
        double y=-(depth/2-105.2)/1000;Need(point.SetCoords(-.026,y,0),"Base slot center move failed");
        d.ClearSelection2(true);Need(point.Select4(false,null),"Cannot constrain linked base slot");d.SketchAddConstraints("sgFIXED");
        Need(s.GetConstrainedStatus()==3,"Base slot sketch must remain constrained");
        row["base_slot_linkage"]=new{front_edge_offset_mm=105.2,slot_end_sketch_y_mm=y*1000,slot_length_mm=35,slot_width_mm=14,constrained_status=s.GetConstrainedStatus()};
        d.SketchManager.InsertSketch(true);Need(d.ForceRebuild3(false),"Linked base slots rebuild failed");
    }
    static object BaseRelations(ModelDoc2 d)
    {
        return Features(d).Where(f=>f.GetTypeName2()=="ProfileFeature" && new[]{"草图1","草图2","草图8"}.Contains(f.Name)).Select(f=>{
            var s=(Sketch)f.GetSpecificFeature2();var raw=s.RelationManager.GetRelations(0) as Array;
            return new{name=f.Name,relations=raw==null?null:raw.Cast<SketchRelation>().Select(r=>new{type=r.GetRelationType(),entities=(r.GetEntities() as Array ?? new object[0]).Cast<object>().Select(e=>e is SketchPoint?(object)new{point=new[]{((SketchPoint)e).X,((SketchPoint)e).Y,((SketchPoint)e).Z}}:e is SketchSegment?(object)new{segment=((SketchSegment)e).GetType(),length=((SketchSegment)e).GetLength()}:new{kind=e==null?"external_null":e.GetType().Name}).ToArray()}).ToArray()};
        }).ToArray();
    }
    static void SyncReceivers(Dictionary<string,object> row)
    {
        var d=Doc("箱体雨棚安装架.SLDPRT");RmActivate(d,"receiver_linkage");RmConfiguration(d,"Default");
        var source=RmFeature(d,"草图13").GetSpecificFeature2() as Sketch;
        var holes=((Array)source.GetSketchSegments()).Cast<SketchSegment>().Where(s=>!s.ConstructionGeometry&&s is SketchArc).Cast<SketchArc>().ToArray();Need(holes.Length==3,"Three source holes required");
        var centers=holes.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return RmPoint(new[]{p.X,p.Y,p.Z},source.ModelToSketchTransform.IInverse());}).OrderByDescending(p=>RmDot(p,RmDepth)).ToArray();
        var feature=RmFeature(d,"参考接收凸台_3x外11内6草图");
        d.ClearSelection2(true);Need(feature.Select2(false,0),"Cannot select receiver sketch");d.EditSketch();var sketch=d.GetActiveSketch2() as Sketch;Need(sketch!=null,"Receiver sketch not active");
        Need(sketch.RelationManager.DeleteAllRelations(),"Cannot release receiver fixed centers");
        var arcs=((Array)sketch.GetSketchSegments()).Cast<SketchSegment>().Where(s=>!s.ConstructionGeometry&&s is SketchArc).Cast<SketchArc>().ToArray();Need(arcs.Length==6,"Six receiver circles required");
        var values=arcs.Select(a=>new{arc=a,point=(SketchPoint)a.GetCenterPoint2()}).OrderBy(a=>a.point.X).ThenBy(a=>a.point.Y).ToArray();
        var requested=centers.Select(p=>RmPoint(p.Zip(RmNormal,(v,n)=>v-n*.0015).ToArray(),sketch.ModelToSketchTransform)).OrderBy(p=>p[0]).ThenBy(p=>p[1]).ToArray();
        var targets=new List<double[]>();foreach(var center in requested){targets.Add(center);targets.Add(center);}
        for(int i=0;i<values.Length;i++)Need(values[i].point.SetCoords(targets[i][0],targets[i][1],0),"Receiver center move rejected");
        foreach(var a in arcs){d.ClearSelection2(true);Need(((SketchSegment)a).Select4(false,null),"Cannot constrain receiver circle");d.SketchAddConstraints("sgFIXED");}
        Need(sketch.GetConstrainedStatus()==3,"Receiver sketch must be constrained");
        row["receiver_centers_sketch_m"]=arcs.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new{x=p.X,y=p.Y,z=p.Z,radius=a.GetRadius()};}).ToArray();
        d.SketchManager.InsertSketch(true);Need(d.ForceRebuild3(false),"Receiver native rebuild failed");
        RmConfiguration(d,"950_RIGHT_NATIVE");Need(d.ForceRebuild3(false),"Right receiver rebuild failed");RmConfiguration(d,"Default");
    }
    static void RelocateTask(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Root=(string)c["package_root"];Top=(string)c["top"];Need(Within(Root,Stage),"Relocate only isolated task files");
        if(c.ContainsKey("cover_span_repair_source"))ValidateCoverRepairInput(c,row);
        var changes=new List<object>();row["reference_changes"]=changes;
        foreach(string path in Directory.GetFiles(Root).Where(p=>p.EndsWith(".SLDASM",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase))) {
            Need((File.GetAttributes(path)&FileAttributes.ReadOnly)==0,"Candidate must be writable");
            var raw=Sw.GetDocumentDependencies2(path,false,false,false) as Array;if(raw==null)continue;string[] deps=raw.Cast<string>().ToArray();Need(deps.Length%2==0,"Unexpected dependency array");
            for(int i=1;i<deps.Length;i+=2) {
                string old=deps[i],target=Path.Combine(Root,Path.GetFileName(old));if(!File.Exists(target)||old.Equals(target,StringComparison.OrdinalIgnoreCase))continue;
                if(!old.EndsWith(".SLDASM",StringComparison.OrdinalIgnoreCase)&&!old.EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase))continue;
                bool ok=Sw.ReplaceReferencedDocument(path,old,target);changes.Add(new{owner=Path.GetFileName(path),old_path=old,target=target,replaced=ok});Flush("relocate/"+Path.GetFileName(path));Need(ok,"Native reference relocation failed");
            }
        }
        row["final_identity"]=Identity(Root);
    }
    static void RebuildAll(ModelDoc2 top, Dictionary<string,object> row)
    {
        // Rebuild the complete assembly once; Snapshot checks every loaded document.
        RmActivate(top,"generation_final_rebuild");bool rebuilt=top.ForceRebuild3(false);if(!rebuilt){row["failed_final_rebuild_health"]=TrAssemblyHealth();Flush("failed_final_rebuild_health");}Need(rebuilt,"Top native rebuild failed");
    }
    static void ApplyInputs(ModelDoc2 top, double h,double w,double depth,Dictionary<string,object> row)
    {
        var changes=new List<object>();row["input_changes"]=changes;var master=Doc("防水寄存柜 模型.SLDPRT");
        var canopy=Doc("太阳能雨棚装配.SLDASM");var canopyDirections=CaptureCanopyDirections(canopy);PrepareCanopySpan(canopy,w,canopyDirections,changes);
        SyncOriginalDoorAuxiliaryHoles(w,changes);SyncCoverFasteners(w,changes,row);SyncFloorFasteners(w,changes,row);SyncCoverSpan(w,changes,row);
        SyncRearRibPilots(h,w,row);SyncRoofBoardMounts(w,changes,row);Set(master,"D1@草图1",w,changes);Set(master,"D2@草图1",h-40,changes);Set(master,"D1@凸台-拉伸1",depth,changes);
        foreach(string file in new[]{"independent_size_rules.json","assembly_size_rules.json"}) {
            var rules=Json.Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(Path.Combine(Stage,file),Encoding.UTF8));
            foreach(var rule in rules) {
                string name=(string)rule["document"];if(name.StartsWith("户外快递副柜"))name=Top;var doc=Doc(name);
                double value=Convert.ToDouble(rule["baseline_mm"])+Convert.ToDouble(rule["height_factor"])*(h-1939);
                if(rule.ContainsKey("width_factor"))value+=Convert.ToDouble(rule["width_factor"])*(w-1000);
                if(rule.ContainsKey("depth_factor"))value+=Convert.ToDouble(rule["depth_factor"])*(depth-550);
                Set(doc,(string)rule["dimension"],value,changes);
            }
        }
        Set(Doc("箱体左侧板.SLDPRT"),"D2@草图68",(w-70)/2,changes);
        Set(Doc("箱体左侧板.SLDPRT"),"D3@阵列(线性)1",Math.Min(800,Math.Max(100,(h-175.8670006)/2)),changes);
        Set(Doc("箱体侧板加强筋.SLDPRT"),"D3@阵列(线性)1",Math.Min(152,Math.Max(50,(h-296.3)/10)),changes);
        Set(Doc("箱体竖门框挡边.SLDPRT"),"D3@阵列(线性)1",Math.Min(400,Math.Max(100,(h-205.4)/4)),changes);
        double pitch=152+(h-1939)/12;
        Set(Doc("顶隔板.SLDPRT"),"D2@草图9",544+depth-550,changes);
        Set(Doc("箱体侧板加强筋.SLDPRT"),"D1@草图7",150.5+(h-1939)/12,changes);
        foreach(string[] target in new[]{new[]{"箱体侧板加强筋.SLDPRT","D3@阵列(线性)1"},new[]{"箱体竖门框挡边.SLDPRT","D3@阵列(线性)2"},new[]{"箱体左侧板.SLDPRT","D3@阵列(线性)*152"},new[]{"箱体防水门框 竖.SLDPRT","D3@阵列(线性)1"},new[]{"箱体防水门框 竖.SLDPRT","D3@阵列(线性)2"},new[]{"箱体右侧板.SLDPRT","D3@阵列(线性)2"}})Set(Doc(target[0]),target[1],pitch,changes);
        Need(master.ForceRebuild3(false),"Cabinet master rebuild failed");
        SyncNarrowShelfWidth(Doc("箱体横层板L.SLDPRT"),w,changes,row);SyncShelfNotchHeight(Doc("箱体横层板L.SLDPRT"),changes,row);
        var narrowShelfRight=Doc("箱体横层板R.SLDPRT");Need(narrowShelfRight.ForceRebuild3(false)&&narrowShelfRight.Extension.GetWhatsWrongCount()==0,"Mirrored narrow shelf failed");
        SyncDividerDepth(depth,changes,row);
        SyncCoverSpan(w,changes,row);
        SyncBaseSlots(depth,row);
        SyncNarrowLamp(w,changes,row);SyncShortLed(w,changes,row);var shade=Doc("遮阳板.SLDPRT");Set(shade,"D1@草图1",w-160,changes);Need(shade.ForceRebuild3(false),"Shade rebuild failed");
        RmActivate(canopy,"canopy_equations");((EquationMgr)canopy.GetEquationMgr()).EvaluateAll();bool canopyOk=canopy.ForceRebuild3(false);if(!canopyOk){row["failed_canopy_health"]=TrAssemblyHealth();Flush("failed_canopy_health");}Need(canopyOk,"Shade-linked canopy failed");
        FinishCanopySpan(canopy,w,changes);RmActivate(canopy,"canopy_span_rebuild");Need(canopy.ForceRebuild3(false),"Canopy punch linkage rebuild failed");
        Need(Math.Abs((RmBox(Doc("立柱连接架.SLDPRT"))[3]-RmBox(Doc("立柱连接架.SLDPRT"))[0])*1000-(w-121))<.005,"Connector span mismatch");
        Need(Math.Abs((RmBox(Doc("太阳能连接压板.SLDPRT"))[3]-RmBox(Doc("太阳能连接压板.SLDPRT"))[0])*1000-w)<.005,"Long press span mismatch");
        SyncReceivers(row);
        var mu=Sw.GetMathUtility() as MathUtility;
        foreach(string[] pair in new[]{new[]{"底座横筋焊接.SLDASM","底座横筋焊件3-2"},new[]{"拆装底座焊接.SLDASM","底座竖筋焊接-2"}}) {
            var doc=Doc(pair[0]);RmActivate(doc,"base_position");var component=((Array)((AssemblyDoc)doc).GetComponents(true)).Cast<Component2>().Single(c=>c.Name2.Split('/').Last()==pair[1]);
            Need(!component.IsFixed() && (component.GetMates()==null||((Array)component.GetMates()).Length==0),"Base placement contract changed");
            var transform=(double[])component.Transform2.ArrayData;transform[9]=w/1000-.070;component.Transform2=mu.CreateTransform(transform) as MathTransform;
        }
        RefreshRoofEndRelief(row);RmActivate(top,"dimension_propagation_before_layout");row["pre_layout_rebuild_return"]=top.ForceRebuild3(false);row["pre_layout_health"]=TrAssemblyHealth();Flush("dimension_propagation_before_layout_not_accepted");
    }
    static void CloseSavedDocumentGraph(Dictionary<string,object> row) {
        var closures=new List<object>();row["saved_reload_document_closure"]=closures;
        for(int pass=0;pass<3;pass++) {
            Sw.CloseAllDocuments(true);var docs=new List<ModelDoc2>();
            for(ModelDoc2 d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2)docs.Add(d);
            if(docs.Count==0)return;
            foreach(var d in docs.OrderBy(d=>d is AssemblyDoc?0:1)) {
                string path=d.GetPathName();Need(Within(path,Root),"Refuse to close unrelated document");
                closures.Add(new{pass=pass,path=path,read_only=d.IsOpenedReadOnly(),save_flag=d.GetSaveFlag()});Sw.CloseDoc(d.GetTitle());
            }
            Flush("saved_reload_close_background/"+pass);
        }
        if(Sw.GetFirstDocument()!=null) {
            for(var d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2)
                Need(Within(d.GetPathName(),Root)&&d.IsOpenedReadOnly()&&!d.GetSaveFlag(),"Only clean isolated read-only references may trigger a saved reload restart");
            RestartSavedReloadCad(row);
        }
        Need(Sw.GetFirstDocument()==null,"Saved document graph did not fully close");
    }
    static void Generate(Dictionary<string,object> c,Dictionary<string,object> row,bool persist)
    {
        Root=(string)c["package_root"];Top=(string)c["top"];Need(Within(Root,Stage),"Task must be isolated inside generator stage");
        var before=Identity(Root);row["input_identity"]=before;
        double[] dims=((IEnumerable)c["dimensions_mm"]).Cast<object>().Select(Convert.ToDouble).ToArray();
        Dictionary<string,string> savedReloadIdentity=null;bool initialWritable=Writable;
        try {
            bool resume=persist&&c.ContainsKey("resume_layout_parts")&&Convert.ToBoolean(c["resume_layout_parts"]);if(resume)CheckLayoutPartsCheckpoint(c,row);else if(persist&&!c.ContainsKey("cover_span_repair_source"))PrepareLayout(c,row);var top=OpenTop(row);var columns=ShColumnBodies();
            if(persist){Need(!resume,"Expanded generator requires a fresh isolated request");if(c.ContainsKey("cover_span_repair_source")){var coverChanges=new List<object>();row["input_changes"]=coverChanges;SyncCoverSpan(dims[1],coverChanges,row);row["reused_verified_layout_without_regeneration"]=true;}else{ApplyInputs(top,dims[0],dims[1],dims[2],row);BuildLayout(top,c,row);BuildLayoutFeatures(c,row);}RebuildAll(top,row);}else {RmActivate(top,"independent_reopen");Need(top.ForceRebuild3(false),"Reopened top rebuild failed");}
            if(persist){SyncLeftRearRib(dims[1],(List<object>)row["input_changes"],row);ReanchorRightRibMate(dims[0],dims[1],row);RefreshLayoutAssemblyGraph(top,row);}row["snapshot"]=Snapshot(top,false);row["full_rebuild_return"]=true;row["health_pass"]=true;row["part_sizes"]=Sizes();
            row["canopy_equations"]=Eqs(Doc("太阳能雨棚装配.SLDASM"));row["canopy_states"]=ShStates(Doc("太阳能雨棚装配.SLDASM"));
            row["controls"]=new[]{"防水寄存柜 模型.SLDPRT","遮阳板.SLDPRT","雨棚LED灯罩壳.SLDPRT","箱体雨棚安装架.SLDPRT"}.Select(n=>new{name=n,dimensions=Dims(Doc(n))}).ToArray();
            var after=ShColumnBodies();foreach(var item in columns){Need(after[item.Key].Length==item.Value.Length,"Standard column body count changed");for(int i=0;i<item.Value.Length;i++)Need(after[item.Key][i].Zip(item.Value[i],(a,b)=>Math.Abs(a-b)).Max()<1e-9,"Standard column local geometry changed");}
            row["standard_columns_unchanged"]=true;
            if(persist){
                SaveDocs(top,row);row["native_save_performed"]=true;row["snapshot_before_saved_reload"]=row["snapshot"];
                savedReloadIdentity=Identity(Root);CloseSavedDocumentGraph(row);Writable=false;
                top=OpenTop(row);RmActivate(top,"saved_file_reload_before_export");Need(top.ForceRebuild3(false),"Saved cabinet failed read-only reload");
                row["snapshot"]=Snapshot(top,false);row["saved_file_reload_before_export"]=true;CheckIdentity(Root,savedReloadIdentity);
            }else row["native_save_performed"]=false;
            row["roof_board_saved_mates"]=LayoutMates(top);row["roof_board_saved_parts"]=new[]{"箱体顶板.SLDPRT","锁控板支架.SLDPRT"}.Select(n=>new{name=n,health=LayoutFeatureHealth(Doc(n)),details=ShDetails(Doc(n))}).ToArray();
            row["narrow_lamp_saved_parts"]=new[]{"雨棚LED灯罩壳.SLDPRT","塑料灯罩.SLDPRT","塑料板压条焊件.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) L.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) R.SLDPRT"}.Select(n=>new{name=n,health=LayoutFeatureHealth(Doc(n)),details=ShDetails(Doc(n))}).ToArray();
            row["canopy_mates"]=LayoutMates(Doc("太阳能雨棚装配.SLDASM"));row["canopy_states"]=ShStates(Doc("太阳能雨棚装配.SLDASM"));
            ExportCurrent(top,row,(string)c["id"]);
        }finally{Sw.CloseAllDocuments(true);Writable=initialWritable;row["final_identity"]=Identity(Root);if(savedReloadIdentity!=null){CheckIdentity(Root,savedReloadIdentity);row["saved_reload_files_unchanged"]=true;}if(!persist){CheckIdentity(Root,before);row["files_unchanged"]=true;}}
    }
    static void SizingSurvey(Dictionary<string,object> c, Dictionary<string,object> row)
    {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);row["input_identity"]=before;
        try {
            var top=OpenTop(row);
            row["documents"]=new[]{"防水寄存柜 模型.SLDPRT","太阳能雨棚 模型.SLDPRT","箱体雨棚安装架.SLDPRT","盖板.SLDPRT","遮阳板.SLDPRT","拆装底座 模型.SLDPRT","底座竖筋焊件2.SLDPRT","底加强筋2.SLDPRT",Top}.Select(n=>new{name=n,dimensions=Dims(Doc(n)),equations=Eqs(Doc(n)),sketches=n.Contains("底加强筋2")||n.Contains("拆装底座 模型")?Sketches(Doc(n)):null}).ToArray();
            row["base_relations"]=new[]{"底加强筋2.SLDPRT","拆装底座 模型.SLDPRT"}.Select(n=>new{name=n,sketches=BaseRelations(Doc(n))}).ToArray();
            row["top_mates"]=ShMates(top);row["survey_without_rebuild"]=true;
        } finally {Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
    static int Main(string[] args)
    {
        Stage=Path.GetDirectoryName(typeof(OutdoorShadeR145).Assembly.Location);
        string contractPath=Path.GetFullPath(args[0]),mode=args[1];EvidencePath=Path.GetFullPath(args[2]);
        Need(new[]{"relocate","sizing_survey","layout_survey","layout_probe","generate","verify"}.Contains(mode),"Unsupported operation");
        Need(!File.Exists(EvidencePath),"Preserve evidence");var contract=Read(contractPath);
        Need((string)contract["scope"]=="22057_SHADE_PORTAL_R145","Wrong scope");
        Writable=mode=="generate"||mode=="layout_probe";var rows=new List<object>();
        E=new Dictionary<string,object>{{"status","starting"},{"mode",mode},{"contract_sha256",Hash(contractPath)},{"started_utc",DateTime.UtcNow.ToString("o")},{"cases",rows}};
        bool owned=false;
        try {
            Need(Process.GetProcessesByName("SLDWORKS").Count(process=>!process.HasExited)==0,"CAD busy");DateTime activation=DateTime.UtcNow;
            Sw=Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28",true)) as ISldWorks;
            var p=Process.GetProcessById(Sw.GetProcessID());var path=new StringBuilder(4096);
            Need(GetLongPathName(p.MainModule.FileName,path,(uint)path.Capacity)>0 && path.ToString().Equals(@"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe",StringComparison.OrdinalIgnoreCase) && p.StartTime.ToUniversalTime()>=activation.AddSeconds(-2) && Process.GetProcessesByName("SLDWORKS").Length==1 && Sw.GetFirstDocument()==null,"Fresh exclusive CAD identity required");
            owned=true;E["process_id"]=p.Id;E["process_start_utc"]=p.StartTime.ToUniversalTime().ToString("o");E["revision"]=Sw.RevisionNumber();
            Sw.Visible=false;Sw.DocumentVisible(false,1);Sw.DocumentVisible(false,2);Sw.CommandInProgress=true;Flush("start");
            foreach(Dictionary<string,object> c in (IEnumerable)contract["cases"]) {
                var layout=LayoutRows(c);ExpectedNativeDocuments=layout.Count==0?122:122+layout.Count*3+layout.Count(r=>(string)r["side"]=="R")+1;ExpectedVisibleSolids=layout.Count==0?367:109+19*layout.Count-layout.Count(r=>Convert.ToDouble(r["height_mm"])<298);var row=new Dictionary<string,object>{{"id",c["id"]},{"index",c["index"]},{"dimensions_mm",c["dimensions_mm"]}};rows.Add(row);Flush(mode+"/open");
                if(mode=="relocate")RelocateTask(c,row);else if(mode=="sizing_survey")SizingSurvey(c,row);else if(mode=="layout_survey")LayoutSurvey(c,row);else if(mode=="layout_probe")LayoutProbe(c,row);else {if(mode=="generate")RelocateTask(c,row);Generate(c,row,mode=="generate");}
                row["status"]="PASS";Flush(mode+"/complete");
            }
            E["status"]=mode+"_finished";return 0;
        } catch(Exception ex) {E["status"]="failed";E["error"]=ex.ToString();return 1;}
        finally {if(owned&&Sw!=null){Sw.CloseAllDocuments(true);Sw.CommandInProgress=false;Sw.ExitApp();E["exit_app_called"]=true;}E["finished_utc"]=DateTime.UtcNow.ToString("o");Flush("finished");}
    }
}
