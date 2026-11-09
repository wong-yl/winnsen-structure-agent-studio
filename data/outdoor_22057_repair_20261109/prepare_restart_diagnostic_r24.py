"""Build a bounded native lifecycle diagnostic without opening or modifying CAD."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v23'
TARGET = WORK / 'restart_diagnostic_r24'

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

assert not TARGET.exists()
manifest = read(STAGE / 'repair_manifest.json')
for relative, digest in manifest['identity'].items():
    assert sha(STAGE / relative) == digest, relative
TARGET.mkdir()
source = TARGET / 'sources'
source.mkdir()
for path in (STAGE / 'scripts/width600_native_r440').glob('*.cs'):
    shutil.copy2(path, source / path.name)
for name in ('SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll'):
    shutil.copy2(STAGE / name, TARGET / name)
path = source / 'OutdoorShadeR145.cs'
text = path.read_text('utf-8-sig')
changes = {
    'Stage=Path.GetDirectoryName(typeof(OutdoorShadeR145).Assembly.Location);': 'Stage=@"' + str(STAGE) + '";',
    'Need(new[]{"relocate","sizing_survey","layout_survey","layout_probe","generate","verify"}.Contains(mode),"Unsupported operation");': 'Need(mode=="verify","Lifecycle diagnostic supports read-only mode only");',
    'Writable=mode=="generate"||mode=="layout_probe";': 'Writable=false;',
    'if(mode=="relocate")RelocateTask(c,row);else if(mode=="sizing_survey")SizingSurvey(c,row);else if(mode=="layout_survey")LayoutSurvey(c,row);else if(mode=="layout_probe")LayoutProbe(c,row);else {if(mode=="generate")RelocateTask(c,row);Generate(c,row,mode=="generate");}': 'RestartDiagnostic(c,row);',
}
for before, after in changes.items():
    assert text.count(before) == 1
    text = text.replace(before, after)
path.write_text(text, 'utf-8')
(source / 'RestartDiagnosticR24.cs').write_text('''using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void RestartDiagnostic(Dictionary<string,object> c,Dictionary<string,object> row) {
        Need(!Writable&&Sw.GetFirstDocument()==null,"Only an empty read-only native lifecycle may be tested");
        Root=(string)c["package_root"];Top=(string)c["top"];
        var before=Identity(Root);row["input_identity"]=before;row["documents_opened"]=0;
        try {
            RestartSavedReloadCad(row);
            Need(Sw.GetFirstDocument()==null,"Lifecycle test opened an unexpected document");
            row["native_save_performed"]=false;
        } catch(Exception ex) {
            row["restart_primary_exception"]=ex.ToString();Flush("restart_primary_exception_captured");throw;
        } finally {
            CheckIdentity(Root,before);row["final_identity"]=Identity(Root);row["files_unchanged"]=true;
        }
    }
}
''', 'utf-8')
tool = TARGET / 'RestartDiagnosticR24.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64',
           '/optimize+', '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(TARGET / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(TARGET / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (TARGET / 'build.log').open('x', encoding='utf-8') as output:
    subprocess.run(command, stdout=output, stderr=output, check=True)
runner = (STAGE / 'scripts/run_native.mjs').read_text('utf-8-sig')
runner = runner.replace("const stage=resolve(dirname(fileURLToPath(import.meta.url)),'..');", 'const stage=' + json.dumps(str(STAGE)) + ';')
old_tool = "const tool=resolve(stage,width<900?(hasShelves?'OutdoorWidth600R440.exe':'OutdoorWidth600R408.exe'):(legacyPilot?'OutdoorLayoutR188.exe':hasShelves?'OutdoorLayoutExpandedR303.exe':'OutdoorLayoutExpandedR301.exe'));"
assert runner.count(old_tool) == 1
runner = runner.replace(old_tool, 'const tool=' + json.dumps(str(tool)) + ';')
runner = runner.replace("!['relocate','sizing_survey','layout_survey','layout_probe','generate','verify'].includes(mode)", "mode!=='verify'")
for name in ('outdoor_22057_layout.mjs', 'native_worker_lock.mjs'):
    runner = runner.replace("from './" + name + "'", 'from ' + json.dumps((STAGE / 'scripts' / name).as_uri()))
(TARGET / 'run_restart_diagnostic.mjs').write_text(runner, 'utf-8')
subprocess.run([r'C:\Program Files\nodejs\node.exe', '--check', str(TARGET / 'run_restart_diagnostic.mjs')], check=True)
result = {'status': 'BUILT_RUNTIME_PENDING', 'purpose': 'No-document process lifecycle with original exception preserved',
          'read_only': True, 'documents_opened': 0, 'native_save_performed': False,
          'generation_manifest_sha256': sha(STAGE / 'repair_manifest.json'), 'build_command': command,
          'identity': {str(p.relative_to(TARGET)): sha(p) for p in TARGET.rglob('*') if p.is_file()}}
(TARGET / 'diagnostic_manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'tool': str(tool), 'source_unchanged': True}, ensure_ascii=False))
