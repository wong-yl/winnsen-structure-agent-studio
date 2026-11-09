"""Verify the retained auxiliary perforations on actual exported narrow parts."""
import json
import math
import re
import sys
from pathlib import Path
import FreeCAD as App
import Part
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
    shelves = [r for r in rows if r['document'] in ('箱体横层板L.SLDPRT', '箱体横层板R.SLDPRT')]
    ribs = [r for r in rows if r['document'] == '箱体横层板加强筋.SLDPRT']
    expected = spec['doors'] - 2
    check('one_rib_per_shelf_boundary', len(shelves) == len(ribs) == expected,
          shelves=len(shelves), ribs=len(ribs), expected=expected)
    used = []
    normal = App.Vector(0, 1, 0)
    for rib in ribs:
        parent = rib['instance'].rsplit('/', 1)[0]
        matches = [s for s in shelves if s['instance'].rsplit('/', 1)[0] == parent]
        check('unique_shelf_rib_pair_' + rib['instance'], len(matches) == 1,
              shelf_candidates=[s['instance'] for s in matches])
        if len(matches) != 1:
            continue
        shelf = matches[0]
        used.append(shelf['instance'])
        shape = rib['shape']
        bores = [h for h in g.cylinders(shape, normal) if h['kind'] == 'bore' and abs(h['radius_mm'] - 1) < 1e-5]
        other = [h for h in g.cylinders(shelf['shape'], normal) if h['kind'] == 'bore' and abs(h['radius_mm'] - 1) < 1e-5]
        center = (shape.BoundBox.XMin + shape.BoundBox.XMax) / 2
        half_pitch = (spec['width'] / 2 - 175) / 2
        targets = sorted([center - half_pitch, center - half_pitch, center + half_pitch, center + half_pitch])
        bores.sort(key=lambda h: (h['axis_point_mm'][0], h['axis_point_mm'][2]))
        check('four_complete_rib_bores_' + rib['instance'], len(bores) == 4 and all(
            abs(h['axis_point_mm'][0] - x) < .005 and
            abs(sum(shape.Faces[i - 1].Area for i in h['faces']) - 1.6 * math.pi) < .01
            for h, x in zip(bores, targets)), expected_x_mm=targets, actual=bores,
            rib_length_mm=shape.BoundBox.XLength, expected_length_mm=spec['width'] / 2 - 7)
        check('rib_length_follows_width_' + rib['instance'],
              abs(shape.BoundBox.XLength - (spec['width'] / 2 - 7)) < .005)
        paired = []
        support = []
        for i, hole in enumerate(bores):
            point = App.Vector(*hole['axis_point_mm'])
            candidates = [(j, h) for j, h in enumerate(other)
                          if (App.Vector(*h['axis_point_mm']) - point).Length < .005]
            gaps = [min(max(a[0] - b[1], b[0] - a[1], 0)
                        for a in hole['intervals_q_mm'] for b in h['intervals_q_mm']) for j, h in candidates]
            check('coaxial_touching_shelf_rib_bore_' + rib['instance'] + '_' + str(i),
                  len(candidates) == 1 and gaps[0] < .005,
                  matching_shelf_bores=len(candidates), axial_gaps_mm=gaps)
            if len(candidates) == 1:
                paired.append(candidates[0][0])
            for label, part, bore in [('rib', shape, hole)] + (
                    [('shelf', shelf['shape'], candidates[0][1])] if len(candidates) == 1 else []):
                lo = min(q[0] for q in bore['intervals_q_mm'])
                hi = max(q[1] for q in bore['intervals_q_mm'])
                base = point + normal * lo
                ring = Part.makeCylinder(2, hi - lo, base, normal).cut(Part.makeCylinder(1, hi - lo, base, normal))
                volume = part.common(ring).Volume
                support.append({'part': label, 'bore': i, 'expected_mm3': ring.Volume, 'actual_mm3': volume})
        check('unique_four_bore_pairs_' + rib['instance'], len(paired) == len(set(paired)) == 4,
              matched_shelf_bore_indices=paired)
        check('complete_material_around_rib_pair_bores_' + rib['instance'], len(support) == 8 and all(
            abs(r['actual_mm3'] - r['expected_mm3']) < .01 for r in support), rings=support)
    check('unique_complete_shelf_rib_coverage', len(used) == len(set(used)) == expected,
          paired_shelves=used, expected=expected)
    hinges = [r for r in rows if r['document'] == '箱体门轴固定板.SLDPRT']
    check('one_joined_hinge_support_per_boundary', len(hinges) == expected and all(
        len(r['shape'].Solids) == 1 for r in hinges), expected=expected, actual=len(hinges))
    used_hinges = []
    for shelf in shelves:
        matches = [r for r in hinges if (r['shape'].CenterOfMass.x < 0) == (shelf['shape'].CenterOfMass.x < 0)
                   and abs(r['shape'].BoundBox.YMin - shelf['shape'].BoundBox.YMin - 12) < .005]
        check('unique_hinge_shelf_pair_' + shelf['instance'], len(matches) == 1,
              candidates=[r['instance'] for r in matches])
        if len(matches) != 1:
            continue
        hinge = matches[0]
        used_hinges.append(hinge['instance'])
        cy = shelf['shape'].BoundBox.YMin + 13
        sign = -1 if shelf['shape'].CenterOfMass.x < 0 else 1
        tabs = []
        for x in (64, spec['width'] / 4 - 23.5, spec['width'] / 2 - 111):
            cx = sign * x
            tab = Part.makeBox(5, 2, 2, App.Vector(cx - 2.5, cy - 1, -23.8))
            tabs.append({'center_mm': [cx, cy], 'expected_mm3': 20,
                         'actual_mm3': hinge['shape'].common(tab).Volume})
        check('three_complete_hinge_tabs_follow_notches_' + hinge['instance'], all(
            abs(r['actual_mm3'] - r['expected_mm3']) < .01 for r in tabs), tabs=tabs)
        interference = hinge['shape'].common(shelf['shape']).Volume
        check('hinge_tabs_fit_shelf_notches_' + hinge['instance'], interference <= .02,
              interference_mm3=interference)
    check('unique_complete_hinge_shelf_coverage', len(used_hinges) == len(set(used_hinges)) == expected,
          matched_hinges=used_hinges, expected=expected)
    report['status'] = 'PASS' if report['checks'] and all(c['pass_'] for c in report['checks']) else 'FAIL'
except Exception as exc:
    report['status'] = 'FAIL'
    report['error'] = repr(exc)
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
assert report['status'] == 'PASS', report.get('error', [c for c in report['checks'] if not c['pass_']])
print('PASS: narrow auxiliary bores retained with complete material support')
