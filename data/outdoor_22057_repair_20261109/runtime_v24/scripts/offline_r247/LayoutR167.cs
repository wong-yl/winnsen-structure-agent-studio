using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static object LayoutPatternDetail(ModelDoc2 d,Feature f)
    {
        if(f.GetTypeName2()=="SketchPattern"){
            var p=f.GetDefinition() as SketchPatternFeatureData;Need(p!=null&&p.AccessSelections(d,null),"Pattern selection access required");
            try{
                var sketch=p.Sketch as Feature;var reference=p.ReferencePoint as SketchPoint;
                return new{name=f.Name,type=f.GetTypeName2(),sketch=sketch==null?null:sketch.Name,reference_type=p.GetReferencePointType(),reference=reference==null?null:new[]{reference.X,reference.Y,reference.Z},
                    geometry=p.GeometryPattern,seeds=((Array)p.PatternFeatureArray).Cast<Feature>().Select(x=>x.Name).ToArray()};
            }finally{p.ReleaseSelectionAccess();}
        }
        return null;
    }
    static void LayoutProbe(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Root=(string)c["package_root"];Top=(string)c["top"];Need(Within(Root,Stage),"Probe only isolated files");var before=Identity(Root);row["input_identity"]=before;
        try{
            var top=OpenTop(row);
            if(c.ContainsKey("inventory_only")){row["inventory"]=LayoutInventory(top);Flush("layout_inventory");return;}
            if(c.ContainsKey("lock_diagnosis")){LayoutLockDiagnosis(c,row);return;}
            var parts=new[]{"箱体锁安装槽.SLDPRT","箱体侧板加强筋.SLDPRT","箱体左侧板.SLDPRT","箱体右侧板.SLDPRT","箱体防水门框 竖.SLDPRT","箱体竖门框挡边.SLDPRT"};
            var details=new List<object>();row["parts"]=details;
            foreach(string n in parts){var d=Doc(n);details.Add(new{name=n,sketches=Sketches(d),sketch_patterns=Features(d).Where(f=>f.GetTypeName2()=="SketchPattern").Select(f=>LayoutPatternDetail(d,f)).ToArray()});Flush("layout_probe/"+n);}
            var rib=Doc("箱体侧板加强筋.SLDPRT");RmActivate(rib,"probe_native_pattern");
            var old=RmFeature(rib,"阵列(线性)1");var p=old.GetDefinition() as LinearPatternFeatureData;Need(p.AccessSelections(rib,null),"Cannot read rib pattern");
            var axis=p.D1Axis;var seeds=p.PatternFeatureArray;bool reverse=p.D1ReverseDirection;bool geometry=p.GeometryPattern;p.ReleaseSelectionAccess();
            rib.ClearSelection2(true);var select=((SelectionMgr)rib.SelectionManager).CreateSelectData() as SelectData;select.Mark=1;
            Need(((Entity)axis).Select4(false,select),"Cannot select native pattern axis");
            foreach(Feature seed in (Array)seeds)Need(seed.Select2(true,4),"Cannot select native pattern feature");
            var made=rib.FeatureManager.FeatureLinearPattern5(2,.123,1,0,reverse,false,"NULL","NULL",geometry,false,false,false,false,false,false,false,false,false,0,0,false,false) as Feature;
            Need(made!=null,"Cannot create native arbitrary-row copy");made.Name="临时原生行距能力探测";
            bool warning;row["native_pattern_probe"]=new{name=made.Name,error=made.GetErrorCode2(out warning),warning=warning,rebuild=rib.ForceRebuild3(false),issues=rib.Extension.GetWhatsWrongCount()};
            row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
    static object LayoutInventory(ModelDoc2 top)
    {
        return ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().Select(c=>{
            object info;var bodies=Path.GetExtension(c.GetPathName()).Equals(".sldprt",StringComparison.OrdinalIgnoreCase)&&!c.IsSuppressed()?c.GetBodies3(0,out info) as Array:null;
            return new{name=c.Name2,document=Path.GetFileName(c.GetPathName()),suppressed=c.IsSuppressed(),hidden=c.IsHidden(true),configuration=c.ReferencedConfiguration,
                bodies=bodies==null?new object[0]:bodies.Cast<Body2>().Select(b=>(object)new{name=b.Name,volume=b.GetMassProperties(1)}).ToArray()};
        }).ToArray();
    }
    static object LayoutLockRelations(ModelDoc2 d)
    {
        return Features(d).Where(f=>new[]{"草图1","草图2","草图3","草图28","阵列 数量+间距","草图32","草图33","草图34","草图35","草图36","草图8","草图46"}.Contains(f.Name)).Select(f=>{
            var s=f.GetSpecificFeature2() as Sketch;
            return new{name=f.Name,parents=(f.GetParents() as Array??new object[0]).Cast<Feature>().Select(p=>p.Name).ToArray(),
                relations=(s.RelationManager.GetRelations(0) as Array??new object[0]).Cast<SketchRelation>().Select(r=>new{type=r.GetRelationType(),entities=(r.GetEntities() as Array??new object[0]).Cast<object>().Select(e=>e is SketchPoint?(object)new{point=new[]{((SketchPoint)e).X,((SketchPoint)e).Y,((SketchPoint)e).Z},id=((SketchPoint)e).GetID()}:e is SketchSegment?(object)new{segment=((SketchSegment)e).GetType(),length=((SketchSegment)e).GetLength()}:new{kind=e==null?"external_null":e.GetType().Name}).ToArray()}).ToArray()};
        }).ToArray();
    }
    static void LayoutLockDiagnosis(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        var d=Doc("箱体锁安装槽.SLDPRT");RmActivate(d,"lock_diagnosis");var steps=new List<object>();row["steps"]=steps;
        row["original_relations"]=LayoutLockRelations(d);row["original_sketches"]=Sketches(d);
        var changes=new List<object>();row["changes"]=changes;
        foreach(string side in new[]{"L","R"}){
            string n=side=="L"?"D2@草图1":"D1@草图32";Set(d,n,side=="L"?397:416,changes);
            steps.Add(new{step="move_seed_"+side,rebuild=d.ForceRebuild3(false),health=LayoutFeatureHealth(d),relations=LayoutLockRelations(d)});Flush("lock_diagnosis/move_seed_"+side);
            if(Convert.ToString(c["lock_diagnosis"])=="seeds_only")continue;
            LayoutLockPattern(d,side=="L"?"草图阵列1":"草图阵列3",side=="L"?new[]{1448.0,764,233}:new[]{1429.0,826,470,214,58},changes);
            steps.Add(new{step="rewrite_pattern_"+side,rebuild=d.ForceRebuild3(false),health=LayoutFeatureHealth(d),relations=LayoutLockRelations(d)});Flush("lock_diagnosis/rewrite_pattern_"+side);
        }
        row["final_sketches"]=Sketches(d);row["native_save_performed"]=false;
    }
    static object LayoutMates(ModelDoc2 d)
    {
        return Features(d).Where(f=>f.GetTypeName2().StartsWith("Mate")).Select(f=>{
            var m=f.GetSpecificFeature2() as Mate2;
            var entities=new List<object>();
            if(m!=null)for(int i=0;i<m.GetMateEntityCount();i++){
                var e=m.MateEntity(i) as MateEntity2;
                var component=e==null?null:e.ReferenceComponent as Component2;
                entities.Add(new{component=component==null?null:component.Name2,type=e==null?-1:e.ReferenceType,parameters=e==null?null:e.EntityParams});
            }
            return new{name=f.Name,type=f.GetTypeName2(),suppressed=f.IsSuppressed2(1,null),entities=entities};
        }).ToArray();
    }
    static void LayoutSurvey(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Root=(string)c["package_root"];Top=(string)c["top"];Need(Within(Root,Stage),"Survey only isolated files");
        var before=Identity(Root);row["input_identity"]=before;
        try{
            var top=OpenTop(row);var documents=new List<object>();row["documents"]=documents;
            foreach(string file in Directory.GetFiles(Root).Where(p=>p.EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".SLDASM",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p)){
                string n=Path.GetFileName(file);
                if(!new[]{"防水柜门","箱体","电控锁装配","导水槽","门板加强筋","户外快递副柜","防水寄存柜"}.Any(k=>n.Contains(k)))continue;
                var d=Doc(n);Flush("layout_survey/"+n);
                documents.Add(new{name=n,configuration=((Configuration)d.GetActiveConfiguration()).Name,configurations=d.GetConfigurationNames(),dimensions=Dims(d),equations=Eqs(d),
                    features=Features(d).Select(f=>new{name=f.Name,type=f.GetTypeName2(),suppressed=f.IsSuppressed2(1,null)}).ToArray(),
                    patterns=d is PartDoc?Patterns(d):null,mates=d is AssemblyDoc?LayoutMates(d):null,
                    sketches=d is PartDoc&&new[]{"箱体锁安装槽.SLDPRT","箱体防水门框 竖.SLDPRT","箱体竖门框挡边.SLDPRT"}.Contains(n)?Sketches(d):null});
            }
            row["snapshot"]=Snapshot(top,false);row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
