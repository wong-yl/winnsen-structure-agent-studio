"""Small no-save survey of native shelf-rib controls and pilot sketches."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/shelf_rib_survey_r422';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r418',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8');key='else if(mode=="layout_probe")LayoutProbe(c,row);';assert t.count(key)==1
p.write_text(t.replace(key,'else if(mode=="layout_probe")ShelfRibSurvey(c,row);'),encoding='utf-8')
(target/'ShelfRibSurveyR422.cs').write_text(r'''using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object RibSurveyDetails(ModelDoc2 d){return new{health=LayoutFeatureHealth(d),details=ShDetails(d),sketches=Sketches(d),extrusions=Features(d).Select(f=>new{feature=f,data=f.GetDefinition() as IExtrudeFeatureData2}).Where(r=>r.data!=null).Select(r=>new{name=r.feature.Name,type=r.feature.GetTypeName2(),end=r.data.GetEndCondition(true),depth=r.data.GetDepth(true),from=r.data.FromType,scope=r.data.FeatureScope,auto_select=r.data.AutoSelect}).ToArray(),circles=Features(d).Where(f=>f.GetTypeName2()=="ProfileFeature").Select(f=>new{name=f.Name,circles=(((Sketch)f.GetSpecificFeature2()).GetSketchSegments() as Array??new object[0]).Cast<SketchSegment>().OfType<SketchArc>().Select(a=>new{center=new[]{((SketchPoint)a.GetCenterPoint2()).X,((SketchPoint)a.GetCenterPoint2()).Y,((SketchPoint)a.GetCenterPoint2()).Z},radius=a.GetRadius()}).ToArray()}).Where(r=>r.circles.Length>0).ToArray(),cylinders=Cylinders(d)};}
    static void ShelfRibSurvey(Dictionary<string,object> c,Dictionary<string,object> row){
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);
        try{var top=OpenTop(row);var rib=Doc("箱体横层板加强筋.SLDPRT");RmActivate(rib,"rib_survey");row["before"]=RibSurveyDetails(rib);Flush("rib_survey_before");
            var changes=new List<object>();Set(rib,"D1@草图1",293,changes);row["length_rebuilt"]=rib.ForceRebuild3(false);row["after"]=RibSurveyDetails(rib);row["native_save_performed"]=false;Flush("rib_survey_after");
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorShelfRibSurveyR422.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_shelf_rib_survey_r422.mjs').write_text((STAGE/'scripts/run_shelf_rib_probe_r421.mjs').read_text(encoding='utf-8').replace('OutdoorShelfRibProbeR421.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/shelf_rib_r421_contract.json');c['cases'][0]['id']='shelf_rib_survey_r422'
write(STAGE/'evidence/shelf_rib_survey_r422_contract.json',c)
write(STAGE/'evidence/shelf_rib_survey_r422_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
