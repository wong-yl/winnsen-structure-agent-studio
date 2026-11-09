"""Persist the isolated graph before packaged-document audits can reload it."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

failure=read(STAGE/'evidence/width600_high_r426_save_generate.json')
assert failure['status']=='failed' and 'actual=527 invalid=0' in failure['error']
target=STAGE/'scripts/width600_native_r430';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r424',target)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='RefreshLayoutAssemblyGraph(top,row);}row["snapshot"]=Snapshot(top,false);'
assert t.count(key)==1
t=t.replace(key,'RefreshLayoutAssemblyGraph(top,row);row["preaudit_instance_counts"]=InstanceBodyCounts(top);SaveDocs(top,row);row["preaudit_candidate_saved_not_accepted"]=true;Flush("preaudit_candidate_saved_not_accepted");}row["snapshot"]=Snapshot(top,false);')
p.write_text(t,encoding='utf-8')
p=target/'FeedbackCadBaseR145.cs';t=p.read_text(encoding='utf-8')
key='Need(total == ExpectedVisibleSolids && invalid == 0, "Expected "'
assert t.count(key)==1
t=t.replace(key,'if(total!=ExpectedVisibleSolids||invalid!=0){E["failed_snapshot_instances"]=instances;E["failed_snapshot_documents"]=docs;Flush("snapshot_failed_instance_accounting");}\n        '+key)
p.write_text(t,encoding='utf-8')
(target/'InstanceBodyCountsR430.cs').write_text('''using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object InstanceBodyCounts(ModelDoc2 top){return ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().Where(c=>c.GetPathName().EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase)).Select(c=>{object info;var bodies=c.IsSuppressed()?null:c.GetBodies3(0,out info) as Array;return new{name=c.Name2,document=Path.GetFileName(c.GetPathName()),suppressed=c.IsSuppressed(),hidden=c.IsHidden(true),bodies=bodies==null?0:bodies.Length};}).ToArray();}
}
''',encoding='utf-8')
tool=STAGE/'OutdoorWidth600R430.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
p=STAGE/'scripts/run_native.mjs';t=p.read_text(encoding='utf-8');assert t.count('OutdoorWidth600R424.exe')==1
p.write_text(t.replace('OutdoorWidth600R424.exe',tool.name),encoding='utf-8')
p=STAGE/'scripts/complete_narrow_high_r432.py';assert not p.exists()
p.write_text((STAGE/'scripts/complete_narrow_high_r426.py').read_text(encoding='utf-8').replace('r426','r432').replace("['width600_high_r411', 'width600_high_r419']","['width600_high_r411', 'width600_high_r419', 'width600_high_r426']"),encoding='utf-8')
write(STAGE/'evidence/preaudit_save_r430_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')},'preserved_failure':'width600_high_r426','thresholds_unchanged':True})
