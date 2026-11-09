"""Release the owned old CAD interface before waiting for its natural exit."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v23'
STAGE = WORK / 'runtime_v24'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

for name in ('restart_release_empty_r24_diagnostic', 'restart_release_loaded_r24_diagnostic'):
    path = PARENT / 'evidence' / (name + '_verify.json')
    evidence = read(path)
    row = evidence['cases'][0]
    assert evidence['status'] == 'verify_finished' and evidence['exit_app_called']
    assert row['files_unchanged'] and row['input_identity'] == row['final_identity']
    assert row['owned_application_reference_release_count'] == 0 and row['old_application_reference_released_before_wait']
    assert row['saved_reload_fresh_process_id'] == evidence['process_id'] != row['generation_process_id']
    assert not read(str(path) + '.after.json')['processes']
assert not STAGE.exists()
manifest = read(PARENT / 'repair_manifest.json')
STAGE.mkdir()
for relative, digest in manifest['identity'].items():
    assert sha(PARENT / relative) == digest, relative
    path = STAGE / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PARENT / relative, path)
source = STAGE / 'scripts/width600_native_r440'
path = source / 'SavedReloadSessionR301.cs'
text = path.read_text('utf-8-sig')
before = 'File.WriteAllText(exitPath,Json.Serialize(receipt),new UTF8Encoding(false));Sw=null;'
after = '''File.WriteAllText(exitPath,Json.Serialize(receipt),new UTF8Encoding(false));
        var oldApplication=Sw;Sw=null;
        row["owned_application_reference_release_count"]=System.Runtime.InteropServices.Marshal.ReleaseComObject(oldApplication);oldApplication=null;
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        row["old_application_reference_released_before_wait"]=true;Flush("saved_reload_old_application_reference_released");'''
assert text.count(before) == 1
path.write_text(text.replace(before, after), 'utf-8')
path = source / 'OutdoorShadeR145.cs'
text = path.read_text('utf-8-sig')
before = 'savedReloadIdentity=Identity(Root);CloseSavedDocumentGraph(row);Writable=false;'
after = 'savedReloadIdentity=Identity(Root);top=null;CloseSavedDocumentGraph(row);Writable=false;'
assert text.count(before) == 1
text = text.replace(before, after)
before = '}finally{Sw.CloseAllDocuments(true);Writable=initialWritable;'
after = '}finally{if(Sw!=null)Sw.CloseAllDocuments(true);Writable=initialWritable;'
assert text.count(before) == 1
path.write_text(text.replace(before, after), 'utf-8')
tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (STAGE / 'evidence/lifecycle_r24_build.log').open('x', encoding='utf-8') as output:
    subprocess.run(command, stdout=output, stderr=output, check=True)
changed = [{'path': rel, 'parent_sha256': digest, 'candidate_sha256': sha(STAGE / rel)}
           for rel, digest in manifest['identity'].items() if sha(STAGE / rel) != digest]
assert {r['path'].replace('\\', '/') for r in changed} == {'OutdoorWidth600R440.exe',
    'scripts/width600_native_r440/OutdoorShadeR145.cs', 'scripts/width600_native_r440/SavedReloadSessionR301.cs'}
result = {'status': 'CANDIDATE_NOT_ACCEPTED', 'parent_manifest_sha256': sha(PARENT / 'repair_manifest.json'),
          'changes': changed, 'scope': 'Release only owned old native application interface before unchanged natural-exit wait; clear obsolete top reference; preserve primary failure when Sw is null',
          'save_gate_unchanged': True, 'audit_sources_unchanged': True, 'cad_forced_termination': False,
          'build_command': command, 'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()}}
(STAGE / 'repair_manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'stage': str(STAGE), 'changes': changed}, ensure_ascii=False))
