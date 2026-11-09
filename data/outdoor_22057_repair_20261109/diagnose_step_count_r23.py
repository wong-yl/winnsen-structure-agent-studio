"""Read preserved STEP files to separate export loss from text-import loss."""
import hashlib
import json
import sys
from pathlib import Path

import FreeCAD
import Part

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v22'
OUT = WORK / 'step_diagnostic_r23'
assert not OUT.exists(), 'Preserve previous diagnostics'
OUT.mkdir()
sys.path.insert(0, str(STAGE / 'scripts'))
import shade_geometry_lib_r145 as g

tag = 'portal_59263d5354f348b0a2630e226233069e'
comparison_path = STAGE / 'evidence' / (tag + '_reopen_geometry.json')
comparison = g.read(comparison_path)
assert comparison['status'] == 'MISMATCH' and all(row['pass'] for row in comparison['checks'])
native_path = STAGE / 'evidence' / (tag + '_reopen_verify.json')
snapshot = g.read(native_path)['cases'][0]['snapshot']
raw_shapes, report = [], {'status': 'DIAGNOSTIC', 'sources': {}, 'imports': [], 'native_modified': False}
for side in ('before', 'after'):
    path = Path(comparison[side])
    report['sources'][str(path)] = g.sha(path)
    raw = Part.read(str(path))
    normalized_path = OUT / (side + '_normalized.step')
    normalized_path.write_bytes(g.normalize(path.read_bytes()))
    clean = Part.read(str(normalized_path))
    report['imports'].append({'side': side, 'raw_solids': len(raw.Solids), 'raw_shells': len(raw.Shells),
                              'normalized_solids': len(clean.Solids), 'normalized_shells': len(clean.Shells),
                              'normalized': str(normalized_path), 'normalized_sha256': g.sha(normalized_path),
                              'non_string_data_unchanged': True})
    raw_shapes.append(raw.Solids)
    print(json.dumps(report['imports'][-1]), flush=True)

remaining = list(enumerate(raw_shapes[1]))
for expected in raw_shapes[0]:
    closest = min(range(len(remaining)), key=lambda i: remaining[i][1].CenterOfMass.distanceToPoint(expected.CenterOfMass)
                  + abs(remaining[i][1].Volume - expected.Volume) / 1000)
    remaining.pop(closest)
missing = []
for index, solid in remaining:
    candidates = []
    for instance in snapshot['instances']:
        if instance['suppressed'] or instance['hidden']:
            continue
        for body in instance.get('bodies', []):
            center = g.transformed(body['mass'][:3], instance['transform'])
            ce = center.distanceToPoint(solid.CenterOfMass)
            ve = abs(body['mass'][3] * 1e9 - solid.Volume)
            if ce < .02 and ve < max(2, solid.Volume * 1e-4):
                candidates.append({'instance': instance['name'], 'document': instance['document'], 'body': body['name'],
                                   'com_error_mm': ce, 'volume_error_mm3': ve})
    missing.append({'after_index': index, 'volume_mm3': solid.Volume, 'center_mm': list(solid.CenterOfMass),
                    'bounds_mm': g.bounds(solid), 'native_candidates': candidates})
report['unmatched_after_solids'] = missing
report['sources'][str(native_path)] = g.sha(native_path)
output = OUT / 'step_count_diagnosis.json'
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'output': str(output), 'missing': missing}, ensure_ascii=False), flush=True)
