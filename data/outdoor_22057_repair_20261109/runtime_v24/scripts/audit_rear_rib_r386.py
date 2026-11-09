"""Check real rear sheet/rib pilot pairs, material rims and nearby clearances."""
import json
import math
import sys
from pathlib import Path
import FreeCAD as App
import Part
import shade_geometry_lib_r145 as g
from layout_contract import validate_specification

native_path,contract_path,output=[Path(p).resolve() for p in sys.argv[1:4]]
assert not output.exists()
native,request=g.read(native_path),g.read(contract_path)['cases'][0]
case=native['cases'][0]
spec=validate_specification(dict(zip(('height','width','depth'),request['dimensions_mm']),columns=request['columns']))
assert spec['width']<900
report={'status':'RUNNING','checks':[],'sources':{str(p):g.sha(p) for p in [native_path,contract_path,Path(__file__)]},'production_release':False}
Z=App.Vector(0,0,1)

def check(name,passed,**data):
    g.check(report,name,passed,**data)
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

def bores(shape):
    return [h for h in g.cylinders(shape,Z) if h['kind']=='bore' and abs(h['radius_mm']-1)<1e-5]

def overlap(a,b):
    aa,bb=g.bounds(a),g.bounds(b)
    if any(min(aa[i+3],bb[i+3])-max(aa[i],bb[i])<1e-7 for i in range(3)):return 0
    return a.common(b).Volume

try:
    rows=g.load_case(case,output.stem,'rear_rib',report['sources'])['mapped']
    center=max(75,spec['width']/2-300)
    selected=[]
    for side,doc,ribdoc,sign in [('L','箱体左侧板.SLDPRT','箱体侧板加强筋.SLDPRT',-1),('R','箱体右侧板.SLDPRT','OUT_RightSideRib.SLDPRT',1)]:
        panel=g.one(rows,doc)
        ribs=[r for r in rows if r['document']==ribdoc and len(bores(r['shape']))==3]
        check('one_rear_rib_'+side,len(ribs)==1,instances=[r['instance'] for r in ribs])
        rib=ribs[0];selected.append(rib)
        targets=sorted([(sign*center,spec['height']-118.8),(sign*(center-15),60),(sign*(center+15),60)])
        for kind,row in [('sheet',panel),('rib',rib)]:
            holes=sorted(bores(row['shape']),key=lambda h:(h['axis_point_mm'][0],h['axis_point_mm'][1]))
            check('three_coaxial_real_pilots_'+side+'_'+kind,len(holes)==3 and all(abs(h['axis_point_mm'][0]-x)<.005 and abs(h['axis_point_mm'][1]-y)<.005 for h,(x,y) in zip(holes,targets)),expected_xy_mm=targets,actual=holes)
            for i,h in enumerate(holes):
                qmin=min(q[0] for q in h['intervals_q_mm']);qmax=max(q[1] for q in h['intervals_q_mm']);thickness=qmax-qmin
                area=sum(row['shape'].Faces[j-1].Area for j in h['faces'])
                p=App.Vector(h['axis_point_mm'][0],h['axis_point_mm'][1],qmin)
                ring=Part.makeCylinder(3,thickness,p,Z).cut(Part.makeCylinder(1,thickness,p,Z))
                actual=row['shape'].common(ring).Volume
                check('complete_bore_and_3mm_material_rim_'+side+'_'+kind+'_'+str(i),thickness>.1 and abs(area-2*math.pi*thickness)<.01 and abs(actual-ring.Volume)<.02,thickness_mm=thickness,cylinder_area_mm2=area,expected_rim_mm3=ring.Volume,actual_rim_mm3=actual)
        gap=panel['shape'].distToShape(rib['shape'])[0]
        check('rear_rib_sheet_installation_'+side,abs(gap-.2)<.005 and overlap(panel['shape'],rib['shape'])<.02,nominal_gap_mm=gap,overlap_mm3=overlap(panel['shape'],rib['shape']))
        check('original_50mm_rear_rib_width_'+side,abs(rib['shape'].BoundBox.XLength-50)<.005,actual_mm=rib['shape'].BoundBox.XLength)
        for neighbor in ['箱体竖隔板.SLDPRT','箱体横层板L.SLDPRT','箱体横层板R.SLDPRT']:
            values=[overlap(rib['shape'],r['shape']) for r in rows if r['document']==neighbor]
            check('rear_rib_neighbor_clearance_'+side+'_'+neighbor,all(v<.02 for v in values),overlap_mm3=values)
    gap=selected[0]['shape'].distToShape(selected[1]['shape'])[0]
    check('left_right_rear_rib_separation',gap>99.99 and overlap(selected[0]['shape'],selected[1]['shape'])<.02,actual_gap_mm=gap)
    report['status']='PASS' if all(c['pass_'] for c in report['checks']) else 'FAIL'
except Exception as exc:
    report['status']='FAIL';report['error']=repr(exc)
finally:output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':report['status'],'error':report.get('error'),'failed':[c for c in report['checks'] if not c['pass_']]},ensure_ascii=True))
sys.exit(0 if report['status']=='PASS' else 1)
