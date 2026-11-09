"""Extend the existing bore audit with unique shelf/rib geometry pairing."""
from generate_model import STAGE

source = STAGE / 'scripts/audit_narrow_auxiliary_r346.py'
target = STAGE / 'scripts/audit_narrow_auxiliary_r425.py'
assert not target.exists()
text = source.read_text(encoding='utf-8')
text = text.replace('import FreeCAD as App\n', 'import FreeCAD as App\nimport Part\n')
key = "    report['status'] = 'PASS' if report['checks'] and all(c['pass_'] for c in report['checks']) else 'FAIL'"
assert text.count(key) == 1
block = '''    shelves = [r for r in rows if r['document'] in ('箱体横层板L.SLDPRT', '箱体横层板R.SLDPRT')]
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
'''
target.write_text(text.replace(key, block + key), encoding='utf-8')
p = STAGE / 'scripts/generate_layout_model.py'
text = p.read_text(encoding='utf-8')
assert text.count('audit_narrow_auxiliary_r346.py') == 1
p.write_text(text.replace('audit_narrow_auxiliary_r346.py', 'audit_narrow_auxiliary_r425.py'), encoding='utf-8')
p = STAGE / 'scripts/qualify_width600_r401.py'
text = p.read_text(encoding='utf-8')
key = "'scripts/audit_narrow_auxiliary_r346.py','evidence/parent_v15_identity.json'"
assert text.count(key) == 1
text = text.replace(key, "'scripts/audit_narrow_auxiliary_r346.py','scripts/audit_narrow_auxiliary_r425.py','evidence/parent_v15_identity.json'")
text = text.replace("shelf auxiliary bores and three notches'", "shelf auxiliary bores and three notches; original shelf-rib length W/2-7 and pilot pitch W/2-175 restored after instance replacement'")
p.write_text(text, encoding='utf-8')
print('Added R425 actual rib bores, unique shelf pairs and full material rings')
