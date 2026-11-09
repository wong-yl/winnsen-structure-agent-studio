"""Local canopy checks at the two short-strip lamp-count transitions."""
import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/led_transition_probe_r371';assert not target.exists()
shutil.copytree(STAGE/'scripts/short_led_candidate_r364',target)
p=target/'NarrowFitProbeR350.cs'
text=p.read_text(encoding='utf-8')
start=text.index('            var canopy=Doc(')
end=text.index('\n        }finally',start)
text=text[:start]+'''            var cycles=new List<object>();row["transition_cycles"]=cycles;
            var canopy=Doc("太阳能雨棚装配.SLDASM");
            foreach(double width in new[]{749.0,899.0}) {
                var cycle=new Dictionary<string,object>();cycles.Add(cycle);cycle["dimensions_mm"]=new[]{1700.0,width,450.0};
                var changes=new List<object>();cycle["input_changes"]=changes;
                var directions=CaptureCanopyDirections(canopy);PrepareCanopySpan(canopy,width,directions,changes);
                SyncNarrowLamp(width,changes,cycle);SyncShortLed(width,changes,cycle);
                Set(Doc("遮阳板.SLDPRT"),"D1@草图1",width-160,changes);Need(Doc("遮阳板.SLDPRT").ForceRebuild3(false),"Transition shade rebuild failed");
                RmActivate(canopy,"transition_canopy_equations");((EquationMgr)canopy.GetEquationMgr()).EvaluateAll();Need(canopy.ForceRebuild3(false),"Transition canopy rebuild failed");
                FinishCanopySpan(canopy,width,changes);Need(canopy.ForceRebuild3(false),"Transition punch rebuild failed");
                cycle["canopy_states"]=ShStates(canopy);cycle["canopy_mates"]=LayoutMates(canopy);cycle["canopy_health"]=LayoutFeatureHealth(canopy);
                foreach(string key in new[]{"canopy","shell","cover","retainer","led_left","led_right"}) {
                    string name=key=="canopy"?"太阳能雨棚装配.SLDASM":key=="shell"?"雨棚LED灯罩壳.SLDPRT":key=="cover"?"塑料灯罩.SLDPRT":key=="retainer"?"塑料板压条焊件.SLDPRT":"LED硬灯条(蓝景13点白色930×16mm) "+(key=="led_left"?"L":"R")+".SLDPRT";
                    string label="w"+width+"_"+key;TrExport(Doc(name),label,cycle);cycle[key+"_export"]=cycle[label+"_export"];
                }
                cycle["files_unchanged"]=true;cycle["native_save_performed"]=false;Flush("transition_local_complete/"+width);
            }'''+text[end:]
p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorLedTransitionProbeR371.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_led_transition_probe_r371.mjs').write_text((STAGE/'scripts/run_lamp_candidate_probe_r360.mjs').read_text(encoding='utf-8').replace('OutdoorLampCandidateProbeR360.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/led_transition_probe_r371_build.json',{'status':'BUILT_NOT_ACCEPTED','scope':'Only two local canopy transitions at 749 and 899; no new full-cabinet sample','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
