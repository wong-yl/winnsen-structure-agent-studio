using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

enum swSketchRelationFilterType_e { swExternal = 3 }
enum swBodyType_e { swSolidBody = 0 }
class Configuration { public string Name = "Default"; }
class ConfigurationManager { public Configuration ActiveConfiguration = new Configuration(); }
class MathTransform { public object ArrayData = new double[] { 1,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0 }; }
class SketchPoint {
    public int[] Id; public double X,Y,Z;
    public SketchPoint(int a,int b,double x=0,double y=0){Id=new[]{a,b};X=x;Y=y;}
    public object GetID(){return Id;}
}
class SketchSegment {
    public int[] Id; public bool ConstructionGeometry;
    public SketchSegment(int a,int b){Id=new[]{a,b};}
    public object GetID(){return Id;} public new int GetType(){return 0;}
    public virtual double GetLength(){return 1;}
}
class SketchLine:SketchSegment {
    public SketchPoint Start,End;
    public SketchLine(int a,int b,SketchPoint start,SketchPoint end):base(a,b){Start=start;End=end;}
    public SketchPoint IGetStartPoint2(){return Start;} public SketchPoint IGetEndPoint2(){return End;}
}
class SketchArc:SketchSegment {
    public SketchArc():base(0,0){}
    public SketchPoint IGetStartPoint2(){return new SketchPoint(0,0);}
    public SketchPoint IGetEndPoint2(){return new SketchPoint(0,0);}
    public SketchPoint IGetCenterPoint2(){return new SketchPoint(0,0);}
    public double GetRadius(){return 1;} public int IsCircle(){return 0;}
}
class SketchRelation {
    public int Type=9; public object[] Types; public object[] Entities;
    public int CountOffset;
    public SketchRelation(int a,int b,int c,int d){Types=new object[]{2,2};Entities=new object[]{new SketchPoint(a,b),new SketchPoint(c,d)};}
    public int GetRelationType(){return Type;} public object GetEntitiesType(){return Types;}
    public object GetEntities(){return Entities;} public int GetEntitiesCount(){return Entities.Length+CountOffset;}
}
class SketchRelationManager {
    public List<SketchRelation> Values=new List<SketchRelation>(); public int Deletes,CountOffset; public bool FailDelete;
    public object GetRelations(int f){if(f!=3)throw new Exception("UNEXPECTED_FILTER");return Values.ToArray();}
    public int GetRelationsCount(int f){return Values.Count+CountOffset;}
    public bool DeleteRelation(SketchRelation r){Deletes++;return !FailDelete&&Values.Remove(r);}
}
class Sketch {
    public SketchRelationManager RelationManager=new SketchRelationManager();
    public MathTransform ModelToSketchTransform=new MathTransform();
    public object[] Segments=new object[0],Points=new object[0];
    public object GetSketchSegments(){return Segments;} public object GetSketchPoints2(){return Points;}
}
class Feature {
    public int Id; public string Name,Type; public Feature[] Parents=new Feature[0]; public Feature Next; public Sketch Sketch; public bool Suppressed;
    public Feature(int id,string name,string type){Id=id;Name=name;Type=type;}
    public int GetID(){return Id;} public object GetParents(){return Parents;}
    public string GetTypeName2(){return Type;} public bool IsSuppressed(){return Suppressed;}
    public int GetErrorCode2(out bool warning){warning=false;return 0;} public int GetErrorCode(){return 0;}
    public object GetNextFeature(){return Next;} public object GetNextSubFeature(){return null;}
    public object GetFirstSubFeature(){return null;} public object GetSpecificFeature2(){return Sketch;}
}
class Vertex { public double[] Point; public Vertex(double[] p){Point=p;} public object GetPoint(){return (double[])Point.Clone();} }
class Body2 {
    public Vertex[] Vertices=Enumerable.Range(0,400).Select(i=>new Vertex(new[]{i*.001,(i%17)*.001,(i%13)*.001})).ToArray();
    public int GetFaceCount(){return 100;} public int GetEdgeCount(){return 600;}
    public object GetMassProperties(double d){return new[]{.1,.2,.3,.01,1.0,.04,.05,.06,.07,.08,.09,.10,.11};}
    public object GetVertices(){return Vertices;}
}
class PartDoc { public Body2[] Bodies=new[]{new Body2()}; public object GetBodies2(int t,bool v){return Bodies;} }
class ModelDoc2:PartDoc {
    public string Path; public bool ReadOnly,Dirty,RebuildPass=true,KeepContextReferences;
    public string[] Configurations=new[]{"Default"}; public ConfigurationManager ConfigurationManager=new ConfigurationManager();
    public List<Feature> Features=new List<Feature>(); public Action AfterRebuild;
    public object[][] ReferenceOverride; public int MasterStatus=4,ContextStatus=4;
    public string GetPathName(){return Path;} public bool IsOpenedReadOnly(){return ReadOnly;} public bool GetSaveFlag(){return Dirty;}
    public object GetConfigurationNames(){return Configurations;} public int GetConfigurationCount(){return Configurations.Length;}
    public object FirstFeature(){return Features.FirstOrDefault();}
    public bool ForceRebuild3(bool all){if(AfterRebuild!=null)AfterRebuild();return RebuildPass;}
    public void ListExternalFileReferences2(out object files,out object parts,out object features,out object types,out object statuses,out object entities,out object components){
        var target=Features.Single(f=>f.Id==321).Sketch.RelationManager;
        bool before=target.Values.Count>0||KeepContextReferences;
        string master=System.IO.Path.Combine(ParametricContext.CadDirectory,"标准寄存柜 模型.SLDPRT");
        string root=System.IO.Path.Combine(ParametricContext.CadDirectory,"标准寄存柜1917×1000×550(总装配).SLDASM");
        object[][] rows=ReferenceOverride??new[]{
          before?new object[]{master,master,root,root}:new object[]{master,master},
          before?new object[]{master,master,"",""}:new object[]{master,master},
          before?new object[]{"基体零件-标准寄存柜 模型-1","基体零件-标准寄存柜 模型-1","草图21  ←  切除-拉伸8","草图21  ←  切除-拉伸8"}:new object[]{"基体零件-标准寄存柜 模型-1","基体零件-标准寄存柜 模型-1"},
          before?new object[]{"实体","特征","点","点"}:new object[]{"实体","特征"},
          before?new object[]{MasterStatus,MasterStatus,ContextStatus,ContextStatus}:new object[]{MasterStatus,MasterStatus},
          before?new object[]{"实体 ← 标准寄存柜 模型","特征 ← 标准寄存柜 模型","面 ← 箱体左侧板焊接<1>/箱体左侧板<1>","面 ← 箱体左侧板焊接<1>/箱体左侧板<1>"}:new object[]{"实体 ← 标准寄存柜 模型","特征 ← 标准寄存柜 模型"},
          before?new object[]{"","","箱体竖隔板L焊接<1>/箱体竖隔板L<1>","箱体竖隔板L焊接<1>/箱体竖隔板L<1>"}:new object[]{"",""}};
        files=rows[0];parts=rows[1];features=rows[2];types=rows[3];statuses=rows[4];entities=rows[5];components=rows[6];
    }
}
class ISldWorks {
    public ModelDoc2[] Documents; public object GetDocuments(){return Documents;} public int GetDocumentCount(){return Documents.Length;} public int GetProcessID(){return 0;}
    public object GetOpenDocumentByName(string path){return Documents.FirstOrDefault(m=>m.Path.Equals(path,StringComparison.OrdinalIgnoreCase));}
}
static class ParametricContext {
    public static string AttemptDirectory,CadDirectory,Phase="locks"; public static int MutationChecks;
    public static void CheckPath(string p,string root){if(!System.IO.Path.GetFullPath(p).StartsWith(System.IO.Path.GetFullPath(root)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("PATH_ESCAPE");}
    public static void AssertMutation(string p){MutationChecks++;CheckPath(p,AttemptDirectory);}
}
public static class NativeParametricLocks {
    public class GeometrySnapshot { public bool traversal_complete=true,has_sheet_metal=true,has_flat_pattern=true; public int body_count=1,error_feature_count,warning_feature_count; }
    static GeometrySnapshot CaptureGeometry(ModelDoc2 m,string side){return new GeometrySnapshot{body_count=m.Bodies.Length,has_sheet_metal=m.Features.Any(f=>f.Type=="SheetMetal"),has_flat_pattern=m.Features.Any(f=>f.Type=="FlatPattern")};}
    static Feature FindFeature(ModelDoc2 m,string name){return m.Features.SingleOrDefault(f=>f.Name==name);}
    static void Release(object v){}
    static void Require(bool pass,string code,int stage,string detail){if(!pass)throw new Exception(code+" "+detail);}
/* ACTUAL_HELPERS */
    static List<object> Results=new List<object>();
    static void Check(bool value,string message){if(!value)throw new Exception("ASSERT "+message);}
    static ModelDoc2 Fixture(){
        var m=new ModelDoc2{Path=System.IO.Path.Combine(ParametricContext.AttemptDirectory,"lock_work_R.SLDPRT")};
        var master=new Feature(19,"基体零件-标准寄存柜 模型-1","SplitBody");
        var target=new Feature(321,"草图21","ProfileFeature"){Parents=new[]{master},Sketch=new Sketch()};
        target.Sketch.RelationManager.Values.Add(new SketchRelation(2,3,0,2));target.Sketch.RelationManager.Values.Add(new SketchRelation(5,6,3,5));
        var p=new[]{new SketchPoint(1,1,0,0),new SketchPoint(2,2,.01,0),new SketchPoint(3,3,.01,.01),new SketchPoint(4,4,0,.01)};
        target.Sketch.Points=p;target.Sketch.Segments=Enumerable.Range(0,4).Select(i=>(object)new SketchLine(i,i+1,p[i],p[(i+1)%4])).ToArray();
        m.Features.Add(master);m.Features.Add(target);m.Features.Add(new Feature(329,"切除-拉伸8","Cut"){Parents=new[]{target,master}});
        m.Features.Add(new Feature(50,"钣金1","SheetMetal"));m.Features.Add(new Feature(64,"平板型式1","FlatPattern"){Suppressed=true});
        for(int i=0;i<11;i++){
            var f=new Feature(500+i,"其它草图"+i,"ProfileFeature"){Sketch=new Sketch(),Parents=new[]{master}};
            for(int j=0;j<(i==10?1:2);j++){var relation=new SketchRelation(i,j,i+1,j+1);if(j==1){relation.Type=1;relation.Types=new object[]{3,3};relation.Entities=new object[]{new SketchSegment(i,j),new SketchSegment(i+1,j+1)};}f.Sketch.RelationManager.Values.Add(relation);}m.Features.Add(f);
        }
        for(int i=0;i<m.Features.Count-1;i++)m.Features[i].Next=m.Features[i+1];return m;
    }
    static SketchRelationManager Target(ModelDoc2 m){return m.Features.Single(f=>f.Id==321).Sketch.RelationManager;}
    static void Case(string name,Action action){action();Results.Add(new{name=name,pass=true});}
    static void RejectBefore(string name,Action<ModelDoc2> alter){Case(name,delegate{var m=Fixture();alter(m);bool rejected=false;try{DetachAuditedRightSourceContext(m,new List<object>(),m.Path);}catch{rejected=true;}Check(rejected,"accepted "+name);Check(m.Features.Where(f=>f.Sketch!=null).Sum(f=>f.Sketch.RelationManager.Deletes)==0,"deleted before rejection "+name);});}
    static void RejectAfter(string name,Action<ModelDoc2> alter){Case(name,delegate{var m=Fixture();alter(m);bool rejected=false;try{DetachAuditedRightSourceContext(m,new List<object>(),m.Path);}catch{rejected=true;}Check(rejected,"accepted "+name);Check(Target(m).Deletes<=2,"extra deletes "+name);Check(m.Features.Where(f=>f.Sketch!=null&&f.Id!=321).All(f=>f.Sketch.RelationManager.Deletes==0),"other manager deleted");});}
    static Dictionary<string,object> OpenRow(ModelDoc2 m,int warnings){return new Dictionary<string,object>{{"operation","open"},{"path",m.Path},{"requestedReadOnly",true},{"actualReadOnly",true},{"errors",0},{"warnings",warnings}};}
    static void WriteRows(object[] rows){string dir=System.IO.Path.Combine(ParametricContext.AttemptDirectory,"evidence");Directory.CreateDirectory(dir);File.WriteAllText(System.IO.Path.Combine(dir,"locks.document-state.json"),new JavaScriptSerializer().Serialize(rows));}
    static void OpenCase(string name,int warnings,bool pass,Action<ModelDoc2,Dictionary<string,object>> alter,int countDelta=1){Case(name,delegate{
        var m=Fixture();m.ReadOnly=true;var row=OpenRow(m,warnings);if(alter!=null)alter(m,row);var rows=new List<object>{new{operation="old"}};for(int i=0;i<countDelta;i++)rows.Add(row);WriteRows(rows.ToArray());var evidence=new List<object>();bool rejected=false;try{RequireNewCleanLockOpen(new ISldWorks{Documents=new[]{m}},m,evidence,System.IO.Path.Combine(ParametricContext.AttemptDirectory,"lock_work_R.SLDPRT"),1,name);}catch{rejected=true;}Check(rejected!=pass,"open expectation "+name);
        if(countDelta==1&&row.ContainsKey("warnings")&&row["warnings"] is int){Check(evidence.Count>=3,"missing raw and loaded observations "+name);var raw=new JavaScriptSerializer().Serialize(evidence[0]);Check(raw.Contains("\"rawWarnings\":"+warnings),"raw warning altered "+name);}
    });}
    public static string RunTests(string root){
        ParametricContext.AttemptDirectory=System.IO.Path.GetFullPath(root);ParametricContext.CadDirectory=System.IO.Path.Combine(root,"native_cad","working_pack");Directory.CreateDirectory(root);
        Case("two exact deletes, 21 retained, current-attempt master preserved",delegate{var m=Fixture();m.MasterStatus=3;m.ContextStatus=4;var rows=new List<object>();var proof=DetachAuditedRightSourceContext(m,rows,m.Path);Check(Target(m).Deletes==2&&Target(m).Values.Count==0,"two deletes");Check(m.Features.Where(f=>f.Sketch!=null&&f.Id!=321).All(f=>f.Sketch.RelationManager.Deletes==0),"only target manager");Check(proof.externalRelations.Length==21&&proof.vertices.Count==400,"complete proof");RequireDetachedRightSourceContext(m,rows,proof,"saved-source");m.Path=System.IO.Path.Combine(ParametricContext.CadDirectory,"箱体竖隔板R参数源.SLDPRT");m.ReadOnly=true;RequireDetachedRightSourceContext(m,rows,proof,"independent-source");});
        RejectBefore("actual readonly",m=>m.ReadOnly=true);
        RejectBefore("L work rejected",m=>m.Path=System.IO.Path.Combine(ParametricContext.AttemptDirectory,"lock_work_L.SLDPRT"));
        RejectBefore("multiple configurations",m=>m.Configurations=new[]{"Default","Other"});
        RejectBefore("wrong active configuration",m=>m.ConfigurationManager.ActiveConfiguration.Name="Other");
        RejectBefore("wrong sketch feature ID",m=>m.Features.Single(f=>f.Id==321).Id=322);
        RejectBefore("wrong sketch parent",m=>m.Features.Single(f=>f.Id==321).Parents=new Feature[0]);
        RejectBefore("wrong sketch feature type",m=>m.Features.Single(f=>f.Id==321).Type="3DProfileFeature");
        RejectBefore("wrong cut parent order",m=>m.Features.Single(f=>f.Id==329).Parents=m.Features.Single(f=>f.Id==329).Parents.Reverse().ToArray());
        RejectBefore("wrong master feature",m=>m.Features.Single(f=>f.Id==19).Type="Imported");
        RejectBefore("missing sheet metal",m=>m.Features.Single(f=>f.Id==50).Type="Other");
        RejectBefore("missing FlatPattern",m=>m.Features.Single(f=>f.Id==64).Type="Other");
        RejectBefore("target relation count three",m=>Target(m).Values.Add(new SketchRelation(8,9,9,10)));
        RejectBefore("relation count array mismatch",m=>Target(m).CountOffset=1);
        RejectBefore("relation type mismatch",m=>Target(m).Values[0].Type=1);
        RejectBefore("relation entity count mismatch",m=>Target(m).Values[0].CountOffset=1);
        RejectBefore("relation entity type mismatch",m=>Target(m).Values[0].Types[0]=3);
        RejectBefore("point pair direction changed",m=>Target(m).Values[0].Entities=Target(m).Values[0].Entities.Reverse().ToArray());
        RejectBefore("point ID words changed",m=>((SketchPoint)Target(m).Values[0].Entities[0]).Id=new[]{3,2});
        RejectBefore("duplicate target relation",m=>Target(m).Values[1]=new SketchRelation(2,3,0,2));
        RejectBefore("unknown external entity",m=>m.Features.Last().Sketch.RelationManager.Values[0].Entities[0]=new object());
        RejectBefore("22 external relations",m=>m.Features.Last().Sketch.RelationManager.Values.Clear());
        RejectBefore("master locked",m=>m.MasterStatus=1);RejectBefore("master broken",m=>m.MasterStatus=0);RejectBefore("context dangling",m=>m.ContextStatus=5);
        RejectBefore("old attempt reference",m=>{object a,b,c,d,e,f,g;m.ListExternalFileReferences2(out a,out b,out c,out d,out e,out f,out g);m.ReferenceOverride=new[]{(object[])a,(object[])b,(object[])c,(object[])d,(object[])e,(object[])f,(object[])g};m.ReferenceOverride[0][2]=System.IO.Path.Combine(ParametricContext.AttemptDirectory,"old-attempt","root.SLDASM");});
        RejectBefore("wrong file reference owner",m=>{object a,b,c,d,e,f,g;m.ListExternalFileReferences2(out a,out b,out c,out d,out e,out f,out g);m.ReferenceOverride=new[]{(object[])a,(object[])b,(object[])c,(object[])d,(object[])e,(object[])f,(object[])g};m.ReferenceOverride[2][2]="草图20  ←  切除-拉伸8";});
        RejectAfter("DeleteRelation false",m=>Target(m).FailDelete=true);
        RejectAfter("rebuild false",m=>m.RebuildPass=false);
        RejectAfter("interior vertex drift beyond 0.002mm",m=>m.AfterRebuild=()=>m.Bodies[0].Vertices[100].Point[1]+=.000003);
        RejectAfter("vertex one-to-one multiplicity",m=>m.AfterRebuild=()=>m.Bodies[0].Vertices[100].Point=(double[])m.Bodies[0].Vertices[101].Point.Clone());
        RejectAfter("other external relation changed",m=>m.AfterRebuild=()=>((SketchPoint)m.Features.Last().Sketch.RelationManager.Values[0].Entities[0]).Id[0]++);
        RejectAfter("sketch21 contour moved",m=>m.AfterRebuild=()=>((SketchPoint)m.Features.Single(f=>f.Id==321).Sketch.Points[0]).X+=.000003);
        RejectAfter("old context file reference retained",m=>m.KeepContextReferences=true);
        Case("persisted target relation reappears",delegate{var m=Fixture();var proof=DetachAuditedRightSourceContext(m,new List<object>(),m.Path);Target(m).Values.Add(new SketchRelation(2,3,0,2));bool rejected=false;try{RequireDetachedRightSourceContext(m,new List<object>(),proof,"reopen");}catch{rejected=true;}Check(rejected,"reappeared accepted");});
        OpenCase("open raw zero clean",0,true,null);OpenCase("open warning128 preserved",128,true,null);OpenCase("open other warning4 preserved",4,true,null);
        OpenCase("open bit32 rejected",32,false,null);OpenCase("open bit1 rejected",1,false,null);OpenCase("open combined bits rejected",33,false,null);
        OpenCase("open errors rejected",0,false,(m,r)=>r["errors"]=1);OpenCase("open dirty rejected",0,false,(m,r)=>m.Dirty=true);
        OpenCase("open requested writable rejected",0,false,(m,r)=>r["requestedReadOnly"]=false);
        OpenCase("open report writable rejected",0,false,(m,r)=>r["actualReadOnly"]=false);
        OpenCase("open actual writable rejected",0,false,(m,r)=>m.ReadOnly=false);
        OpenCase("open wrong operation rejected",0,false,(m,r)=>r["operation"]="save");
        OpenCase("open wrong recorded path rejected",0,false,(m,r)=>r["path"]=m.Path+".other");
        OpenCase("open actual path mismatch rejected",0,false,(m,r)=>m.Path+=".other");
        OpenCase("open no added record rejected",0,false,null,0);OpenCase("open two added records rejected",0,false,null,2);
        Case("natural source clean and readonly",delegate{var m=Fixture();m.ReadOnly=true;var rows=new List<object>();RequireNaturallyLoadedRightSourceClean(new ISldWorks{Documents=new[]{m}},rows,m.Path,"temporary");Check(rows.Count==1,"natural source observation missing");});
        Case("natural source dirty rejected",delegate{var m=Fixture();m.ReadOnly=true;m.Dirty=true;var rows=new List<object>();bool rejected=false;try{RequireNaturallyLoadedRightSourceClean(new ISldWorks{Documents=new[]{m}},rows,m.Path,"committed");}catch{rejected=true;}Check(rejected&&rows.Count==1,"natural source dirty accepted");});
        Case("natural source absent stays absent",delegate{var rows=new List<object>();RequireNaturallyLoadedRightSourceClean(new ISldWorks{Documents=new ModelDoc2[0]},rows,System.IO.Path.Combine(ParametricContext.CadDirectory,"source.SLDPRT"),"temporary");var raw=new JavaScriptSerializer().Serialize(rows[0]);Check(raw.Contains("\"loaded\":false")&&raw.Contains("\"saveFlag\":null"),"absent source forged");});
        Check(ParametricContext.MutationChecks>0,"mutation guard unused");
        return new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(new{pass=true,cases=Results.Count,results=Results,cadStarted=false,productionMainInvoked=false,newExeInvoked=false});
    }
}
