"""Complete the explicit R442 save / R443 independent-reopen evidence chain."""
import stat
from pathlib import Path
from generate_model import STAGE, PYTHON, read, write, sha, run, native
from generate_layout_model import counts
from layout_contract import validate_specification

tag = 'width600_high_r442'
contract_path = STAGE / 'evidence' / (tag + '_contract.json')
saved_path = STAGE / 'evidence/width600_high_r442_save_generate.json'
reopen_path = STAGE / 'evidence/width600_high_r443_verify_verify.json'
progress = STAGE / 'evidence/saved_recovery_r444_progress.json'
assert not progress.exists()
contract, saved, reopened = read(contract_path), read(saved_path), read(reopen_path)
request = contract['cases'][0]
spec = validate_specification(dict(zip(('height', 'width', 'depth'), request['dimensions_mm']), columns=request['columns']))
solids, documents = counts(spec)
root = Path(request['package_root'])
assert root == STAGE / ('generated_' + tag)
identity = saved['cases'][0]['final_identity']
assert {p.name: sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')} == identity
for p in root.iterdir():
    if p.suffix.lower() in ('.sldprt', '.sldasm'):
        p.chmod(stat.S_IREAD)
checks = []
state = {'status': 'RUNNING', 'candidate': str(root), 'preserved_failure': str(saved_path)}


def audit(script, arguments, name):
    output = STAGE / 'evidence' / name
    run([PYTHON, '-B', str(STAGE / 'scripts' / script), *map(str, arguments), str(output)], output.with_suffix('.log'))
    assert read(output)['status'] == 'PASS'
    checks.append(output)
    state['completed_checks'] = [str(p) for p in checks]
    write(progress, state)
    return output


write(progress, state)
try:
    audit('audit_saved_snapshot_r444.py', [saved_path, reopen_path, contract_path], tag + '_recovery_snapshot_r444.json')
    for script, suffix in [('audit_layout_r368.py', 'layout'), ('audit_shade_r369.py', 'shade'),
                           ('audit_cover_fasteners_r404.py', 'fasteners'), ('audit_narrow_auxiliary_r437.py', 'auxiliary'),
                           ('audit_narrow_lamp_r370.py', 'lamp'), ('audit_rear_rib_r386.py', 'rear_rib'), ('audit_roof_board_r398.py', 'roof_board')]:
        if suffix == 'shade':
            output = STAGE / 'evidence' / (tag + '_recovery_' + suffix + '_r444.json')
            run([PYTHON, '-B', str(STAGE / 'scripts' / script), str(reopen_path), str(output), str(contract_path)], output.with_suffix('.log'))
            assert read(output)['status'] == 'PASS'
            checks.append(output)
        else:
            audit(script, [reopen_path, contract_path], tag + '_recovery_' + suffix + '_r444.json')
        state['last_completed'] = suffix
        write(progress, state)
    second_path, second = native(contract_path.name, 'verify', 'width600_high_r444_reopened')
    audit('audit_saved_snapshot_r444.py', [saved_path, second_path, contract_path], tag + '_recovery_second_snapshot_r444.json')
    audit('compare_step_pair_cached.py', [reopened['cases'][0]['export']['path'], second['cases'][0]['export']['path']], tag + '_independent_geometry_r444.json')
    phases = [saved_path, reopen_path, second_path]
    assert len({read(p)['process_id'] for p in phases}) == 3
    assert len({read(p)['process_start_utc'] for p in phases}) == 3
    recovery_path = STAGE / 'evidence' / (tag + '_saved_failure_recovery_r444.json')
    diagnostic_path = STAGE / 'evidence/canopy_health_probe_r443.json'
    diagnostic = read(diagnostic_path)['cases'][0]
    low_path = STAGE / 'evidence/width600_low_r410_save_generate.json'
    low_health = read(low_path)['cases'][0]['post_layout_health_after_graph_refresh']
    signature = lambda rows: [{**r, 'document': Path(r['document']).name} for r in rows]
    assert diagnostic['files_unchanged'] and signature(diagnostic['loaded_health_after_top_rebuild']) == signature(low_health)
    for key in ('canopy_open_health', 'canopy_rebuild_health', 'canopy_loaded_by_top_health'):
        assert diagnostic[key]['issues'] == 0 and not diagnostic[key]['features']
    recovery_sources = [*phases, *checks, contract_path, diagnostic_path, low_path, Path(__file__)]
    recovery_sources.extend(Path(str(p) + suffix) for p in phases for suffix in ('.launch.json', '.after.json'))
    recovery = {'status': 'PASS', 'kind': 'SAVED_NATIVE_WITH_FAILED_SAME_PROCESS_RELOAD_AND_TWO_INDEPENDENT_REOPENS',
                'preserved_failed_save_phase': str(saved_path), 'independent_reopens': [str(reopen_path), str(second_path)],
                'candidate_files_sha256': identity, 'contract': str(contract_path),
                'sources': {str(p): sha(p) for p in recovery_sources},
                'checks': [str(p) for p in checks], 'native_save_phase_status_remains': saved['status'],
                'inherited_lower_frame_feature_warnings': signature(low_health),
                'scope': 'Native saved geometry and engineering-assistance generation; no new manufacturing gate', 'production_release': False}
    assert not recovery_path.exists()
    write(recovery_path, recovery)
    result = {'status': 'PASS', 'specification': spec, 'dimensions_mm': request['dimensions_mm'], 'models': str(root),
              'top': request['top'], 'native_phases': [str(p) for p in phases], 'checks': [str(p) for p in checks],
              'contract': str(contract_path), 'visible_solids': solids, 'native_documents': documents,
              'saved_failure_recovery': {'path': str(recovery_path), 'sha256': sha(recovery_path)}, 'production_release': False}
    output = STAGE / 'evidence' / (tag + '_result.json')
    assert not output.exists()
    write(output, result)
    state.update(status='PASS', result=str(output), recovery=str(recovery_path))
except Exception as exc:
    state.update(status='FAIL', error=repr(exc))
    raise
finally:
    write(progress, state)
print('PASS: R442 saved candidate accepted through two independent native reopens')
