"""Read saved geometry only: inventory cover nuts and their carrier sheet."""
import json
from pathlib import Path
import FreeCAD as App
import shade_geometry_lib_r145 as g

stage=Path(__file__).resolve().parents[1]
native=stage/'evidence/width600_low_r399_reopen_verify.json'
out=stage/'evidence/cover_bodies_r403.json'
assert not out.exists()
sources={str(native):g.sha(native),str(Path(__file__)):g.sha(Path(__file__))}
rows=g.load_case(g.read(native)['cases'][0],out.stem,'diagnostic',sources)['mapped']
inner=[r for r in rows if r['document']=='箱体内顶板.SLDPRT']
main=max(inner,key=lambda r:r['shape'].Volume)
data=[]
for r in inner:
    cylinders=g.cylinders(r['shape'],App.Vector(0,1,0))
    data.append({'instance':r['instance'],'body':r['body'],'bounds_mm':g.bounds(r['shape']),
        'cylinders':[h for h in cylinders if h['radius_mm']<5],
        'main_gap_mm':r['shape'].distToShape(main['shape'])[0],
        'main_intersection_mm3':r['shape'].common(main['shape']).Volume})
out.write_text(json.dumps({'status':'DIAGNOSTIC','sources':sources,'bodies':data},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(data,ensure_ascii=True))
