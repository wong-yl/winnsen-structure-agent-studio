"""Retain all previous bore checks and audit the three real hinge tabs."""
from generate_model import STAGE
p=STAGE/'scripts/audit_narrow_auxiliary_r437.py';assert not p.exists()
t=(STAGE/'scripts/audit_narrow_auxiliary_r425.py').read_text(encoding='utf-8')
key="    report['status'] = 'PASS' if report['checks'] and all(c['pass_'] for c in report['checks']) else 'FAIL'";assert t.count(key)==1
block='''    hinges = [r for r in rows if r['document'] == '箱体门轴固定板.SLDPRT']
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
'''
p.write_text(t.replace(key,block+key),encoding='utf-8')
p=STAGE/'scripts/generate_layout_model.py';t=p.read_text(encoding='utf-8');assert t.count('audit_narrow_auxiliary_r425.py')==1;p.write_text(t.replace('audit_narrow_auxiliary_r425.py','audit_narrow_auxiliary_r437.py'),encoding='utf-8')
p=STAGE/'scripts/qualify_width600_r401.py';t=p.read_text(encoding='utf-8');key="'scripts/audit_narrow_auxiliary_r425.py'";assert t.count(key)==1
t=t.replace(key,key+",'scripts/audit_narrow_auxiliary_r437.py'")
t=t.replace("'local_count_transitions_mm':[748.5,898.5]", "'hinge_support_tabs':'original D5@草图2=W/2-175; each plate remains one joined body; all three 5x2x2mm tabs, unique shelf pairing and notch fit audited per task',\n    'local_count_transitions_mm':[748.5,898.5]")
p.write_text(t,encoding='utf-8')
print('Added R437 complete hinge tabs and unique shelf pairing')
