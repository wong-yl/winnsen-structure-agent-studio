"""Read-only acceptance of completed task and original identities; never promotes a task."""
import hashlib
import json
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(r'D:\Winnsen_Structure_Agent_Studio')
WORK = Path(__file__).resolve().parent
TASK = 'OUT22057-5a052ec0-2402-432b-9bd6-ef78117f37d4'
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,v): Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2),'utf-8')
task = read(ROOT / 'data/outdoor_22057_tasks' / (TASK+'.json'))
assert task['state'] == 'ready' and task['artifact'], 'Task not accepted; do not promote'
result = read(ROOT/'data/outdoor_22057_release/generated'/TASK/'result.json')
native = read(result['native_result'])
assert result['status'] == native['status'] == 'PASS'
assert task['specification'] == result['specification'] == native['specification']
archive = ROOT/'data/outdoor_22057_release'/task['artifact']['archive']
assert sha(archive) == task['artifact']['sha256'] == result['sha256']
assert archive.stat().st_size == result['bytes'] == task['artifact']['bytes']
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    manifest = json.loads(z.read('manifest.json'))
    for name, identity in manifest.items():
        data = z.read(name)
        assert len(data) == identity['bytes'] and hashlib.sha256(data).hexdigest() == identity['sha256'], name
phases = []
for p in native['native_phases']:
    e=read(p)
    assert e['exit_app_called'] and e['status'] in ('generate_finished','verify_finished')
    phases.append({'path':p,'sha256':sha(p),'status':e['status'],'process_id':e['process_id'],'exit_app_called':e['exit_app_called']})
assert len({p['process_id'] for p in phases}) == 3
audits=[]
for p in native['checks']:
    e=read(p);assert e['status']=='PASS',p
    audits.append({'path':p,'sha256':sha(p),'status':e['status']})
changes=[]
original=read(WORK/'original_identity.json')
for p, expected in original.items():
    actual=sha(p)
    if actual!=expected:changes.append({'path':p,'before':expected,'after':actual})
assert not changes,'Original/seed/shared inputs changed'
identity=read(WORK/'task_identity.json')
assert sha(ROOT/'data/outdoor_22057_tasks'/(identity['original_task']+'.json'))==identity['original_task_sha256']
cmd="@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count"
count=int(subprocess.check_output(['powershell.exe','-NoProfile','-Command',cmd],text=True).strip())
assert count==0, 'CAD still running'
summary={'status':'PASS','task_id':TASK,'specification':task['specification'],'archive':str(archive),'archive_sha256':sha(archive),'archive_bytes':archive.stat().st_size,'manifest_entries':len(manifest),'native_documents':native['native_documents'],'visible_solids':native['visible_solids'],'native_phases':phases,'audits':audits,'original_paths_checked':len(original),'original_changes':changes,'original_failed_task_unchanged':True,'final_sldworks_count':count,'production_release':False}
write(WORK/'acceptance.json',summary)
print(json.dumps({k:v for k,v in summary.items() if k not in ('native_phases','audits','specification')},ensure_ascii=False))
