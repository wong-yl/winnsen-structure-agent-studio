"""Prepare a reviewable route only after all original per-case checks pass."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v19'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def write_new(path, value):
    assert not path.exists(), 'Preserve prior evidence: ' + str(path)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')

result_path = STAGE / 'evidence/probe_22057_1939_860_550_l7_r9_r19_result.json'
result = read(result_path)
assert result['status'] == 'PASS' and result['native_documents'] == 180 and result['visible_solids'] == 399
assert result['production_release'] is False
phases = []
for path in map(Path, result['native_phases']):
    evidence, launch = read(path), read(str(path) + '.launch.json')
    assert evidence['status'] in ('generate_finished', 'verify_finished') and evidence['exit_app_called']
    assert len(evidence['cases'][0]['snapshot']['documents']) == 180 and evidence['cases'][0]['snapshot']['visible_solids'] == 399
    assert not read(str(path) + '.after.json')['processes']
    phases.append({'path': str(path), 'sha256': sha(path), 'process_id': evidence['process_id'], 'tool_sha256': launch['tool_sha256']})
assert len(phases) == len({row['process_id'] for row in phases}) == 3
assert len({row['tool_sha256'] for row in phases}) == 1
assert len(result['checks']) == 9
for path in result['checks']:
    assert read(path)['status'] == 'PASS', path
manifest = read(STAGE / 'repair_manifest.json')
for relative, digest in manifest['identity'].items():
    assert sha(STAGE / relative) == digest, relative
for path, digest in read(WORK / 'original_identity.json').items():
    assert sha(path) == digest, path
qualification = read(WORK / 'runtime_v17/qualification.json')
for relative, digest in qualification['support_sha256'].items():
    assert sha(WORK / 'runtime_v17' / relative) == digest, relative
for path, digest in qualification['external_support_sha256'].items():
    assert sha(path) == digest, path

receipt_path = STAGE / 'evidence/write_state_repair_acceptance.json'
receipt = {
    'status': 'PASS', 'accepted_utc': datetime.now(timezone.utc).isoformat(),
    'specification': result['specification'], 'native_result': str(result_path), 'native_result_sha256': sha(result_path),
    'native_phases': phases, 'checks': {path: sha(path) for path in result['checks']},
    'source_manifest_sha256': sha(STAGE / 'repair_manifest.json'), 'save_gate_unchanged': True,
    'old_files_unchanged': True, 'scope': 'Exact 1939H/860W/550D left7 right9 runtime repair acceptance',
    'range_requalification': False, 'production_release': False,
}
write_new(receipt_path, receipt)
qualification['release_id'] = 'OUT22057-WRITE-STATE-REPAIR-20261109-v19'
qualification['runtime_isolation']['scope'] = 'Original lock isolation retained; r440 persistence access and STA entry repaired in a new candidate. Geometry rules, seed, geometry auditors and input limits unchanged.'
qualification['runtime_isolation']['runtime_acceptance'] = str(receipt_path)
qualification['write_state_repair'] = {
    'parent_qualification': str(WORK / 'runtime_v17/qualification.json'),
    'parent_sha256': sha(WORK / 'runtime_v17/qualification.json'),
    'diagnostic_sha256': sha(WORK / 'runtime_v18/evidence/save_probe_r18_generate.json'),
    'accepted_exact_case': receipt, 'native_source_changes': manifest['changes'],
    'status': 'PASS', 'inherited_range_geometry_qualification': True,
    'per_task_publication_gates': 'Unchanged native save, independent reopens, nine narrow-case audits and archive identity checks',
}
qualification['support_sha256'] = dict(manifest['identity'])
for path in [STAGE / 'repair_manifest.json', receipt_path, result_path, *map(Path, result['native_phases']), *map(Path, result['checks'])]:
    qualification['support_sha256'][str(path.relative_to(STAGE))] = sha(path)
write_new(STAGE / 'qualification.json', qualification)
gate = read(WORK / 'release_gate.before.json')
gate['generation'].update(source_stage=str(STAGE), script=str(STAGE / 'scripts/portal_generate.py'),
                          qualification_sha256=sha(STAGE / 'qualification.json'), script_sha256=sha(STAGE / 'scripts/portal_generate.py'))
gate['variable_trial_release_id'] = qualification['release_id']
assert gate['production_release'] is False
write_new(WORK / 'release_gate.r19.candidate.json', gate)
print(json.dumps({'status': 'CANDIDATE_READY_FOR_PORTAL_TASK', 'qualification_sha256': sha(STAGE / 'qualification.json'),
                  'route_candidate': str(WORK / 'release_gate.r19.candidate.json'), 'route_applied': False}, ensure_ascii=False))
