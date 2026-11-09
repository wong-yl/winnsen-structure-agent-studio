"""Preserve r308 failures; update narrow cover openings before shrinking their sheet."""
import shutil
import subprocess
from generate_model import STAGE, sha, write

builds = []
for base, revision in [('expanded_native_r301', 309), ('expanded_native_r303', 310)]:
    source = STAGE / 'scripts' / base
    target = STAGE / 'scripts' / ('width600_native_r' + str(revision))
    assert not target.exists()
    shutil.copytree(source, target)
    path = target / 'OutdoorShadeR145.cs'
    text = path.read_text(encoding='utf-8-sig')
    needle = 'Set(master,"D1@草图1",w,changes);'
    assert text.count(needle) == 1
    text = text.replace(needle, 'if(w<900)SyncCoverSpan(w,changes,row);\n        ' + needle)
    path.write_text(text, encoding='utf-8')
    tool = STAGE / ('OutdoorWidth600R' + str(revision) + '.exe')
    assert not tool.exists()
    command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
               '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
               '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
               '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *[str(p) for p in target.glob('*.cs')]]
    subprocess.run(command, check=True)
    builds.append({'tool': str(tool), 'sha256': sha(tool), 'baseline': base,
                   'sources': {str(p): sha(p) for p in target.glob('*.cs')},
                   'change': 'W<900: shrink cover openings before shrinking cabinet master; otherwise unchanged'})
runner = STAGE / 'scripts/run_native.mjs'
text = runner.read_text(encoding='utf-8')
assert text.count("const tool=resolve(stage,") == 1
text = text.replace("const tool=resolve(stage,", "const narrow=width<900;\nconst tool=resolve(stage,")
text = text.replace("?'OutdoorLayoutExpandedR303.exe':'OutdoorLayoutExpandedR301.exe'", "?(narrow?'OutdoorWidth600R310.exe':'OutdoorLayoutExpandedR303.exe'):(narrow?'OutdoorWidth600R309.exe':'OutdoorLayoutExpandedR301.exe')")
runner.write_text(text, encoding='utf-8')
write(STAGE / 'evidence/native_width600_r309_build.json', {'status': 'BUILT_NOT_ACCEPTED', 'builds': builds})
