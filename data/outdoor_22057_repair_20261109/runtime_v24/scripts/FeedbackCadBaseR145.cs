// Bounded offline-copy packaging and relocation audit. Never saves source CAD.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static ISldWorks Sw;
    static JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 200000000 };
    static Dictionary<string, object> E;
    static string EvidencePath, Root;
    static string Top; static bool Writable; static int ExpectedVisibleSolids=431;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetLongPathName(string path, StringBuilder output, uint capacity);
    static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static string Hash(string p) { using (var s = SHA256.Create()) using (var f = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return BitConverter.ToString(s.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }
    static bool Within(string p, string root) { return Path.GetFullPath(p).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
    static void Flush(string op) { E["operation"] = op; File.WriteAllText(EvidencePath, Json.Serialize(E), new UTF8Encoding(false)); }
    static Dictionary<string, object> Read(string p) { return Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(p, Encoding.UTF8)); }
    static Dictionary<string, string> Identity(string root) { return Directory.GetFiles(root).Where(p => !Path.GetFileName(p).StartsWith("~$")).OrderBy(p => p).ToDictionary(p => Path.GetFileName(p), p => Hash(p), StringComparer.OrdinalIgnoreCase); }
    static void CheckIdentity(string root, Dictionary<string, string> expected) { var now = Identity(root); Need(now.Count == expected.Count && expected.All(r => now.ContainsKey(r.Key) && now[r.Key] == r.Value), "Input files changed: " + root); }
    static void LoadedInside()
    {
        for (ModelDoc2 d = Sw.GetFirstDocument() as ModelDoc2; d != null; d = d.GetNext() as ModelDoc2)
            Need(d.GetPathName().Length > 0 && Within(d.GetPathName(), Root), "Cross-root loaded document: " + d.GetPathName());
    }
    static ModelDoc2 OpenTop(Dictionary<string, object> row)
    {
        Need(Sw.GetFirstDocument() == null, "Previous case still loaded"); Sw.SetCurrentWorkingDirectory(Root);
        int err = 0, warn = 0;
        var d = Sw.OpenDoc6(Path.Combine(Root, Top), 2, Writable?1:3, "", ref err, ref warn) as ModelDoc2;
        row["open_errors"] = err; row["open_warnings"] = warn;
        Need(d != null && err == 0 && d.IsOpenedReadOnly()!=Writable, "Read-only assembly open failed");
        row["resolve_return"] = ((AssemblyDoc)d).ResolveAllLightWeightComponents(false);
        Need(((Configuration)d.GetActiveConfiguration()).Name == "BOTTOM_SHORT_PIN_FIX", "Saved top configuration changed");
        d.ShowFeatureErrorDialog = false; LoadedInside();
        foreach (string file in Directory.GetFiles(Root).Where(p => !Path.GetFileName(p).StartsWith("~$")).Where(p => p.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p))
        {
            if (Sw.GetOpenDocumentByName(file) != null) continue;
            int e = 0, w = 0; var extra = Sw.OpenDoc6(file, file.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase) ? 2 : 1, Writable?1:3, "", ref e, ref w) as ModelDoc2;
            Need(extra != null && e == 0 && extra.IsOpenedReadOnly()!=Writable, "Additional packaged document cannot open: " + file); LoadedInside();
        }
        int activateError = 0; Need(Sw.ActivateDoc3(d.GetTitle(), false, 0, ref activateError) != null, "Cannot activate top");
        return d;
    }
    static object Snapshot(ModelDoc2 top, bool rebuild)
    {
        var docs = new List<object>();
        if (rebuild) { Flush("rebuild/" + Path.GetFileName(Root)); Need(top.ForceRebuild3(false), "Relocated rebuild failed"); }
        var reopened=new List<object>();
        foreach(string file in Directory.GetFiles(Root).Where(p=>!Path.GetFileName(p).StartsWith("~$")&&(p.EndsWith(".sldprt",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".sldasm",StringComparison.OrdinalIgnoreCase))).OrderBy(p=>p)){
            if(Sw.GetOpenDocumentByName(file)!=null)continue;int errors=0,warnings=0;var restored=Sw.OpenDoc6(file,file.EndsWith(".sldasm",StringComparison.OrdinalIgnoreCase)?2:1,Writable?1:3,"",ref errors,ref warnings) as ModelDoc2;
            Need(restored!=null&&errors==0&&restored.IsOpenedReadOnly()!=Writable,"Cannot reload packaged document unloaded after instance suppression: "+file);reopened.Add(new{path=file,errors=errors,warnings=warnings});
        }
        if(reopened.Count>0){var records=E.ContainsKey("reloaded_unloaded_packaged_documents")?(List<object>)E["reloaded_unloaded_packaged_documents"]:new List<object>();E["reloaded_unloaded_packaged_documents"]=records;records.Add(new{root=Root,documents=reopened});Flush("snapshot/reloaded_unloaded_packaged_documents");}
        int restoreActiveError=0;Need(Sw.ActivateDoc3(top.GetTitle(),false,0,ref restoreActiveError)!=null,"Cannot restore top after packaged reference reload");
        LoadedInside();
        for (ModelDoc2 d = Sw.GetFirstDocument() as ModelDoc2; d != null; d = d.GetNext() as ModelDoc2)
        {
            int wrong = d.Extension.GetWhatsWrongCount(); Need(wrong == 0, "Unhealthy native document: " + d.GetPathName());
            object mp, cp, ft, dt, st, en, fc; int co; string cn;
            d.Extension.ListExternalFileReferences(out mp, out cp, out ft, out dt, out st, out en, out fc, out co, out cn);
            docs.Add(new { name = Path.GetFileName(d.GetPathName()), actual_path = d.GetPathName(), configuration = ((Configuration)d.GetActiveConfiguration()).Name,
                issues = wrong, external_model_paths = mp, external_component_paths = cp, external_statuses = st, external_features = ft, external_data_types = dt, external_entities = en, external_feature_components = fc, external_config_option = co, external_config_name = cn });
        }
        Need(docs.Count == ExpectedNativeDocuments, "Native document count mismatch expected=" + ExpectedNativeDocuments + " actual=" + docs.Count);
        var instances = new List<object>(); int total = 0, invalid = 0;
        foreach (Component2 c in ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().OrderBy(c => c.Name2, StringComparer.Ordinal))
        {
            string path = c.GetPathName(); Need(path.Length > 0 && Within(path, Root) && File.Exists(path), "Component reference escaped or missing: " + path);
            var bodies = new List<object>();
            if (path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase) && !c.IsSuppressed())
            {
                object info; Array raw = c.GetBodies3(0, out info) as Array;
                if (raw != null) foreach (Body2 b in raw)
                {
                    int check = b.Check2(); if (check != 0) invalid++;
                    bodies.Add(new { name = b.Name, check = check, box_m = b.GetBodyBox(), mass = b.GetMassProperties(1), faces = b.GetFaceCount(), edges = b.GetEdgeCount() });
                }
                if (!c.IsHidden(true)) total += bodies.Count;
            }
            instances.Add(new { name = c.Name2, document = Path.GetFileName(path), configuration = c.ReferencedConfiguration,
                suppressed = c.IsSuppressed(), hidden = c.IsHidden(true), fixed_instance = c.IsFixed(), transform = c.Transform2 == null ? null : c.Transform2.ArrayData, bodies = bodies });
        }
        Need(total == ExpectedVisibleSolids && invalid == 0, "Expected "+ExpectedVisibleSolids+" healthy visible solids; actual=" + total + " invalid=" + invalid);
        return new { documents = docs, instances = instances, visible_solids = total, invalid_bodies = invalid, rebuilt_read_only = rebuild };
    }
    static void Pack(Dictionary<string, object> c, Dictionary<string, object> row)
    {
        Top=(string)c["source_top"]; Root = (string)c["source_root"]; string dest = (string)c["package_root"];
        Need(!Directory.Exists(dest), "Preserve existing destination");
        var before = Identity(Root); row["source_identity"] = before;
        try
        {
            var top = OpenTop(row); row["source_snapshot"] = Snapshot(top, false); Flush("pack/names/" + c["id"]);
            var p = top.Extension.GetPackAndGo(); Need(p != null, "PackAndGo unavailable");
            p.IncludeDrawings = false; p.IncludeSimulationResults = false; p.IncludeSuppressed = true; p.IncludeToolboxComponents = true; p.FlattenToSingleFolder = true;
            object raw; Need(p.GetDocumentNames(out raw), "Cannot enumerate pack sources");
            string[] names = ((Array)raw).Cast<string>().ToArray(); row["pack_sources"] = names;
            foreach (string n in names)
            {
                Need(File.Exists(n), "Missing pack dependency: " + n);
                Need(Within(n, Root) || (n.EndsWith(".step", StringComparison.OrdinalIgnoreCase) && before.ContainsKey(Path.GetFileName(n)) && Hash(n) == before[Path.GetFileName(n)]), "Unexpected external pack dependency: " + n);
            }
            Need(names.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Length, "Duplicate pack basenames");
            Directory.CreateDirectory(dest); Need(p.SetSaveToName(true, dest + Path.DirectorySeparatorChar), "Pack destination rejected");
            object actual, statuses; Need(p.GetDocumentSaveToNames(out actual, out statuses), "Cannot inspect pack targets");
            Need(((Array)actual).Cast<string>().SequenceEqual(names.Select(n => Path.Combine(dest, Path.GetFileName(n))), StringComparer.OrdinalIgnoreCase), "Pack mapping differs");
            Flush("pack/save/" + c["id"]); object result = top.Extension.SavePackAndGo(p); row["pack_statuses"] = result;
            Need(result is Array && ((Array)result).Cast<int>().All(v => v == 0), "PackAndGo copy failed");
            foreach (string n in before.Keys)
            {
                string target = Path.Combine(dest, n);
                if (!File.Exists(target)) { File.Copy(Path.Combine(Root, n), target); }
            }
            var after = Identity(dest); Need(after.Count == before.Count && before.Keys.All(after.ContainsKey), "Package file set differs");
            foreach (string n in before.Keys.Where(n => !n.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))) Need(before[n] == after[n], "Non-native source changed");
            row["package_identity"] = after; row["package_root"] = dest;
        }
        finally { Sw.CloseAllDocuments(true); CheckIdentity(Root, before); row["source_unchanged"] = true; }
    }
    static void Verify(Dictionary<string, object> c, Dictionary<string, object> row)
    {
        Top=(string)c["top"]; Root = (string)c["relocated_root"]; var before = Identity(Root); row["input_identity"] = before;
        try { var top = OpenTop(row); row["snapshot"] = Snapshot(top, true); }
        finally { Sw.CloseAllDocuments(true); CheckIdentity(Root, before); row["relocated_files_unchanged"] = true; }
    }
    private static List<Feature> Features(ModelDoc2 model)
    {
        var rows = new List<Feature>();
        var seen = new HashSet<IntPtr>();
        Func<Feature, IntPtr> identity = f => { IntPtr p = Marshal.GetIUnknownForObject(f); Marshal.Release(p); return p; };
        Action<Feature, int> add = null;
        add = (f, depth) =>
        {
            if (!seen.Add(identity(f))) return;
            Need(depth < 64 && rows.Count < 20000, "Feature graph exceeds diagnostic bounds");
            rows.Add(f);
            var siblings = new HashSet<IntPtr>();
            for (Feature c = f.GetFirstSubFeature() as Feature; c != null; c = c.GetNextSubFeature() as Feature)
            { if (!siblings.Add(identity(c))) break; add(c, depth + 1); }
        };
        var roots = new HashSet<IntPtr>();
        for (Feature f = model.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
        { if (!roots.Add(identity(f))) break; add(f, 0); }
        return rows;
    }

    static object Dims(ModelDoc2 doc) {
        var result=new List<object>();var seen=new HashSet<string>();
        foreach(Feature f in Features(doc)) {
            var ds=new HashSet<IntPtr>();
            for(DisplayDimension dd=f.GetFirstDisplayDimension() as DisplayDimension;dd!=null;dd=f.GetNextDisplayDimension(dd) as DisplayDimension) {
                IntPtr p=Marshal.GetIUnknownForObject(dd);Marshal.Release(p);if(!ds.Add(p))break;
                Need(ds.Count<1000,"Dimension traversal limit");Dimension d=dd.GetDimension2(0);
                if(d!=null&&seen.Add(d.FullName))result.Add(new{name=d.FullName,value_m=d.SystemValue,driven=d.DrivenState,feature=f.Name});
            }
        } return result;
    }
    static object Sketches(ModelDoc2 doc) {
        var result=new List<object>();
        foreach(Feature f in Features(doc).Where(f=>f.GetTypeName2()=="ProfileFeature")) {
            Sketch s=f.GetSpecificFeature2() as Sketch;if(s==null)continue;
            Array pts=s.GetSketchPoints2() as Array;Array seg=s.GetSketchSegments() as Array;
            result.Add(new{name=f.Name,transform=s.ModelToSketchTransform.ArrayData,status=s.GetConstrainedStatus(),
                points=pts==null?null:pts.Cast<SketchPoint>().Select(p=>new{xyz_m=new[]{p.X,p.Y,p.Z},id=p.GetID(),type=p.Type}).ToArray(),
                segments=seg==null?null:seg.Cast<SketchSegment>().Select(a=>new{type=a.GetType(),construction=a.ConstructionGeometry,length_m=a.GetLength()}).ToArray()});
        } return result;
    }
    static object Eqs(ModelDoc2 d) {EquationMgr q=d.GetEquationMgr() as EquationMgr;return Enumerable.Range(0,q.GetCount()).Select(i=>new{index=i,expression=q.Equation[i],value=q.Value[i],status=q.Status}).ToArray();}
    static void Survey(Dictionary<string,object> c,Dictionary<string,object> row) {
        Top=(string)c["top"];Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;
        try {
            ModelDoc2 top=OpenTop(row);row["snapshot"]=Snapshot(top,false);var docs=new List<object>();row["survey_documents"]=docs;
            foreach(string name in new[]{"太阳能雨棚 模型.SLDPRT","防水寄存柜 模型.SLDPRT","左立柱焊件1.SLDPRT","左立柱焊件2.SLDPRT","左立柱焊件3.SLDPRT","左立柱焊件4.SLDPRT","右立柱焊件4.SLDPRT","太阳能雨棚装配.SLDASM","立柱连接架.SLDPRT","箱体雨棚安装架.SLDPRT"}) {
                Flush("survey/"+c["id"]+"/"+name);ModelDoc2 doc=Doc(name);
                docs.Add(new{name=name,configuration=((Configuration)doc.GetActiveConfiguration()).Name,dimensions=Dims(doc),sketches=Sketches(doc),equations=Eqs(doc),
                    features=Features(doc).Select(f=>new{name=f.Name,type=f.GetTypeName2(),suppressed=f.IsSuppressed2(1,null)}).ToArray(),bodies=doc is PartDoc?Bodies(doc):null});
            }
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
    static void RenamePacked(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["source_top"];var before=Identity(Root);
        string oldPath=Path.Combine(Root,Top),newPath=Path.Combine(Root,(string)c["top"]);
        Need(!File.Exists(newPath),"Preserve renamed destination");
        try {
            Flush("rename/open/"+c["id"]);Sw.SetCurrentWorkingDirectory(Root);int e=0,w=0;
            ModelDoc2 top=Sw.OpenDoc6(oldPath,2,1,"",ref e,ref w) as ModelDoc2;
            Need(top!=null&&e==0&&!top.IsOpenedReadOnly(),"Packed rename open failed");LoadedInside();
            ((AssemblyDoc)top).ResolveAllLightWeightComponents(false);LoadedInside();
            Flush("rename/save/"+c["id"]);bool ok=top.Extension.SaveAs(newPath,0,1,null,ref e,ref w);
            row["rename_save"]=new{saved=ok,errors=e,warnings=w,path=top.GetPathName()};
            Need(ok&&e==0&&w==0&&top.GetPathName().Equals(newPath,StringComparison.OrdinalIgnoreCase),"Native top rename failed");
        } finally {Sw.CloseAllDocuments(true);}
        Need(Hash(oldPath)==before[Top],"Previous packed entry unexpectedly changed");
        string preserved=Path.Combine(Path.GetDirectoryName(Root),"preserved_entry_"+c["index"]);
        Directory.CreateDirectory(preserved);File.Move(oldPath,Path.Combine(preserved,Top));
        Top=(string)c["top"];var after=Identity(Root);
        Need(after.Count==before.Count,"Rename file count differs");
        foreach(string n in before.Keys.Where(n=>n!=(string)c["source_top"]))Need(after[n]==before[n],"Rename changed a dependency: "+n);
        row["package_identity"]=after;row["renamed_top"]=Top;
    }


    static ModelDoc2 Doc(string name) {var d=Sw.GetOpenDocumentByName(Path.Combine(Root,name)) as ModelDoc2;Need(d!=null,"Missing repair part "+name);return d;}
    static void Set(ModelDoc2 d,string n,double mm,List<object> changes) {
        Flush("control/"+Path.GetFileName(d.GetPathName())+"/"+n);Dimension dim=d.Parameter(n) as Dimension;Need(dim!=null&&dim.DrivenState==2,"Driving control required: "+n);
        double before=dim.SystemValue;if(Math.Abs(before-mm/1000)<1e-9){changes.Add(new{document=Path.GetFileName(d.GetPathName()),dimension=n,actual_mm=mm,unchanged=true});return;}int result=dim.SetSystemValue3(mm/1000,1,null);
        changes.Add(new{document=Path.GetFileName(d.GetPathName()),dimension=n,before_mm=before*1000,requested_mm=mm,actual_mm=dim.SystemValue*1000,result=result});
        Need(result==0&&Math.Abs(dim.SystemValue-mm/1000)<1e-9,"Control rejected");
    }
    static object Patterns(ModelDoc2 d) {
        var rows=new List<object>();
        foreach(Feature f in Features(d).Where(f=>f.GetTypeName2()=="LPattern")) {
            var p=f.GetDefinition() as LinearPatternFeatureData;Need(p!=null,"Pattern definition unavailable");bool access=false;
            try{access=p.AccessSelections(d,null);Array fs=p.PatternFeatureArray as Array;
                rows.Add(new{name=f.Name,access=access,spacing=p.D1Spacing,count=p.D1TotalInstances,reverse=p.D1ReverseDirection,
                    seed=fs==null?null:fs.Cast<Feature>().Select(g=>new{name=g.Name,type=g.GetTypeName2()}).ToArray()});
            } finally{if(access)p.ReleaseSelectionAccess();}
        }return rows;
    }
    static void Probe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(Convert.ToInt32(c["index"])==25,"Probe is B only");Top=(string)c["top"];Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;
        try {
            ModelDoc2 top=OpenTop(row);var targets=new[]{"箱体侧板加强筋.SLDPRT","箱体左侧板.SLDPRT","箱体防水门框 竖.SLDPRT","箱体竖门框挡边.SLDPRT"};
            row["patterns"]=targets.Select(n=>new{name=n,patterns=Patterns(Doc(n))}).ToArray();Flush("patterns_recorded");
            var controls=new List<object>();row["controls"]=controls;double dh=61,pitch=152+dh/12;
            Set(Doc("顶隔板.SLDPRT"),"D2@草图9",534,controls);
            Set(Doc("箱体侧板加强筋.SLDPRT"),"D1@草图7",150.5+dh/12,controls);
            Set(Doc("箱体侧板加强筋.SLDPRT"),"D3@阵列(线性)1",pitch,controls);
            Set(Doc("箱体竖门框挡边.SLDPRT"),"D3@阵列(线性)2",pitch,controls);
            Set(Doc("箱体左侧板.SLDPRT"),"D3@阵列(线性)*152",pitch,controls);
            Set(Doc("箱体防水门框 竖.SLDPRT"),"D3@阵列(线性)1",pitch,controls);
            Set(Doc("箱体防水门框 竖.SLDPRT"),"D3@阵列(线性)2",pitch,controls);
            foreach(string n in new[]{"顶隔板.SLDPRT","箱体侧板加强筋.SLDPRT","箱体竖门框挡边.SLDPRT","箱体左侧板.SLDPRT","箱体右侧板.SLDPRT","箱体防水门框 竖.SLDPRT"}) {
                ModelDoc2 d=Doc(n);Flush("probe/rebuild/"+n);bool ok=d.ForceRebuild3(false);Need(ok&&d.Extension.GetWhatsWrongCount()==0,"Target rebuild failed: "+n);
            }
            Flush("probe/rebuild_top");Need(top.ForceRebuild3(false),"Probe top rebuild failed");row["snapshot"]=Snapshot(top,false);
            string path=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_B.step");Need(!File.Exists(path),"Preserve export");
            int e=0,w=0;Flush("probe/export");bool export=top.Extension.SaveAs(path,0,3,null,ref e,ref w);
            row["export"]=new{path=path,saved=export,errors=e,warnings=w};Need(export&&e==0&&w==0,"Probe export failed");
            row["post_documents"]=targets.Select(n=>new{name=n,dimensions=Dims(Doc(n)),sketches=Sketches(Doc(n))}).ToArray();
            row["native_save_performed"]=false;
        } finally {Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }


    static object Sizes() {return new[]{"箱体左侧板.SLDPRT","箱体内顶板.SLDPRT","箱体顶板.SLDPRT","箱体底板.SLDPRT","防水柜门 模型.SLDPRT","防水柜门1╱12L.SLDPRT","防水柜门2╱12L.SLDPRT","防水柜门3╱12L.SLDPRT","立柱连接架.SLDPRT","雨棚LED灯罩壳.SLDPRT","太阳能板(1140×840).SLDPRT","遮阳板.SLDPRT"}.Select(n=>new{name=n,box_m=((PartDoc)Doc(n)).GetPartBox(true)}).ToArray();}
    static void ExportCurrent(ModelDoc2 top,Dictionary<string,object> row,string label) {
        string path=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_"+label+".step");Need(!File.Exists(path),"Preserve export");
        int e=0,w=0;Flush("export/"+label);bool ok=top.Extension.SaveAs(path,0,3,null,ref e,ref w);
        row["export"]=new{path=path,saved=ok,errors=e,warnings=w,sha256=File.Exists(path)?Hash(path):null};Need(ok&&e==0&&w==0,"STEP export failed");
    }
    static void SaveDocs(ModelDoc2 top,Dictionary<string,object> row) {
        var docs=new List<ModelDoc2>();for(ModelDoc2 d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2)docs.Add(d);
        var saves=new List<object>();row["saved_documents"]=saves;
        foreach(ModelDoc2 d in docs.OrderBy(d=>d.GetPathName()==top.GetPathName()?2:d is AssemblyDoc?1:0)) {
            if(!d.GetSaveFlag())continue;string n=Path.GetFileName(d.GetPathName());
            if(new[]{"太阳能板(1140×840).SLDPRT","门轴销.SLDPRT","门轴销(短).SLDPRT","电控锁ZJA-S500.SLDPRT","LK-4-6-V2.0 24口锁控板.SLDPRT"}.Contains(n,StringComparer.OrdinalIgnoreCase))continue;
            Need(!d.IsOpenedReadOnly()&&Within(d.GetPathName(),Root),"Refuse save outside writable copy");
            Flush("persist/save/"+n);int e=0,w=0;bool ok=d.Save3(1,ref e,ref w);saves.Add(new{name=n,saved=ok,errors=e,warnings=w});Need(ok&&e==0&&w==0,"Native save failed "+n);
        }
    }
    static void Persist(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(Writable&&Convert.ToInt32(c["index"])==25,"Right pitch repair is B only");
        Top=(string)c["top"];Root=(string)c["package_root"];row["input_identity"]=Identity(Root);
        try {
            ModelDoc2 top=OpenTop(row);var right=Doc("箱体右侧板.SLDPRT");
            row["patterns_before"]=Patterns(right);
            Dimension dim=right.Parameter("D3@阵列(线性)2") as Dimension;
            Need(dim!=null&&Math.Abs(dim.SystemValue-0.152)<1e-9,"Expected original right pitch");
            var controls=new List<object>();row["controls"]=controls;
            Set(right,"D3@阵列(线性)2",152.0+61.0/12.0,controls);
            Need(right.ForceRebuild3(false)&&right.Extension.GetWhatsWrongCount()==0,"Right rebuild failed");
            Need(top.ForceRebuild3(false),"Top rebuild failed");row["full_rebuild_return"]=true;
            row["snapshot_before_save"]=Snapshot(top,false);SaveDocs(top,row);
            row["snapshot"]=Snapshot(top,false);row["patterns_after"]=Patterns(right);
            row["part_sizes"]=Sizes();row["native_save_performed"]=true;
        } finally {Sw.CloseAllDocuments(true);row["final_identity"]=Identity(Root);}
    }
    static object VerifiedControls(Dictionary<string,object> c) {
        var rows=new List<object>();double h=Convert.ToInt32(((System.Collections.IList)c["dimensions_mm"])[0]);
        double depth=Convert.ToInt32(((System.Collections.IList)c["dimensions_mm"])[2]);double pitch=152+(h-1939)/12;
        string[][] keys=new[]{
            new[]{"顶隔板.SLDPRT","D2@草图9"},new[]{"箱体侧板加强筋.SLDPRT","D1@草图7"},
            new[]{"箱体侧板加强筋.SLDPRT","D3@阵列(线性)1"},new[]{"箱体竖门框挡边.SLDPRT","D3@阵列(线性)2"},
            new[]{"箱体左侧板.SLDPRT","D3@阵列(线性)*152"},new[]{"箱体防水门框 竖.SLDPRT","D3@阵列(线性)1"},
            new[]{"箱体防水门框 竖.SLDPRT","D3@阵列(线性)2"},new[]{"箱体右侧板.SLDPRT","D3@阵列(线性)2"}};
        for(int i=0;i<keys.Length;i++) {
            Dimension d=Doc(keys[i][0]).Parameter(keys[i][1]) as Dimension;Need(d!=null,"Missing saved control");
            double expected=i==0?544+depth-550:i==1?150.5+(h-1939)/12:pitch;
            Need(Math.Abs(d.SystemValue*1000-expected)<1e-6,"Saved feedback control differs");
            rows.Add(new{document=keys[i][0],dimension=keys[i][1],actual_mm=d.SystemValue*1000,expected_mm=expected});
        }return rows;
    }
    static void VerifyExport(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(!Writable,"Verify must be read only");Top=(string)c["top"];Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;
        try {
            var top=OpenTop(row);Flush("verify/rebuild/"+c["id"]);row["full_rebuild_return"]=top.ForceRebuild3(false);Need(Convert.ToBoolean(row["full_rebuild_return"]),"Independent rebuild failed");
            row["snapshot"]=Snapshot(top,false);row["part_sizes"]=Sizes();row["verified_controls"]=VerifiedControls(c);row["right_patterns"]=Patterns(Doc("箱体右侧板.SLDPRT"));ExportCurrent(top,row,(string)c["id"]);
        } finally {Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }


    static object Bodies(ModelDoc2 d) {
        Array raw=((PartDoc)d).GetBodies2(0,false) as Array;Need(raw!=null&&raw.Length>0,"No solid bodies");
        return raw.Cast<Body2>().Select(b=>new{name=b.Name,check=b.Check2(),bounds_m=b.GetBodyBox(),mass=b.GetMassProperties(1),faces=b.GetFaceCount(),edges=b.GetEdgeCount()}).ToArray();
    }
    static void RecordFlatHealth(ModelDoc2 d,Dictionary<string,object> part,string stage) {
        var health=new List<object>();int active=0;
        foreach(Feature f in Features(d)){bool warning;int error=f.GetErrorCode2(out warning);bool[] suppressed=f.IsSuppressed2(1,null) as bool[];
            health.Add(new{name=f.Name,type=f.GetTypeName2(),error=error,warning=warning,suppressed=suppressed});
            if(suppressed!=null&&suppressed.Length==1&&!suppressed[0]&&(error!=0||warning))active++;}
        part[stage+"_feature_health"]=health;part[stage+"_active_feature_warning_count"]=active;Flush("flat/health/"+stage+"/"+Path.GetFileName(d.GetPathName()));
        Need(active==0,"Frame has an active feature error or warning at "+stage);
    }
    static void FlatVerify(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(!Writable,"Mount flat check must be read only");Top=(string)c["top"];Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;
        var checks=new List<object>();row["part_checks"]=checks;
        try {
            var targets=new List<string[]>{new[]{"雨棚LED灯罩壳.SLDPRT","Default"}};
            foreach(string[] target in targets) {string name=target[0],config=target[1];
                Sw.SetCurrentWorkingDirectory(Root);int e=0,w=0;Flush("flat/open/"+name);
                var d=Sw.OpenDoc6(Path.Combine(Root,name),1,3,"",ref e,ref w) as ModelDoc2;Need(d!=null&&e==0&&d.IsOpenedReadOnly(),"Flat read-only open failed");LoadedInside();
                int ae=0;Need(Sw.ActivateDoc3(d.GetTitle(),false,0,ref ae)!=null,"Flat activate failed");
                if(((Configuration)d.GetActiveConfiguration()).Name!=config)Need(d.ShowConfiguration2(config),"Requested flat configuration missing: "+name+"/"+config);Need(((Configuration)d.GetActiveConfiguration()).Name==config,"Unexpected actual flat configuration: "+name+"/"+config);
                Need(d.ForceRebuild3(false)&&d.Extension.GetWhatsWrongCount()==0,"Folded body rebuild failed");
                var part=new Dictionary<string,object>{{"document",name},{"configuration",config},{"folded_bodies",Bodies(d)}};checks.Add(part);string materialDatabase="",materialName=((PartDoc)d).GetMaterialPropertyName2(config,out materialDatabase);part["saved_material"]=new{database=materialDatabase,name=materialName};RecordFlatHealth(d,part,"folded");part["bend_snapshot"]=TrBendSnapshot(d);
                var flats=Features(d).Where(f=>f.GetTypeName2()=="FlatPattern").ToArray();part["native_flat_features"]=flats.Select(f=>f.Name).ToArray();
                if(flats.Length==0){part["status"]="NO_NATIVE_FLAT_FEATURE";Need(false,"Affected sheet part has no flat pattern: "+name);}
                Need(flats.Length==1,"Ambiguous flat-pattern coverage");Feature flat=flats[0];
                bool[] raw=flat.IsSuppressed2(1,null) as bool[];Need(raw!=null&&raw.Length==1&&raw[0],"Folded input flat suppression unproven");
                Flush("flat/unfold/"+name);Need(flat.SetSuppression2(1,1,null)&&d.ForceRebuild3(false)&&d.Extension.GetWhatsWrongCount()==0,"Native unfold failed");part["unfolded_bodies"]=Bodies(d);RecordFlatHealth(d,part,"unfolded");
                string path=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_"+(string)c["id"]+"_"+Path.GetFileNameWithoutExtension(name)+"_"+config+".step");Need(!File.Exists(path),"Preserve flat export");
                bool ok=d.Extension.SaveAs(path,0,3,null,ref e,ref w);Need(ok&&e==0&&w==0,"Flat export failed");part["flat_export"]=new{path=path,sha256=Hash(path)};
                Flush("flat/refold/"+name);Need(flat.SetSuppression2(0,1,null)&&d.ForceRebuild3(false)&&d.Extension.GetWhatsWrongCount()==0,"Native refold failed");part["refolded_bodies"]=Bodies(d);RecordFlatHealth(d,part,"refolded");TrExport(d,"refold_"+Path.GetFileNameWithoutExtension(name)+"_"+config,part);part["status"]="PASS";
                Sw.CloseAllDocuments(true);
            }
        } finally {Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }


    static void Mount(Dictionary<string,object> c, Dictionary<string,object> row, bool persist) {
        Need(Writable,"Writable isolated candidate required for mount probe");
        Top=(string)c["top"];Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;
        try {
            ModelDoc2 top=OpenTop(row);row["snapshot_before_mount"]=Snapshot(top,false);
            ApplyTransitionMount(c,row,top);
            Flush("mount/rebuild/"+c["id"]);Need(top.ForceRebuild3(false),"Mount rebuild failed");
            row["snapshot_after_mount"]=Snapshot(top,false);row["verified_controls"]=VerifiedControls(c);
            row["expected_visible_solids"]=ExpectedVisibleSolids;
            if(persist) {SaveDocs(top,row);row["snapshot"]=Snapshot(top,false);row["native_save_performed"]=true;}
            else {ExportCurrent(top,row,(string)c["id"]);row["snapshot"]=row["snapshot_after_mount"];row["native_save_performed"]=false;}
        } finally {Sw.CloseAllDocuments(true);if(!persist){CheckIdentity(Root,before);row["files_unchanged"]=true;}row["final_identity"]=Identity(Root);}
    }

    [STAThread]
    static int LegacyMain(string[] args)
    {
        string contractPath = Path.GetFullPath(args[0]), mode = args[1]; EvidencePath = Path.GetFullPath(args[2]);
        Need(mode == "shade_survey" || mode == "shade_persist" || mode == "shade_verify" || mode == "mount_probe" || mode == "mount_persist" || mode == "pack" || mode == "verify" || mode == "survey" || mode == "probe" || mode == "persist" || mode == "verify_export" || mode == "flat_verify", "Unsupported operation"); Need(!File.Exists(EvidencePath), "Preserve evidence");
        Writable=mode=="shade_persist"||mode=="persist"||mode=="mount_probe"||mode=="mount_persist"; var contract = Read(contractPath); Need((string)contract["scope"] == "22057_REFERENCE_MOUNT_CORRECTION_20261016_R18", "Wrong scope");
        var selected = args[3].Split(',').Select(int.Parse).ToArray(); var rows = new List<object>();
        E = new Dictionary<string, object> { {"status", "starting"}, {"mode", mode}, {"contract_sha256", Hash(contractPath)}, {"started_utc", DateTime.UtcNow.ToString("o")}, {"cases", rows} }; bool owned = false;
        try
        {
            Need(Process.GetProcessesByName("SLDWORKS").Count(p => !p.HasExited) == 0, "CAD busy"); DateTime activation = DateTime.UtcNow;
            Sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks;
            var proc = Process.GetProcessById(Sw.GetProcessID()); var exe = new StringBuilder(4096);
            Need(GetLongPathName(proc.MainModule.FileName, exe, (uint)exe.Capacity) > 0 && exe.ToString().Equals(@"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe", StringComparison.OrdinalIgnoreCase)
                && proc.StartTime.ToUniversalTime() >= activation.AddSeconds(-2) && Process.GetProcessesByName("SLDWORKS").Length == 1 && Sw.GetFirstDocument() == null, "Exclusive fresh CAD identity unproven");
            owned = true; E["process_id"] = proc.Id; E["process_start_utc"] = proc.StartTime.ToUniversalTime().ToString("o"); E["revision"] = Sw.RevisionNumber();
            Sw.Visible = false; Sw.CommandInProgress = true; Flush("start");
            foreach (var raw in (System.Collections.IEnumerable)contract["cases"])
            {
                var c = (Dictionary<string, object>)raw; if (!selected.Contains(Convert.ToInt32(c["index"]))) continue;
                ExpectedVisibleSolids=c.ContainsKey("expected_visible_solids")?Convert.ToInt32(c["expected_visible_solids"]):431;
                var row = new Dictionary<string, object> { {"index", c["index"]}, {"id", c["id"]}, {"status", "starting"} }; rows.Add(row); Flush(mode + "/open/" + c["id"]);
                if(mode=="shade_survey") ShSurvey(c,row); else if(mode=="shade_persist") LedPersist(c,row); else if(mode=="shade_verify") LedVerify(c,row); else if(mode=="mount_probe") TrLocalProbe(c,row); else if(mode=="mount_persist") TrAssemblyPersist(c,row); else if (mode == "pack") { Pack(c, row); if((string)c["source_top"]!=(string)c["top"]) RenamePacked(c,row); } else if(mode=="flat_verify") FlatVerify(c,row); else if(mode=="persist") Persist(c,row); else if(mode=="verify_export") VerifyExport(c,row); else if(mode=="probe") Probe(c,row); else if(mode=="survey") Survey(c,row); else Verify(c, row);
                row["status"] = "PASS"; Flush(mode + "/complete/" + c["id"]);
            }
            Need(rows.Count == selected.Length, "Case selection incomplete"); E["status"] = mode + "_finished"; return 0;
        }
        catch (Exception ex) { E["status"] = "failed"; E["error"] = ex.ToString(); return 1; }
        finally
        {
            if (owned) { Sw.CloseAllDocuments(true); Sw.CommandInProgress = false; Sw.ExitApp(); E["exit_app_called"] = true; }
            E["finished_utc"] = DateTime.UtcNow.ToString("o"); Flush("finished");
        }
    }
}
