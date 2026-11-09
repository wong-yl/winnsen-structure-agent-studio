"""Accept only the affected reference-style canopy mounting interfaces.

Read-only FreeCAD Python, no GUI or native CAD edits. Inputs are a verify_export
JSON with actual STEP paths and native snapshots, plus the preserved r12 JSON.
This is not a manufacturing, load, thread or production-release gate.
"""
import argparse
import hashlib
import itertools
import json
import math
import re
import sys
import traceback
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

import FreeCAD
import Import
import Part

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'evidence'
BASELINE = EVIDENCE / 'feedback_cad_verify_export_r12_delivery_reopen.json'
TOL = .005
VOLUME_TOL = .02
V = FreeCAD.Vector
X = V(1, 0, 0)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def vec(p):
    return [p.x, p.y, p.z]


def bounds(shape):
    b = shape.BoundBox
    return [getattr(b, k) for k in ('XMin', 'YMin', 'ZMin', 'XMax', 'YMax', 'ZMax')]


def canonical(axis):
    axis = V(*vec(axis))
    axis.normalize()
    for a in vec(axis):
        if abs(a) > 1e-8:
            if a < 0:
                axis = -axis
            break
    return axis


def extents(shape, axis):
    values = [v.Point.dot(axis) for v in shape.Vertexes]
    return [min(values), max(values)]


def normalize(raw):
    pattern = re.compile(rb"'(?:[^']|'')*'")

    def change(match):
        value = match.group(0)
        if all(b < 128 for b in value):
            return value
        return ("'\\X2\\" + value[1:-1].decode('gb18030').encode('utf-16-be').hex().upper() + "\\X0\\'").encode('ascii')

    clean = pattern.sub(change, raw)
    assert pattern.sub(b"''", raw) == pattern.sub(b"''", clean), 'Non-string STEP data changed'
    return clean


def load_case(case, output_stem, tag, sources):
    export = case['export']
    path = Path(export['path'])
    digest = sha(path)
    assert export.get('saved') and export.get('errors') == 0, 'Native STEP export failed'
    if export.get('sha256'):
        assert digest == export['sha256'], 'STEP input hash differs'
    sources[str(path)] = digest
    clean = normalize(path.read_bytes())
    normalized = EVIDENCE / (output_stem + '_' + tag + '_' + digest[:12] + '_normalized.step')
    if normalized.exists():
        assert normalized.read_bytes() == clean
    else:
        normalized.write_bytes(clean)
    sources[str(normalized)] = sha(normalized)
    doc = FreeCAD.newDocument('ReferenceMountAcceptR20' + tag.replace('_', ''))
    models = {}
    try:
        Import.insert(str(normalized), doc.Name)
        for o in doc.Objects:
            if o.TypeId != 'Part::Feature' or o.Shape.isNull():
                continue
            assert o.Label not in models, 'Duplicate STEP label'
            shape = o.Shape.copy()
            shape.Placement = o.getGlobalPlacement()
            models[o.Label] = shape
    finally:
        FreeCAD.closeDocument(doc.Name)
    snapshot = case.get('snapshot') or case.get('snapshot_after')
    assert snapshot is not None, 'Missing native snapshot'
    assert len(models) == snapshot['visible_solids'], 'STEP/snapshot visible solid count differs'
    assert snapshot['invalid_bodies'] == 0, 'Native snapshot has invalid bodies'
    mapped = correspondence(models, snapshot)
    return dict(models=models, mapped=mapped, snapshot=snapshot, source=str(path), source_sha256=digest,
                normalized=str(normalized), part_count=len(models), case=case)


def transformed(point, transform):
    return V(transform[0]*point[0]+transform[3]*point[1]+transform[6]*point[2]+transform[9],
             transform[1]*point[0]+transform[4]*point[1]+transform[7]*point[2]+transform[10],
             transform[2]*point[0]+transform[5]*point[1]+transform[8]*point[2]+transform[11])*1000


def correspondence(models, snapshot):
    """Bind actual STEP solids to native instances by COM and volume."""
    remaining = set(models)
    measured = {name:dict(center=shape.CenterOfMass,volume=shape.Volume) for name,shape in models.items()}
    records = []
    for instance in snapshot['instances']:
        if instance['suppressed'] or instance['hidden']:
            continue
        for body in instance.get('bodies', []):
            mass = body['mass']
            center = transformed(mass[:3], instance['transform'])
            volume = mass[3]*1e9
            candidates = [(name, (measured[name]['center']-center).Length,
                           abs(measured[name]['volume']-volume)) for name in remaining
                          if abs(measured[name]['volume']-volume) <= max(2., abs(volume)*1e-4)]
            candidates = [r for r in candidates if r[1] < .02]
            assert len(candidates) == 1, ('Ambiguous STEP/native correspondence', instance['name'], body['name'], candidates)
            name, ce, ve = candidates[0]
            remaining.remove(name)
            records.append(dict(key=instance['name']+'::'+body['name'], instance=instance['name'],
                                document=instance['document'], body=body['name'], label=name,
                                com_error_mm=ce, volume_error_mm3=ve, shape=models[name]))
    assert not remaining, ('Unmapped STEP solids', sorted(remaining))
    return records


def planes(shape, normal):
    return [(i, f, f.CenterOfMass.dot(normal)) for i, f in enumerate(shape.Faces, 1)
            if isinstance(f.Surface, Part.Plane) and abs(abs(f.Surface.Axis.dot(normal))-1) < 1e-7]


def cylinders(shape, normal):
    result = []
    for i, face in enumerate(shape.Faces, 1):
        surface = face.Surface
        if not isinstance(surface, Part.Cylinder) or abs(abs(surface.Axis.dot(normal))-1) > 1e-7:
            continue
        point = surface.Center-normal*surface.Center.dot(normal)
        u0, u1, v0, v1 = face.ParameterRange
        u, v = (u0+u1)/2, (v0+v1)/2
        radial = face.valueAt(u, v)-surface.Center
        radial -= normal*radial.dot(normal)
        kind = 'bore' if face.normalAt(u, v).dot(radial) < 0 else 'external'
        group = next((r for r in result if r['kind'] == kind and abs(r['radius_mm']-surface.Radius) < 1e-5
                      and (V(*r['axis_point_mm'])-point).Length < 1e-5), None)
        if group is None:
            group = dict(radius_mm=surface.Radius, axis_point_mm=vec(point), kind=kind, faces=[], intervals_q_mm=[])
            result.append(group)
        group['faces'].append(i)
        group['intervals_q_mm'].append(extents(face, normal))
    return result


def contact(left, right, normal):
    details = []
    for ia, a, qa in planes(left, normal):
        for ib, b, qb in planes(right, normal):
            if abs(qa-qb) < TOL:
                moved = b.copy()
                moved.translate(normal*(qa-qb))
                area = a.common(moved).Area
                if area > .01:
                    details.append(dict(left_face=ia, right_face=ib, normal_gap_mm=qb-qa,
                                        area_mm2=area, left_area_mm2=a.Area, right_area_mm2=b.Area))
    return dict(distance_mm=left.distToShape(right)[0], common_volume_mm3=left.common(right).Volume,
                finite_contact_area_mm2=sum(r['area_mm2'] for r in details), face_pairs=details)


def one(rows, document, side=None, main=False):
    result = [r for r in rows if r['document'] == document
              and (side is None or (r['shape'].CenterOfMass.x < 0) == (side == 'L'))
              and (not main or r['shape'].BoundBox.XLength > 70)]
    assert len(result) == 1, ('Expected one native part', document, side, [r['key'] for r in result])
    return result[0]


def check(report, name, passed, **evidence):
    report['checks'].append(dict(name=name, pass_=bool(passed), **evidence))
    return bool(passed)


def roof_normal(models):
    roof = models['箱体顶板']
    candidates = [f for f in roof.Faces if isinstance(f.Surface, Part.Plane) and abs(f.Surface.Axis.y) > .9]
    primary = max(candidates, key=lambda f:f.Area)
    return roof, canonical(primary.Surface.Axis)


def close(a, b, tolerance=TOL):
    return abs(a-b) <= tolerance


def mounting_checks(current, baseline, report, case_label):
    models, rows = current['models'], current['mapped']
    roof, n = roof_normal(models)
    d = V(0, -n.z, n.y)
    roof_q = max(q for i,f,q in planes(roof,n) if f.Area > 1e5)
    expected_axis = (current['case']['dimensions_mm'][1]-80)/2
    report['measured_coordinates'] = dict(normal=vec(n),depth_forward=vec(d),roof_outer_q_mm=roof_q,
                                          source_slope_deg=math.degrees(math.atan2(-n.z,n.y)))
    adapters = [r['key'] for r in rows if '窄柜雨棚独立转接板' in r['document']]
    old_m8 = [r['key'] for r in rows if r['document'] in ('窄柜雨棚螺钉M8x16候选.SLDPRT','垫圈8.SLDPRT','螺母M8.SLDPRT')]
    check(report,'no_transition_plate_or_old_interface_M8',not adapters and not old_m8,
          adapters=adapters,old_interface_M8_parts=old_m8)
    report['mounting_sides'] = []
    for side, sign, foot_doc in [('L',-1,'左立柱焊件4.SLDPRT'),('R',1,'右立柱焊件4.SLDPRT')]:
        rack_r = one(rows,'箱体雨棚安装架.SLDPRT',side,True)
        foot_r = one(rows,foot_doc,side)
        base_rack = one(baseline['mapped'],'箱体雨棚安装架.SLDPRT',side,True)
        base_foot = one(baseline['mapped'],foot_doc,side)
        rack,foot = rack_r['shape'],foot_r['shape']
        rq,fq = extents(rack,n),extents(foot,n)
        rack_planes,foot_planes = planes(rack,n),planes(foot,n)
        top_q = max(q for i,f,q in rack_planes if f.Area > 1000)
        foot_bearing = sorted(foot_planes,key=lambda r:-r[1].Area)[:2]
        foot_bottom = min(q for i,f,q in foot_bearing)
        foot_top = max(q for i,f,q in foot_bearing)
        rack_base_length = extents(base_rack['shape'],d)
        foot_base_length = extents(base_foot['shape'],d)
        length = extents(rack,d)
        foot_length = extents(foot,d)
        cap_bottom = max(q for i,f,q in rack_planes if q < top_q-TOL and f.Area > 1000)
        dims=dict(rack_width_mm=rack.BoundBox.XLength,rack_height_mm=rq[1]-rq[0],
                  rack_length_mm=length[1]-length[0],foot_width_mm=foot.BoundBox.XLength,
                  foot_length_mm=foot_length[1]-foot_length[0],rack_sheet_thickness_mm=top_q-cap_bottom,
                  foot_sheet_thickness_mm=foot_top-foot_bottom)
        check(report,side+'_section_dimensions',close(dims['rack_width_mm'],80) and close(dims['rack_height_mm'],20)
              and close(dims['foot_width_mm'],44) and close(dims['rack_sheet_thickness_mm'],1.5)
              and close(dims['foot_sheet_thickness_mm'],1.5) and len(rack.Solids)==len(foot.Solids)==1
              and rack.isValid() and foot.isValid(),**dims)
        check(report,side+'_depth_length_preserved',close(length[1]-length[0],rack_base_length[1]-rack_base_length[0])
              and close(foot_length[1]-foot_length[0],foot_base_length[1]-foot_base_length[0]),
              current_rack_length_mm=length[1]-length[0],baseline_rack_length_mm=rack_base_length[1]-rack_base_length[0],
              current_foot_length_mm=foot_length[1]-foot_length[0],baseline_foot_length_mm=foot_base_length[1]-foot_base_length[0])
        rc,fc = cylinders(rack,n),cylinders(foot,n)
        rh = [r for r in rc if r['kind']=='bore' and close(r['radius_mm'],4.4,1e-5)]
        fh = [r for r in fc if r['kind']=='bore' and close(r['radius_mm'],4,1e-5)]
        rb = [r for r in rc if r['kind']=='bore' and close(r['radius_mm'],3,1e-5)]
        ro = [r for r in rc if r['kind']=='external' and close(r['radius_mm'],5.5,1e-5)]
        old_receivers = [r['key'] for r in rows if r['document']=='箱体雨棚安装架.SLDPRT'
                         and (r['shape'].CenterOfMass.x < 0)==(side=='L') and r['shape'].BoundBox.XLength < 70]
        hole_count_ok=len(rh)==len(fh)==len(rb)==len(ro)==3 and not old_receivers
        check(report,side+'_three_integral_receivers_and_holes',hole_count_ok,
              sheet_hole_count=len(rh),foot_hole_count=len(fh),receiver_bore_count=len(rb),
              receiver_outer_count=len(ro),remaining_discrete_old_receivers=old_receivers)
        axes=[]
        if len(rh)==len(fh)==3:
            base_holes=[r for r in cylinders(base_foot['shape'],n) if r['kind']=='bore' and close(r['radius_mm'],4,1e-5)]
            old_s=sorted(max(foot_base_length)-V(*r['axis_point_mm']).dot(d) for r in base_holes)
            new_s=sorted(max(foot_length)-V(*r['axis_point_mm']).dot(d) for r in fh)
            check(report,side+'_depth_hole_offsets_preserved',len(old_s)==len(new_s)==3 and
                  all(close(a,b) for a,b in zip(old_s,new_s)),baseline_offsets_mm=old_s,current_offsets_mm=new_s)
            for index,r in enumerate(sorted(rh,key=lambda r:-V(*r['axis_point_mm']).dot(d))):
                p=V(*r['axis_point_mm'])
                f=min(fh,key=lambda r:(V(*r['axis_point_mm'])-p).Length)
                error=(V(*f['axis_point_mm'])-p).Length
                evidence=dict(hole=index+1,x_mm=p.x,expected_x_mm=sign*expected_axis,
                              sheet_foot_axis_error_mm=error,contact_point_mm=vec(p+n*top_q))
                check(report,side+'_coaxial_axis_'+str(index+1),close(p.x,sign*expected_axis) and error<TOL,**evidence)
                if rb and ro:
                    b=min(rb,key=lambda r:(V(*r['axis_point_mm'])-p).Length)
                    o=min(ro,key=lambda r:(V(*r['axis_point_mm'])-p).Length)
                    lo=min(z[0] for z in b['intervals_q_mm']);hi=max(z[1] for z in b['intervals_q_mm'])
                    receiving=dict(bore_diameter_mm=2*b['radius_mm'],outer_diameter_mm=2*o['radius_mm'],
                        axial_length_mm=hi-lo,cap_underside_gap_mm=cap_bottom-hi,
                        receiver_axis_error_mm=max((V(*b['axis_point_mm'])-p).Length,(V(*o['axis_point_mm'])-p).Length))
                    check(report,side+'_receiver_'+str(index+1),close(hi-lo,4) and close(cap_bottom-hi,0)
                          and receiving['receiver_axis_error_mm']<TOL,**receiving)
                    evidence['receiver']=receiving
                axes.append(evidence)
        mating=contact(rack,foot,n)
        cap_area=sum(f.Area for i,f,q in rack_planes if close(q,top_q))
        check(report,side+'_direct_finite_bearing',close(foot_bottom-top_q,0)
              and mating['distance_mm']<TOL and mating['common_volume_mm3']<VOLUME_TOL
              and mating['finite_contact_area_mm2']>1000
              and abs(mating['finite_contact_area_mm2']-cap_area)<.1,
              foot_bottom_minus_rack_top_mm=foot_bottom-top_q,rack_cap_area_mm2=cap_area,**mating)
        roof_gap=rack.distToShape(roof)[0]
        check(report,side+'_nominal_roof_gap_recorded',close(roof_gap,.2),
              measured_gap_mm=roof_gap,note='Reference nominal 0.2 mm gap only; weld or fastening is not confirmed')
        report['mounting_sides'].append(dict(side=side,rack=rack_r['label'],foot=foot_r['label'],
            rack_top_q_mm=top_q,foot_bottom_q_mm=foot_bottom,rack_base_q_mm=rq[0],
            roof_q_mm=roof_q,axes=axes,dimensions=dims,mating=mating,roof_gap_mm=roof_gap))
    return n


def difference(expected, actual):
    missing=expected.cut(actual).Volume
    extra=actual.cut(expected).Volume
    return dict(missing_mm3=missing,extra_mm3=extra,pass_=missing<VOLUME_TOL and extra<VOLUME_TOL)


def identity_from_pack(case_id, report):
    """Use the relocated package identity when PackAndGo changed references."""
    observations=[]
    for name in ('feedback_cad_pack_r18_reference_mount_copy.json',
                 'feedback_cad_verify_export_r21_cover_reopen.json'):
        path=EVIDENCE/name
        if not path.exists():
            continue
        data=read(path)
        report.setdefault('additional_inputs',{})[str(path)]=sha(path)
        for case in data.get('cases',[]):
            if case.get('id')!=case_id:
                continue
            identity=case.get('package_identity') or case.get('input_identity')
            if identity:
                observations.append(dict(source=str(path),identity=identity))
    return observations


def approved_suppression(baseline, report):
    instances={r['name']:r for r in baseline['snapshot']['instances']}
    root=Path(baseline['snapshot']['documents'][0]['actual_path']).parent
    path=root/'hardware_identity.json'
    data=read(path)
    expected_sha=baseline['case']['input_identity']['hardware_identity.json']
    assert sha(path)==expected_sha,'Baseline hardware suppression identity changed'
    report.setdefault('additional_inputs',{})[str(path)]=expected_sha
    assert len(data)==36,'Exactly 36 legacy interface hardware records required'
    names={r.get('name',r.get('instanceName')) for r in data}
    assert len(names)==36 and all(n in instances for n in names)
    matching=[]
    for r in data:
        name=r.get('name',r.get('instanceName'))
        i=instances[name]
        document=Path(r.get('file',r.get('doc',''))).name
        assert i['document']==document and not i['suppressed'] and not i['hidden']
        assert '/' not in name,'Legacy hardware must be the precise top-level instance'
        tr=r['transform'];actual=i['transform']
        error=max(abs(a-b) for a,b in zip(tr,actual))
        assert len(tr)==len(actual)==16 and error<1e-8,'Baseline hardware position changed'
        matching.append(dict(name=name,document=document,layout_name=r.get('layout_name'),
                             baseline_transform_error=error,baseline_translation_m=actual[9:12]))
    counts=Counter(r['document'] for r in matching)
    assert counts==Counter({'窄柜雨棚螺钉M8x16候选.SLDPRT':12,'垫圈8.SLDPRT':18,'螺母M8.SLDPRT':6})
    adapters=[r for r in instances.values() if r['document'] in
              ('窄柜雨棚独立转接板L.SLDPRT','窄柜雨棚独立转接板R.SLDPRT')]
    assert len(adapters)==2 and all(not r['suppressed'] and not r['hidden'] for r in adapters)
    for r in adapters:
        names.add(r['name']);matching.append(dict(name=r['name'],document=r['document'],layout_name='adapter'))
    assert len(names)==38
    report['explicit_38_instance_suppression_allowlist']=matching
    return names


def local_body_match(old, new):
    remaining=list(new)
    matched=[]
    for body in old:
        candidates=[b for b in remaining if b['name']==body['name']]
        method='body_name'
        if not candidates:
            method='unique_local_volume_and_center'
            candidates=[b for b in remaining if abs(b['mass'][3]-body['mass'][3])<max(1e-10,abs(body['mass'][3])*2e-5)
                        and sum((a-bb)**2 for a,bb in zip(b['mass'][:3],body['mass'][:3]))**.5<1e-7]
        if len(candidates)!=1:
            return None
        match=candidates[0];remaining.remove(match)
        matched.append((body,match,method))
    return matched if not remaining else None


def nonmount_delta(current, baseline, report, normal):
    old={r['name']:r for r in baseline['snapshot']['instances']}
    new={r['name']:r for r in current['snapshot']['instances']}
    check(report,'instance_name_set_unchanged',set(old)==set(new),
          removed=sorted(set(old)-set(new)),added=sorted(set(new)-set(old)))
    suppression=approved_suppression(baseline,report)
    changing={'箱体雨棚安装架.SLDPRT','左立柱焊件2.SLDPRT','左立柱焊件3.SLDPRT','左立柱焊件4.SLDPRT',
              '右立柱焊件2.SLDPRT','右立柱焊件3.SLDPRT','右立柱焊件4.SLDPRT','立柱固定焊件.SLDPRT',
              '立柱连接架.SLDPRT','太阳能雨棚 模型.SLDPRT','防水寄存柜 模型.SLDPRT'}
    if report['label']=='A':changing.add('盖板.SLDPRT')
    geometric=[];state=[];pose=[];permitted=[];local=[]
    old_shapes={}
    new_shapes={}
    for r in baseline['mapped']:old_shapes.setdefault(r['instance'],[]).append(r)
    for r in current['mapped']:new_shapes.setdefault(r['instance'],[]).append(r)
    for name in sorted(set(old)&set(new)):
        a,b=old[name],new[name]
        exact_document=a['document']==b['document']
        if name in suppression:
            okay=exact_document and b['suppressed'] and a['configuration']==b['configuration']
            state.append(dict(instance=name,document=b['document'],pass_=okay,rule='explicit_38_suppression',
                              baseline_suppressed=a['suppressed'],current_suppressed=b['suppressed']))
            continue
        fields=('configuration','suppressed','hidden','fixed_instance')
        differences={k:[a[k],b[k]] for k in fields if a[k]!=b[k]}
        state.append(dict(instance=name,document=b['document'],pass_=exact_document and not differences,
                          differences=differences,rule='unchanged_state'))
        if b['document'] in changing:
            permitted.append(dict(instance=name,document=b['document'],rule='explicit_mount_or_A_cover_geometry_scope'))
            if b['document'] in ('盖板.SLDPRT','防水寄存柜 模型.SLDPRT') and a.get('transform') and b.get('transform'):
                transform_error=max(abs(x-y) for x,y in zip(a['transform'],b['transform']))
                pose.append(dict(instance=name,document=b['document'],pass_=transform_error<1e-8,
                                 transform_error=transform_error,rule='Geometry change allowed; cabinet/cover pose remains fixed'))
            continue
        if a.get('transform') is not None and b.get('transform') is not None:
            expected_shift=-normal*1.5 if name=='太阳能雨棚装配-1' or name.startswith('太阳能雨棚装配-1/') else V()
            rotation_error=max(abs(a['transform'][i]-b['transform'][i]) for i in range(9))
            shift=(V(*b['transform'][9:12])-V(*a['transform'][9:12]))*1000
            shift_error=(shift-expected_shift).Length
            okay=rotation_error<1e-8 and shift_error<TOL
            pose.append(dict(instance=name,document=b['document'],pass_=okay,
                             rotation_error=rotation_error,observed_shift_mm=vec(shift),expected_shift_mm=vec(expected_shift)))
        matched=local_body_match(a.get('bodies',[]),b.get('bodies',[]))
        if matched is None:
            local.append(dict(instance=name,pass_=False,reason='Body set has no complete unique match'))
        else:
            for aa,bb,method in matched:
                dm=[abs(x-y) for x,y in zip(aa['mass'],bb['mass'])]
                okay=(max(dm[:3],default=0)<1e-7 and dm[3]<max(1e-10,abs(aa['mass'][3])*2e-5)
                      and aa['faces']==bb['faces'] and aa['edges']==bb['edges'] and bb['check']==0)
                local.append(dict(instance=name,body_before=aa['name'],body_after=bb['name'],method=method,
                                  pass_=okay,local_center_error_m=max(dm[:3],default=0),volume_error_m3=dm[3]))
        if b['suppressed'] or b['hidden'] or not a.get('bodies'):
            continue
        left=old_shapes.get(name,[]);right=new_shapes.get(name,[])
        remaining=list(right)
        for aa in left:
            candidates=[bb for bb in remaining if bb['body']==aa['body']]
            if not candidates:
                candidates=[bb for bb in remaining if abs(bb['shape'].Volume-aa['shape'].Volume)<VOLUME_TOL]
            if len(candidates)!=1:
                geometric.append(dict(instance=name,body=aa['body'],pass_=False,
                                      reason='Visible body has no complete unique shape mapping'))
                continue
            bb=candidates[0];remaining.remove(bb)
            expected=aa['shape'].copy()
            if name=='太阳能雨棚装配-1' or name.startswith('太阳能雨棚装配-1/'):
                expected.translate(-normal*1.5)
            equality=difference(expected,bb['shape'])
            geometric.append(dict(instance=name,document=b['document'],body_before=aa['body'],body_after=bb['body'],**equality))
        if remaining:
            geometric.append(dict(instance=name,pass_=False,reason='Unmatched additional visible bodies'))
    report['delta_scope']=dict(state_checks=state,pose_checks=pose,local_body_checks=local,
                               unchanged_visible_geometry=geometric,explicit_geometry_exemptions=permitted,
                               applies_rain_canopy_shift_only_to='太阳能雨棚装配-1 hierarchy',
                               nonmount_cabinet_expected_shift_mm=[0,0,0])
    check(report,'explicit_suppression_and_nonmount_states',all(r['pass_'] for r in state),tested_instances=len(state))
    check(report,'unaffected_instances_pose_retained',all(r['pass_'] for r in pose),tested_instances=len(pose))
    check(report,'unaffected_native_body_sets_retained',all(r['pass_'] for r in local),tested_bodies=len(local))
    check(report,'unaffected_visible_solids_exact_geometry_retained',bool(geometric) and all(r['pass_'] for r in geometric),
          tested_bodies=len(geometric),scope='Nominal visible BRep equality with permitted canopy rigid translation; no new door/axis rules inferred')


def upper_and_purchase_checks(current, baseline, report, normal):
    rows=current['mapped']
    left=one(rows,'左立柱焊件1.SLDPRT')['shape']
    right=one(rows,'右立柱焊件1.SLDPRT')['shape']
    width=right.BoundBox.XMax-left.BoundBox.XMin
    old_left=one(baseline['mapped'],'左立柱焊件1.SLDPRT')['shape']
    old_right=one(baseline['mapped'],'右立柱焊件1.SLDPRT')['shape']
    old_width=old_right.BoundBox.XMax-old_left.BoundBox.XMin
    def lateral_planes(shape):
        result=[]
        for i,face in enumerate(shape.Faces):
            if not isinstance(face.Surface,Part.Plane):continue
            n=face.Surface.Axis
            if abs(abs(n.x)-1)>1e-7:continue
            result.append(dict(face=i+1,x_mm=face.CenterOfMass.x,area_mm2=face.Area,bounds_mm=bounds(face)))
        return sorted(result,key=lambda r:r['area_mm2'],reverse=True)
    side_planes=dict(left=lateral_planes(left),right=lateral_planes(right),baseline_left=lateral_planes(old_left),baseline_right=lateral_planes(old_right))
    left_main=[r for r in side_planes['left'] if close(r['x_mm'],-500)]
    right_main=[r for r in side_planes['right'] if close(r['x_mm'],500)]
    check(report,'upper_main_planes_1000',bool(left_main and right_main),main_plane_span_mm=1000 if left_main and right_main else None,planes=side_planes,
          note='1000 mm is accepted only from actual finite main side planes, not overall bounding-box width')
    check(report,'inherited_upper_overall_width_retained',close(width,old_width),outer_width_mm=width,baseline_outer_width_mm=old_width,
          left_outer_x_mm=left.BoundBox.XMin,right_outer_x_mm=right.BoundBox.XMax,
          right_protruding_vertices_mm=[vec(v.Point) for v in right.Vertexes if v.Point.x>500+TOL],
          note='Inherited full envelope is 1009.6066 mm, separately recorded; it is not reported as a 1000 mm overall envelope')
    fixed_docs=('遮阳板.SLDPRT','塑料灯罩.SLDPRT')
    base_by_key={r['key']:r for r in baseline['mapped']}
    for r in rows:
        if r['document'] not in fixed_docs and not r['document'].startswith('LED硬灯条'):
            continue
        assert r['key'] in base_by_key,'Purchased instance key changed; provide a mapping'
        source=base_by_key[r['key']]['shape']
        expected=source.copy();expected.translate(-normal*1.5)
        equality=difference(expected,r['shape'])
        delta=r['shape'].CenterOfMass-source.CenterOfMass
        check(report,'preserved_upper_geometry_'+r['key'],equality['pass_'],
              document=r['document'],instance=r['instance'],observed_com_shift_mm=vec(delta),
              expected_com_shift_mm=vec(-normal*1.5),**{k:v for k,v in equality.items() if k!='pass_'})
    hidden_doc='太阳能板(1140×840).SLDPRT'
    old_hidden=[r for r in baseline['snapshot']['instances'] if r['document']==hidden_doc]
    new_hidden=[r for r in current['snapshot']['instances'] if r['document']==hidden_doc]
    identity_observations=identity_from_pack(current['case']['id'],report)
    package_identity=identity_observations[-1]['identity'] if identity_observations else baseline['case'].get('input_identity',{})
    identities=(package_identity,current['case'].get('input_identity',{}))
    same_identity=identities[0].get(hidden_doc) is not None and identities[0].get(hidden_doc)==identities[1].get(hidden_doc)
    check(report,'hidden_purchased_panel_source_unchanged',len(old_hidden)==len(new_hidden)==1 and same_identity,
          document=hidden_doc,baseline_sha256=identities[0].get(hidden_doc),current_sha256=identities[1].get(hidden_doc),
          hidden_in_step=bool(new_hidden and new_hidden[0]['hidden']),
          package_baseline=identity_observations[-1]['source'] if identity_observations else 'r12 source',
          coverage='Byte-identical purchased part in the relocated package proves unchanged geometry. Hidden panel has no whole-STEP collision coverage.')
    if old_hidden and new_hidden:
        shift=V(*new_hidden[0]['transform'][9:12])*1000-V(*old_hidden[0]['transform'][9:12])*1000
        check(report,'hidden_panel_transform_shift',(shift+normal*1.5).Length<TOL,
              observed_translation_shift_mm=vec(shift),expected_shift_mm=vec(-normal*1.5))


def boxes_near(a,b,margin=.005):
    x,y=a.BoundBox,b.BoundBox
    return all(getattr(x,axis+'Max')+margin>=getattr(y,axis+'Min') and
               getattr(y,axis+'Max')+margin>=getattr(x,axis+'Min') for axis in 'XYZ')


def affected_neighbors(current, baseline, report):
    prefixes=('左立柱焊件','右立柱焊件','立柱固定焊件','立柱连接架','太阳能连接压板','太阳能板压条',
              '遮阳板','雨棚LED灯罩壳','塑料板压条焊件','塑料灯罩','雨棚监控安装挂架','箱体雨棚安装架','箱体顶板')
    rows=[r for r in current['mapped'] if r['document'].startswith(prefixes)]
    exact={r['key']:r for r in baseline['mapped']}
    base={}
    mapping=[]
    old_instances={r['name']:r for r in baseline['snapshot']['instances']}
    new_instances={r['name']:r for r in current['snapshot']['instances']}
    for r in rows:
        if r['key'] in exact:
            match=exact[r['key']];method='exact_instance_body_key'
        else:
            old=old_instances[r['instance']];new=new_instances[r['instance']]
            assert old['document']==new['document']==r['document'] and old['configuration']==new['configuration'],'Neighbor instance identity changed'
            candidates=[b for b in baseline['mapped'] if b['instance']==r['instance'] and b['document']==r['document']]
            current_candidates=[b for b in rows if b['instance']==r['instance']]
            if len(old['bodies'])==len(new['bodies'])==len(candidates)==len(current_candidates)==1:
                match=candidates[0];method='exact_unique_single_body_instance'
            elif r['document']=='箱体雨棚安装架.SLDPRT' and len(old['bodies'])==len(candidates)==4 and len(new['bodies'])==len(current_candidates)==1:
                sheets=[b for b in old['bodies'] if b['faces']==41 and b['edges']==82 and b['check']==0]
                stocks=[b for b in old['bodies'] if b['faces']==25 and b['edges']==54 and b['check']==0]
                assert len(sheets)==1 and len(stocks)==3,'Old bracket sheet/Stock identity ambiguous'
                match=next(b for b in candidates if b['body']==sheets[0]['name']);method='exact_bracket_4_to_1_old_sheet_only'
            else:raise AssertionError('No complete unique old neighbor body mapping: '+r['key'])
        base[r['key']]=match;mapping.append(dict(current_key=r['key'],baseline_key=match['key'],method=method))
    report['affected_neighbor_baseline_mapping']=mapping
    adjacency=[]
    max_new=0.
    for a,b in itertools.combinations(rows,2):
        near_current=boxes_near(a['shape'],b['shape'],.05)
        near_baseline=a['key'] in base and b['key'] in base and boxes_near(base[a['key']]['shape'],base[b['key']]['shape'],.05)
        if not near_current and not near_baseline:continue
        assert a['key'] in base and b['key'] in base,'Missing exact old neighbor mapping'
        ba,bb=base[a['key']]['shape'],base[b['key']]['shape']
        baseline_gap=ba.distToShape(bb)[0]
        baseline_volume=ba.common(bb).Volume if boxes_near(ba,bb) else 0.
        volume=a['shape'].common(b['shape']).Volume if boxes_near(a['shape'],b['shape']) else 0.
        gap=a['shape'].distToShape(b['shape'])[0]
        growth=max(0,volume-baseline_volume)
        max_new=max(max_new,growth)
        # Existing finite welding overlaps are reported and bounded by r12.
        no_growth=growth<VOLUME_TOL
        continuity=baseline_gap is None or baseline_gap>.05 or gap<=max(baseline_gap,.001)+TOL
        row=dict(left=a['key'],right=b['key'],left_label=a['label'],right_label=b['label'],
                 gap_mm=gap,common_volume_mm3=volume,baseline_gap_mm=baseline_gap,
                 baseline_common_volume_mm3=baseline_volume,overlap_growth_mm3=growth,
                 pass_=no_growth and continuity)
        adjacency.append(row)
    report['affected_neighbor_pairs']=adjacency
    check(report,'affected_neighbors_no_new_interference_or_opened_joint',bool(adjacency) and all(r['pass_'] for r in adjacency),
          tested_pair_count=len(adjacency),maximum_new_overlap_mm3=max_new,
          limitation='Only named affected visible canopy/roof solids. Existing overlaps are bounded by r12; load or weld quality is not accepted.')


def serial_correspondence(rows):
    return [{k:v for k,v in r.items() if k!='shape'} for r in rows]


def run_case(case, baseline_case, output_stem, sources, negative=False):
    label='A' if case['id'].startswith('13_') else 'B' if case['id'].startswith('25_') else None
    assert label is not None,'Only A/13 and B/25 are authorized'
    print('LOAD',label,case['export']['path'],flush=True)
    current=load_case(case,output_stem,'current_'+label,sources)
    baseline=current if negative else load_case(baseline_case,output_stem,'baseline_'+label,sources)
    report=dict(label=label,id=case['id'],status='RUNNING',checks=[],part_count=current['part_count'],
                native_visible_solids=current['snapshot']['visible_solids'],
                step_native_correspondence=serial_correspondence(current['mapped']))
    if negative:
        check(report,'negative_control_input_431_solids',current['part_count']==431,
              measured=current['part_count'],expected=431)
    n=mounting_checks(current,baseline,report,label)
    critical_ok=all(r['pass_'] for r in report['checks'] if not r['name'].startswith('negative_control_input'))
    if negative and not critical_ok:
        report['checks_not_run']=['upper purchased geometry comparison','affected neighbor collision comparison']
        report['checks_not_run_reason']='Old mount was already rejected by its transition, axes, section and receiver checks.'
    else:
        upper_and_purchase_checks(current,baseline,report,n)
        affected_neighbors(current,baseline,report)
        nonmount_delta(current,baseline,report,n)
    failed=[r['name'] for r in report['checks'] if not r['pass_']]
    report['failed_checks']=failed
    report['status']='REJECTED' if failed else 'PASS_AFFECTED_MOUNT_INTERFACES_ONLY'
    print('RESULT',label,report['status'],'failed',failed,flush=True)
    return report


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',type=Path,required=True)
    parser.add_argument('--baseline',type=Path,default=BASELINE)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--case',choices=('A','B','all'),default='all')
    parser.add_argument('--negative-control',action='store_true')
    args=parser.parse_args()
    output=args.output.resolve()
    assert output.parent==EVIDENCE.resolve() and output.name.startswith('reference_mount_accept_r50')
    assert not output.exists(),'Preserve existing acceptance evidence; choose a new output name'
    source=args.input.resolve();baseline=args.baseline.resolve()
    sources={str(source):sha(source),str(baseline):sha(baseline),str(Path(__file__).resolve()):sha(__file__)}
    report=dict(status='RUNNING',scope='Affected reference-style canopy mounting interfaces and named visible neighbors only',
                script=str(Path(__file__).resolve()),script_sha256=sha(__file__),runtime=sys.executable,
                freecad_version=FreeCAD.Version(),started_utc=datetime.now(timezone.utc).isoformat(),
                input=str(source),baseline=str(baseline),negative_control=args.negative_control,
                cases=[],sources=sources,production_release=False,native_cad_modified=False,
                limits=['No manufacturing, strength, screw length, actual receiver thread or weld acceptance.',
                        'The 0.2 mm reference rack/roof gap is recorded and is not confirmed attachment.',
                        'A hidden purchased solar panel is checked by unchanged source identity and placement only, not by whole-STEP collision.'])
    def flush():output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    flush()
    try:
        data,old=read(source),read(baseline)
        old_cases={r['id']:r for r in old['cases']}
        selected=[r for r in data['cases'] if r['id'] in old_cases and 'export' in r
                  and (args.case=='all' or (args.case=='A')==r['id'].startswith('13_'))]
        assert selected,'No exported A/B case found'
        for case in selected:
            report['cases'].append(run_case(case,old_cases[case['id']],output.stem,sources,args.negative_control))
            flush()
        if args.negative_control:
            required={'no_transition_plate_or_old_interface_M8','L_direct_finite_bearing','R_direct_finite_bearing'}
            report['negative_control_rejected_old_mount']=all(c['part_count']==431 and c['status']=='REJECTED'
                and required.issubset(set(c['failed_checks'])) for c in report['cases'])
            report['status']='NEGATIVE_CONTROL_PASS' if report['negative_control_rejected_old_mount'] else 'NEGATIVE_CONTROL_FAIL'
        else:
            report['status']='PASS_AFFECTED_MOUNT_INTERFACES_ONLY' if all(c['status']=='PASS_AFFECTED_MOUNT_INTERFACES_ONLY' for c in report['cases']) else 'FAIL'
        report['case_coverage']=[r['label'] for r in report['cases']]
        for case in report['cases']:
            sources.update(case.get('additional_inputs',{}))
        assert all(sha(p)==h for p,h in sources.items()),'Read-only input changed during check'
    except Exception:
        report['status']='ERROR'
        report['error']=traceback.format_exc()
        raise
    finally:
        report['finished_utc']=datetime.now(timezone.utc).isoformat()
        flush()
    print('REPORT',str(output),report['status'],flush=True)
    if report['status'] in ('FAIL','NEGATIVE_CONTROL_FAIL','ERROR'):
        sys.exit(1)


if __name__=='__main__':
    main()
