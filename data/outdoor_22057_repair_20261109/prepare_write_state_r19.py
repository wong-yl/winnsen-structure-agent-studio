"""Build the smallest write-state repair after the evidence-only failure is known."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v18'
STAGE = WORK / 'runtime_v19'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')

diagnostic_path = PARENT / 'evidence/save_probe_r18_generate.json'
diagnostic = read(diagnostic_path)
assert diagnostic['status'] == 'failed' and 'Refuse save outside writable copy' in diagnostic['error']
blocked = [row for row in diagnostic['cases'][0]['save_preflight_documents']
           if row['save_flag'] and not row['fixed_purchased']
           and not (row['opened_read_only'] and row['readonly_audit_hit'])
           and (row['opened_read_only'] or not row['within_root'])]
assert blocked and all(row['opened_read_only'] and row['within_root'] and 'ReadOnly' not in row['file_attributes'] for row in blocked)
assert not STAGE.exists(), 'Preserve all existing candidates'
manifest = read(PARENT / 'diagnostic_manifest.json')
STAGE.mkdir()
for relative, digest in manifest['identity'].items():
    source = PARENT / relative
    assert sha(source) == digest, relative
    dest = STAGE / relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, dest)

source_dir = STAGE / 'scripts/width600_native_r440'
target = source_dir / 'FeedbackCadBaseR145.cs'
text = target.read_text('utf-8-sig')
old_save = text[text.index('    static void SaveDocs('):text.index('    static void Persist(')]
old_save_bytes = target.read_bytes().split(b'    static void SaveDocs(', 1)[1].split(b'    static void Persist(', 1)[0]
helper = '''    static void RequireCandidateWriteState(ModelDoc2 document,string reason) {
        if(!Writable||document==null)return;
        string path=document.GetPathName();
        Need(Within(path,Root)&&File.Exists(path),"Write access requires an existing isolated candidate");
        Need(Path.GetDirectoryName(Path.GetFullPath(Root)).Equals(Path.GetFullPath(Stage),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(Root).StartsWith("generated_",StringComparison.Ordinal),"Write access requires a fresh generation root");
        if(FixedPurchasedDocument(document)||ReadOnlyAuditPaths.Contains(path))return;
        Need(path.EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".SLDASM",StringComparison.OrdinalIgnoreCase),"Write access is native candidate only");
        Need((File.GetAttributes(path)&FileAttributes.ReadOnly)==0,"Candidate file remains read-only on disk");
        bool before=document.IsOpenedReadOnly();if(!before)return;
        bool saveFlag=document.GetSaveFlag(),changed=document.SetReadOnlyState(false),after=document.IsOpenedReadOnly();
        List<object> records;if(E.ContainsKey("candidate_write_access"))records=(List<object>)E["candidate_write_access"];else{records=new List<object>();E["candidate_write_access"]=records;}
        records.Add(new{path=path,reason=reason,read_only_before=before,set_read_only_result=changed,actual_read_only=after,save_flag_before=saveFlag,save_flag_after=document.GetSaveFlag(),within_root=Within(path,Root)});
        Flush("candidate_write_access/"+Path.GetFileName(path));
        Need(changed&&!after,"Native candidate write access was refused: "+path);
    }
    static void RequireCandidateWriteGraph(string reason) {
        if(!Writable)return;LoadedInside();
        var documents=new List<ModelDoc2>();for(ModelDoc2 d=Sw.GetFirstDocument() as ModelDoc2;d!=null;d=d.GetNext() as ModelDoc2)documents.Add(d);
        foreach(var document in documents)RequireCandidateWriteState(document,reason);
    }
'''
marker = '    static HashSet<string> ReadOnlyAuditPaths='
assert text.count(marker) == 1
text = text.replace(marker, helper + marker)
marker = '        d.ShowFeatureErrorDialog = false; LoadedInside();'
assert text.count(marker) == 1
text = text.replace(marker, marker + 'RequireCandidateWriteGraph("open_top_resolved_references");')
marker = '        Need(d!=null&&e==0&&d.IsOpenedReadOnly()!=writableMode,"Cannot open required packaged document "+file);'
assert text.count(marker) == 1
text = text.replace(marker, '        if(d!=null&&e==0&&writableMode)RequireCandidateWriteState(d,"open_required");\n' + marker)
marker = 'static ModelDoc2 Doc(string name) {string file=Path.Combine(Root,name);return Sw.GetOpenDocumentByName(file) as ModelDoc2??OpenRequired(file);}'
assert text.count(marker) == 1
text = text.replace(marker, 'static ModelDoc2 Doc(string name) {string file=Path.Combine(Root,name);var document=Sw.GetOpenDocumentByName(file) as ModelDoc2??OpenRequired(file);RequireCandidateWriteState(document,"document_access");return document;}')
assert text[text.index('    static void SaveDocs('):text.index('    static void Persist(')] == old_save
target.write_text(text, 'utf-8')
assert target.read_bytes().split(b'    static void SaveDocs(', 1)[1].split(b'    static void Persist(', 1)[0] == old_save_bytes

target = source_dir / 'LayoutBuildR170.cs'
text = target.read_text('utf-8-sig')
marker = '            Need(loaded!=null&&errors==0&&!loaded.IsOpenedReadOnly(),"Cannot reload module unloaded after original-instance suppression");'
assert text.count(marker) == 1
text = text.replace(marker, '            if(loaded!=null&&errors==0)RequireCandidateWriteState(loaded,"layout_module_reload");\n' + marker)
marker = '        RmActivate(owner,"layout_add/"+label);'
assert text.count(marker) == 1
text = text.replace(marker, '        RequireCandidateWriteState(Sw.GetOpenDocumentByName(path) as ModelDoc2,"layout_module_access");\n' + marker)
target.write_text(text, 'utf-8')

target = source_dir / 'OutdoorShadeR145.cs'
text = target.read_text('utf-8-sig')
marker = 'row["preaudit_instance_counts"]=InstanceBodyCounts(top);SaveDocs(top,row);'
assert text.count(marker) == 1
text = text.replace(marker, 'row["preaudit_instance_counts"]=InstanceBodyCounts(top);RequireCandidateWriteGraph("preaudit_save_loaded_references");SaveDocs(top,row);')
marker = '    static int Main(string[] args)'
assert text.count(marker) == 1
text = text.replace(marker, '    [STAThread]\n' + marker)
marker = '        bool owned=false;'
assert text.count(marker) == 1
text = text.replace(marker, '        E["apartment_state"]=System.Threading.Thread.CurrentThread.GetApartmentState().ToString();\n' + marker)
target.write_text(text, 'utf-8')

tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, source_dir.glob('*.cs'))]
with (STAGE / 'evidence/write_state_r19_build.log').open('w', encoding='utf-8') as log:
    subprocess.run(command, stdout=log, stderr=log, check=True)
changed = [{'path': rel, 'parent_sha256': digest, 'candidate_sha256': sha(STAGE / rel)}
           for rel, digest in manifest['identity'].items() if sha(STAGE / rel) != digest]
assert {row['path'].replace('\\', '/') for row in changed} == {
    'OutdoorWidth600R440.exe', 'scripts/width600_native_r440/FeedbackCadBaseR145.cs',
    'scripts/width600_native_r440/LayoutBuildR170.cs', 'scripts/width600_native_r440/OutdoorShadeR145.cs'}
write(STAGE / 'repair_manifest.json', {
    'status': 'CANDIDATE_NOT_ACCEPTED', 'diagnostic': str(diagnostic_path), 'diagnostic_sha256': sha(diagnostic_path),
    'parent_manifest_sha256': sha(PARENT / 'diagnostic_manifest.json'), 'changes': changed,
    'scope': 'Establish actual write access only for fresh local native candidates before generation persistence; STA entry; SaveDocs unchanged',
    'save_gate_unchanged': True, 'audit_sources_unchanged': True, 'forced_termination': False,
    'build_command': command,
    'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()},
})
print(json.dumps({'stage': str(STAGE), 'changes': changed, 'gate_changed': False}, ensure_ascii=False))
