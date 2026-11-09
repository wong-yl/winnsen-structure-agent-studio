import json
from pathlib import Path
import FreeCAD as App
import Import
import shade_geometry_lib_r145 as g

stage=Path(__file__).resolve().parents[1]
native_path=stage/'evidence/lamp_candidate_r360_layout_probe.json'
native=g.read(native_path);case=native['cases'][0]
report={'status':'DIAGNOSTIC','sources':{str(native_path):g.sha(native_path)},'parts':[],'native_save_performed':False}
for key in ['shell','cover','retainer','canopy']:
    source=Path(case[key+'_export']['path']);assert g.sha(source)==case[key+'_export']['sha256']
    target=stage/'evidence'/('lamp_candidate_r361_'+key+'_normalized.step')
    clean=g.normalize(source.read_bytes())
    if target.exists():assert target.read_bytes()==clean
    else:target.write_bytes(clean)
    report['sources'][str(source)]=g.sha(source)
    doc=App.newDocument('LampR361'+key)
    try:
        Import.insert(str(target),doc.Name)
        values=[]
        for obj in doc.Objects:
            if obj.TypeId!='Part::Feature' or obj.Shape.isNull():continue
            shape=obj.Shape.copy();shape.Placement=obj.getGlobalPlacement()
            values.append({'label':obj.Label,'bounds_mm':g.bounds(shape),'volume_mm3':shape.Volume,'valid':shape.isValid()})
        report['parts'].append({'kind':key,'solids':len(values),'geometry':values})
    finally:App.closeDocument(doc.Name)
visible=[]
for instance in case['led_instances']:
    for body in instance['bodies']:
        if not body['visible']:continue
        b=body['bounds_m'];points=[g.transformed([x,y,z],instance['transform']) for x in [b[0],b[3]] for y in [b[1],b[4]] for z in [b[2],b[5]]]
        visible.append({'instance':instance['name'],'body':body['name'],'x_mm':[min(p.x for p in points),max(p.x for p in points)]})
report['native_visible_led_bodies']=visible
report['visible_led_x_mm']=[min(row['x_mm'][0] for row in visible),max(row['x_mm'][1] for row in visible)]
report['visible_led_span_mm']=report['visible_led_x_mm'][1]-report['visible_led_x_mm'][0]
out=stage/'evidence/lamp_candidate_geometry_r361.json'
assert not out.exists();out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':report['status'],'visible_led_span_mm':report['visible_led_span_mm'],'visible_led_x_mm':report['visible_led_x_mm'],'parts':[(p['kind'],p['solids']) for p in report['parts']]},ensure_ascii=True))
