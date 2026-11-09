"""Reopen the downloaded layout while its original generation folder is absent."""
import argparse
import hashlib
import json
import stat
import zipfile
from pathlib import Path
from generate_model import STAGE, PYTHON, NODE, read, run, sha, write
from generate_layout_model import counts
from layout_contract import validate_specification

parser = argparse.ArgumentParser()
parser.add_argument('archive')
parser.add_argument('task_file')
args = parser.parse_args()
archive = Path(args.archive).resolve()
task = read(args.task_file)
assert task['state'] == 'ready' and sha(archive) == task['artifact']['sha256']
assert archive.stat().st_size == task['artifact']['bytes']
spec = validate_specification(task['specification'])
solids, documents = counts(spec)
destination = STAGE / 'downloaded_layout_r190'
destination.mkdir(exist_ok=False)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    manifest = json.loads(z.read('manifest.json'))
    assert len(z.namelist()) == len(set(z.namelist()))
    for name in z.namelist():
        assert (destination / name).resolve().is_relative_to(destination)
    for name, identity in manifest.items():
        data = z.read(name)
        assert len(data) == identity['bytes'] and hashlib.sha256(data).hexdigest() == identity['sha256']
    z.extractall(destination)
root = destination / 'models'
assert len([p for p in root.iterdir() if p.suffix.lower() in ('.sldprt', '.sldasm')]) == documents
assert read(destination / 'verification/size_request.json') == spec
source = read(STAGE / 'seed_manifest.json')
contract = {'scope': '22057_SHADE_PORTAL_R145', 'cases': [{
    'id': 'downloaded_r190', 'index': 13, 'package_root': str(root), 'top': source['top'],
    'dimensions_mm': [spec[k] for k in ('height', 'width', 'depth')], 'columns': spec['columns']}]}
contract_name = 'downloaded_r190_contract.json'
assert not (STAGE / 'evidence' / contract_name).exists()
write(STAGE / 'evidence' / contract_name, contract)
for p in root.iterdir():
    p.chmod(stat.S_IREAD)

generation_root = STAGE / ('generated_portal_' + task['id'][9:].replace('-', ''))
unavailable_root = generation_root.with_name(generation_root.name + '_temporarily_unavailable_r190')
assert generation_root.resolve().parent == unavailable_root.resolve().parent == STAGE.resolve()
assert generation_root.is_dir() and not generation_root.is_symlink() and not unavailable_root.exists()
assert not generation_root.lstat().st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT
identity = {p.name: sha(p) for p in generation_root.iterdir() if p.is_file()}
write(STAGE / 'evidence/downloaded_r190_source_unavailable_plan.json', {
    'generation_root': str(generation_root), 'temporary_root': str(unavailable_root), 'source_identity': identity})
generation_root.rename(unavailable_root)
try:
    assert not generation_root.exists()
    run([NODE, str(STAGE / 'scripts/run_layout_offline_r189.mjs'), contract_name, 'verify', 'downloaded_r190'],
        STAGE / 'evidence/downloaded_r190_verify.log')
finally:
    assert not generation_root.exists() and unavailable_root.is_dir()
    unavailable_root.rename(generation_root)
    assert {p.name: sha(p) for p in generation_root.iterdir() if p.is_file()} == identity
verified = STAGE / 'evidence/downloaded_r190_verify.json'
native = read(verified)
case = native['cases'][0]
assert native['status'] == 'verify_finished' and native['exit_app_called']
assert case['files_unchanged'] and case['suppression_states_restored']
assert case['snapshot']['visible_solids'] == solids and len(case['snapshot']['documents']) == documents
comparison = STAGE / 'evidence/downloaded_r190_geometry.json'
run([PYTHON, str(STAGE / 'scripts/compare_step_pair_cached.py'),
     str(destination / 'geometry/whole_assembly.step'), case['export']['path'], str(comparison)],
    comparison.with_suffix('.log'))
assert read(comparison)['status'] == 'PASS'
assert read(comparison)['before_solids'] == read(comparison)['after_solids'] == solids
report = {'status': 'PASS', 'task_id': task['id'], 'specification': spec,
          'archive': str(archive), 'archive_sha256': sha(archive), 'native_documents': documents,
          'visible_solids': solids, 'generation_folder_unavailable_during_reopen': True,
          'native_reference_rewrite_performed': False, 'generation_folder_restored_unchanged': True,
          'independent_reopen': str(verified), 'geometry': str(comparison), 'extracted': str(destination),
          'tool_sha256': sha(__file__), 'production_release': False}
write(STAGE / 'evidence/downloaded_r190_acceptance.json', report)
print(json.dumps(report, ensure_ascii=False))
