"""Isolate saved-file STEP export in a fresh, owned, read-only native session."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v22'
STAGE = WORK / 'runtime_v23'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

diagnostic = read(WORK / 'saved_export_r23_diagnostic.json')
comparison = read(WORK / 'saved_export_r23_geometry.json')
assert diagnostic['files_unchanged'] and diagnostic['native_save_performed'] is False and not diagnostic['bad_endpoints']
assert comparison['status'] == 'PASS' and comparison['before_solids'] == comparison['after_solids'] == 399
assert not STAGE.exists(), 'Preserve previous candidate'
manifest = read(PARENT / 'repair_manifest.json')
STAGE.mkdir()
for relative, digest in manifest['identity'].items():
    assert sha(PARENT / relative) == digest, relative
    target = STAGE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PARENT / relative, target)
source = STAGE / 'scripts/width600_native_r440'
path = source / 'OutdoorShadeR145.cs'
text = path.read_text('utf-8-sig')
before = 'savedReloadIdentity=Identity(Root);CloseSavedDocumentGraph(row);Writable=false;'
after = '''savedReloadIdentity=Identity(Root);CloseSavedDocumentGraph(row);Writable=false;
                if(!row.ContainsKey("saved_reload_fresh_process_id"))RestartSavedReloadCad(row);
                Need(Sw.GetFirstDocument()==null&&Sw.GetProcessID()==Convert.ToInt32(row["saved_reload_fresh_process_id"])&&Sw.GetProcessID()!=Convert.ToInt32(row["generation_process_id"]),"Saved STEP export requires one fresh isolated CAD session");
                row["saved_reload_independent_session_required"]=true;row["saved_reload_export_process_id"]=Sw.GetProcessID();'''
assert text.count(before) == 1
path.write_text(text.replace(before, after), 'utf-8')

path = source / 'FeedbackCadBaseR145.cs'
text = path.read_text('utf-8-sig')
before = '        int e=0,w=0;Flush("export/"+label);bool ok=top.Extension.SaveAs(path,0,3,null,ref e,ref w);'
after = '''        var previous=Sw.ActiveDoc as ModelDoc2;
        string previousPath=previous==null?null:previous.GetPathName();
        int selectedBefore=((SelectionMgr)top.SelectionManager).GetSelectedObjectCount2(-1);
        RmActivate(top,"step_export_active_top");
        var active=Sw.ActiveDoc as ModelDoc2;var selection=top.SelectionManager as SelectionMgr;
        Need(active!=null&&Path.GetFullPath(active.GetPathName()).Equals(Path.GetFullPath(top.GetPathName()),StringComparison.OrdinalIgnoreCase),"STEP export target is not the active top assembly");
        Need(((Configuration)active.GetActiveConfiguration()).Name=="BOTTOM_SHORT_PIN_FIX"&&selection!=null&&selection.GetSelectedObjectCount2(-1)==0,"STEP export requires the saved top configuration and an empty selection");
        row["step_export_preflight"]=new{previous_active_document=previousPath,target=top.GetPathName(),active_document=active.GetPathName(),configuration=((Configuration)active.GetActiveConfiguration()).Name,selected_before=selectedBefore,selected_after=selection.GetSelectedObjectCount2(-1),read_only=active.IsOpenedReadOnly(),process_id=Sw.GetProcessID()};
        int e=0,w=0;Flush("export/"+label);bool ok=top.Extension.SaveAs(path,0,3,null,ref e,ref w);'''
assert text.count(before) == 1
path.write_text(text.replace(before, after), 'utf-8')

tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (STAGE / 'evidence/export_r23_build.log').open('x', encoding='utf-8') as output:
    subprocess.run(command, stdout=output, stderr=output, check=True)
changed = [{'path': relative, 'parent_sha256': digest, 'candidate_sha256': sha(STAGE / relative)}
           for relative, digest in manifest['identity'].items() if sha(STAGE / relative) != digest]
assert {r['path'].replace('\\', '/') for r in changed} == {
    'OutdoorWidth600R440.exe', 'scripts/width600_native_r440/OutdoorShadeR145.cs',
    'scripts/width600_native_r440/FeedbackCadBaseR145.cs'}
result = {'status': 'CANDIDATE_NOT_ACCEPTED', 'parent_manifest_sha256': sha(PARENT / 'repair_manifest.json'),
          'diagnostic': str(WORK / 'saved_export_r23_diagnostic.json'), 'diagnostic_sha256': sha(WORK / 'saved_export_r23_diagnostic.json'),
          'changes': changed, 'scope': 'Fresh saved-file native export session; explicit active top and empty selection',
          'save_gate_unchanged': True, 'audit_sources_unchanged': True, 'cad_forced_termination': False,
          'build_command': command, 'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()}}
(STAGE / 'repair_manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'stage': str(STAGE), 'changes': changed}, ensure_ascii=False))
