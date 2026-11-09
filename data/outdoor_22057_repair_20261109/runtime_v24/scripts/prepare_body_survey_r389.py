"""Throwaway inventory diagnosis only; does not save or accept the model."""
import shutil
import subprocess
from generate_model import STAGE,sha,write
target=STAGE/'scripts/body_survey_r389';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r384',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='else if(mode=="layout_probe")LayoutProbe(c,row);'
assert t.count(key)==1
t=t.replace(key,'else if(mode=="layout_probe"){RelocateTask(c,row);BodySurvey(c,row);}')
p.write_text(t,encoding='utf-8')
(target/'BodySurveyR389.cs').write_text(r'''using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void BodySurvey(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];PrepareLayout(c,row);var before=Identity(Root);
        try {
            var top=OpenTop(row);var dims=((IEnumerable)c["dimensions_mm"]).Cast<object>().Select(Convert.ToDouble).ToArray();
            ApplyInputs(top,dims[0],dims[1],dims[2],row);BuildLayout(top,c,row);BuildLayoutFeatures(c,row);RebuildAll(top,row);
            var rows=new List<object>();row["component_body_inventory"]=rows;int total=0;
            foreach(var v in ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().OrderBy(v=>v.Name2)) {
                if(v.IsSuppressed()||v.IsHidden(true)||!v.GetPathName().EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase))continue;
                object info;var raw=v.GetBodies3(0,out info) as Array;
                var bodies=(raw??new object[0]).Cast<Body2>().Select(b=>new{name=b.Name,visible=b.Visible,check=b.Check2(),mass=b.GetMassProperties(1),box=b.GetBodyBox(),faces=b.GetFaceCount(),edges=b.GetEdgeCount()}).ToArray();
                total+=bodies.Length;rows.Add(new{instance=v.Name2,document=Path.GetFileName(v.GetPathName()),configuration=v.ReferencedConfiguration,bodies=bodies});Flush("body_inventory/"+v.Name2);
            }
            row["actual_visible_solids"]=total;row["expected_visible_solids"]=ExpectedVisibleSolids;row["health"]=TrAssemblyHealth();row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorBodySurveyR389.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_body_survey_r389.mjs').write_text((STAGE/'scripts/run_rib_source_probe_r382.mjs').read_text(encoding='utf-8').replace('OutdoorRibSourceProbeR382.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/body_survey_r389_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
