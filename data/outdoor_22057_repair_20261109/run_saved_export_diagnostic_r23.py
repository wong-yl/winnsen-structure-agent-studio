"""Re-export the preserved R22 failed candidate in one new read-only CAD process."""
import json
import re
import subprocess
import sys
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v22'
sys.path.insert(0, str(STAGE / 'scripts'))
from generate_model import native, read, sha, run

tag = 'portal_59263d5354f348b0a2630e226233069e'
original = STAGE / 'evidence' / (tag + '_reopen_verify.json')
prior = read(original)
contract = STAGE / 'evidence' / (tag + '_contract.json')
assert contract.exists()
task = read(WORK.parent / 'outdoor_22057_tasks/OUT22057-59263d53-54f3-48b0-a263-0e226233069e.json')
assert task['state'] == 'failed'
for relative, digest in read(STAGE / 'repair_manifest.json')['identity'].items():
    assert sha(STAGE / relative) == digest, relative
for path in (WORK.parent / 'outdoor_22057_tasks').glob('OUT22057-*.json'):
    assert read(path).get('state') not in ('created', 'queued', 'claimed', 'running', 'generating', 'validating'), str(path)

path, evidence = native(contract.name, 'verify', 'saved_export_r23_diagnostic')
case = evidence['cases'][0]
assert evidence['process_id'] != prior['process_id'] and evidence['exit_app_called']
assert not read(str(path) + '.after.json')['processes']
assert case['files_unchanged'] and not case['native_save_performed']
assert case['input_identity'] == case['final_identity'] == prior['cases'][0]['final_identity']
assert case['snapshot']['visible_solids'] == 399 and case['snapshot']['invalid_bodies'] == 0

export = Path(case['export']['path'])
raw = export.read_bytes()
types = {int(k): t.decode('ascii') for k, t in re.findall(rb'#(\d+)\s*=\s*([A-Z_]+)\s*\(', raw)}
edges = re.findall(rb"#(\d+)\s*=\s*EDGE_CURVE\s*\(\s*'(?:[^']|'')*'\s*,\s*#(\d+)\s*,\s*#(\d+)\s*,", raw)
assert len(edges) == sum(t == 'EDGE_CURVE' for t in types.values()) and len(edges) > 0
bad = [{'edge': int(k), 'endpoints': [int(a), int(b)], 'types': [types.get(int(a)), types.get(int(b))]}
       for k, a, b in edges if types.get(int(a)) != 'VERTEX_POINT' or types.get(int(b)) != 'VERTEX_POINT']
output = WORK / 'saved_export_r23_diagnostic.json'
assert not output.exists()
receipt = {'status': 'DIAGNOSTIC', 'native_evidence': str(path), 'native_evidence_sha256': sha(path),
           'process_id': evidence['process_id'], 'source_task_state': task['state'],
           'files_unchanged': True, 'native_save_performed': False, 'step': str(export),
           'step_sha256': sha(export), 'edge_count': len(edges), 'bad_endpoints': bad}
output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), 'utf-8')
assert not bad, bad
comparison = WORK / 'saved_export_r23_geometry.json'
run([sys.executable, str(STAGE / 'scripts/compare_step_pair_cached.py'), prior['cases'][0]['export']['path'], str(export), str(comparison)], comparison.with_suffix('.log'))
assert read(comparison)['status'] == 'PASS' and read(comparison)['before_solids'] == read(comparison)['after_solids'] == 399
print(json.dumps({'diagnostic': str(output), 'comparison': str(comparison)}, ensure_ascii=False), flush=True)
