"""Supplement original gates with STEP reference and fresh-session evidence."""
import argparse
import hashlib
import json
import re
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v23'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument('result')
args = parser.parse_args()
result_path = Path(args.result).resolve()
assert result_path.parent == STAGE / 'evidence'
result = read(result_path)
assert result['status'] == 'PASS' and result['visible_solids'] == 399 and result['native_documents'] == 180
assert len(result['checks']) == 9 and all(read(p)['status'] == 'PASS' for p in result['checks'])
assert result['specification'] == read(WORK / 'task_identity.json')['specification']
output = result_path.with_name(result_path.name.removesuffix('_result.json') + '_export_session_acceptance.json')
assert not output.exists()
sources = {str(result_path): sha(result_path)}
phases = []
pids = []
for evidence_path in map(Path, result['native_phases']):
    native = read(evidence_path)
    assert native['status'] in ('generate_finished', 'verify_finished') and native['exit_app_called']
    assert not read(str(evidence_path) + '.after.json')['processes']
    row = native['cases'][0]
    export = row['export']
    path = Path(export['path'])
    assert export['saved'] and export['errors'] == export['warnings'] == 0 and sha(path) == export['sha256']
    preflight = row['step_export_preflight']
    assert preflight['target'] == preflight['active_document'] == str(Path(result['models']) / result['top'])
    assert preflight['configuration'] == 'BOTTOM_SHORT_PIN_FIX' and preflight['selected_after'] == 0
    assert preflight['read_only'] and preflight['process_id'] == native['process_id']
    pids.append(native['process_id'])
    sources[str(evidence_path)] = sha(evidence_path)
    sources[str(path)] = sha(path)
    raw = path.read_bytes()
    types = {int(k): t.decode('ascii') for k, t in re.findall(rb'#(\d+)\s*=\s*([A-Z_]+)\s*\(', raw)}
    edges = re.findall(rb"#(\d+)\s*=\s*EDGE_CURVE\s*\(\s*'(?:[^']|'')*'\s*,\s*#(\d+)\s*,\s*#(\d+)\s*,", raw)
    assert len(edges) > 0 and len(edges) == sum(t == 'EDGE_CURVE' for t in types.values())
    bad = [(int(k), int(a), int(b)) for k, a, b in edges
           if types.get(int(a)) != 'VERTEX_POINT' or types.get(int(b)) != 'VERTEX_POINT']
    assert not bad, (str(path), bad)
    phase = {'path': str(evidence_path), 'process_id': native['process_id'], 'edge_count': len(edges),
             'invalid_edge_endpoints': 0, 'preflight': preflight}
    if native['status'] == 'generate_finished':
        assert row['native_save_performed'] and row['saved_reload_files_unchanged']
        assert row['saved_reload_independent_session_required'] and row['saved_reload_zero_loaded_documents']
        assert row['generation_process_id'] != row['saved_reload_fresh_process_id'] == row['saved_reload_export_process_id'] == native['process_id']
        generation_pid = row['generation_process_id']
        receipt_path = Path(row['saved_reload_exit_receipt'])
        receipt = read(receipt_path)
        assert receipt['exit_app_called'] and receipt['process_id'] == generation_pid
        cleanup_path = STAGE / (receipt_path.stem + '_owned_cleanup_poll_r4.json')
        cleanup = read(cleanup_path)
        assert cleanup['exit_confirmed'] and cleanup['pid'] == generation_pid
        assert cleanup['action'] in ('already exited', 'waited for verified owned CAD to exit naturally')
        sources[str(receipt_path)] = sha(receipt_path)
        sources[str(cleanup_path)] = sha(cleanup_path)
        phase.update(generation_process_id=generation_pid, generation_natural_exit=str(cleanup_path))
    else:
        assert row['files_unchanged'] and row['native_save_performed'] is False
    phases.append(phase)
assert len(pids) == len(set(pids)) == 3 and generation_pid not in pids
for path in map(Path, result['checks']):
    check = read(path)
    if path.name.endswith('_geometry.json'):
        assert check['before_solids'] == check['after_solids'] == len(check['checks']) == 399
        assert sha(check['before']) == check['before_sha256'] and sha(check['after']) == check['after_sha256']
    sources[str(path)] = sha(path)
receipt = {'status': 'PASS', 'native_result': str(result_path), 'native_result_sha256': sha(result_path),
           'native_phases': phases, 'source_sha256': sources, 'generation_process_id': generation_pid,
           'export_process_ids': pids, 'original_geometry_gates_unchanged': True,
           'cad_forced_termination': False, 'production_release': False}
output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'status': 'PASS', 'receipt': str(output), 'process_ids': [generation_pid, *pids]}, ensure_ascii=False))
