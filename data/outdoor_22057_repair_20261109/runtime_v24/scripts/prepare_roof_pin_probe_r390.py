"""Throwaway roof locating-post linkage survey; no native save."""
import shutil
import subprocess
from generate_model import STAGE,sha,write
target=STAGE/'scripts/roof_pin_probe_r390';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r384',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='else if(mode=="layout_probe")LayoutProbe(c,row);';assert t.count(key)==1
p.write_text(t.replace(key,'else if(mode=="layout_probe"){RelocateTask(c,row);RoofPinProbe(c,row);}'),encoding='utf-8')
(target/'RoofPinProbeR390.cs').write_text(r'''using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object RoofPinProfile(ModelDoc2 d) {
        var s=(Sketch)RmFeature(d,"草图24").GetSpecificFeature2();
        return new{transform=s.ModelToSketchTransform.ArrayData,sketch_status=s.GetConstrainedStatus(),
            circles=(s.GetSketchSegments() as Array??new object[0]).Cast<SketchSegment>().OfType<SketchArc>().Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new{local=new[]{p.X,p.Y,p.Z},model=RmPoint(new[]{p.X,p.Y,p.Z},s.ModelToSketchTransform.IInverse()),radius=a.GetRadius()};}).ToArray(),
            relations=(s.RelationManager.GetRelations(0) as Array??new object[0]).Cast<SketchRelation>().Select(r=>new{type=r.GetRelationType(),entities=(r.GetEntities() as Array??new object[0]).Cast<object>().Select(e=>e is SketchPoint?(object)new{point=new[]{((SketchPoint)e).X,((SketchPoint)e).Y,((SketchPoint)e).Z},id=((SketchPoint)e).GetID()}:e is SketchSegment?(object)new{segment=((SketchSegment)e).GetType(),length=((SketchSegment)e).GetLength()}:new{kind=e==null?"external_null":e.GetType().Name}).ToArray()}).ToArray()};
    }
    static void RoofPinState(string label,Dictionary<string,object> row) {
        row["roof_"+label]=new{profile=RoofPinProfile(Doc("箱体顶板.SLDPRT")),parts=new[]{"箱体顶板.SLDPRT","顶部维护门板.SLDPRT","顶部维护门板装配.SLDASM","顶隔板.SLDPRT"}.Select(n=>{var d=Doc(n);return new{name=n,details=ShDetails(d),box=RmBox(d),cylinders=d is PartDoc?Cylinders(d):null,mates=d is AssemblyDoc?LayoutMates(d):null};}).ToArray()};Flush("roof_pin/"+label);
    }
    static void RoofPinProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);
        try {
            var top=OpenTop(row);RoofPinState("original",row);var log=new List<object>();row["input_changes"]=log;
            var master=Doc("防水寄存柜 模型.SLDPRT");Set(master,"D1@草图1",600,log);Set(master,"D2@草图1",1660,log);Set(master,"D1@凸台-拉伸1",450,log);Need(master.ForceRebuild3(false),"Roof master failed");
            top.ForceRebuild3(false);RoofPinState("narrow",row);row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorRoofPinProbeR390.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_roof_pin_probe_r390.mjs').write_text((STAGE/'scripts/run_body_survey_r389.mjs').read_text(encoding='utf-8').replace('OutdoorBodySurveyR389.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/roof_pin_probe_r390_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
