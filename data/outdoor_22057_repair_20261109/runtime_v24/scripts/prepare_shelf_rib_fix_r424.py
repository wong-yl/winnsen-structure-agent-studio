"""Restore original shelf-rib dimensions after instance replacement unloads parts."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

probe_path = STAGE / 'evidence/shelf_rib_pitch_r423_layout_probe.json'
probe = read(probe_path)
case = probe['cases'][0]
assert probe['status'] == 'layout_probe_finished' and probe['exit_app_called']
assert case['files_unchanged'] and not case.get('notch_retry_error')
assert case['right_weld']['health'] == []
assert case['after_notch_retry_shelf']['issues'] == 0

target = STAGE / 'scripts/width600_native_r424'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/width600_native_r418', target)
p = target / 'OutdoorShadeR145.cs'
text = p.read_text(encoding='utf-8')
key = 'var shelf=Doc("箱体横层板L.SLDPRT");SyncNarrowShelfWidth(shelf,Convert.ToDouble(((System.Collections.IList)row["dimensions_mm"])[1]),shelfChanges,row);'
assert text.count(key) == 1
replacement = ('double width=Convert.ToDouble(((System.Collections.IList)row["dimensions_mm"])[1]);'
    'var rib=Doc("箱体横层板加强筋.SLDPRT");'
    'Set(rib,"D1@草图1",width/2-7,shelfChanges);'
    'Set(rib,"D4@草图14",width/2-175,shelfChanges);'
    'Need(rib.ForceRebuild3(false)&&rib.Extension.GetWhatsWrongCount()==0,"Final shelf rib linkage failed");'
    'var shelf=Doc("箱体横层板L.SLDPRT");SyncNarrowShelfWidth(shelf,width,shelfChanges,row);')
p.write_text(text.replace(key, replacement), encoding='utf-8')
tool = STAGE / 'OutdoorWidth600R424.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo',
    '/target:exe', '/platform:x64', '/optimize+', '/out:' + str(tool),
    '/reference:System.Web.Extensions.dll', '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
    '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, target.glob('*.cs'))], check=True)
runner = STAGE / 'scripts/run_native.mjs'
text = runner.read_text(encoding='utf-8')
key = "hasShelves?'OutdoorWidth600R418.exe':'OutdoorWidth600R408.exe'"
assert text.count(key) == 1
runner.write_text(text.replace(key, "hasShelves?'OutdoorWidth600R424.exe':'OutdoorWidth600R408.exe'"), encoding='utf-8')
write(STAGE / 'evidence/shelf_rib_fix_r424_build.json', {
    'status': 'CANDIDATE_NOT_ACCEPTED', 'tool': str(tool), 'sha256': sha(tool),
    'sources': {str(p): sha(p) for p in target.glob('*.cs')},
    'local_probe': str(probe_path), 'local_probe_sha256': sha(probe_path),
    'preserved_failures': ['width600_high_r411', 'width600_high_r419'],
    'runner_sha256': sha(runner), 'original_mates_preserved': True})
print('Built R424; full native acceptance pending')
