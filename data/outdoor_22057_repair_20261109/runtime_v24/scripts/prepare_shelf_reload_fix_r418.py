"""Reapply narrow shelf profiles after original instances have unloaded them."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

probe=read(STAGE/'evidence/shelf_reload_r417_layout_probe.json')
assert probe['status']=='layout_probe_finished' and probe['cases'][0]['files_unchanged']
assert probe['cases'][0]['after_notch_retry_shelf']['rebuilt']
assert probe['cases'][0]['after_notch_retry_shelf']['issues']==0
assert not probe['cases'][0].get('notch_retry_error')
target=STAGE/'scripts/width600_native_r418';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r409',target)
p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8')
key='var shelfChanges=new List<object>();row["final_shelf_notch_changes"]=shelfChanges;SyncShelfNotchHeight(Doc("箱体横层板L.SLDPRT"),shelfChanges,row);'
assert text.count(key)==1
replacement='var shelfChanges=new List<object>();row["final_shelf_notch_changes"]=shelfChanges;var shelf=Doc("箱体横层板L.SLDPRT");SyncNarrowShelfWidth(shelf,Convert.ToDouble(((System.Collections.IList)row["dimensions_mm"])[1]),shelfChanges,row);SyncShelfNotchHeight(shelf,shelfChanges,row);'
p.write_text(text.replace(key,replacement),encoding='utf-8')
tool=STAGE/'OutdoorWidth600R418.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
runner=STAGE/'scripts/run_native.mjs';text=runner.read_text(encoding='utf-8')
key="hasShelves?'OutdoorWidth600R409.exe':'OutdoorWidth600R408.exe'";assert text.count(key)==1
runner.write_text(text.replace(key,"hasShelves?'OutdoorWidth600R418.exe':'OutdoorWidth600R408.exe'"),encoding='utf-8')
write(STAGE/'evidence/shelf_reload_fix_r418_build.json',{'status':'CANDIDATE_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')},'local_probe':str(STAGE/'evidence/shelf_reload_r417_layout_probe.json'),'local_probe_sha256':sha(STAGE/'evidence/shelf_reload_r417_layout_probe.json'),'preserved_failure':str(STAGE/'evidence/width600_high_r411_save_generate.json'),'runner_sha256':sha(runner)})
