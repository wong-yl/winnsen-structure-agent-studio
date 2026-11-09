"""Bind a preserved failed post-save session to an independent read-only reopen.

This checks native snapshot invariants and STEP correspondence. It is not an
exact Boolean comparison against a missing pre-reload STEP export.
"""
import json
import sys
from pathlib import Path

import shade_geometry_lib_r145 as g
from generate_model import sha, read, write
from generate_layout_model import counts
from layout_contract import validate_specification

saved_path, reopened_path, contract_path, output = [Path(p).resolve() for p in sys.argv[1:5]]
assert not output.exists(), 'Preserve recovery evidence'
saved, reopened, contract = read(saved_path), read(reopened_path), read(contract_path)
before, after, request = saved['cases'][0], reopened['cases'][0], contract['cases'][0]
spec = validate_specification(dict(zip(('height', 'width', 'depth'), request['dimensions_mm']), columns=request['columns']))
expected_solids, expected_documents = counts(spec)
report = {'status': 'RUNNING', 'method': 'native_snapshot_invariants_and_unique_STEP_correspondence',
          'saved_failure': str(saved_path), 'independent_reopen': str(reopened_path),
          'checks': [], 'sources': {str(p): sha(p) for p in (saved_path, reopened_path, contract_path, Path(__file__))},
          'missing_before_reload_STEP': True, 'production_release': False}


def check(name, passed, **data):
    g.check(report, name, passed, **data)
    write(output, report)
    assert passed, name


try:
    check('bounded_saved_reload_failure', saved['status'] == 'failed'
          and saved['mode'] == 'generate' and 'Unhealthy native document:' in saved['error']
          and before['id'] == after['id'] == request['id']
          and request['dimensions_mm'] == [2200, 600, 700]
          and before['native_save_performed'] and before['health_pass']
          and before['saved_reload_files_unchanged']
          and before['snapshot'] == before['snapshot_before_saved_reload']
          and not before.get('saved_file_reload_before_export') and 'export' not in before)
    check('fresh_readonly_reopen', reopened['status'] == 'verify_finished'
          and reopened['mode'] == 'verify' and after['health_pass']
          and after['files_unchanged'] and not after['native_save_performed']
          and saved['process_id'] != reopened['process_id']
          and saved['process_start_utc'] != reopened['process_start_utc']
          and saved['exit_app_called'] and reopened['exit_app_called'])
    identities = {p.name: sha(p) for p in Path(request['package_root']).iterdir()
                  if p.is_file() and not p.name.startswith('~$')}
    check('identical_saved_inputs_and_outputs', identities == before['final_identity']
          == after['input_identity'] == after['final_identity'], files=len(identities))
    launches = [read(str(p) + '.launch.json') for p in (saved_path, reopened_path)]
    check('same_contract_and_native_tool', all(v['contract_sha256'] == sha(contract_path) for v in [saved, reopened, *launches])
          and launches[0]['tool_path'] == launches[1]['tool_path']
          and launches[0]['tool_sha256'] == launches[1]['tool_sha256'] == sha(launches[0]['tool_path']))
    for p, launch in zip((saved_path, reopened_path), launches):
        report['sources'][str(p) + '.launch.json'] = sha(str(p) + '.launch.json')
        report['sources'][str(p) + '.after.json'] = sha(str(p) + '.after.json')
        check('exclusive_process_' + p.stem, not launch['before']['processes']
              and not read(str(p) + '.after.json')['processes']
              and launch['service'] == {'http_status': 200, 'status': 'ok'})
    for label, snapshot in [('saved', before['snapshot_before_saved_reload']), ('reopened', after['snapshot'])]:
        check('healthy_counts_' + label, snapshot['visible_solids'] == expected_solids
              and snapshot['invalid_bodies'] == 0 and len(snapshot['documents']) == expected_documents
              and all(d['issues'] == 0 for d in snapshot['documents']),
              solids=snapshot['visible_solids'], documents=len(snapshot['documents']))
    old = {i['name']: i for i in before['snapshot_before_saved_reload']['instances']}
    new = {i['name']: i for i in after['snapshot']['instances']}
    check('same_unique_instances', len(old) == len(before['snapshot']['instances'])
          and len(new) == len(after['snapshot']['instances']) and old.keys() == new.keys(), instances=len(old))
    deltas = dict(rotation=0., translation_mm=0., bounds_mm=0., com_mm=0., volume_mm3=0., area_mm2=0.)
    bodies = 0
    for name, a in old.items():
        b = new[name]
        assert all(a[k] == b[k] for k in ('document', 'configuration', 'suppressed', 'hidden', 'fixed_instance')), name
        if a.get('transform'):
            deltas['rotation'] = max(deltas['rotation'], max(abs(x-y) for x, y in zip(a['transform'][:9], b['transform'][:9])))
            deltas['translation_mm'] = max(deltas['translation_mm'], max(abs(x-y)*1000 for x, y in zip(a['transform'][9:12], b['transform'][9:12])))
        ab = {v['name']: v for v in a.get('bodies', [])}
        bb = {v['name']: v for v in b.get('bodies', [])}
        assert len(ab) == len(a.get('bodies', [])) and len(bb) == len(b.get('bodies', [])) and ab.keys() == bb.keys(), name
        for body_name, x in ab.items():
            y = bb[body_name]
            assert x['check'] == y['check'] == 0 and x['faces'] == y['faces'] and x['edges'] == y['edges'], (name, body_name)
            for key, field, indices, factor in [('bounds_mm', 'box_m', range(6), 1000), ('com_mm', 'mass', range(3), 1000),
                                               ('volume_mm3', 'mass', [3], 1e9), ('area_mm2', 'mass', [4], 1e6)]:
                deltas[key] = max(deltas[key], max(abs(x[field][i]-y[field][i])*factor for i in indices))
            bodies += 1
    check('native_body_geometry_invariants', deltas['rotation'] < 1e-9
          and all(deltas[k] < .001 for k in ('translation_mm', 'bounds_mm', 'com_mm', 'area_mm2'))
          and deltas['volume_mm3'] < .01, bodies_including_hidden=bodies, maximum_deltas=deltas)
    bound_case = dict(after, snapshot=before['snapshot_before_saved_reload'])
    current = g.load_case(bound_case, output.stem, 'saved_snapshot', report['sources'])
    check('saved_snapshot_to_reopened_STEP_unique_valid_bodies', current['part_count'] == expected_solids
          and len(current['mapped']) == expected_solids and all(r['shape'].isValid() for r in current['mapped']),
          mapped_solids=len(current['mapped']), correspondence=g.serial_correspondence(current['mapped']))
    check('clean_STEP_export', after['export']['saved'] and after['export']['errors'] == after['export']['warnings'] == 0)
    report['status'] = 'PASS'
except Exception as exc:
    report.update(status='FAIL', error=repr(exc))
write(output, report)
assert report['status'] == 'PASS', report.get('error')
print('PASS: preserved saved snapshot matches the independent native reopen and STEP')
