"""Verify the retained auxiliary perforations on actual exported narrow parts."""
import json
import math
import re
import sys
from pathlib import Path
import FreeCAD as App
import shade_geometry_lib_r145 as g
from layout_contract import validate_specification

native_path, contract_path, output = [Path(p).resolve() for p in sys.argv[1:4]]
assert not output.exists()
native, request = g.read(native_path), g.read(contract_path)['cases'][0]
spec = validate_specification(dict(zip(('height', 'width', 'depth'), request['dimensions_mm']), columns=request['columns']))
report = {'status': 'RUNNING', 'sources': {str(p): g.sha(p) for p in (native_path, contract_path, Path(__file__))}, 'checks': []}

def check(name, passed, **data):
    g.check(report, name, passed, **data)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

try:
    current = g.load_case(native['cases'][0], output.stem, 'narrow_auxiliary', report['sources'])
    rows = current['mapped']
    for row in rows:
        if re.fullmatch(r'OUT_[LR]\d{2}_Panel[LR]\.SLDPRT', row['document']):
            shape = row['shape']
            bores = [h for h in g.cylinders(shape, App.Vector(0, 1, 0)) if h['kind'] == 'bore' and abs(h['radius_mm'] - 1.5) < 1e-5]
            center = (shape.BoundBox.XMin + shape.BoundBox.XMax) / 2
            half_span = (shape.BoundBox.XLength - 149) / 2
            targets = [center - half_span + i * half_span / 3 for i in range(7)]
            bores.sort(key=lambda h: h['axis_point_mm'][0])
            check('seven_complete_door_auxiliary_bores_' + row['instance'], len(bores) == 7 and all(
                abs(h['axis_point_mm'][0] - x) < .005 and
                abs(sum(shape.Faces[i - 1].Area for i in h['faces']) - 3 * math.pi) < .01
                for h, x in zip(bores, targets)), expected_x_mm=targets, actual=bores)
        elif row['document'] in ('箱体横层板L.SLDPRT', '箱体横层板R.SLDPRT'):
            shape = row['shape']
            holes = [h for h in g.cylinders(shape, App.Vector(0, 1, 0)) if h['kind'] == 'bore' and abs(h['radius_mm'] - 1) < 1e-5]
            sign = -1 if shape.CenterOfMass.x < 0 else 1
            targets = sorted([sign * x for x in (spec['width'] / 2 - 87.75, 87.25) for _ in range(2)])
            holes.sort(key=lambda h: (h['axis_point_mm'][0], h['axis_point_mm'][2]))
            check('four_complete_shelf_auxiliary_bores_' + row['instance'], len(holes) == 4 and all(
                abs(h['axis_point_mm'][0] - x) < .005 and
                abs(sum(shape.Faces[i - 1].Area for i in h['faces']) - 1.6 * math.pi) < .01
                for h, x in zip(holes, targets)), expected_x_mm=targets, actual=holes)
    report['status'] = 'PASS' if report['checks'] and all(c['pass_'] for c in report['checks']) else 'FAIL'
except Exception as exc:
    report['status'] = 'FAIL'
    report['error'] = repr(exc)
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
assert report['status'] == 'PASS', report.get('error', [c for c in report['checks'] if not c['pass_']])
print('PASS: narrow auxiliary bores retained with complete material support')
