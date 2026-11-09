"""Preserve and locate the r19 clearance failure without modifying native CAD."""
import json
import sys
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v19'
OUT = WORK / 'layout_diagnostic_r20'
assert not OUT.exists(), 'Preserve earlier diagnostic evidence'
OUT.mkdir()
sys.path.insert(0, str(STAGE / 'scripts'))
import shade_geometry_lib_r145 as g
from audit_layout_r368 import overlap

g.EVIDENCE = OUT
native_path = STAGE / 'evidence/probe_22057_1939_860_550_l7_r9_r19_reopen_verify.json'
case = g.read(native_path)['cases'][0]
sources = {str(native_path): g.sha(native_path)}
current = g.load_case(case, 'r19_clearance', 'current', sources)
mapped = current['mapped']
shelves = [r for r in mapped if r['document'] in ('箱体横层板L.SLDPRT', '箱体横层板R.SLDPRT')]
ribs = [r for r in mapped if r['document'] in ('箱体侧板加强筋.SLDPRT', 'OUT_RightSideRib.SLDPRT')]

def describe(row):
    return {'instance': row['instance'], 'document': row['document'], 'body': row['body'],
            'bounds_mm': g.bounds(row['shape']), 'volume_mm3': row['shape'].Volume,
            'center_mm': list(row['shape'].CenterOfMass)}

contacts = []
for shelf in shelves:
    for rib in ribs:
        volume = overlap(shelf['shape'], rib['shape'])
        if volume <= .0001:
            continue
        common = shelf['shape'].common(rib['shape'])
        contacts.append({'shelf': describe(shelf), 'rib': describe(rib),
                         'overlap_mm3': volume, 'overlap_bounds_mm': g.bounds(common),
                         'overlap_solids': [{'volume_mm3': s.Volume, 'bounds_mm': g.bounds(s)} for s in common.Solids]})
report = {'status': 'DIAGNOSTIC', 'native_cad_modified': False, 'sources': sources,
          'ribs': [describe(r) for r in ribs], 'shelf_count': len(shelves), 'contacts': contacts,
          'layout_feature_changes': case.get('layout_feature_changes'),
          'right_rib_reanchor': case.get('right_rear_rib_mate_after')}
(OUT / 'clearance_locations.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({k: report[k] for k in ('status', 'ribs', 'contacts')}, ensure_ascii=False))
