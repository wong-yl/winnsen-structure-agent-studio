"""Block-only source sketch reports underconstrained; verify its two fixed block relations directly."""
import shutil
import subprocess
from generate_model import STAGE,sha,write
builds=[]
for base,revision in [('width600_native_r322',324),('width600_native_r323',325)]:
    target=STAGE/'scripts'/('width600_native_r'+str(revision));assert not target.exists()
    shutil.copytree(STAGE/'scripts'/base,target)
    p=target/'FloorFastenersR317.cs';text=p.read_text(encoding='utf-8')
    text=text.replace('Need(s.GetConstrainedStatus()==3,"Floor nut blocks must be constrained");','var fixedRelations=(s.RelationManager.GetRelations(0) as Array??new object[0]).Cast<SketchRelation>().Count(r=>r.GetRelationType()==17||r.GetRelationType()==70);row["floor_fixed_block_relations"]=fixedRelations;Flush("floor_fixed_block_relations");Need(fixedRelations==2&&blocks.All(b=>b.block.LockAngle),"Both floor nut blocks must have fixed relations and locked angles");')
    p.write_text(text,encoding='utf-8')
    tool=STAGE/('OutdoorWidth600R'+str(revision)+'.exe')
    subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
    builds.append({'tool':str(tool),'sha256':sha(tool),'baseline':base,'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
p=STAGE/'scripts/run_native.mjs';text=p.read_text(encoding='utf-8').replace('OutdoorWidth600R323.exe','OutdoorWidth600R325.exe').replace('OutdoorWidth600R322.exe','OutdoorWidth600R324.exe');p.write_text(text,encoding='utf-8')
write(STAGE/'evidence/native_floor_block_state_r324_build.json',{'status':'BUILT_NOT_ACCEPTED','source_block_only_sketch_status':2,'source_evidence':str(STAGE/'evidence/width600_floor_r316_layout_probe.json'),'builds':builds})
