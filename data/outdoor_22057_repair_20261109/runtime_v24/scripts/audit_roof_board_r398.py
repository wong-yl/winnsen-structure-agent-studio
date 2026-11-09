"""Verify connected roof posts, unchanged controller mounts and nearby geometry."""
import json
import math
import sys
from pathlib import Path
import FreeCAD as App
import shade_geometry_lib_r145 as g

native_path,contract_path,output=[Path(p).resolve() for p in sys.argv[1:4]]
assert not output.exists()
native,request=g.read(native_path),g.read(contract_path)['cases'][0]
case=native['cases'][0]
width=request['dimensions_mm'][1]
assert 600<=width<900
report={'status':'RUNNING','checks':[],'sources':{str(p):g.sha(p) for p in [native_path,contract_path,Path(__file__)]},'production_release':False}
normal=App.Vector(0,math.cos(math.radians(2)),-math.sin(math.radians(2)))

def check(name,passed,**data):
    g.check(report,name,passed,**data)
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

def overlap(a,b):
    aa,bb=g.bounds(a),g.bounds(b)
    if any(min(aa[i+3],bb[i+3])-max(aa[i],bb[i])<1e-7 for i in range(3)):return 0
    return a.common(b).Volume

def signature(shape):
    return [shape.Volume,shape.Area,*sorted(e.Length for e in shape.Edges)]

try:
    rows=g.load_case(case,output.stem,'roof_board',report['sources'])['mapped']
    roofrows=[r for r in rows if r['document']=='箱体顶板.SLDPRT']
    check('one_connected_original_roof_body',len(roofrows)==1,actual_bodies=len(roofrows))
    roof=roofrows[0]['shape']
    pins=sorted([c for c in g.cylinders(roof,normal) if c['kind']=='external' and abs(c['radius_mm']-2)<1e-5],key=lambda c:c['axis_point_mm'][0])
    center=min(260,width/2-125)
    expected=[-center-80,-center+80,center-80,center+80]
    check('four_original_4mm_posts_at_linked_centers',len(pins)==4 and all(abs(p['axis_point_mm'][0]-x)<.005 for p,x in zip(pins,expected)),expected_x_mm=expected,actual=pins)
    for i,pin in enumerate(pins):
        length=max(q[1] for q in pin['intervals_q_mm'])-min(q[0] for q in pin['intervals_q_mm'])
        check('original_10mm_post_length_'+str(i),abs(length-10)<.005,actual_mm=length)
    baseline_path=Path(g.read(Path(__file__).resolve().parents[1]/'seed_manifest.json')['root']).parent/'evidence/source_requal_r146_sizing_survey.json'
    report['sources'][str(baseline_path)]=g.sha(baseline_path)
    baseline=g.load_case(g.read(baseline_path)['cases'][0],output.stem,'roof_board_baseline',report['sources'])['mapped']
    brackets=[r for r in rows if r['document']=='锁控板支架.SLDPRT']
    boards=[r for r in rows if r['document']=='LK-4-6-V2.0 24口锁控板.SLDPRT']
    check('two_original_brackets_and_controllers',len(brackets)==len(boards)==2,brackets=len(brackets),controllers=len(boards))
    matches=[]
    for bracket in brackets:
        side='L' if bracket['shape'].CenterOfMass.x<0 else 'R'
        base=g.one(baseline,bracket['document'],side)
        a,b=signature(bracket['shape']),signature(base['shape'])
        check('standard_bracket_geometry_'+side,len(a)==len(b) and max(abs(x-y) for x,y in zip(a,b))<.005,max_signature_error=max(abs(x-y) for x,y in zip(a,b)))
        holes=[h for h in g.cylinders(bracket['shape'],normal) if h['kind']=='bore' and abs(h['radius_mm']-3)<1e-5]
        own=[]
        for i,pin in enumerate(pins):
            for j,hole in enumerate(holes):
                delta=(App.Vector(*hole['axis_point_mm'])-App.Vector(*pin['axis_point_mm'])).Length
                penetration=max(min(a[1],b[1])-max(a[0],b[0]) for a in pin['intervals_q_mm'] for b in hole['intervals_q_mm'])
                if delta<.005 and penetration>.1:own.append((i,j));matches.append(i)
        check('two_actual_coaxial_supported_posts_'+side,len(own)==2 and len({j for i,j in own})==2,matches=own)
        check('roof_bracket_nominal_contact_'+side,roof.distToShape(bracket['shape'])[0]<.005 and overlap(roof,bracket['shape'])<.02,distance_mm=roof.distToShape(bracket['shape'])[0],overlap_mm3=overlap(roof,bracket['shape']))
    check('unique_four_post_coverage',len(matches)==len(set(matches))==4,post_indices=matches)
    for board in boards:
        side='L' if board['shape'].CenterOfMass.x<0 else 'R'
        base=g.one(baseline,board['document'],side)
        a,b=signature(board['shape']),signature(base['shape'])
        check('standard_controller_geometry_'+side,len(a)==len(b) and max(abs(x-y) for x,y in zip(a,b))<.005)
    protected_neighbors={'顶部维护门板.SLDPRT','箱体内顶板.SLDPRT','箱体左侧板.SLDPRT','箱体右侧板.SLDPRT','箱体竖隔板.SLDPRT','顶隔板.SLDPRT'}
    for part in brackets+boards:
        values=[{'instance':r['instance'],'overlap_mm3':overlap(part['shape'],r['shape'])} for r in rows if r['document'] in protected_neighbors]
        check('controller_neighbors_'+part['instance'],all(v['overlap_mm3']<.02 for v in values),values=values)
    check('separate_left_and_right_brackets',overlap(brackets[0]['shape'],brackets[1]['shape'])<.02,gap_mm=brackets[0]['shape'].distToShape(brackets[1]['shape'])[0])
    mates={m['name']:m for m in case['roof_board_saved_mates']}
    for name in ['重合29','同心101','同心102']:
        check('original_active_mount_mate_'+name,name in mates and not any(mates[name]['suppressed']),mate=mates.get(name))
    report['status']='PASS' if all(c['pass_'] for c in report['checks']) else 'FAIL'
except Exception as exc:
    report['status']='FAIL';report['error']=repr(exc)
finally:output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':report['status'],'error':report.get('error'),'failed':[c for c in report['checks'] if not c['pass_']]},ensure_ascii=True))
sys.exit(0 if report['status']=='PASS' else 1)
