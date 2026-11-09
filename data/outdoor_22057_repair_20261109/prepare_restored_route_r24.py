"""Prepare the route only after the complete exact-case native qualification."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v24'
def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def write_new(path, value):
    assert not path.exists(), 'Preserve prior evidence: ' + str(path)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')

result_path = STAGE / 'evidence/probe_22057_1939_860_550_l7_r9_r24_result.json'
result = read(result_path)
assert result['status'] == 'PASS' and result['native_documents'] == 180 and result['visible_solids'] == 399
assert result['production_release'] is False
original_task = read(WORK.parent / 'outdoor_22057_tasks/OUT22057-8bdbcd9b-3e71-4ae1-bea6-4c6b96940ff2.json')
assert result['specification'] == original_task['specification']
phases = []
for path in map(Path, result['native_phases']):
    evidence, launch = read(path), read(str(path) + '.launch.json')
    assert evidence['status'] in ('generate_finished', 'verify_finished') and evidence['exit_app_called']
    case = evidence['cases'][0]
    assert len(case['snapshot']['documents']) == 180 and case['snapshot']['visible_solids'] == 399
    assert case['health_pass'] and case['full_rebuild_return']
    assert not read(str(path) + '.after.json')['processes']
    assert launch['contract_sha256'] == sha(result['contract']) and sha(launch['tool_path']) == launch['tool_sha256']
    if evidence['status'] == 'generate_finished':
        assert case['native_save_performed'] and case['saved_file_reload_before_export'] and case['saved_reload_files_unchanged']
        assert case['divider_right_rib_binding']['mates_preserved'] and case['divider_right_rib_final']['fully_constrained']
        assert case['divider_right_rib_final']['original_three_mates_retained']
        assert {p.name: sha(p) for p in Path(result['models']).iterdir() if p.is_file() and not p.name.startswith('~$')} == case['final_identity']
    phases.append({'path': str(path), 'sha256': sha(path), 'process_id': evidence['process_id'], 'tool_sha256': launch['tool_sha256']})
assert len(phases) == len({row['process_id'] for row in phases}) == 3
assert len({row['tool_sha256'] for row in phases}) == 1
assert len(result['checks']) == 9
for path in result['checks']:
    assert read(path)['status'] == 'PASS', path
saved_mates_path = result_path.with_name(result_path.name.removesuffix('_result.json') + '_saved_divider_acceptance.json')
saved_mates = read(saved_mates_path)
assert saved_mates['status'] == 'PASS' and saved_mates['native_result_sha256'] == sha(result_path)
assert saved_mates['process_id'] not in {phase['process_id'] for phase in phases}
assert saved_mates['configuration'] == 'Default' and saved_mates['fixed_instance'] is False
assert saved_mates['constrained'] == 3 and saved_mates['mate_count'] == 3
assert saved_mates['saved_original_mates_preserved'] and saved_mates['candidate_files_unchanged']
assert sha(saved_mates['evidence']) == saved_mates['evidence_sha256']
reader_manifest_path = Path(saved_mates['reader_manifest'])
assert sha(reader_manifest_path) == saved_mates['reader_manifest_sha256']
reader_manifest = read(reader_manifest_path)
for relative, digest in reader_manifest['identity'].items():
    assert sha(reader_manifest_path.parent / relative) == digest, relative
manifest = read(STAGE / 'repair_manifest.json')
for relative, digest in manifest['identity'].items():
    assert sha(STAGE / relative) == digest, relative
for path, digest in read(WORK / 'original_identity.json').items():
    assert sha(path) == digest, path
export_acceptance_path = result_path.with_name(result_path.name.removesuffix('_result.json') + '_export_session_acceptance.json')
export_acceptance = read(export_acceptance_path)
assert export_acceptance['status'] == 'PASS' and export_acceptance['native_result_sha256'] == sha(result_path)
for path, digest in export_acceptance['source_sha256'].items():
    assert sha(path) == digest, path
qualification = read(WORK / 'runtime_v17/qualification.json')
qualification['checked_utc'] = datetime.now(timezone.utc).isoformat()
for relative, digest in qualification['support_sha256'].items():
    assert sha(WORK / 'runtime_v17' / relative) == digest, relative
for path, digest in qualification['external_support_sha256'].items():
    assert sha(path) == digest, path
assert sha(WORK.parent / 'outdoor_22057_release/release_gate.json') == sha(WORK / 'release_gate.before.json')
receipt_path = STAGE / 'evidence/runtime_geometry_repair_acceptance.json'
receipt = {'status': 'PASS', 'accepted_utc': datetime.now(timezone.utc).isoformat(),
           'specification': result['specification'], 'native_result': str(result_path), 'native_result_sha256': sha(result_path),
           'native_phases': phases, 'checks': {path: sha(path) for path in result['checks']},
           'export_sessions': export_acceptance, 'export_sessions_sha256': sha(export_acceptance_path),
           'saved_divider_reopen': saved_mates, 'saved_divider_reopen_sha256': sha(saved_mates_path),
           'source_manifest_sha256': sha(STAGE / 'repair_manifest.json'), 'save_gate_unchanged': True,
           'old_files_unchanged': True, 'scope': 'Exact 1939H/860W/550D left7 right9 full runtime and geometry repair acceptance',
           'range_requalification': False, 'production_release': False}
write_new(receipt_path, receipt)
qualification['release_id'] = 'OUT22057-RUNTIME-GEOMETRY-REPAIR-20261109-v24'
qualification['runtime_isolation']['scope'] = 'Original lock isolation; strict candidate write access and STA entry; right divider rib follows right-column reliefs with original mates; cover nut absolute positions. Saved STEP export uses one fresh owned native session, explicit active top and empty selection; original native save, input limits and all nine audit gates unchanged.'
qualification['runtime_isolation']['runtime_acceptance'] = str(receipt_path)
qualification['runtime_geometry_repair'] = {
    'parent_qualification': str(WORK / 'runtime_v17/qualification.json'),
    'parent_sha256': sha(WORK / 'runtime_v17/qualification.json'),
    'write_state_parent': str(WORK / 'runtime_v19/repair_manifest.json'),
    'write_state_parent_sha256': sha(WORK / 'runtime_v19/repair_manifest.json'),
    'accepted_exact_case': receipt, 'native_source_changes': manifest['changes'], 'status': 'PASS',
    'range_requalification': False, 'inherited_input_limits_only': True,
    'per_task_publication_gates': 'Every new task must pass original native save, two independent reopens, geometry audits and archive identity checks before download',
}
qualification['support_sha256'] = dict(manifest['identity'])
paths = [STAGE / 'repair_manifest.json', receipt_path, result_path, saved_mates_path, export_acceptance_path,
         *map(Path, export_acceptance['source_sha256']),
         Path(saved_mates['evidence']), Path(saved_mates['evidence'] + '.launch.json'),
         Path(saved_mates['evidence'] + '.after.json'),
         *map(Path, result['native_phases']), *map(Path, result['checks'])]
for phase in result['native_phases']:
    paths.extend([Path(phase + '.launch.json'), Path(phase + '.after.json')])
for path in paths:
    qualification['support_sha256'][str(path.relative_to(STAGE))] = sha(path)
for relative, digest in reader_manifest['identity'].items():
    qualification['external_support_sha256'][str(reader_manifest_path.parent / relative)] = digest
for path in (reader_manifest_path, WORK / 'prepare_saved_divider_check_r24.py', WORK / 'run_saved_divider_check_r24.py', WORK / 'verify_export_sessions_r24.py', WORK / 'r23_protected_failed_tasks.json'):
    qualification['external_support_sha256'][str(path)] = sha(path)
write_new(STAGE / 'qualification.json', qualification)
gate = read(WORK / 'release_gate.before.json')
gate['generation'].update(source_stage=str(STAGE), script=str(STAGE / 'scripts/portal_generate.py'),
                          qualification_sha256=sha(STAGE / 'qualification.json'), script_sha256=sha(STAGE / 'scripts/portal_generate.py'))
gate['variable_trial_release_id'] = qualification['release_id']
assert gate['production_release'] is False
write_new(WORK / 'release_gate.r24.candidate.json', gate)
print(json.dumps({'status': 'CANDIDATE_READY_FOR_PORTAL_TASK', 'qualification_sha256': sha(STAGE / 'qualification.json'),
                  'route_candidate': str(WORK / 'release_gate.r24.candidate.json'), 'route_applied': False}, ensure_ascii=False))
