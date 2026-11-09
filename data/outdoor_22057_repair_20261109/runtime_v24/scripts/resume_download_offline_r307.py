"""Retry the same downloaded bytes; preserve the failed r190 native evidence."""
import stat
from pathlib import Path
from generate_model import STAGE, PYTHON, NODE, read, run, sha, write

task_id = read(STAGE / 'evidence/fixture_submission_r302.json')['task_id']
fixture = STAGE / 'evidence/portal_fixture_r172'
task = read(fixture / 'data/outdoor_22057_tasks' / (task_id+'.json'))
archive = fixture / 'browser_download_r190.zip'
assert task['state']=='ready' and sha(archive)==task['artifact']['sha256']
root = STAGE / 'downloaded_layout_r190/models'
contract = read(STAGE / 'evidence/downloaded_r190_contract.json')
contract['cases'][0]['id'] = 'downloaded_r307'
contract_path = STAGE / 'evidence/downloaded_r307_contract.json'
assert not contract_path.exists()
write(contract_path, contract)
generation_root = STAGE / ('generated_portal_'+task_id[9:].replace('-',''))
unavailable = generation_root.with_name(generation_root.name+'_temporarily_unavailable_r307')
assert generation_root.is_dir() and not generation_root.is_symlink() and not unavailable.exists()
assert generation_root.resolve().parent==unavailable.resolve().parent==STAGE.resolve()
assert not generation_root.lstat().st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT
identity = {p.name:sha(p) for p in generation_root.iterdir() if p.is_file()}
before = {p.name:sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')}
generation_root.rename(unavailable)
try:
    run([NODE,str(STAGE/'scripts/run_layout_offline_r307.mjs'),contract_path.name,'verify','downloaded_r307'],STAGE/'evidence/downloaded_r307_verify.log')
finally:
    assert not generation_root.exists() and unavailable.is_dir()
    unavailable.rename(generation_root)
    assert {p.name:sha(p) for p in generation_root.iterdir() if p.is_file()}==identity
    assert {p.name:sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')}==before
native_path=STAGE/'evidence/downloaded_r307_verify.json'
native=read(native_path);case=native['cases'][0]
assert native['status']=='verify_finished' and native['exit_app_called']
assert case['files_unchanged'] and case['suppression_states_restored']
assert case['snapshot']['visible_solids']==167 and len(case['snapshot']['documents'])==130
comparison=STAGE/'evidence/downloaded_r307_geometry.json'
run([PYTHON,str(STAGE/'scripts/compare_step_pair_cached.py'),str(root.parent/'geometry/whole_assembly.step'),case['export']['path'],str(comparison)],comparison.with_suffix('.log'))
assert read(comparison)['status']=='PASS'
assert read(comparison)['before_solids']==read(comparison)['after_solids']==167
target=STAGE/'evidence/downloaded_r190_acceptance.json'
assert not target.exists()
write(target,{'status':'PASS','task_id':task_id,'specification':task['specification'],'archive':str(archive),'archive_sha256':sha(archive),
    'native_documents':130,'visible_solids':167,'generation_folder_unavailable_during_reopen':True,
    'native_reference_rewrite_performed':False,'generation_folder_restored_unchanged':True,
    'independent_reopen':str(native_path),'geometry':str(comparison),'extracted':str(root.parent),
    'previous_failure_preserved':str(STAGE/'evidence/downloaded_r190_verify.json'),
    'tool_sha256':sha(STAGE/'OutdoorLayoutOfflineR307.exe'),'production_release':False})
print('PASS: downloaded native package opened with original generation folder unavailable')
