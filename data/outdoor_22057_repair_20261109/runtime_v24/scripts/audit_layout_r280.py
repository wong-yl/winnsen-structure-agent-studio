"""Audit arbitrary outdoor door rows against native identities and actual geometry."""
import json
import math
import re
import sys
from pathlib import Path

import FreeCAD as App
import Part
import shade_geometry_lib_r145 as g
from layout_contract import door_rows, validate_specification

STAGE = Path(__file__).resolve().parents[1]
TOL = .005
X, Y, Z = (App.Vector(*v) for v in ((1, 0, 0), (0, 1, 0), (0, 0, 1)))


def axes(shape, radius, normal):
    return [h for h in g.cylinders(shape, normal) if h['kind'] == 'bore' and abs(h['radius_mm'] - radius) < 1e-5]


def point(hole):
    return App.Vector(*hole['axis_point_mm'])


def axial_gap(left, right):
    return min(max(a[0] - b[1], b[0] - a[1], 0)
               for a in left['intervals_q_mm'] for b in right['intervals_q_mm'])


def signature(shape):
    return [shape.Volume, shape.Area, *sorted(edge.Length for edge in shape.Edges)]


def overlap(left, right):
    a, b = g.bounds(left), g.bounds(right)
    if any(min(a[i + 3], b[i + 3]) - max(a[i], b[i]) < 1e-7 for i in range(3)):
        return 0.0
    return left.common(right).Volume


def main():
    native_path, contract_path, output = (Path(p).resolve() for p in sys.argv[1:4])
    assert not output.exists(), 'Preserve geometry evidence'
    native, contract = g.read(native_path), g.read(contract_path)
    case = native['cases'][0]
    request = contract['cases'][0]
    spec = validate_specification(dict(zip(('height', 'width', 'depth'), request['dimensions_mm']), columns=request['columns']))
    rows = door_rows(spec)
    compact_interface = any(not row['bottom_door'] and row['height_mm'] < 130 for row in rows)
    shelf_hole_z = -147 if compact_interface else -96.35
    count = len(rows)
    expected_solids = 129 + 19 * count - sum(row['height_mm'] < 298 for row in rows)
    report = {'status': 'RUNNING', 'checks': [], 'production_release': False,
              'specification': spec, 'sources': {str(p): g.sha(p) for p in (native_path, contract_path, Path(__file__))}}

    def check(name, passed, **data):
        g.check(report, name, passed, **data)
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

    try:
        current = g.load_case(case, output.stem, 'layout', report['sources'])
        mapped = current['mapped']
        baseline_path = Path(g.read(STAGE / 'seed_manifest.json')['root']).parent / 'evidence/source_requal_r146_sizing_survey.json'
        report['sources'][str(baseline_path)] = g.sha(baseline_path)
        baseline_case = g.read(baseline_path)['cases'][0]
        baseline = g.load_case(baseline_case, output.stem, 'baseline', report['sources'])
        dh, dw, dd = [a - b for a, b in zip(request['dimensions_mm'], baseline_case['dimensions_mm'])]
        check('exact_native_and_geometry_counts', current['part_count'] == expected_solids and
              len(current['snapshot']['documents']) == 123 + 3 * count + len(spec['columns'][1]['doors']) and
              all(row['shape'].isValid() for row in mapped), expected_solids=expected_solids,
              actual_solids=current['part_count'], native_documents=len(current['snapshot']['documents']))
        panels = [r for r in mapped if re.fullmatch(r'OUT_[LR]\d{2}_Panel[LR]\.SLDPRT', r['document'])]
        plates = [r for r in mapped if r['document'] in ('电控锁安装板L.SLDPRT', '电控锁安装板R.SLDPRT')]
        pads = [r for r in mapped if r['document'] == 'U型锁钩垫板.SLDPRT']
        check('one_panel_lock_and_hook_per_door', len(panels) == len(plates) == len(pads) == count,
              panels=len(panels), lock_plates=len(plates), hooks=len(pads), expected=count)
        for side in ('L', 'R'):
            own = sorted((r for r in plates if (r['shape'].CenterOfMass.x < 0) == (side == 'L')),
                         key=lambda r: r['shape'].CenterOfMass.y)
            collisions = [{'lower': a['instance'], 'upper': b['instance'],
                           'overlap_mm3': overlap(a['shape'], b['shape'])} for a, b in zip(own, own[1:])]
            check('adjacent_lock_plate_clearance_' + side,
                  all(c['overlap_mm3'] <= .02 for c in collisions), pairs=collisions)
        channels = [r for r in mapped if r['document'] == '箱体锁安装槽.SLDPRT']
        channel_axes = [(r['key'], i, h) for r in channels for i, h in enumerate(axes(r['shape'], 2.7, Z))]
        matches = []
        for plate in plates:
            candidates = []
            for i, hole in enumerate(axes(plate['shape'], 2.5, Z)):
                found = [(key, j, other) for key, j, other in channel_axes if (point(other) - point(hole)).Length < TOL]
                if found:
                    candidates.append((i, found))
            check('two_coaxial_lock_bores_' + plate['instance'], len(candidates) == 2 and all(len(found) == 1 for i, found in candidates),
                  matching_plate_bores=len(candidates), channel_candidates=[len(found) for i, found in candidates])
            matches.extend((found[0][0], found[0][1]) for i, found in candidates if len(found) == 1)
        check('complete_unique_lock_bore_coverage', len(channel_axes) == len(matches) == len(set(matches)) == 2 * count,
              expected=2 * count, channel_bores=len(channel_axes), matched=len(matches), unique=len(set(matches)))
        for row in rows:
            prefix = f"OUT_{row['side']}{row['index']:02}"
            selected = [p for p in panels if p['document'] == prefix + '_Panel' + row['side'] + '.SLDPRT']
            assert len(selected) == 1, ('Missing unique door panel', prefix)
            panel = selected[0]
            b = panel['shape'].BoundBox
            w = spec['width']
            expected_x = [-w / 2 + 24, -2] if row['side'] == 'L' else [2, w / 2 - 24]
            error = max(abs(b.XMin - expected_x[0]), abs(b.XMax - expected_x[1]),
                        abs(b.YMin - row['bottom_mm']), abs(b.YLength - row['height_mm']))
            check('door_pose_' + prefix, error < TOL, bounds_mm=g.bounds(panel['shape']), max_error_mm=error)
            hinge_axes = axes(panel['shape'], 5.1, Y)
            hinge_error = max((abs(abs(point(h).x) - (w / 2 - 34.5)) for h in hinge_axes), default=float('inf'))
            hinge_error = max(hinge_error, max((abs(point(h).z + 10) for h in hinge_axes), default=float('inf')))
            check('door_hinge_axis_' + prefix, len(hinge_axes) == 1 and hinge_error < TOL, axis_error_mm=hinge_error)
            own_plate = [p for p in plates if (p['shape'].CenterOfMass.x < 0) == (row['side'] == 'L') and
                         abs((p['shape'].BoundBox.YMin + p['shape'].BoundBox.YMax) / 2 - row['center_mm'] + 15) < TOL]
            own_pad = [p for p in pads if (p['shape'].CenterOfMass.x < 0) == (row['side'] == 'L') and
                       abs((p['shape'].BoundBox.YMin + p['shape'].BoundBox.YMax) / 2 - row['center_mm']) < TOL]
            assert len(own_plate) == len(own_pad) == 1, ('Lock pairing not unique', prefix)
            py = (own_plate[0]['shape'].BoundBox.YMin + own_plate[0]['shape'].BoundBox.YMax) / 2
            hy = (own_pad[0]['shape'].BoundBox.YMin + own_pad[0]['shape'].BoundBox.YMax) / 2
            check('lock_engagement_' + prefix, abs(py - row['center_mm'] + 15) < TOL and abs(hy - py - 15) < TOL,
                  door_center_mm=row['center_mm'], plate_center_mm=py, hook_center_mm=hy)
        shelves = [r for r in mapped if r['document'] in ('箱体横层板L.SLDPRT', '箱体横层板R.SLDPRT')]
        hinges = [r for r in mapped if r['document'] == '箱体门轴固定板.SLDPRT']
        check('one_shelf_and_hinge_plate_per_internal_boundary', len(shelves) == len(hinges) == count - 2,
              expected=count - 2, shelves=len(shelves), hinge_plates=len(hinges))
        used_interfaces = []
        all_channel_interfaces = [(r, i, h) for r in channels for i, h in enumerate(axes(r['shape'], 1, X))]
        obstacle_documents = {'箱体左侧板.SLDPRT', '箱体右侧板.SLDPRT', '箱体侧板加强筋.SLDPRT',
                              'OUT_RightSideRib.SLDPRT', '箱体竖门框挡边.SLDPRT', '箱体防水门框 竖.SLDPRT', '箱体锁安装槽.SLDPRT'}
        source_overlap = {}
        for side in ('L', 'R'):
            seed_shelf = min((r for r in baseline['mapped'] if r['document'] == '箱体横层板' + side + '.SLDPRT'),
                             key=lambda r: r['shape'].BoundBox.YMin)
            for document in obstacle_documents - {'OUT_RightSideRib.SLDPRT'}:
                source_overlap[(side, document)] = sum(overlap(seed_shelf['shape'], r['shape']) for r in baseline['mapped'] if r['document'] == document)
        for row in (r for r in rows if not r['top_door']):
            prefix = f"OUT_{row['side']}{row['index']:02}"
            own_shelves = [s for s in shelves if (s['shape'].CenterOfMass.x < 0) == (row['side'] == 'L') and
                           abs(s['shape'].BoundBox.YMin - row['boundary_mm'] - 7) < TOL]
            own_hinges = [h for h in hinges if (h['shape'].CenterOfMass.x < 0) == (row['side'] == 'L') and
                          abs(h['shape'].BoundBox.YMin - row['boundary_mm'] - 19) < TOL]
            assert len(own_shelves) == len(own_hinges) == 1, ('Boundary pairing not unique', prefix)
            shelf, hinge = own_shelves[0], own_hinges[0]
            lock_overlap = sum(overlap(shelf['shape'], plate['shape']) for plate in plates)
            check('shelf_lock_plate_clearance_' + prefix, lock_overlap <= .02,
                  overlap_mm3=lock_overlap)
            error = max(abs(shelf['shape'].BoundBox.YMin - row['boundary_mm'] - 7),
                        abs(hinge['shape'].BoundBox.YMin - row['boundary_mm'] - 19))
            check('shelf_hinge_pose_' + prefix, error < TOL, max_error_mm=error)
            for document in sorted(obstacle_documents):
                source_document = '箱体侧板加强筋.SLDPRT' if document == 'OUT_RightSideRib.SLDPRT' else document
                volume = sum(overlap(shelf['shape'], r['shape']) for r in mapped if r['document'] == document)
                allowed = source_overlap[(row['side'], source_document)]
                check('shelf_frame_clearance_' + prefix + '_' + document, volume <= allowed + .02,
                      overlap_mm3=volume, accepted_source_overlap_mm3=allowed)
            hinge_bores = axes(hinge['shape'], 3.25, Y)
            expected_pin_x = (-1 if row['side'] == 'L' else 1) * (spec['width'] / 2 - 34.5)
            check('hinge_plate_bore_' + prefix, len(hinge_bores) == 1 and
                  abs(point(hinge_bores[0]).x - expected_pin_x) < TOL and abs(point(hinge_bores[0]).z + 10) < TOL,
                  axes_mm=[hole['axis_point_mm'] for hole in hinge_bores])
            candidates = []
            for h in axes(shelf['shape'], 1, X):
                for channel, i, other in all_channel_interfaces:
                    if (point(h) - point(other)).Length < TOL and abs(axial_gap(h, other) - .5) < TOL:
                        candidates.append((channel['key'], i, h, other))
                        shelf_area = sum(shelf['shape'].Faces[j - 1].Area for j in h['faces'])
                        channel_area = sum(channel['shape'].Faces[j - 1].Area for j in other['faces'])
                        check('shelf_channel_depth_and_complete_bores_' + prefix,
                              abs(point(h).z - shelf_hole_z) < TOL
                              and abs(point(other).z - shelf_hole_z) < TOL
                              and abs(shelf_area - 2 * math.pi * .8) < .01
                              and abs(channel_area - 2 * math.pi) < .01,
                              expected_z_mm=shelf_hole_z, actual_z_mm=point(h).z,
                              shelf_cylinder_area_mm2=shelf_area, channel_cylinder_area_mm2=channel_area,
                              axial_gap_mm=axial_gap(h, other))
            check('unique_shelf_channel_interface_' + prefix, len(candidates) == 1, candidates=len(candidates))
            used_interfaces.extend((key, i) for key, i, h, other in candidates)
        check('unique_shelf_channel_coverage', len(used_interfaces) == len(set(used_interfaces)) == count - 2,
              matched=len(used_interfaces), unique=len(set(used_interfaces)), expected=count - 2)
        pins = [r for r in mapped if r['document'] in ('门轴销.SLDPRT', '门轴销(短).SLDPRT')]
        check('exact_long_and_end_pin_counts', sum(p['document'] == '门轴销.SLDPRT' for p in pins) == count - 2 and
              sum(p['document'] == '门轴销(短).SLDPRT' for p in pins) == 4,
              long_pins=sum(p['document'] == '门轴销.SLDPRT' for p in pins), short_pins=sum(p['document'] == '门轴销(短).SLDPRT' for p in pins))
        for pin in pins:
            b = pin['shape'].BoundBox
            error = max(abs(abs((b.XMin + b.XMax) / 2) - (spec['width'] / 2 - 34.5)),
                        abs((b.ZMin + b.ZMax) / 2 + 10), abs(b.YLength - (60 if '(短)' in pin['document'] else 87)))
            check('fixed_pin_axis_and_length_' + pin['instance'], error < TOL, max_error_mm=error)
        for side in ('L', 'R'):
            side_rows = [r for r in rows if r['side'] == side]
            expected_min = sorted(r['bottom_mm'] - 53 for r in side_rows if not r['bottom_door'])
            actual_min = sorted(p['shape'].BoundBox.YMin for p in pins if p['document'] == '门轴销.SLDPRT' and
                                (p['shape'].CenterOfMass.x < 0) == (side == 'L'))
            check('long_pin_insertion_' + side, len(actual_min) == len(expected_min) and
                  all(abs(a - b) < TOL for a, b in zip(actual_min, expected_min)), expected_ymin_mm=expected_min, actual_ymin_mm=actual_min)
        mappings = {
            '箱体底板.SLDPRT': [-dw/2, 0, -dd, dw/2, 0, 0],
            '箱体内顶板.SLDPRT': [-dw/2, dh, -dd, dw/2, dh, 0],
            '箱体竖隔板.SLDPRT': [0, 0, -dd, 0, dh, 0],
            '底座横筋焊件2.SLDPRT': [-dw/2, 0, 0, dw/2, 0, 0],
            '底座竖筋焊件2.SLDPRT': [-dw/2, 0, -dd, -dw/2, 0, 0],
            '底加强筋1.SLDPRT': [-dw/2, 0, 0, dw/2, 0, 0],
            '底加强筋2.SLDPRT': [-dw/2, 0, -dd, -dw/2, 0, 0],
        }
        for document, delta in mappings.items():
            instances = sorted({r['instance'] for r in mapped if r['document'] == document})
            old_instances = {r['instance'] for r in baseline['mapped'] if r['document'] == document}
            check('frame_instance_coverage_' + document, set(instances) == old_instances)
            for instance in instances:
                old = max((r for r in baseline['mapped'] if r['instance'] == instance), key=lambda r: r['shape'].Volume)
                new = max((r for r in mapped if r['instance'] == instance), key=lambda r: r['shape'].Volume)
                actual_delta = list(delta)
                if document in ('底座竖筋焊件2.SLDPRT', '底加强筋2.SLDPRT'):
                    actual_delta[0] = actual_delta[3] = (-1 if old['shape'].CenterOfMass.x < 0 else 1) * dw / 2
                if document in ('底座横筋焊件2.SLDPRT', '底加强筋1.SLDPRT'):
                    actual_delta[2] = actual_delta[5] = -dd if old['shape'].CenterOfMass.z < -baseline_case['dimensions_mm'][2] / 2 else 0
                actual, expected = g.bounds(new['shape']), [v + change for v, change in zip(g.bounds(old['shape']), actual_delta)]
                error = max(abs(a - b) for a, b in zip(actual, expected))
                check('frame_and_base_datum_' + instance, error < TOL, actual_bounds_mm=actual,
                      expected_bounds_mm=expected, max_error_mm=error)
        pilot_targets = [App.Vector(x, 0, z) for x in (-8.5, 8.5)
                         for z in (-248.8, -spec['depth'] + 101.2)]
        for document in ('箱体内顶板.SLDPRT', '箱体底板.SLDPRT', '箱体竖隔板.SLDPRT'):
            parts = [r for r in mapped if r['document'] == document]
            holes = [(r, hole) for r in parts for hole in axes(r['shape'], 1, Y)
                     if abs(abs(point(hole).x) - 8.5) < TOL]
            targets = [target for target in pilot_targets if document != '箱体竖隔板.SLDPRT' or target.x < 0]
            matches = [[i for i, (r, hole) in enumerate(holes)
                        if (point(hole) - target).Length < TOL] for target in targets]
            layers = 2 if document == '箱体竖隔板.SLDPRT' else 1
            areas = [sum(r['shape'].Faces[i - 1].Area for i in hole['faces']) for r, hole in holes]
            expected_area = 2 * math.pi * layers
            check('divider_pilot_alignment_' + document,
                  len(holes) == len(targets) and all(len(m) == 1 for m in matches)
                  and len({m[0] for m in matches if len(m) == 1}) == len(targets),
                  expected_axes_mm=[list(target) for target in targets],
                  actual_axes_mm=[hole['axis_point_mm'] for r, hole in holes], matches=matches)
            check('divider_pilot_full_bores_' + document,
                  len(areas) == len(targets) and all(abs(area - expected_area) < .01 for area in areas),
                  cylinder_areas_mm2=areas, expected_area_mm2=expected_area)
        base_bores = [h for r in mapped if r['document'].startswith('底座竖筋焊件') for h in axes(r['shape'], 7.45, Y)]
        rib_bores = [h for r in mapped if r['document'].startswith('底加强筋') for h in axes(r['shape'], 7, Y)]
        base_matches = [[i for i, target in enumerate(rib_bores) if (point(h) - point(target)).Length < TOL] for h in base_bores]
        check('base_mounting_axes', len(base_bores) == 2 and len(rib_bores) == 4 and all(len(m) == 1 for m in base_matches) and
              len({m[0] for m in base_matches if len(m) == 1}) == 2,
              base_bores=len(base_bores), rib_bores=len(rib_bores), matches=base_matches)
        purchased = {'门轴销.SLDPRT', '门轴销(短).SLDPRT', '电控锁ZJA-S500.SLDPRT', '电控U型锁钩ZJA-S500.SLDPRT',
                     '塑料轴套(云绅模具).SLDPRT', 'LK-4-6-V2.0 24口锁控板.SLDPRT'}
        for document in sorted(purchased):
            before = [signature(r['shape']) for r in baseline['mapped'] if r['document'] == document]
            after = [signature(r['shape']) for r in mapped if r['document'] == document]
            errors = [min((max(abs(a-b) for a,b in zip(values, old)) for old in before if len(old) == len(values)), default=float('inf')) for values in after]
            check('fixed_purchased_geometry_' + document, (bool(after) or (document == '门轴销.SLDPRT' and count == 2)) and all(error < .001 for error in errors), count=len(after), max_signature_error=max(errors, default=float('inf')))
        for document in ('箱体左侧板.SLDPRT', '箱体右侧板.SLDPRT'):
            if spec['height'] <= 1939:
                continue
            old = g.one(baseline['mapped'], document)['shape']
            new = g.one(mapped, document)['shape']
            expected = g.extents(old, Y)[1] + dh
            actual = g.extents(new, Y)[1]
            check('side_top_trim_material_' + document, abs(actual - expected) < TOL,
                  actual_top_y_mm=actual, expected_top_y_mm=expected)
        # Shared frame, floor and base datums remain independent of the number of rows.
        check('native_rebuild_and_document_health', case.get('full_rebuild_return') and case.get('health_pass') and
              all(d['issues'] == 0 for d in current['snapshot']['documents']))
        report['status'] = 'PASS' if all(c['pass_'] for c in report['checks']) else 'FAIL'
    except Exception as exc:
        report['status'] = 'FAIL'
        report['error'] = repr(exc)
        raise
    finally:
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'status': report['status'], 'failed': [c for c in report['checks'] if not c['pass_']]}, ensure_ascii=True))
    return 0 if report['status'] == 'PASS' else 1


if __name__ == '__main__':
    sys.exit(main())
