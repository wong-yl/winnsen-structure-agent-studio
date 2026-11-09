"""Check four complete cover slots against their actual M4 nut bores and sheet support."""
import json
import math
import sys
from pathlib import Path
import FreeCAD as App
import Part
import shade_geometry_lib_r145 as g
from layout_contract import validate_specification

native_path, contract_path, output = map(lambda p: Path(p).resolve(), sys.argv[1:4])
assert not output.exists()
native, contract = g.read(native_path), g.read(contract_path)['cases'][0]
spec = validate_specification(dict(zip(('height', 'width', 'depth'), contract['dimensions_mm']), columns=contract['columns']))
report = {'status': 'RUNNING', 'sources': {str(native_path): g.sha(native_path), str(contract_path): g.sha(contract_path), str(Path(__file__)): g.sha(Path(__file__))}, 'checks': []}
def check(name, passed, **data):
    g.check(report, name, passed, **data)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
try:
    current = g.load_case(native['cases'][0], output.stem, 'cover_fasteners', report['sources'])
    rows = current['mapped']
    inner = max((r['shape'] for r in rows if r['document'] == '箱体内顶板.SLDPRT'), key=lambda s: s.Volume)
    normal = App.Vector(0, 1, 0)
    nuts = [h for h in g.cylinders(inner, normal) if h['kind'] == 'bore' and abs(h['radius_mm'] - 2) < 1e-5]
    targets = sorted([sign * x for sign in (-1, 1) for x in (162.5, spec['width'] / 2 - 117.5)])
    actual = sorted(nuts, key=lambda h: h['axis_point_mm'][0])
    check('four_complete_M4_bores_follow_width', len(actual) == 4 and
          all(abs(h['axis_point_mm'][0]-x) < .005 and abs(h['axis_point_mm'][2]+43.4) < .005 and
              abs(sum(inner.Faces[i-1].Area for i in h['faces']) - 12*math.pi) < .01 for h, x in zip(actual, targets)),
          expected_x_mm=targets, actual_bores=actual)
    covers = [r for r in rows if r['document'] == '盖板.SLDPRT']
    used = []
    for row in covers:
        cover = row['shape']
        wires = {}
        for face in cover.Faces:
            if not isinstance(face.Surface, Part.Plane) or abs(face.Surface.Axis.y) < .99:
                continue
            for wire in face.Wires:
                b = wire.BoundBox
                if wire.isClosed() and abs(b.XLength-6) < .005 and abs(b.ZLength-12) < .005:
                    key = (round((b.XMin+b.XMax)/2, 5), round((b.ZMin+b.ZMax)/2, 5))
                    wires[key] = wire
        matches = [[i for i, h in enumerate(actual) if abs(h['axis_point_mm'][0]-x)<.005 and abs(h['axis_point_mm'][2]-z)<.005] for x, z in wires]
        used += [found[0] for found in matches if len(found) == 1]
        check('two_closed_slots_align_M4_'+row['instance'], len(wires)==2 and all(len(found)==1 for found in matches),
              slot_centers_mm=[list(p) for p in wires], matches=matches)
        rim = [min(x-cover.BoundBox.XMin, cover.BoundBox.XMax-x)-3 for x, z in wires]
        check('cover_slots_keep_87_mm_side_material_'+row['instance'], len(rim)==2 and all(abs(v-87)<.005 for v in rim), actual_mm=rim)
    check('unique_four_slot_and_nut_pairing', len(covers)==2 and len(used)==len(set(used))==4)
    outer = [h for h in g.cylinders(inner, normal) if h['kind']=='external' and abs(h['radius_mm']-3.95)<1e-5]
    check('four_standard_nut_outer_bodies_supported', len(outer)==4 and all(
        len([h for h in outer if abs(h['axis_point_mm'][0]-x)<.005 and abs(h['axis_point_mm'][2]+43.4)<.005])==1 for x in targets), outer=outer)
    floor = max((r['shape'] for r in rows if r['document'] == '箱体底板.SLDPRT'), key=lambda s: s.Volume)
    floor_holes = [h for h in g.cylinders(floor, App.Vector(0, 0, 1)) if h['kind']=='bore' and abs(h['radius_mm']-3)<1e-5]
    floor_targets = [-(spec['width']/2-35), spec['width']/2-35]
    floor_holes.sort(key=lambda h: h['axis_point_mm'][0])
    check('two_complete_floor_M6_bores_follow_width', len(floor_holes)==2 and all(
        abs(h['axis_point_mm'][0]-x)<.005 and abs(h['axis_point_mm'][1]-20)<.005 and
        abs(sum(floor.Faces[i-1].Area for i in h['faces'])-24*math.pi)<.01 for h,x in zip(floor_holes,floor_targets)),
        expected_x_mm=floor_targets, actual_bores=floor_holes)
    floor_outer = [h for h in g.cylinders(floor, App.Vector(0, 0, 1)) if h['kind']=='external' and abs(h['radius_mm']-5.5)<1e-5]
    check('two_standard_floor_nuts_preserved', len(floor_outer)==2 and all(
        len([h for h in floor_outer if abs(h['axis_point_mm'][0]-x)<.005 and abs(h['axis_point_mm'][1]-20)<.005])==1 for x in floor_targets), actual=floor_outer)
    report['status'] = 'PASS' if all(c['pass_'] for c in report['checks']) else 'FAIL'
except Exception as exc:
    report['status'] = 'FAIL'
    report['error'] = repr(exc)
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
assert report['status'] == 'PASS', report.get('error', [c for c in report['checks'] if not c['pass_']])
print('PASS: complete cover slots and four M4 nuts align')
