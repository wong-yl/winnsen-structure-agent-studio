"""Read-only geometry checks for the short LED native diagnostic."""
import json
import math
import sys
from pathlib import Path
import FreeCAD as App
import Import
import Part
import shade_geometry_lib_r145 as g

stage=Path(__file__).resolve().parents[1]
native_path=Path(sys.argv[1]).resolve()
output=Path(sys.argv[2]).resolve()
assert not output.exists()
native=g.read(native_path);case=native['cases'][0]
report={'status':'RUNNING','checks':[],'sources':{str(native_path):g.sha(native_path),str(Path(__file__)):g.sha(__file__)},'native_save_performed':False}

def check(name,passed,**data):
    g.check(report,name,passed,**data)
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

def matrix(t):
    m=App.Matrix()
    for r in range(3):
        for c in range(3):setattr(m,f'A{r+1}{c+1}',t[c*3+r])
        setattr(m,f'A{r+1}4',t[9+r]*1000)
    return m

def load(key):
    p=Path(case[key+'_export']['path']);assert g.sha(p)==case[key+'_export']['sha256']
    report['sources'][str(p)]=g.sha(p)
    target=output.with_name(output.stem+'_'+key+'_normalized.step')
    data=g.normalize(p.read_bytes())
    if target.exists():assert target.read_bytes()==data
    else:target.write_bytes(data)
    d=App.newDocument('ShortLed'+key)
    try:
        Import.insert(str(target),d.Name)
        rows=[]
        for obj in d.Objects:
            if obj.TypeId!='Part::Feature' or obj.Shape.isNull():continue
            s=obj.Shape.copy();s.Placement=obj.getGlobalPlacement()
            rows.append({'label':obj.Label,'shape':s})
        return rows
    finally:App.closeDocument(d.Name)

try:
    check('native_diagnostic_completed_without_file_changes',native['status']=='layout_probe_finished' and case['files_unchanged'] and not case['native_save_performed'] and 'apply_error' not in case)
    states=case['canopy_states']
    led_states=[s for s in states if 'LED硬灯条' in s['name']]
    check('two_led_instances_fully_constrained',len(led_states)==2 and all(s['constrained']==3 for s in led_states),states=led_states)
    check('short_parts_native_health',len(case['short_led_parts'])==2 and all(r['rebuild'] and not r['health'] and r['body_visible'] for r in case['short_led_parts']))
    required={'D2@草图1':16,'D1@凸台-拉伸1':1.8,'D1@草图2':13.5,'D2@草图2':15,'D1@凸台-拉伸2':2,'D1@圆顶1':3,'D3@阵列(线性)1':75,'D3@草图3':8,'D4@草图3':4}
    for p in case['short_led_parts']:
        dims={r['name'].split('@LED')[0]:r['value_m']*1000 for r in p['details']['dimensions']}
        check('original_cross_section_and_packages_'+p['side'],all(abs(dims[k]-v)<1e-6 for k,v in required.items()),actual_mm={k:dims.get(k) for k in required})
    meshes={k:load(k) for k in ('led_left','led_right','shell','cover','retainer','canopy')}
    led_shapes=[]
    length=case['short_led_linkage']['strip_length_mm'];count=case['short_led_linkage']['leds_per_row']
    for key,side in [('led_left','L'),('led_right','R')]:
        rows=meshes[key]
        check('one_actual_short_solid_'+side,len(rows)==1 and len(rows[0]['shape'].Solids)==1 and rows[0]['shape'].isValid(),solids=sum(len(r['shape'].Solids) for r in rows))
        shape=rows[0]['shape'];b=g.bounds(shape)
        check('short_strip_dimensions_'+side,max(abs(a-v) for a,v in zip((b[3]-b[0],b[4]-b[1],b[5]-b[2]),(length,6.8,16)))<.005,bounds_mm=b)
        bosses=[c for c in g.cylinders(shape,App.Vector(0,1,0)) if c['kind']=='external' and abs(c['radius_mm']-6.75)<1e-5]
        xs=sorted(c['axis_point_mm'][0] for c in bosses)
        expected=[-length/2+15+75*i for i in range(count)]
        check('unscaled_packages_and_pitch_'+side,len(xs)==count and all(abs(a-b)<.005 for a,b in zip(xs,expected)),expected_centers_mm=expected,actual_centers_mm=xs)
        pose=next(s['transform'] for s in led_states if s['name'].endswith(' '+side+'-1'))
        placed=shape.copy();placed.transformShape(matrix(pose),True);led_shapes.append((side,placed))
    shade_rows=[r for r in meshes['canopy'] if '遮阳板' in r['label']]
    check('unique_shade_in_canopy_export',len(shade_rows)==1,labels=[r['label'] for r in shade_rows])
    shade=shade_rows[0]['shape'];shell=meshes['shell'][0]['shape']
    shell_pose=next(s['transform'] for s in states if s['name']=='雨棚LED灯罩焊接-1')
    shell.transformShape(matrix(shell_pose),True)
    for side,shape in led_shapes:
        bounds=g.bounds(shape);shell_bounds=g.bounds(shell)
        margins=[bounds[0]-shell_bounds[0],shell_bounds[3]-bounds[3]]
        overlap=shape.common(shell).Volume
        check('short_led_inside_shell_'+side,min(margins)>0 and overlap<.001,end_material_clearance_mm=margins,interference_mm3=overlap)
        check('short_led_shade_contact_'+side,shape.distToShape(shade)[0]<.005 and shape.common(shade).Volume<.001,gap_mm=shape.distToShape(shade)[0],interference_mm3=shape.common(shade).Volume)
    gap=led_shapes[0][1].distToShape(led_shapes[1][1])[0];overlap=led_shapes[0][1].common(led_shapes[1][1]).Volume
    check('two_rows_clearance',abs(gap-2)<.005 and overlap<.001,gap_mm=gap,interference_mm3=overlap)
    mates=[m for m in case['canopy_mates'] if m['name'] in ('同心4','同心5','同心6','同心7')]
    pairs=[]
    for mate in mates:
        a,b=mate['entities'];av,bv=a['parameters'],b['parameters'];axis=App.Vector(*av[3:6]);delta=App.Vector(*av[:3])-App.Vector(*bv[:3]);error=delta.cross(axis).Length*1000
        pairs.append({'mate':mate['name'],'axis_error_mm':error,'pilot_axis_mm':[v*1000 for v in av[:3]],'slot_axis_mm':[v*1000 for v in bv[:3]],'radii_mm':[av[6]*1000,bv[6]*1000]})
    check('four_original_mount_axes_coaxial',len(pairs)==4 and all(p['axis_error_mm']<.005 and max(abs(a-b) for a,b in zip(p['radii_mm'],(1.5,2)))<.005 for p in pairs),pairs=pairs)
    report['status']='PASS_LOCAL_NOT_SAVED' if all(r['pass_'] for r in report['checks']) else 'FAIL'
except Exception as exc:
    report['status']='FAIL';report['error']=repr(exc)
finally:output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':report['status'],'failed':[r for r in report['checks'] if not r['pass_']],'error':report.get('error')},ensure_ascii=True))
sys.exit(0 if report['status']=='PASS_LOCAL_NOT_SAVED' else 1)
