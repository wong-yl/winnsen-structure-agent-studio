"""Generate outdoor rows, audit their interfaces, and compare independent reopens."""
import argparse
import json
import stat
from pathlib import Path

from generate_model import STAGE, PYTHON, read, sha, write, run, native
from layout_contract import validate_specification
from prepare_layout_case import prepare


def counts(spec):
    return ((109 if spec['width'] < 900 else 129) + 19 * spec['doors'] - sum(d['heightMm'] < 298 for c in spec['columns'] for d in c['doors']),
            123 + 3 * spec['doors'] + len(spec['columns'][1]['doors']))


def finish(contract_path, saved_path, tag):
    contract_path, saved_path = Path(contract_path).resolve(), Path(saved_path).resolve()
    assert contract_path.parent == saved_path.parent == STAGE / 'evidence'
    contract, saved = read(contract_path), read(saved_path)
    request = contract['cases'][0]
    spec = validate_specification(dict(zip(('height', 'width', 'depth'), request['dimensions_mm']), columns=request['columns']))
    expected_solids, expected_documents = counts(spec)
    assert saved['status'] == 'generate_finished' and saved['contract_sha256'] == sha(contract_path)
    case = saved['cases'][0]
    assert case['native_save_performed'] and case['health_pass'] and case['snapshot']['visible_solids'] == expected_solids
    assert case['saved_file_reload_before_export'] and case['saved_reload_files_unchanged']
    assert len(case['snapshot']['documents']) == expected_documents
    root = Path(request['package_root'])
    assert root.parent == STAGE and root.name == 'generated_' + request['id']
    identity = case['final_identity']
    assert {p.name: sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')} == identity
    for p in root.iterdir():
        if p.suffix.lower() in ('.sldprt', '.sldasm'):
            p.chmod(stat.S_IREAD)
    phases, checks = [saved_path], []
    for phase in ('reopen', 'reopened'):
        path, value = native(contract_path.name, 'verify', tag + '_' + phase)
        phases.append(path)
        assert value['cases'][0]['snapshot']['visible_solids'] == expected_solids
        comparison = STAGE / 'evidence' / (tag + '_' + phase + '_geometry.json')
        run([PYTHON, str(STAGE / 'scripts/compare_step_pair_cached.py'), case['export']['path'], value['cases'][0]['export']['path'], str(comparison)], comparison.with_suffix('.log'))
        result = read(comparison)
        assert result['status'] == 'PASS' and result['before_solids'] == result['after_solids'] == expected_solids
        checks.append(comparison)
        if phase == 'reopen':
            legacy_pilot = [spec[k] for k in ('height', 'width', 'depth')] == [1939, 950, 550] and [[d['heightMm'] for d in c['doors']] for c in spec['columns']] == [[450, 600, 756], [170, 200, 300, 400, 724]]
            shade_audit = 'audit_shade_r369.py' if spec['width'] < 900 else 'audit_shade_r201.py' if legacy_pilot else 'audit_shade_r239.py'
            layout_audit = 'audit_layout_r368.py' if spec['width'] < 900 else 'audit_layout_r289.py'
            audits = [(layout_audit, 'layout'), (shade_audit, 'shade')]
            if spec['width'] < 900:
                audits.extend([('audit_cover_fasteners_r404.py', 'fasteners'), ('audit_narrow_auxiliary_r437.py', 'auxiliary'), ('audit_narrow_lamp_r370.py', 'lamp'), ('audit_rear_rib_r386.py', 'rear_rib'), ('audit_roof_board_r398.py', 'roof_board')])
            for script, suffix in audits:
                audit = STAGE / 'evidence' / (tag + '_' + phase + '_' + suffix + '.json')
                arguments = [str(path), str(contract_path), str(audit)] if suffix in ('layout', 'fasteners', 'auxiliary', 'lamp', 'rear_rib', 'roof_board') else [str(path), str(audit), str(contract_path)]
                run([PYTHON, str(STAGE / 'scripts' / script), *arguments], audit.with_suffix('.log'))
                assert read(audit)['status'] == 'PASS'
                checks.append(audit)
    assert len({read(p)['process_id'] for p in phases}) == 3
    assert len({read(str(p) + '.launch.json')['tool_sha256'] for p in phases}) == 1
    source = read(STAGE / 'seed_manifest.json')
    for name, digest in source['files'].items():
        assert sha(Path(source['root']) / name) == digest
    result = {'status': 'PASS', 'specification': spec, 'dimensions_mm': request['dimensions_mm'], 'models': str(root),
              'top': request['top'], 'native_phases': [str(p) for p in phases], 'checks': [str(p) for p in checks],
              'contract': str(contract_path), 'visible_solids': expected_solids, 'native_documents': expected_documents,
              'production_release': False}
    write(STAGE / 'evidence' / (tag + '_result.json'), result)
    return result


def generate(spec, tag):
    spec = validate_specification(spec)
    contract_path = prepare(spec, tag)
    saved, value = native(contract_path.name, 'generate', tag + '_save')
    return finish(contract_path, saved, tag)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--specification')
    parser.add_argument('--saved')
    parser.add_argument('--contract')
    parser.add_argument('--tag', required=True)
    args = parser.parse_args()
    if args.saved:
        assert args.contract and not args.specification
        result = finish(args.contract, args.saved, args.tag)
    else:
        assert args.specification and not args.contract
        result = generate(read(args.specification), args.tag)
    print(json.dumps(result, ensure_ascii=False))
