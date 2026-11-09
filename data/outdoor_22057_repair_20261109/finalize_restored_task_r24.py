"""Verify the actual accepted and downloaded task; never change task state."""
import argparse
import hashlib
import json
import subprocess
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path

WORK = Path(__file__).resolve().parent
ROOT = WORK.parents[1]

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument('task_id')
parser.add_argument('--downloaded', required=True)
args = parser.parse_args()
assert args.task_id.startswith('OUT22057-') and len(args.task_id) == 45
task_path = ROOT / 'data/outdoor_22057_tasks' / (args.task_id + '.json')
task = read(task_path)
assert task['state'] == 'ready' and task['artifact'] and task['production_release'] is False
result = read(ROOT / 'data/outdoor_22057_release/generated' / args.task_id / 'result.json')
native = read(result['native_result'])
assert result['status'] == native['status'] == 'PASS'
assert result['taskId'] == args.task_id
assert task['specification'] == result['specification'] == native['specification']
assert task['specification'] == read(WORK.parent / 'outdoor_22057_tasks/OUT22057-8bdbcd9b-3e71-4ae1-bea6-4c6b96940ff2.json')['specification']
archive = ROOT / 'data/outdoor_22057_release' / task['artifact']['archive']
assert sha(archive) == task['artifact']['sha256'] == result['sha256']
assert archive.stat().st_size == task['artifact']['bytes'] == result['bytes']
downloaded = Path(args.downloaded)
assert downloaded.is_file() and sha(downloaded) == sha(archive)
download_receipt = read(task_path.with_suffix('.download.json'))
assert download_receipt['taskId'] == args.task_id and download_receipt['username'] == task['username']
assert download_receipt['sha256'] == sha(archive) and download_receipt['bytes'] == archive.stat().st_size
with zipfile.ZipFile(archive) as bundle:
    assert bundle.testzip() is None
    manifest = json.loads(bundle.read('manifest.json'))
    for name, identity in manifest.items():
        content = bundle.read(name)
        assert len(content) == identity['bytes'] and hashlib.sha256(content).hexdigest() == identity['sha256'], name
    assert set(bundle.namelist()) == set(manifest) | {'manifest.json', '00_使用说明.txt'}
phases = []
for path in map(Path, native['native_phases']):
    evidence, launch = read(path), read(str(path) + '.launch.json')
    assert evidence['exit_app_called'] and evidence['status'] in ('generate_finished', 'verify_finished')
    assert evidence['cases'][0]['snapshot']['visible_solids'] == 399
    assert len(evidence['cases'][0]['snapshot']['documents']) == 180
    assert sha(launch['tool_path']) == launch['tool_sha256']
    assert launch['contract_sha256'] == sha(native['contract'])
    assert not read(str(path) + '.after.json')['processes']
    phases.append({'path': str(path), 'sha256': sha(path), 'process_id': evidence['process_id'],
                   'tool_sha256': launch['tool_sha256'], 'status': evidence['status']})
assert len(phases) == 3 and len({row['process_id'] for row in phases}) == 3
assert len({row['tool_sha256'] for row in phases}) == 1
audits = []
for path in map(Path, native['checks']):
    evidence = read(path)
    assert evidence['status'] == 'PASS', path
    audits.append({'path': str(path), 'sha256': sha(path), 'status': evidence['status']})
assert len(audits) == 9
native_result_path = Path(result['native_result'])
saved_mates_path = native_result_path.with_name(native_result_path.name.removesuffix('_result.json') + '_saved_divider_acceptance.json')
export_acceptance_path = native_result_path.with_name(native_result_path.name.removesuffix('_result.json') + '_export_session_acceptance.json')
export_acceptance = read(export_acceptance_path)
assert export_acceptance['status'] == 'PASS' and export_acceptance['native_result_sha256'] == sha(native_result_path)
for path, digest in export_acceptance['source_sha256'].items():
    assert sha(path) == digest, path
saved_mates = read(saved_mates_path)
assert saved_mates['status'] == 'PASS' and saved_mates['native_result_sha256'] == sha(native_result_path)
assert saved_mates['process_id'] not in {phase['process_id'] for phase in phases}
assert saved_mates['configuration'] == 'Default' and saved_mates['fixed_instance'] is False
assert saved_mates['constrained'] == 3 and saved_mates['mate_count'] == 3
assert saved_mates['saved_original_mates_preserved'] and saved_mates['candidate_files_unchanged']
assert sha(saved_mates['evidence']) == saved_mates['evidence_sha256']
assert sha(saved_mates['reader_manifest']) == saved_mates['reader_manifest_sha256']
saved_mates_native = read(saved_mates['evidence'])
assert saved_mates_native['status'] == 'verify_finished' and saved_mates_native['exit_app_called']
assert not read(saved_mates['evidence'] + '.after.json')['processes']
repair_stage = WORK / 'runtime_v24'
assert Path(native['models']).parent == repair_stage
qualification = read(repair_stage / 'qualification.json')
assert qualification['runtime_geometry_repair']['range_requalification'] is False
for relative, expected in qualification['support_sha256'].items():
    assert sha(repair_stage / relative) == expected, relative
for path, expected in qualification['external_support_sha256'].items():
    assert sha(path) == expected, path
assert sha(ROOT / 'data/outdoor_22057_release/release_gate.json') == sha(WORK / 'release_gate.r24.candidate.json')
for phase in native['native_phases']:
    evidence = read(phase)
    if evidence['status'] == 'generate_finished':
        case = evidence['cases'][0]
        assert case['divider_right_rib_binding']['mates_preserved']
        assert case['divider_right_rib_final']['fully_constrained']
        assert case['divider_right_rib_final']['original_three_mates_retained']
for version in ('runtime_v19', 'runtime_v20', 'runtime_v21', 'runtime_v22', 'runtime_v23'):
    stage = WORK / version
    for relative, expected in read(stage / 'repair_manifest.json')['identity'].items():
        assert sha(stage / relative) == expected, (version, relative)
preserved_failed_tasks = read(WORK / 'r23_protected_failed_tasks.json')['tasks']
for failed in preserved_failed_tasks:
    assert sha(failed['path']) == failed['sha256'] and read(failed['path'])['state'] == 'failed'
original = read(WORK / 'original_identity.json')
for path, expected in original.items():
    assert sha(path) == expected, path
parent_q = read(WORK / 'runtime_v17/qualification.json')
for relative, expected in parent_q['support_sha256'].items():
    assert sha(WORK / 'runtime_v17' / relative) == expected, relative
identity = read(WORK / 'task_identity.json')
assert sha(ROOT / 'data/outdoor_22057_tasks' / (identity['original_task'] + '.json')) == identity['original_task_sha256']
count = int(subprocess.check_output(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
    '@(Get-CimInstance Win32_Process -Filter "Name=\'SLDWORKS.exe\'").Count'], text=True).strip())
assert count == 0
with urllib.request.urlopen('http://127.0.0.1:5180/status.json', timeout=10) as response:
    assert response.status == 200 and json.load(response)['status'] == 'ok'
summary = {
    'status': 'PASS', 'checked_utc': datetime.now(timezone.utc).isoformat(), 'task_id': args.task_id,
    'specification': task['specification'], 'archive': str(archive), 'downloaded': str(downloaded),
    'sha256': sha(archive), 'bytes': archive.stat().st_size, 'manifest_entries': len(manifest),
    'native_documents': 180, 'visible_solids': 399, 'native_phases': phases, 'audits': audits,
    'export_sessions': export_acceptance, 'export_sessions_sha256': sha(export_acceptance_path),
    'saved_divider_reopen': saved_mates, 'saved_divider_reopen_sha256': sha(saved_mates_path),
    'protected_original_paths_checked': len(original), 'v17_support_files_checked': len(parent_q['support_sha256']),
    'original_changes': [], 'original_failed_task_unchanged': True, 'preserved_failed_tasks': preserved_failed_tasks,
    'download_receipt': download_receipt, 'final_sldworks_count': count, 'production_release': False,
}
output = WORK / (args.task_id + '_acceptance.json')
assert not output.exists(), 'Preserve earlier acceptance'
output.write_text(json.dumps(summary, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({key: value for key, value in summary.items() if key not in ('native_phases', 'audits', 'specification')}, ensure_ascii=False))
