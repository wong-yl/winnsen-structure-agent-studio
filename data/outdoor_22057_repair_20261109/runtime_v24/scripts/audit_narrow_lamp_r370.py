"""Measure narrow lamp/LED geometry from the independently reopened whole assembly."""
import json
import sys
from pathlib import Path
import FreeCAD as App
import Part
import shade_geometry_lib_r145 as g
from layout_contract import validate_specification

native_path,contract_path,output=[Path(p).resolve() for p in sys.argv[1:4]]
assert not output.exists(),'Preserve lamp evidence'
native,request=g.read(native_path),g.read(contract_path)['cases'][0]
case=native['cases'][0]
spec=validate_specification(dict(zip(('height','width','depth'),request['dimensions_mm']),columns=request['columns']))
width=spec['width'];assert 600<=width<900
count=max(2,int((width-470-53.5)//75)+1);count-=count%2
length=75*(count-1)+30
report={'status':'RUNNING','checks':[],'sources':{str(p):g.sha(p) for p in [native_path,contract_path,Path(__file__)]},'production_release':False}

def check(name,passed,**data):
    g.check(report,name,passed,**data)
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

def matrix(t):
    m=App.Matrix()
    for r in range(3):
        for c in range(3):setattr(m,f'A{r+1}{c+1}',t[c*3+r])
        setattr(m,f'A{r+1}4',t[9+r]*1000)
    return m

try:
    current=g.load_case(case,output.stem,'narrow_lamp',report['sources'])
    rows=current['mapped'];instances={r['name']:r for r in case['snapshot']['instances']}
    def own(doc):return [r for r in rows if r['document']==doc]
    def local(row):
        s=row['shape'].copy();s.transformShape(matrix(instances[row['instance']]['transform']).inverse(),True);return s
    shell=g.one(rows,'雨棚LED灯罩壳.SLDPRT')['shape'];cover=g.one(rows,'塑料灯罩.SLDPRT')['shape'];shade=g.one(rows,'遮阳板.SLDPRT')['shape']
    retainers=own('塑料板压条焊件.SLDPRT')
    check('one_shell_cover_two_retainers',len(own('雨棚LED灯罩壳.SLDPRT'))==len(own('塑料灯罩.SLDPRT'))==1 and len(retainers)==2)
    for doc,expected in [('雨棚LED灯罩壳.SLDPRT',width-222),('塑料灯罩.SLDPRT',width-443),('塑料板压条焊件.SLDPRT',width-440)]:
        measured=[r['shape'].BoundBox.XLength for r in own(doc)]
        check('approved_actual_width_'+doc,bool(measured) and all(abs(v-expected)<.005 for v in measured),expected_mm=expected,actual_mm=measured)
    shell_local=local(g.one(rows,'雨棚LED灯罩壳.SLDPRT'))
    windows=[]
    for f in shell_local.Faces:
        if not isinstance(f.Surface,Part.Plane) or abs(f.Surface.Axis.y)<.99:continue
        for wire in f.Wires:
            b=wire.BoundBox
            if len(wire.Edges)==4 and abs(b.XLength-(width-470))<.005 and abs(b.ZLength-80)<.005:windows.append(g.bounds(wire))
    check('two_complete_lamp_window_walls',len(windows)==2 and all(abs((b[0]+b[3])/2)<.005 for b in windows),window_bounds_mm=windows,expected_width_mm=width-470)
    cover_local=local(g.one(rows,'塑料灯罩.SLDPRT'))
    check('cover_keeps_3mm_thickness_and_overlap',abs(cover_local.BoundBox.YLength-3)<.005 and abs(cover_local.BoundBox.ZLength-105)<.005 and abs((cover_local.BoundBox.XLength-(width-470))/2-13.5)<.005,side_overlap_mm=(cover_local.BoundBox.XLength-(width-470))/2,end_overlap_mm=(cover_local.BoundBox.ZLength-80)/2)
    for r in retainers:
        s=r['shape'];contact=s.distToShape(cover)[0];shell_contact=s.distToShape(shell)[0];a=s.common(cover).Volume;b=s.common(shell).Volume
        check('retainer_cover_and_shell_contact_'+r['instance'],contact<.005 and shell_contact<.005 and max(a,b)<.001,cover_gap_mm=contact,shell_gap_mm=shell_contact,interference_mm3=[a,b])
    check('cover_shell_1mm_gap',abs(cover.distToShape(shell)[0]-1)<.005 and cover.common(shell).Volume<.001,gap_mm=cover.distToShape(shell)[0])
    saved=case['narrow_lamp_saved_parts'];check('saved_native_lamp_documents_healthy',len(saved)==5 and all(not p['health'] for p in saved))
    for p in saved:
        if p['name']=='塑料板压条焊件.SLDPRT':
            dims={d['name'].split('@塑料')[0]:d['value_m'] for d in p['details']['dimensions']}
            check('five_retainer_fingers_linked',abs(dims['D3@阵列(线性)1']*1000-(width-490)/4)<.005 and dims['D1@阵列(线性)1']==5,actual_pitch_mm=dims['D3@阵列(线性)1']*1000)
    leds=[]
    required={'D2@草图1':16,'D1@凸台-拉伸1':1.8,'D1@草图2':13.5,'D2@草图2':15,'D1@凸台-拉伸2':2,'D1@圆顶1':3,'D3@阵列(线性)1':75,'D3@草图3':8,'D4@草图3':4}
    for side in ('L','R'):
        doc='LED硬灯条(蓝景13点白色930×16mm) '+side+'.SLDPRT'
        selected=own(doc);check('single_actual_led_body_'+side,len(selected)==1)
        r=selected[0];s=local(r);leds.append(r['shape'])
        check('short_led_actual_dimensions_'+side,max(abs(a-b) for a,b in zip((s.BoundBox.XLength,s.BoundBox.YLength,s.BoundBox.ZLength),(length,6.8,16)))<.005,actual_mm=[s.BoundBox.XLength,s.BoundBox.YLength,s.BoundBox.ZLength])
        cylinders=[h for h in g.cylinders(s,App.Vector(0,1,0)) if h['kind']=='external' and abs(h['radius_mm']-6.75)<1e-5]
        xs=sorted(h['axis_point_mm'][0] for h in cylinders);expected=[-length/2+15+75*i for i in range(count)]
        check('original_led_packages_and_75mm_pitch_'+side,len(xs)==count and all(abs(a-b)<.005 for a,b in zip(xs,expected)),actual_centers_mm=xs,expected_centers_mm=expected)
        p=next(p for p in saved if p['name']==doc);dims={d['name'].split('@LED')[0]:d['value_m']*1000 for d in p['details']['dimensions']}
        check('saved_unscaled_cross_section_'+side,all(abs(dims[k]-v)<1e-6 for k,v in required.items()))
        margins=[r['shape'].BoundBox.XMin-shell.BoundBox.XMin,shell.BoundBox.XMax-r['shape'].BoundBox.XMax]
        check('led_shell_and_shade_interfaces_'+side,min(margins)>0 and r['shape'].common(shell).Volume<.001 and r['shape'].common(shade).Volume<.001 and r['shape'].distToShape(shade)[0]<.005,end_clearance_mm=margins,shade_contact_mm=r['shape'].distToShape(shade)[0])
        world=matrix(instances[r['instance']]['transform'])
        bead_x=[world.multVec(App.Vector(x,3.8,0)).x for x in xs]
        check('all_packages_behind_actual_window_'+side,min(bead_x)-6.75>=-(width-470)/2+.005 and max(bead_x)+6.75<=(width-470)/2-.005,package_centers_world_x_mm=bead_x,window_x_mm=[-(width-470)/2,(width-470)/2])
    check('led_rows_2mm_gap',abs(leds[0].distToShape(leds[1])[0]-2)<.005 and leds[0].common(leds[1]).Volume<.001,gap_mm=leds[0].distToShape(leds[1])[0])
    mates=[m for m in case['canopy_mates'] if m['name'] in ('同心4','同心5','同心6','同心7')];pairs=[]
    for m in mates:
        a,b=[e['parameters'] for e in m['entities']];error=(App.Vector(*a[:3])-App.Vector(*b[:3])).cross(App.Vector(*a[3:6])).Length*1000
        pairs.append({'mate':m['name'],'axis_error_mm':error,'radii_mm':[a[6]*1000,b[6]*1000]})
    check('four_mount_pilot_axes_coaxial',len(pairs)==4 and all(p['axis_error_mm']<.005 and max(abs(a-b) for a,b in zip(p['radii_mm'],(1.5,2)))<.005 for p in pairs),pairs=pairs)
    states=[s for s in case['canopy_states'] if 'LED硬灯条' in s['name'] or s['name']=='雨棚LED灯罩焊接-1']
    check('led_and_lamp_fully_constrained',len(states)==3 and all(s['constrained']==3 for s in states),states=[{'name':s['name'],'constrained':s['constrained']} for s in states])
    report['status']='PASS' if all(c['pass_'] for c in report['checks']) else 'FAIL'
except Exception as exc:
    report['status']='FAIL';report['error']=repr(exc)
finally:output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':report['status'],'failed':[c for c in report['checks'] if not c['pass_']],'error':report.get('error')},ensure_ascii=True))
sys.exit(0 if report['status']=='PASS' else 1)
