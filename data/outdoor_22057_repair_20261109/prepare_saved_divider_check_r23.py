"""Build a separate saved-mate reader; preserve the running generation tool."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v23'
TARGET = WORK / 'saved_divider_check_r23'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


manifest = read(STAGE / 'repair_manifest.json')
for relative, digest in manifest['identity'].items():
    assert sha(STAGE / relative) == digest, relative
assert not TARGET.exists(), 'Preserve earlier reader'
TARGET.mkdir()
source = TARGET / 'sources'
source.mkdir()
original = STAGE / 'scripts/width600_native_r440'
for path in original.glob('*.cs'):
    shutil.copy2(path, source / path.name)
for name in ('SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll'):
    shutil.copy2(STAGE / name, TARGET / name)

path = source / 'OutdoorShadeR145.cs'
text = path.read_text('utf-8-sig')
changes = {
    'Stage=Path.GetDirectoryName(typeof(OutdoorShadeR145).Assembly.Location);':
        'Stage=@"' + str(STAGE) + '";',
    'Need(new[]{"relocate","sizing_survey","layout_survey","layout_probe","generate","verify"}.Contains(mode),"Unsupported operation");':
        'Need(mode=="verify","Saved divider reader supports read-only verification only");',
    'Writable=mode=="generate"||mode=="layout_probe";': 'Writable=false;',
    'if(mode=="relocate")RelocateTask(c,row);else if(mode=="sizing_survey")SizingSurvey(c,row);else if(mode=="layout_survey")LayoutSurvey(c,row);else if(mode=="layout_probe")LayoutProbe(c,row);else {if(mode=="generate")RelocateTask(c,row);Generate(c,row,mode=="generate");}':
        'SavedDividerReadOnly(c,row);',
}
for before, after in changes.items():
    assert text.count(before) == 1, before
    text = text.replace(before, after)
path.write_text(text, 'utf-8')

(source / 'SavedDividerReadOnlyR23.cs').write_text('''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static void SavedDividerReadOnly(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Need(!Writable,"Saved divider reader must remain read-only");
        Root=(string)c["package_root"];Top=(string)c["top"];
        Need(Within(Root,Stage)&&Path.GetFileName(Root).StartsWith("generated_"),"Only an isolated generated candidate may be read");
        var before=Identity(Root);row["input_identity"]=before;
        try {
            var top=OpenTop(row);RmActivate(top,"saved_divider_independent_reopen");
            Need(top.ForceRebuild3(false),"Saved top rebuild failed");
            var owner=Doc("箱体竖隔板 焊接.SLDASM");RmActivate(owner,"saved_divider_direct_owner");
            Need(owner.IsOpenedReadOnly()&&owner.ForceRebuild3(false),"Saved divider owner must rebuild read-only");
            VerifyDividerRightRib(c,row);
            var parts=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().ToArray();
            var right=parts.Single(p=>Path.GetFileName(p.GetPathName())=="OUT_RightSideRib.SLDPRT");
            var left=parts.Single(p=>Path.GetFileName(p.GetPathName())=="箱体侧板加强筋.SLDPRT");
            Need(right.ReferencedConfiguration=="Default"&&!right.IsSuppressed()&&!right.IsHidden(true),"Saved right rib configuration or visibility changed");
            row["saved_divider_direct_owner"]=new{
                owner=owner.GetPathName(),configuration=((Configuration)owner.GetActiveConfiguration()).Name,
                read_only=owner.IsOpenedReadOnly(),health=LayoutFeatureHealth(owner),
                right=new{name=right.Name2,path=right.GetPathName(),configuration=right.ReferencedConfiguration,
                    fixed_instance=right.IsFixed(),constrained=right.GetConstrainedStatus(),mate_count=((Array)right.GetMates()).Length,
                    transform=right.Transform2.ArrayData},
                left=new{name=left.Name2,path=left.GetPathName(),configuration=left.ReferencedConfiguration,
                    fixed_instance=left.IsFixed(),constrained=left.GetConstrainedStatus(),transform=left.Transform2.ArrayData},
                mates=LayoutMates(owner),mate_signatures=DividerMateSignatures(owner,right.Name2),dimensions=Dims(owner)
            };
            row["native_save_performed"]=false;row["full_rebuild_return"]=true;row["health_pass"]=true;
            Flush("saved_divider_direct_owner_verified");
        } finally {
            Sw.CloseAllDocuments(true);CheckIdentity(Root,before);
            row["final_identity"]=Identity(Root);row["files_unchanged"]=true;
        }
    }
}
''', 'utf-8')

tool = TARGET / 'SavedDividerReadOnlyR23.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe',
           '/platform:x64', '/optimize+', '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(TARGET / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(TARGET / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (TARGET / 'build.log').open('x', encoding='utf-8') as output:
    subprocess.run(command, stdout=output, stderr=output, check=True)

runner = (STAGE / 'scripts/run_native.mjs').read_text('utf-8-sig')
stage_line = "const stage=resolve(dirname(fileURLToPath(import.meta.url)),'..');"
assert runner.count(stage_line) == 1
runner = runner.replace(stage_line, 'const stage=' + json.dumps(str(STAGE)) + ';')
old_tool = "const tool=resolve(stage,width<900?(hasShelves?'OutdoorWidth600R440.exe':'OutdoorWidth600R408.exe'):(legacyPilot?'OutdoorLayoutR188.exe':hasShelves?'OutdoorLayoutExpandedR303.exe':'OutdoorLayoutExpandedR301.exe'));"
assert runner.count(old_tool) == 1
runner = runner.replace(old_tool, 'const tool=' + json.dumps(str(tool)) + ';')
runner = runner.replace("!['relocate','sizing_survey','layout_survey','layout_probe','generate','verify'].includes(mode)", "mode!=='verify'")
runner = runner.replace("from './outdoor_22057_layout.mjs'", 'from ' + json.dumps((STAGE / 'scripts/outdoor_22057_layout.mjs').as_uri()))
runner = runner.replace("from './native_worker_lock.mjs'", 'from ' + json.dumps((STAGE / 'scripts/native_worker_lock.mjs').as_uri()))
(TARGET / 'run_saved_divider_native.mjs').write_text(runner, 'utf-8')
subprocess.run([r'C:\Program Files\nodejs\node.exe', '--check', str(TARGET / 'run_saved_divider_native.mjs')], check=True)

result = {'status': 'BUILT_RUNTIME_PENDING', 'generation_manifest_sha256': sha(STAGE / 'repair_manifest.json'),
          'generation_tool_sha256': sha(STAGE / 'OutdoorWidth600R440.exe'), 'source_changes': changes,
          'read_only': True, 'generation_sources_unchanged': True, 'build_command': command,
          'identity': {str(path.relative_to(TARGET)): sha(path) for path in TARGET.rglob('*') if path.is_file()}}
(TARGET / 'reader_manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
for relative, digest in manifest['identity'].items():
    assert sha(STAGE / relative) == digest, relative
print(json.dumps({'status': result['status'], 'tool': str(tool), 'generation_tool_unchanged': True}, ensure_ascii=False))
