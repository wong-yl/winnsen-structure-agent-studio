import shutil
import subprocess
from generate_model import STAGE, sha, write

target = STAGE / 'scripts/lamp_probe_r359'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/narrow_fit_probe_r350', target)
probe = target / 'NarrowFitProbeR350.cs'
probe.write_text('''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void NarrowFitProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);Writable=false;
        try {
            var top=OpenTop(row);try{ApplyInputs(top,1700,600,450,row);}catch(Exception ex){row["apply_error"]=ex.ToString();}
            var records=new List<object>();row["lamp_parts"]=records;
            foreach(string n in new[]{"雨棚LED灯罩壳.SLDPRT","塑料灯罩.SLDPRT","塑料板压条焊件.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) L.SLDPRT","LED硬灯条(蓝景13点白色930×16mm) R.SLDPRT","雨棚LED灯罩焊接.SLDASM"}){
                var d=Doc(n);bool rebuilt=d.ForceRebuild3(false);records.Add(new{name=n,rebuild=rebuilt,health=LayoutFeatureHealth(d),details=ShDetails(d),mates=d is AssemblyDoc?LayoutMates(d):null,states=d is AssemblyDoc?ShStates(d):null});Flush("lamp_survey/"+n);
            }
            TrExport(Doc("雨棚LED灯罩壳.SLDPRT"),"shell",row);TrExport(Doc("塑料灯罩.SLDPRT"),"cover",row);TrExport(Doc("塑料板压条焊件.SLDPRT"),"retainer",row);
            TrExport(Doc("LED硬灯条(蓝景13点白色930×16mm) L.SLDPRT"),"led_left",row);TrExport(Doc("LED硬灯条(蓝景13点白色930×16mm) R.SLDPRT"),"led_right",row);
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;row["native_save_performed"]=false;}
    }
}
''', encoding='utf-8')
tool = STAGE / 'OutdoorLampProbeR359.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/out:' + str(tool), '/reference:System.Web.Extensions.dll', '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'), '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *[str(p) for p in target.glob('*.cs')]], check=True)
(STAGE / 'scripts/run_lamp_probe_r359.mjs').write_text((STAGE / 'scripts/run_narrow_fit_probe_r350.mjs').read_text(encoding='utf-8').replace('OutdoorNarrowFitProbeR350.exe', tool.name), encoding='utf-8')
write(STAGE / 'evidence/lamp_probe_r359_build.json', {'status': 'DIAGNOSTIC_NOT_ACCEPTED', 'tool': str(tool), 'sha256': sha(tool), 'sources': {str(p): sha(p) for p in target.glob('*.cs')}})
