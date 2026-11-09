import shutil
import subprocess
from generate_model import STAGE, sha, write

target = STAGE / 'scripts/led_survey_r363'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/lamp_candidate_probe_r360', target)
probe = target / 'NarrowFitProbeR350.cs'
probe.write_text('''using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object LedSketchSurvey(ModelDoc2 d) {
        return Features(d).Where(f=>f.GetTypeName2()=="ProfileFeature").Select(f=>{
            var s=f.GetSpecificFeature2() as Sketch;
            return new{name=f.Name,transform=s.ModelToSketchTransform.ArrayData,status=s.GetConstrainedStatus(),segments=(s.GetSketchSegments() as Array ?? new object[0]).Cast<SketchSegment>().Select(a=>new{kind=a.GetType(),construction=a.ConstructionGeometry,length=a.GetLength(),start=a is SketchLine?(object)new[]{((SketchPoint)((SketchLine)a).GetStartPoint2()).X,((SketchPoint)((SketchLine)a).GetStartPoint2()).Y,((SketchPoint)((SketchLine)a).GetStartPoint2()).Z}:null,end=a is SketchLine?(object)new[]{((SketchPoint)((SketchLine)a).GetEndPoint2()).X,((SketchPoint)((SketchLine)a).GetEndPoint2()).Y,((SketchPoint)((SketchLine)a).GetEndPoint2()).Z}:null,arc=a is SketchArc?(object)new{center=new[]{((SketchPoint)((SketchArc)a).GetCenterPoint2()).X,((SketchPoint)((SketchArc)a).GetCenterPoint2()).Y,((SketchPoint)((SketchArc)a).GetCenterPoint2()).Z},radius=((SketchArc)a).GetRadius()}:null}).ToArray()};
        }).ToArray();
    }
    static void NarrowFitProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);Writable=false;
        try {
            var top=OpenTop(row);ApplyInputs(top,1700,600,450,row);
            var records=new List<object>();row["led_survey"]=records;
            foreach(string n in new[]{"遮阳板.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) L.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) R.SLDPRT"}){
                var d=Doc(n);records.Add(new{name=n,details=ShDetails(d),sketches=LedSketchSurvey(d)});Flush("led_sketch_survey/"+n);
            }
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;row["native_save_performed"]=false;}
    }
}
''', encoding='utf-8')
tool=STAGE/'OutdoorLedSurveyR363.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_led_survey_r363.mjs').write_text((STAGE/'scripts/run_lamp_candidate_probe_r360.mjs').read_text(encoding='utf-8').replace('OutdoorLampCandidateProbeR360.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/led_survey_r363_build.json',{'status':'BUILT_DIAGNOSTIC','user_approved':'Narrow cabinets may use suitable short LED strips; preserve cross-section and LED package dimensions','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
