"""Test releasing only the owned old application reference before natural exit."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'restart_diagnostic_r24'
TARGET = WORK / 'restart_release_diagnostic_r24'
assert not TARGET.exists()
shutil.copytree(PARENT, TARGET)
source = TARGET / 'sources'
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
path = source / 'RestartDiagnosticR24.cs'
text = path.read_text('utf-8-sig')
text = text.replace('RestartSavedReloadCad(row);', '''if(c.ContainsKey("diagnostic_load_top")&&Convert.ToBoolean(c["diagnostic_load_top"])) {
                var top=OpenTop(row);Need(top.IsOpenedReadOnly()&&top.ForceRebuild3(false),"Read-only load failed");
                row["loaded_top_read_only"]=true;row["documents_opened"]=1;
                top=null;Sw.CloseAllDocuments(true);Need(Sw.GetFirstDocument()==null,"Diagnostic documents did not close");
            }
            RestartSavedReloadCad(row);''')
path.write_text(text, 'utf-8')
tool = TARGET / 'RestartReleaseDiagnosticR24.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(TARGET / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(TARGET / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (TARGET / 'release_build.log').open('x', encoding='utf-8') as output:
    subprocess.run(command, stdout=output, stderr=output, check=True)
runner = (TARGET / 'run_restart_diagnostic.mjs').read_text('utf-8-sig')
runner = runner.replace(str(PARENT / 'RestartDiagnosticR24.exe').replace('\\', '\\\\'), str(tool).replace('\\', '\\\\'))
(TARGET / 'run_release_diagnostic.mjs').write_text(runner, 'utf-8')
subprocess.run([r'C:\Program Files\nodejs\node.exe', '--check', str(TARGET / 'run_release_diagnostic.mjs')], check=True)
manifest = {'status': 'BUILT_RUNTIME_PENDING', 'read_only': True, 'native_save_performed': False,
            'scope': 'Release only owned application RCW after ExitApp; collect unreachable wrappers, no forced CAD termination',
            'identity': {str(p.relative_to(TARGET)): hashlib.sha256(p.read_bytes()).hexdigest() for p in TARGET.rglob('*') if p.is_file()}}
(TARGET / 'release_diagnostic_manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'tool': str(tool)}, ensure_ascii=False))
