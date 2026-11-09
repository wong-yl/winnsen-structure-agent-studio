"""Read-only diagnosis using one geometry measurement per solid; no CAD changes."""
import hashlib
import json
import sys
from pathlib import Path

import FreeCAD
import Part

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v22'
OUT = WORK / 'step_diagnostic_cached_r23'
assert not OUT.exists(), 'Preserve previous diagnostics'
OUT.mkdir()
sys.path.insert(0, str(STAGE / 'scripts'))
import shade_geometry_lib_r145 as g

tag = 'portal_59263d5354f348b0a2630e226233069e'
comparison_path = STAGE / 'evidence' / (tag + '_reopen_geometry.json')
comparison = g.read(comparison_path)
native_path = STAGE / 'evidence' / (tag + '_reopen_verify.json')
snapshot = g.read(native_path)['cases'][0]['snapshot']
assert comparison['status'] == 'MISMATCH' and all(c['pass'] for c in comparison['checks'])
report = {'status': 'DIAGNOSTIC', 'sources': {}, 'imports': [], 'native_modified': False}
records = []
for side in ('before', 'after'):
    path = Path(comparison[side])
    report['sources'][str(path)] = g.sha(path)
    shape = Part.read(str(path))
    rows = []
    for index, solid in enumerate(shape.Solids):
        rows.append({'index': index, 'shape': solid, 'center': solid.CenterOfMass,
                     'volume': solid.Volume, 'bounds': g.bounds(solid)})
    records.append(rows)
    item = {'side': side, 'solids': len(rows), 'shells': len(shape.Shells)}
    report['imports'].append(item)
    print(json.dumps(item), flush=True)

remaining = list(records[1])
matched = []
for expected in records[0]:
    closest = min(range(len(remaining)), key=lambda i: remaining[i]['center'].distanceToPoint(expected['center'])
                  + abs(remaining[i]['volume'] - expected['volume']) / 1000)
    actual = remaining.pop(closest)
    matched.append({'before_index': expected['index'], 'after_index': actual['index'],
                    'com_error_mm': expected['center'].distanceToPoint(actual['center']),
                    'volume_error_mm3': abs(expected['volume'] - actual['volume'])})

unmatched = []
for row in remaining:
    candidates = []
    for instance in snapshot['instances']:
        if instance['suppressed'] or instance['hidden']:
            continue
        for body in instance.get('bodies', []):
            center = g.transformed(body['mass'][:3], instance['transform'])
            ce = center.distanceToPoint(row['center'])
            ve = abs(body['mass'][3] * 1e9 - row['volume'])
            if ce < .02 and ve < max(2, row['volume'] * 1e-4):
                candidates.append({'instance': instance['name'], 'document': instance['document'], 'body': body['name'],
                                   'com_error_mm': ce, 'volume_error_mm3': ve})
    nearest_before = sorted(records[0], key=lambda r: r['center'].distanceToPoint(row['center']))[:3]
    unmatched.append({'after_index': row['index'], 'volume_mm3': row['volume'], 'center_mm': list(row['center']),
                      'bounds_mm': row['bounds'], 'native_candidates': candidates,
                      'nearest_before': [{'index': r['index'], 'center_mm': list(r['center']), 'volume_mm3': r['volume'],
                                          'distance_mm': r['center'].distanceToPoint(row['center'])} for r in nearest_before]})
report['matched'] = matched
report['unmatched_after_solids'] = unmatched
report['sources'][str(native_path)] = g.sha(native_path)
report['tool_sha256'] = g.sha(__file__)
output = OUT / 'step_count_diagnosis.json'
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'output': str(output), 'unmatched': unmatched}, ensure_ascii=False), flush=True)
