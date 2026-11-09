"""Supplement a completed exact-case chain with a separate read-only native reopen."""
import argparse
import hashlib
import json
import math
import stat
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v23'
READER = WORK / 'saved_divider_check_r23'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def same(actual, expected):
    if isinstance(expected, bool) or expected is None or isinstance(expected, str):
        assert actual == expected, (actual, expected)
    elif isinstance(expected, (int, float)):
        assert isinstance(actual, (int, float)) and math.isclose(actual, expected, abs_tol=1e-8, rel_tol=0), (actual, expected)
    elif isinstance(expected, list):
        assert isinstance(actual, list) and len(actual) == len(expected)
        for a, b in zip(actual, expected):
            same(a, b)
    else:
        assert isinstance(actual, dict) and actual.keys() == expected.keys()
        for key in expected:
            same(actual[key], expected[key])


def mate_signatures(mates, right_name):
    rows = []
    for mate in sorted(mates, key=lambda row: row['name']):
        entities = []
        for entity in mate['entities']:
            values = entity['parameters'] or []
            if entity['type'] == 3 and len(values) >= 6:
                values = [*values[3:6], sum(a * b for a, b in zip(values[:3], values[3:6]))]
            entities.append({'component': '__divider_right_rib__' if entity['component'] == right_name else entity['component'],
                             'type': entity['type'], 'parameters': [round(v, 8) for v in values]})
        rows.append({'name': mate['name'], 'type': mate['type'], 'suppressed': mate['suppressed'], 'entities': entities})
    return rows


parser = argparse.ArgumentParser()
parser.add_argument('result')
args = parser.parse_args()
result_path = Path(args.result).resolve()
assert result_path.parent == STAGE / 'evidence' and result_path.name.endswith('_result.json')
result = read(result_path)
assert result['status'] == 'PASS' and result['native_documents'] == 180 and result['visible_solids'] == 399
assert len(result['checks']) == 9 and all(read(path)['status'] == 'PASS' for path in result['checks'])
assert result['specification'] == read(WORK / 'task_identity.json')['specification']
phases = [read(path) for path in result['native_phases']]
assert len(phases) == 3 and len({phase['process_id'] for phase in phases}) == 3
saved = phases[0]['cases'][0]
assert phases[0]['status'] == 'generate_finished' and saved['divider_right_rib_binding']['mates_preserved']
assert saved['divider_right_rib_final']['original_three_mates_retained']
root = Path(result['models'])
assert root.parent == STAGE and root.name.startswith('generated_')
identity = {path.name: sha(path) for path in root.iterdir() if path.is_file() and not path.name.startswith('~$')}
assert identity == saved['final_identity']
assert all(path.stat().st_file_attributes & stat.FILE_ATTRIBUTE_READONLY for path in root.iterdir()
           if path.suffix.lower() in ('.sldprt', '.sldasm'))
for relative, digest in read(STAGE / 'repair_manifest.json')['identity'].items():
    assert sha(STAGE / relative) == digest, relative
reader_manifest = read(READER / 'reader_manifest.json')
for relative, digest in reader_manifest['identity'].items():
    assert sha(READER / relative) == digest, relative
for path in (WORK.parent / 'outdoor_22057_tasks').glob('OUT22057-*.json'):
    assert read(path).get('state') not in ('created', 'queued', 'claimed', 'running', 'generating', 'validating'), str(path)

tag = result_path.name.removesuffix('_result.json') + '_saved_divider'
evidence = STAGE / 'evidence' / (tag + '_verify.json')
receipt_path = STAGE / 'evidence' / (tag + '_acceptance.json')
assert not evidence.exists() and not receipt_path.exists(), 'Preserve earlier supplement'
with evidence.with_suffix('.log').open('x', encoding='utf-8') as output:
    subprocess.run([r'C:\Program Files\nodejs\node.exe', str(READER / 'run_saved_divider_native.mjs'),
                    Path(result['contract']).name, 'verify', tag], cwd=STAGE, stdout=output, stderr=output, check=True)
native = read(evidence)
assert native['status'] == 'verify_finished' and native['exit_app_called']
assert native['process_id'] not in {phase['process_id'] for phase in phases}
assert native['contract_sha256'] == sha(result['contract'])
assert not read(str(evidence) + '.after.json')['processes']
case = native['cases'][0]
assert case['files_unchanged'] and case['native_save_performed'] is False
assert case['input_identity'] == case['final_identity'] == identity
actual_states = {state['name']: state for state in case['divider_right_rib_final']['states']}
expected_states = {state['name']: state for state in saved['divider_right_rib_final']['states']}
assert len(actual_states) == len(case['divider_right_rib_final']['states'])
assert len(expected_states) == len(saved['divider_right_rib_final']['states'])
same(actual_states, expected_states)
assert not case['divider_right_rib_final']['health']
owner = case['saved_divider_direct_owner']
assert owner['read_only'] and not owner['health']
right = owner['right']
assert right['configuration'] == 'Default' and right['fixed_instance'] is False and right['constrained'] == 3 and right['mate_count'] == 3
same(owner['mate_signatures'], mate_signatures(saved['divider_right_rib_final']['mates'], right['name']))
assert sha(STAGE / 'OutdoorWidth600R440.exe') == reader_manifest['generation_tool_sha256']
receipt = {'status': 'PASS', 'native_result': str(result_path), 'native_result_sha256': sha(result_path),
           'evidence': str(evidence), 'evidence_sha256': sha(evidence), 'process_id': native['process_id'],
           'generation_process_ids': [phase['process_id'] for phase in phases],
           'reader_manifest': str(READER / 'reader_manifest.json'), 'reader_manifest_sha256': sha(READER / 'reader_manifest.json'),
           'configuration': right['configuration'], 'fixed_instance': False, 'constrained': 3, 'mate_count': 3,
           'saved_original_mates_preserved': True, 'all_direct_component_states_preserved': True,
           'candidate_files_unchanged': True, 'generation_tool_unchanged': True, 'production_release': False}
receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'status': 'PASS', 'receipt': str(receipt_path), 'process_id': native['process_id']}, ensure_ascii=False))
