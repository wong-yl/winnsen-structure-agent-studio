"""Read-only task geometry audit against the engineer-reviewed standard columns."""
import json
import math
import sys
from pathlib import Path
import FreeCAD as App
import Part
import shade_geometry_lib_r145 as g
from layout_contract import validate_specification

stage=Path(__file__).resolve().parents[1]
native_path=Path(sys.argv[1]).resolve()
output=Path(sys.argv[2]).resolve()
assert not output.exists(), 'Preserve evidence'
native=g.read(native_path)
case=native['cases'][0]
contract=g.read(Path(sys.argv[3]).resolve())['cases'][0]
spec=validate_specification(dict(zip(('height','width','depth'),contract['dimensions_mm']),columns=contract['columns']))
expected_solids=129+19*spec['doors']-sum(d['heightMm']<298 for c in spec['columns'] for d in c['doors'])
baseline_path=Path(g.read(stage/'seed_manifest.json')['root']).parent/'evidence/source_requal_r146_sizing_survey.json'
baseline_case=g.read(baseline_path)['cases'][0]
report={'status':'RUNNING','checks':[],'sources':{str(native_path):g.sha(native_path),str(baseline_path):g.sha(baseline_path)},'production_release':False}
sources=report['sources']
def flush():output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
def check(name,passed,**data):
    g.check(report,name,passed,**data)
    flush()
def matrix(t):
    m=App.Matrix()
    for row in range(3):
        for col in range(3):setattr(m,f'A{row+1}{col+1}',t[col*3+row])
        setattr(m,f'A{row+1}4',t[9+row]*1000)
    return m
def local(row,instances):
    shape=row['shape'].copy()
    shape.transformShape(matrix(instances[row['instance']]['transform']).inverse(),True)
    return shape
flush()
try:
    current=g.load_case(case,output.stem,'task',sources)
    baseline=g.load_case(baseline_case,output.stem,'reviewed',sources)
    width=case['dimensions_mm'][1]
    shade_width=width-160
    check('exact_unique_valid_visible_solids',current['part_count']==expected_solids and all(r['shape'].isValid() for r in current['mapped']))
    mount={'checks':[]}
    g.mounting_checks(current,baseline,mount,'task')
    report['mount_interfaces']=mount
    check('six_coaxial_receivers_and_direct_feet',all(r['pass_'] for r in mount['checks']),failed=[r for r in mount['checks'] if not r['pass_']])
    for doc,expected in [('遮阳板.SLDPRT',shade_width),('立柱连接架.SLDPRT',shade_width+39),('太阳能连接压板.SLDPRT',width),('雨棚LED灯罩壳.SLDPRT',shade_width-62)]:
        rows=[r for r in current['mapped'] if r['document']==doc]
        values=[g.extents(r['shape'],App.Vector(1,0,0)) for r in rows]
        actual=[b-a for a,b in values]
        check('linked_width_'+doc,bool(rows) and all(abs(v-expected)<.005 for v in actual),expected_mm=expected,actual_mm=actual)
    old_instances={r['name']:r for r in baseline['snapshot']['instances']}
    new_instances={r['name']:r for r in current['snapshot']['instances']}
    connectors=[r for r in current['mapped'] if r['document']=='立柱连接架.SLDPRT']
    check('two_connector_bodies_present',len(connectors)==2)
    for connector in connectors:
        shape=local(connector,new_instances)
        cylinder=next(f.Surface for f in shape.Faces if isinstance(f.Surface,Part.Cylinder) and abs(f.Surface.Radius-2.5)<1e-5)
        holes=[h for h in g.cylinders(shape,g.canonical(cylinder.Axis)) if h['kind']=='bore' and abs(h['radius_mm']-2.5)<1e-5]
        actual=sorted(h['axis_point_mm'][0] for h in holes)
        expected=[i*(width-150)/4 for i in range(-2,3)]
        areas=[sum(shape.Faces[i-1].Area for i in h['faces']) for h in holes]
        check('five_complete_connector_bores_'+connector['instance'],len(holes)==5 and
              all(abs(a-b)<.005 for a,b in zip(actual,expected)) and
              all(abs(area-2*math.pi*2.5*3)<.005 for area in areas),
              expected_x_mm=expected,actual_x_mm=actual,cylinder_wall_areas_mm2=areas)
        faces=[f for f in shape.Faces if isinstance(f.Surface,Part.Plane) and abs(f.Surface.Axis.y)>.9]
        face=max(faces,key=lambda f:f.Area)
        slots=[wire for wire in face.Wires if len(wire.Edges)==4 and
               all(abs(a-b)<.005 for a,b in zip(sorted(e.Length for e in wire.Edges),[30,30,50,50]))]
        centers=sorted((wire.BoundBox.XMin+wire.BoundBox.XMax)/2 for wire in slots)
        wanted=[-(width-230)/2,0,(width-230)/2]
        margins=[min(w.BoundBox.XMin-face.BoundBox.XMin,face.BoundBox.XMax-w.BoundBox.XMax) for w in slots]
        check('three_closed_connector_slots_'+connector['instance'],len(slots)==3 and
              all(abs(a-b)<.005 for a,b in zip(centers,wanted)) and all(v>=27.795 for v in margins),
              expected_centers_x_mm=wanted,actual_centers_x_mm=centers,end_material_mm=margins)
    before={r['key']:r for r in baseline['mapped']}
    columns={f'{side}立柱焊件{i}.SLDPRT' for side in ('左','右') for i in range(1,5)}|{'立柱固定焊件.SLDPRT'}
    fixed=[]
    for r in current['mapped']:
        if r['document'] not in columns|{'太阳能板压条.SLDPRT','塑料灯罩.SLDPRT','塑料板压条焊件.SLDPRT'}:continue
        b=before[r['key']]
        delta=g.difference(local(b,old_instances),local(r,new_instances))
        fixed.append({'key':r['key'],'document':r['document'],**delta})
    report['fixed_part_local_geometry']=fixed
    check('12_standard_column_bodies_and_fixed_short_strips_preserved',len([r for r in fixed if r['document'] in columns])==12 and all(r['pass_'] for r in fixed),count=len(fixed),failed=[r for r in fixed if not r['pass_']])
    roof=g.one(current['mapped'],'箱体顶板.SLDPRT')['shape']
    covers=[r for r in current['mapped'] if r['document']=='盖板.SLDPRT']
    check('two_covers_present',len(covers)==2)
    for cover in covers:
        gap=cover['shape'].distToShape(roof)[0]
        overlap=cover['shape'].common(roof).Volume
        check('cover_roof_gap_'+cover['instance'],gap>=2.795 and overlap<.001,gap_mm=gap,overlap_mm3=overlap)
    led=g.one(current['mapped'],'雨棚LED灯罩壳.SLDPRT')['shape']
    for doc,expected in [('左立柱焊件1.SLDPRT',2),('右立柱焊件1.SLDPRT',2),('遮阳板.SLDPRT',.5)]:
        other=g.one(current['mapped'],doc)['shape']
        gap=led.distToShape(other)[0]
        overlap=led.common(other).Volume
        check('led_gap_'+doc,abs(gap-expected)<.005 and overlap<.001,gap_mm=gap,overlap_mm3=overlap)
    presses=[r for r in current['mapped'] if r['document']=='太阳能连接压板.SLDPRT']
    main=[r for r in current['mapped'] if r['document'] in ('左立柱焊件1.SLDPRT','右立柱焊件1.SLDPRT')]
    matches=[];used=set()
    for r in presses:
        axis=next(f.Surface.Axis for f in r['shape'].Faces if isinstance(f.Surface,Part.Cylinder) and abs(f.Surface.Radius-4)<1e-5)
        holes=[h for h in g.cylinders(r['shape'],g.canonical(axis)) if h['kind']=='bore' and abs(h['radius_mm']-4)<1e-5]
        candidates=[(c,h) for c in main for h in g.cylinders(c['shape'],g.canonical(axis)) if h['kind']=='external' and abs(h['radius_mm']-3)<1e-5]
        assert len(holes)==2
        for h in holes:
            c,p=min(candidates,key=lambda pair:(App.Vector(*pair[1]['axis_point_mm'])-App.Vector(*h['axis_point_mm'])).Length)
            error=(App.Vector(*p['axis_point_mm'])-App.Vector(*h['axis_point_mm'])).Length
            key=(c['instance'],tuple(round(x,4) for x in p['axis_point_mm']))
            matches.append({'press':r['instance'],'column':c['instance'],'axis_error_mm':error,'unique':key not in used});used.add(key)
    check('four_long_press_holes_coaxial',len(matches)==len(used)==4 and all(r['axis_error_mm']<.005 for r in matches),matches=matches)
    report['status']='PASS' if all(r['pass_'] for r in report['checks']) else 'FAIL'
except Exception as exc:
    report['status']='FAIL';report['error']=repr(exc)
    raise
finally:flush()
print(json.dumps({'status':report['status'],'failed':[r for r in report['checks'] if not r['pass_']]},ensure_ascii=True))
sys.exit(0 if report['status']=='PASS' else 1)
