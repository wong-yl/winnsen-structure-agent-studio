"""Append direct-owner evidence before the unchanged failed replacement guard."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v20'
STAGE = WORK / 'runtime_v21'
def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()
assert not STAGE.exists()
manifest = read(PARENT / 'repair_manifest.json')
STAGE.mkdir()
for relative, digest in manifest['identity'].items():
    assert sha(PARENT / relative) == digest
    target = STAGE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PARENT / relative, target)
source = STAGE / 'scripts/width600_native_r440'
target = source / 'LayoutBuildR170.cs'
text = target.read_text('utf-8-sig')
marker = '        Need(right.IsFixed()&&(right.GetMates()==null||((Array)right.GetMates()).Length==0),'
assert text.count(marker) == 1
insert = '''        row["divider_right_rib_direct_owner_probe"]=new{owner=owner.GetPathName(),configuration=((Configuration)owner.GetActiveConfiguration()).Name,component=right.Name2,fixed_instance=right.IsFixed(),constrained=constrained,mate_count=right.GetMates()==null?0:((Array)right.GetMates()).Length,mates=LayoutMates(owner),states=ShStates(owner),health=LayoutFeatureHealth(owner),original_dirty=Doc("箱体侧板加强筋.SLDPRT").GetSaveFlag(),replacement_dirty=Doc("OUT_RightSideRib.SLDPRT").GetSaveFlag()};Flush("divider_right_rib_direct_owner_probe");
'''
target.write_text(text.replace(marker, insert + marker), 'utf-8')
tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (STAGE / 'evidence/constraint_probe_r21_build.log').open('w', encoding='utf-8') as log:
    subprocess.run(command, stdout=log, stderr=log, check=True)
result = {'status': 'DIAGNOSTIC_ONLY_NOT_ACCEPTED', 'parent_manifest_sha256': sha(PARENT / 'repair_manifest.json'),
          'scope': 'Direct-owner constraint evidence only; original replacement guard unchanged',
          'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()}}
(STAGE / 'repair_manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'stage': str(STAGE), 'diagnostic_only': True}, ensure_ascii=False))
