"""Throwaway, no-save survey of the original roof-post mounting partners."""
import json
import shutil
import subprocess
from generate_model import STAGE, sha, write, read

target = STAGE / 'scripts/roof_mount_probe_r393'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/roof_pin_probe_r390', target)
p = target / 'RoofPinProbeR390.cs'
t = p.read_text(encoding='utf-8')
key = 'row["roof_"+label]='
assert t.count(key) == 1
t = t.replace(key, '''row["mount_"+label]=new{top_mates=LayoutMates(Doc(Top)),top_states=ShStates(Doc(Top)),parts=new[]{"锁控板装配.SLDASM","锁控板支架.SLDPRT","LK-4-6-V2.0 24口锁控板.SLDPRT","箱体焊接.SLDASM"}.Select(n=>{var d=Doc(n);return new{name=n,details=ShDetails(d),cylinders=d is PartDoc?Cylinders(d):null,mates=d is AssemblyDoc?LayoutMates(d):null,states=d is AssemblyDoc?ShStates(d):null};}).ToArray()};
        row["roof_"+label]=''')
p.write_text(t, encoding='utf-8')
tool = STAGE / 'OutdoorRoofMountProbeR393.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/out:'+str(tool), '/reference:System.Web.Extensions.dll', '/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'), '/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'), *map(str, target.glob('*.cs'))], check=True)
(STAGE/'scripts/run_roof_mount_probe_r393.mjs').write_text((STAGE/'scripts/run_roof_pin_probe_r391.mjs').read_text(encoding='utf-8').replace('OutdoorRoofPinProbeR391.exe', tool.name), encoding='utf-8')
c = read(STAGE/'evidence/roof_pin_r391_contract.json')
r = c['cases'][0]
r['id'] = 'roof_mount_r393'
r['package_root'] = r['relocated_root'] = str(STAGE/'generated_roof_mount_r393')
write(STAGE/'evidence/roof_mount_r393_contract.json', c)
write(STAGE/'evidence/roof_mount_probe_r393_build.json', {'status':'DIAGNOSTIC_NOT_ACCEPTED', 'tool':str(tool), 'sha256':sha(tool), 'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
