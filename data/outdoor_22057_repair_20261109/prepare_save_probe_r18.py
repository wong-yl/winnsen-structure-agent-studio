"""Add evidence only to a fresh native candidate; preserve all previous runs."""
import hashlib
import json
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v17'
STAGE = WORK / 'runtime_v18'
PYTHON = r'C:\Users\Administrator\AppData\Local\Programs\Python\Python310\python.exe'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')

assert not STAGE.exists(), 'Preserve existing diagnostic candidate'
qualification = read(PARENT / 'qualification.json')
for relative, digest in qualification['support_sha256'].items():
    assert sha(PARENT / relative) == digest, relative
for absolute, digest in qualification['external_support_sha256'].items():
    assert sha(absolute) == digest, absolute
STAGE.mkdir()
for relative in qualification['support_sha256']:
    dest = STAGE / relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PARENT / relative, dest)
for folder, pattern in [('evidence', '*.json'), ('scripts', '*.py')]:
    for source in (PARENT / folder).glob(pattern):
        # This run's failed task is frozen in its original directory.
        if source.name.startswith('portal_5a052ec02402432b9bd6ef78117f37d4'):
            continue
        dest = STAGE / folder / source.name
        if not dest.exists():
            shutil.copy2(source, dest)
for source in PARENT.glob('*.json'):
    if source.name != 'qualification.json' and not (STAGE / source.name).exists():
        shutil.copy2(source, STAGE / source.name)
shutil.copy2(PARENT / 'qualification.json', STAGE / 'parent_qualification.json')

target = STAGE / 'scripts/width600_native_r440/FeedbackCadBaseR145.cs'
original = target.read_text('utf-8-sig')
key = '        var saves=new List<object>();row["saved_documents"]=saves;'
assert original.count(key) == 1
addition = '''
        var saveChecks=new List<object>();row["save_preflight_documents"]=saveChecks;
        row["save_preflight_root"]=Root;row["save_preflight_readonly_audit_paths"]=ReadOnlyAuditPaths.ToArray();
        foreach(ModelDoc2 candidate in docs.OrderBy(d=>d.GetPathName()==top.GetPathName()?2:d is AssemblyDoc?1:0)) {
            string path=candidate.GetPathName();bool exists=File.Exists(path);
            saveChecks.Add(new{path=path,full_path=Path.GetFullPath(path),root=Root,root_full_path=Path.GetFullPath(Root),
                save_flag=candidate.GetSaveFlag(),opened_read_only=candidate.IsOpenedReadOnly(),within_root=Within(path,Root),
                readonly_audit_hit=ReadOnlyAuditPaths.Contains(path),fixed_purchased=FixedPurchasedDocument(candidate),
                file_exists=exists,file_attributes=exists?File.GetAttributes(path).ToString():null});
        }
        Flush("persist/save_preflight");'''
target.write_text(original.replace(key, key + addition), 'utf-8')
assert target.read_text('utf-8').replace(addition, '') == original
assert 'Stop-Process' not in (STAGE / 'scripts/cleanup_owned_residual_poll_r4.ps1').read_text('utf-8-sig')
tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'),
           *map(str, (target.parent).glob('*.cs'))]
with (STAGE / 'evidence/save_probe_r18_build.log').open('w', encoding='utf-8') as log:
    subprocess.run(command, stdout=log, stderr=log, check=True)
changes = [{'path': rel, 'parent_sha256': digest, 'candidate_sha256': sha(STAGE / rel)}
           for rel, digest in qualification['support_sha256'].items() if sha(STAGE / rel) != digest]
assert {x['path'].replace('\\', '/') for x in changes} == {
    'OutdoorWidth600R440.exe', 'scripts/width600_native_r440/FeedbackCadBaseR145.cs'}
write(STAGE / 'diagnostic_manifest.json', {
    'status': 'DIAGNOSTIC_NOT_ACCEPTED', 'created_utc': datetime.now(timezone.utc).isoformat(),
    'parent_qualification': str(PARENT / 'qualification.json'), 'parent_sha256': sha(PARENT / 'qualification.json'),
    'scope': 'SaveDocs observation only; original guards and all other sources unchanged',
    'changes': changes, 'build_command': command,
    'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()},
})
task = read(WORK.parent / 'outdoor_22057_tasks/OUT22057-5a052ec0-2402-432b-9bd6-ef78117f37d4.json')
spec = STAGE / 'evidence/save_probe_r18_specification.json'
write(spec, task['specification'])
subprocess.run([PYTHON, '-B', str(STAGE / 'scripts/prepare_layout_case.py'), str(spec),
                'probe_22057_1939_860_550_l7_r9_r18'], check=True)
print(json.dumps({'stage': str(STAGE), 'changes': changes, 'gate_changed': False}, ensure_ascii=False))
