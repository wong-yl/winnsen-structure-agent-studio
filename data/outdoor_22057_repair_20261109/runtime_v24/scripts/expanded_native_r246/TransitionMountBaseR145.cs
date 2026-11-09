// A-only local native sheet-metal feasibility probe. No native Save call here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    const double TrFootTopQ=.019, TrReliefLow=.036, TrReliefHigh=.068, TrJogLine=.064;
    static double[] TrPoint(double x,double q,double p){return new[]{x,RmCos*q+RmSin*p,-RmSin*q+RmCos*p};}
    static Face2 TrMainFace(ModelDoc2 d,double x)
    {
        return ((Array)RmBodies(d).Single().GetFaces()).Cast<Face2>().Where(f=>{
            var s=(Surface)f.GetSurface();if(!s.IsPlane())return false;var plane=(double[])s.PlaneParams;
            return Math.Abs(Math.Abs(plane[0])-1)<1e-7&&Math.Abs(plane[3]-x)<1e-7;
        }).OrderByDescending(f=>f.GetArea()).First();
    }
    static double[] TrSpan(Face2 face,double q)
    {
        var values=new List<double>();
        foreach(Edge edge in (Array)face.GetEdges()){
            var va=edge.GetStartVertex() as Vertex;var vb=edge.GetEndVertex() as Vertex;if(va==null||vb==null)continue;
            var a=(double[])va.GetPoint();var b=(double[])vb.GetPoint();double qa=RmDot(a,RmNormal),qb=RmDot(b,RmNormal);
            if(q<Math.Min(qa,qb)-1e-8||q>Math.Max(qa,qb)+1e-8||Math.Abs(qb-qa)<1e-9)continue;
            double t=(q-qa)/(qb-qa);values.Add(RmDot(a.Zip(b,(v,w)=>v+(w-v)*t).ToArray(),RmDepth));
        }
        Need(values.Count>=2,"Main web section must intersect at least two exact edges");return new[]{values.Min(),values.Max()};
    }
    static Feature TrNewFeature(ModelDoc2 d,HashSet<string> old,string label)
    {
        var created=Features(d).Where(f=>!old.Contains(f.Name)).ToArray();
        var root=created.FirstOrDefault(f=>f.GetTypeName2()!="ProfileFeature"&&f.GetTypeName2()!="UiBend");
        Need(root!=null,"Native operation did not create a feature: "+label);return root;
    }
    static void TrExport(ModelDoc2 d,string label,Dictionary<string,object> record)
    {
        string path=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_"+label+".step");
        Need(!File.Exists(path),"Preserve transition export");int e=0,w=0;bool ok=d.Extension.SaveAs(path,0,3,null,ref e,ref w);
        record[label+"_export"]=new{path=path,sha256=File.Exists(path)?Hash(path):null,saved=ok,errors=e,warnings=w};Flush("transition/export/"+label);Need(ok&&e==0&&w==0,"Transition export failed");
    }
    static Feature TrSketch(ModelDoc2 d,Face2 face,List<double[][]> polygons,string name)
    {
        d.ClearSelection2(true);Need(((Entity)face).Select4(false,null),"Cannot select transition sketch plane");d.SketchManager.InsertSketch(true);
        Sketch sketch=d.GetActiveSketch2() as Sketch;Need(sketch!=null,"Transition sketch not active");var segs=new List<SketchSegment>();bool previous=d.SketchManager.AddToDB;
        try{d.SketchManager.AddToDB=true;foreach(var polygon in polygons)for(int i=0;i<polygon.Length-1;i++){
            var a=RmPoint(polygon[i],sketch.ModelToSketchTransform);var b=RmPoint(polygon[i+1],sketch.ModelToSketchTransform);
            Need(Math.Abs(a[2])+Math.Abs(b[2])<1e-7,"Transition sketch points are off plane");
            var segment=d.SketchManager.CreateLine(a[0],a[1],0,b[0],b[1],0);Need(segment!=null,"Cannot create transition sketch segment");segs.Add(segment);
        }}finally{d.SketchManager.AddToDB=previous;}
        foreach(SketchPoint point in (Array)sketch.GetSketchPoints2()){d.ClearSelection2(true);Need(point.Select4(false,null),"Cannot fix transition sketch endpoint");d.SketchAddConstraints("sgFIXED");}
        RmEvidence[name+"_sketch_status"]=sketch.GetConstrainedStatus();RmEvidence[name+"_sketch_points"]=((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(point=>new{xyz_m=new[]{point.X,point.Y,point.Z},status=point.Status}).ToArray();Flush("transition/fixed_endpoints/"+name);
        Need(sketch.GetConstrainedStatus()==3,"Transition sketch must be fully constrained");var feature=RmSketchFeature(d,sketch);d.SketchManager.InsertSketch(true);feature.Name=name;return feature;
    }
    static object TrHealth(ModelDoc2 d)
    {
        return Features(d).Select(f=>{bool warning;int error=f.GetErrorCode2(out warning);return new{name=f.Name,type=f.GetTypeName2(),error=error,warning=warning,suppressed=f.IsSuppressed2(1,null)};}).ToArray();
    }
    static bool TrCycle(ModelDoc2 d,string label,Dictionary<string,object> record)
    {
        var flat=Features(d).Single(f=>f.GetTypeName2()=="FlatPattern");
        var cycle=new Dictionary<string,object>();record[label]=cycle;
        Need(flat.SetSuppression2(1,1,null),"Cannot activate diagnostic flat pattern");
        bool rebuilt=d.ForceRebuild3(false);cycle["flat_rebuild"]=rebuilt;cycle["flat_issues"]=d.Extension.GetWhatsWrongCount();cycle["flat_health"]=TrHealth(d);cycle["flat_bend_snapshot"]=TrBendSnapshot(d);cycle["flat_bodies"]=Bodies(d);if(rebuilt&&d.Extension.GetWhatsWrongCount()==0)TrExport(d,label+"_flat",cycle);else cycle["flat_export_skipped"]="native_flat_error";
        bool clean=rebuilt&&d.Extension.GetWhatsWrongCount()==0&&Features(d).All(f=>{bool warn;return f.GetErrorCode2(out warn)==0&&!warn;});
        cycle["flat_clean"]=clean;Need(flat.SetSuppression2(0,1,null),"Cannot restore diagnostic folded state");
        cycle["refold_rebuild"]=d.ForceRebuild3(false);cycle["refold_health"]=TrHealth(d);cycle["refold_bodies"]=Bodies(d);TrExport(d,label+"_refold",cycle);Flush("transition/cycle/"+label);return clean;
    }
    static void TrResetFlatFixedFace(ModelDoc2 d,Dictionary<string,object> record)
    {
        var flat=Features(d).Single(f=>f.GetTypeName2()=="FlatPattern");var data=flat.GetDefinition() as FlatPatternFeatureData;
        Need(data!=null&&data.AccessSelections(d,null),"Cannot access diagnostic flat definition");bool submitted=false;
        try{var face=TrMainFace(d,-.440);data.FixedFace2=face;submitted=flat.ModifyDefinition(data,d,null);Need(submitted,"Cannot select retained upper main web for flat pattern");}
        finally{if(!submitted)data.ReleaseSelectionAccess();}
        record["flat_fixed_face_resubmission"]=new{modify_return=submitted,rebuild_return=d.ForceRebuild3(false),health=TrHealth(d)};Flush("transition/flat_fixed_face_resubmitted");
    }

    static object TrRead(Func<object> read){try{return read();}catch(Exception ex){return new{error=ex.Message,hresult=ex.HResult};}}
    static object TrBendSnapshot(ModelDoc2 d)
    {
        return Features(d).Where(f=>new[]{"OneBend","UiBend","DerivedBend","FlatPattern","SheetMetal","FlattenBends","ProcessBends"}.Contains(f.GetTypeName2())).Select(f=>{
            var row=new Dictionary<string,object>{{"name",f.Name},{"type",f.GetTypeName2()}};
            row["parents"]=TrRead(()=>{var a=f.GetParents() as Array;return a==null?null:a.Cast<Feature>().Select(p=>new{name=p.Name,type=p.GetTypeName2()}).ToArray();});
            row["children"]=TrRead(()=>{var a=f.GetChildren() as Array;return a==null?null:a.Cast<Feature>().Select(p=>new{name=p.Name,type=p.GetTypeName2()}).ToArray();});
            row["definition"]=TrRead(()=>{object raw=f.GetDefinition();var one=raw as OneBendFeatureData;var sheet=raw as SheetMetalFeatureData;
                if(one!=null)return (object)new{radius=one.BendRadius,allowance_type=one.BendAllowanceType,k_factor=one.KFactor,allowance=one.BendAllowance,angle=one.BendAngle,order=one.BendOrder,direction=one.BendDirection,down=one.BendDown,custom_type=one.GetCustomBendAllowance().Type,custom_deduction=one.GetCustomBendAllowance().BendDeduction,custom_k_factor=one.GetCustomBendAllowance().KFactor,default_radius=one.UseDefaultBendRadius,default_allowance=one.UseDefaultBendAllowance};
                if(sheet!=null)return (object)new{thickness=sheet.Thickness,radius=sheet.BendRadius,allowance_type=sheet.BendAllowanceType,k_factor=sheet.KFactor,allowance=sheet.BendAllowance};
                return new{one_bend=one!=null,flat=raw is FlatPatternFeatureData,bends=raw is BendsFeatureData};});return row;
        }).ToArray();
    }
    static bool TrRegenerateFlat(ModelDoc2 d,Dictionary<string,object> record)
    {
        var probe=new Dictionary<string,object>();record["flat_regeneration"]=probe;
        var protectedFeatures=Features(d).Where(f=>!new[]{"FlatPattern","UiBend","ProfileFeature","FoldUpDownSketchFolder"}.Contains(f.GetTypeName2())).Select(f=>new{name=f.Name,type=f.GetTypeName2()}).ToArray();
        var flat=Features(d).Single(f=>f.GetTypeName2()=="FlatPattern");d.ClearSelection2(true);Need(flat.Select2(false,0),"Cannot select diagnostic terminal flat");
        probe["delete_return"]=d.Extension.DeleteSelection2(2);Need(Convert.ToBoolean(probe["delete_return"]),"Cannot remove diagnostic terminal flat");
        Need(protectedFeatures.All(p=>Features(d).Any(f=>f.Name==p.name&&f.GetTypeName2()==p.type)),"Diagnostic terminal deletion removed an upstream feature");
        probe["rebuild_after_delete"]=d.ForceRebuild3(false);probe["set_bend_flat_return"]=d.SetBendState(2);probe["rebuild_after_flat"]=d.ForceRebuild3(false);
        probe["flat_count"]=Features(d).Count(f=>f.GetTypeName2()=="FlatPattern");probe["health"]=TrHealth(d);probe["bend_snapshot"]=TrBendSnapshot(d);Flush("transition/regenerated_flat_diagnostic");
        if(Convert.ToInt32(probe["flat_count"])!=1)return false;
        probe["set_bend_folded_return"]=d.SetBendState(3);Need(Features(d).Single(f=>f.GetTypeName2()=="FlatPattern").SetSuppression2(0,1,null),"Cannot restore regenerated folded state");
        probe["refold_rebuild"]=d.ForceRebuild3(false);return TrCycle(d,"regenerated_terminal",record);
    }


    static void TrRecognizeFoldedBody(ModelDoc2 original,Dictionary<string,object> record)
    {
        var probe=new Dictionary<string,object>();record["body_recognition"]=probe;
        var body=RmBodies(original).Single().Copy() as Body2;Need(body!=null,"Cannot copy corrected folded solid");
        probe["input_body"]=Bodies(original);probe["input_sheet_flag"]=body.IsSheetMetal();
        var d=Sw.NewDocument(@"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2020\templates\gb_part.prtdot",0,0,0) as ModelDoc2;
        Need(d!=null,"Cannot create isolated in-memory recognition part");RmActivate(d,"isolated_body_recognition");
        var imported=((PartDoc)d).CreateFeatureFromBody3(body,false,0) as Feature;Need(imported!=null,"Cannot insert corrected folded solid");
        imported.Name="局部过渡_正确折叠实体";probe["inserted_body"]=Bodies(d);TrExport(d,"recognition_before_bends",probe);
        d.ClearSelection2(true);Need(((Entity)TrMainFace(d,-.440)).Select4(false,null),"Cannot select retained upper web for bend recognition");
        probe["insert_bends_return"]=((PartDoc)d).InsertBends2(.0002,"",.3333,-1,false,.5,true);
        probe["rebuild"]=d.ForceRebuild3(false);probe["health"]=TrHealth(d);probe["bend_snapshot"]=TrBendSnapshot(d);probe["recognized_bodies"]=Bodies(d);Flush("transition/body_recognition_result");
        Need(Convert.ToBoolean(probe["insert_bends_return"]),"Corrected folded solid bend recognition failed");
        TrExport(d,"recognition_folded",probe);probe["flat_clean"]=TrCycle(d,"recognition",probe);
        Need(Convert.ToBoolean(probe["flat_clean"]),"Recognized corrected folded solid still fails native unfold");
        var allowanceEdits=new List<object>();probe["allowance_edits"]=allowanceEdits;var originalBends=Features(d).Where(f=>f.GetDefinition() is OneBendFeatureData).ToArray();
        probe["recognized_bend_count"]=originalBends.Length;
        foreach(var f in originalBends){var b=f.GetDefinition() as OneBendFeatureData;if(Math.Abs(b.BendAngle-Math.PI/2)>1e-7)continue;
            bool access=b.AccessSelections(d,null);b.UseDefaultBendAllowance=false;var allowance=d.FeatureManager.CreateCustomBendAllowance();allowance.Type=4;allowance.BendDeduction=.0025;b.SetCustomBendAllowance(allowance);bool changed=f.ModifyDefinition(b,d,null);allowanceEdits.Add(new{name=f.Name,access_selections=access,modify_return=changed});Flush("transition/recognized_allowance_edit");Need(changed,"Cannot set original 90-degree bend deduction");}
        probe["retained_compensation_rebuild"]=d.ForceRebuild3(false);probe["retained_compensation_bends"]=TrBendSnapshot(d);probe["retained_compensation_flat_clean"]=TrCycle(d,"recognition_retained_deduction",probe);
        Need(Convert.ToBoolean(probe["retained_compensation_flat_clean"]),"Recognized body fails with retained 90-degree deduction");

        string native=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_rear_local_candidate.SLDPRT");Need(!File.Exists(native),"Preserve prior local native candidate");
        int errors=0,warnings=0;bool saved=d.Extension.SaveAs(native,0,1,null,ref errors,ref warnings);probe["native_candidate"]=new{path=native,saved=saved,errors=errors,warnings=warnings,sha256=File.Exists(native)?Hash(native):null};Flush("transition/local_native_candidate_saved");Need(saved&&errors==0&&warnings==0,"Cannot save independent local native candidate");
        Sw.CloseDoc(d.GetTitle());int openErrors=0,openWarnings=0;d=Sw.OpenDoc6(native,1,3,"",ref openErrors,ref openWarnings) as ModelDoc2;Need(d!=null&&openErrors==0&&d.IsOpenedReadOnly(),"Cannot independently reopen local native candidate");
        probe["reopened"]=new{errors=openErrors,warnings=openWarnings,rebuild=d.ForceRebuild3(false),bodies=Bodies(d),health=TrHealth(d),bend_snapshot=TrBendSnapshot(d)};
        TrExport(d,"saved_candidate_reopened_folded",probe);probe["reopened_flat_clean"]=TrCycle(d,"saved_candidate_reopened",probe);Need(Convert.ToBoolean(probe["reopened_flat_clean"]),"Saved local native candidate unfold failed");

    }


    static void TrLocalProbe(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Need(Convert.ToInt32(c["index"])==13&&Writable,"Only isolated A support mirror candidates authorized");Root=(string)c["package_root"];var before=Identity(Root);row["input_identity"]=before;
        var record=new Dictionary<string,object>{{"case_id",c["id"]}};row["local_transition"]=record;RmEvidence=record;var pieces=new List<object>();record["right_pieces"]=pieces;
        try{
            foreach(bool rear in new[]{true,false}){
                string role=rear?"rear":"front",index=rear?"3":"2";
                string evidence=Path.Combine(Path.GetDirectoryName(EvidencePath),rear?"feedback_cad_mount_probe_r74_rear_r15_native_candidate.json":"feedback_cad_mount_probe_r75_front_r15_native_candidate.json");
                var prior=Read(evidence);Need((string)prior["status"]=="mount_probe_finished","Accepted left support native evidence required");
                var first=(Dictionary<string,object>)((System.Collections.IList)prior["cases"])[0];var local=(Dictionary<string,object>)first["local_transition"];var recognition=(Dictionary<string,object>)local["body_recognition"];var native=(Dictionary<string,object>)recognition["native_candidate"];
                string path=(string)native["path"];Need(Hash(path)==(string)native["sha256"],"Accepted left support changed");
                var part=new Dictionary<string,object>{{"role",role},{"left_native",path},{"left_sha256",Hash(path)},{"left_evidence_sha256",Hash(evidence)}};pieces.Add(part);
                int errors=0,warnings=0;var original=Sw.OpenDoc6(Path.Combine(Root,"右立柱焊件"+index+".SLDPRT"),1,3,"",ref errors,ref warnings) as ModelDoc2;Need(original!=null&&errors==0,"Cannot read protected right baseline");RmActivate(original,"right_baseline_"+role);TrExport(original,role+"_right_shifted_baseline",part);Sw.CloseAllDocuments(true);
                errors=0;warnings=0;var left=Sw.OpenDoc6(path,1,3,"",ref errors,ref warnings) as ModelDoc2;Need(left!=null&&errors==0&&left.IsOpenedReadOnly(),"Cannot read accepted left native candidate");RmActivate(left,"mirror_left_"+role);
                var body=RmBodies(left).Single().Copy() as Body2;Need(body!=null,"Cannot copy left support for right mirror");
                var utility=(MathUtility)Sw.GetMathUtility();var mirror=utility.CreateTransform(new double[]{-1,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0}) as MathTransform;Need(mirror!=null,"Cannot form exact X reflection");part["mirror_transform"]=mirror.ArrayData;Need(body.ApplyTransform(mirror),"Cannot mirror corrected left support body");
                var box=(double[])body.GetBodyBox();Need(box[0]>.4149&&box[3]<.4601,"Mirrored body is not at expected right-side position");part["mirrored_bounds_m"]=box;
                var d=Sw.NewDocument(@"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2020\templates\gb_part.prtdot",0,0,0) as ModelDoc2;Need(d!=null,"Cannot create isolated right native part");RmActivate(d,"right_recognition_"+role);
                var imported=((PartDoc)d).CreateFeatureFromBody3(body,false,0) as Feature;Need(imported!=null,"Cannot insert mirrored support");imported.Name="右支撑_25mm局部过渡实体";TrExport(d,role+"_right_before_recognition",part);
                d.ClearSelection2(true);Need(((Entity)TrMainFace(d,.440)).Select4(false,null),"Cannot select retained right upper web");Need(((PartDoc)d).InsertBends2(.0002,"",.3333,-1,false,.5,true),"Cannot recognize mirrored support bends");Need(d.ForceRebuild3(false),"Cannot rebuild mirrored support recognition");
                foreach(var f in Features(d).Where(f=>f.GetDefinition() is OneBendFeatureData).ToArray()){var b=f.GetDefinition() as OneBendFeatureData;if(Math.Abs(b.BendAngle-Math.PI/2)>1e-7)continue;
                    b.UseDefaultBendAllowance=false;var allowance=d.FeatureManager.CreateCustomBendAllowance();allowance.Type=4;allowance.BendDeduction=.0025;b.SetCustomBendAllowance(allowance);Need(f.ModifyDefinition(b,d,null),"Cannot retain right original 90-degree deduction");}
                Need(d.ForceRebuild3(false),"Cannot rebuild right retained allowances");part["folded_bodies"]=Bodies(d);part["bend_snapshot"]=TrBendSnapshot(d);TrExport(d,role+"_right_folded",part);Need(TrCycle(d,role+"_right",part),"Right native unfold failed");
                string output=Path.Combine(Path.GetDirectoryName(EvidencePath),Path.GetFileNameWithoutExtension(EvidencePath)+"_right_"+role+"_local_candidate.SLDPRT");Need(!File.Exists(output),"Preserve earlier right local candidate");errors=0;warnings=0;bool saved=d.Extension.SaveAs(output,0,1,null,ref errors,ref warnings);Need(saved&&errors==0&&warnings==0,"Cannot save right local candidate");part["native_candidate"]=new{path=output,sha256=Hash(output),errors=errors,warnings=warnings};
                Sw.CloseAllDocuments(true);errors=0;warnings=0;d=Sw.OpenDoc6(output,1,3,"",ref errors,ref warnings) as ModelDoc2;Need(d!=null&&errors==0&&d.IsOpenedReadOnly(),"Cannot reopen saved right native candidate");RmActivate(d,"right_reopen_"+role);Need(d.ForceRebuild3(false),"Cannot rebuild reopened right candidate");TrExport(d,role+"_right_reopened_folded",part);part["reopened_flat_clean"]=TrCycle(d,role+"_right_reopened",part);Need(Convert.ToBoolean(part["reopened_flat_clean"]),"Reopened right native unfold failed");
                Sw.CloseAllDocuments(true);Need(Hash(path)==(string)native["sha256"],"Protected left support candidate changed");Flush("transition/right_candidate_complete/"+role);
            }
            row["native_save_performed"]=true;row["save_scope"]="Two independent right local native evidence candidates only";
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }

    static object TrAssemblyHealth()
    {
        var rows=new List<object>();for(ModelDoc2 d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2){var bad=Features(d).Select(f=>{bool w;int e=f.GetErrorCode2(out w);return new{name=f.Name,type=f.GetTypeName2(),error=e,warning=w};}).Where(x=>x.error!=0||x.warning).ToArray();if(bad.Length>0||d.Extension.GetWhatsWrongCount()>0)rows.Add(new{document=d.GetPathName(),issues=d.Extension.GetWhatsWrongCount(),features=bad});}return rows;
    }
    static void TrAssemblyPersist(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Need(Writable&&Convert.ToInt32(c["index"])==13,"A-only integrated transition candidate authorized");Top=(string)c["top"];Root=(string)c["package_root"];Need(Root.Contains("22057_950局部过渡整合候选_20261017_r80"),"Exact independent integration root required");
        var before=Identity(Root);row["input_identity"]=before;var edits=new Dictionary<string,object>{{"case_id",c["id"]}};row["integrated_transition"]=edits;RmEvidence=edits;
        var local=Read(Path.Combine(Path.GetDirectoryName(EvidencePath),"transition_local_acceptance_r79.json"));Need(Convert.ToBoolean(local["pass"]),"Four local native acceptance required");
        var replacements=new List<object>();edits["replacement_parts"]=replacements;
        try{
            foreach(var raw in (System.Collections.IEnumerable)local["pieces"]){var piece=(Dictionary<string,object>)raw;string name=((string)piece["side"]=="left"?"左":"右")+"立柱焊件"+((string)piece["role"]=="rear"?"3":"2")+".SLDPRT",path=(string)piece["native_path"],dest=Path.Combine(Root,name);Need(Hash(path)==(string)piece["native_sha256"],"Accepted native support hash changed");
                int e=0,w=0;Sw.SetCurrentWorkingDirectory(Root);var original=Sw.OpenDoc6(dest,1,3,"",ref e,ref w) as ModelDoc2;Need(original!=null&&e==0,"Cannot read original support material");string database="",material=((PartDoc)original).GetMaterialPropertyName2("Default",out database);Sw.CloseAllDocuments(true);
                File.SetAttributes(dest,FileAttributes.Normal);File.Copy(path,dest,true);File.SetAttributes(dest,FileAttributes.Normal);replacements.Add(new{document=name,source=path,source_sha256=Hash(path),material_database=database,material_name=material});
                e=0;w=0;var replacement=Sw.OpenDoc6(dest,1,1,"Default",ref e,ref w) as ModelDoc2;Need(replacement!=null&&e==0&&!replacement.IsOpenedReadOnly(),"Cannot open writable replacement support");
                if(!String.IsNullOrEmpty(material))((PartDoc)replacement).SetMaterialPropertyName2("Default",database,material);Need(replacement.ForceRebuild3(false)&&replacement.Extension.GetWhatsWrongCount()==0,"Replacement support invalid after material assignment");e=0;w=0;Need(replacement.Save3(1,ref e,ref w)&&e==0&&w==0,"Cannot save integrated support with original material");Sw.CloseAllDocuments(true);Flush("transition/integrated_support_replaced/"+name);
            }
            var top=OpenTop(row);edits["loaded_after_replacement_health"]=TrAssemblyHealth();Flush("transition/integrated_top_opened");
            var fixedPart=Doc("立柱固定焊件.SLDPRT");RmActivate(fixedPart,"restore_original_upper_fixed_piece");Need(RmFeature(fixedPart,"参考安装结构_下部内移25").SetSuppression2(0,2,null),"Cannot restore original upper fixed piece");RmRebuild(fixedPart,"original_upper_fixed_restored");edits["fixed_part_bodies"]=Bodies(fixedPart);
            var frame=Doc("立柱连接架.SLDPRT");RmActivate(frame,"restore_original_879_frame");foreach(string name in new[]{"参考安装_左端区内移25","参考安装_右端区内移25"})Need(RmFeature(frame,name).SetSuppression2(0,2,null),"Cannot restore original frame end");RmRebuild(frame,"original_879_frame_restored");var box=RmBox(frame);Need(Math.Abs((box[3]-box[0])-.879)<1e-7,"Restored frame must be exactly 879 mm");edits["frame_bodies"]=Bodies(frame);edits["frame_width_mm"]=(box[3]-box[0])*1000;
            bool rebuilt=top.ForceRebuild3(false);edits["top_rebuild"]=rebuilt;edits["final_health"]=TrAssemblyHealth();Flush("transition/integrated_rebuild_result");Need(rebuilt,"Integrated transition assembly rebuild failed");
            row["snapshot_after_mount"]=Snapshot(top,false);row["verified_controls"]=VerifiedControls(c);row["expected_visible_solids"]=ExpectedVisibleSolids;SaveDocs(top,row);row["snapshot"]=Snapshot(top,false);row["native_save_performed"]=true;ExportCurrent(top,row,(string)c["id"]);
        }finally{Sw.CloseAllDocuments(true);row["final_identity"]=Identity(Root);}
    }

    static void ApplyTransitionMount(Dictionary<string,object> c,Dictionary<string,object> row,ModelDoc2 top){Need(false,"Local probe must pass before assembly implementation");}
}
