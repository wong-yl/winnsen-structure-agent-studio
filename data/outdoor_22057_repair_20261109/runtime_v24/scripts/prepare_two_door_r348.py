import shutil
import subprocess
from generate_model import STAGE,sha,write

target=STAGE/'scripts/width600_native_r348';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r343',target)
for name,source in [('ShelfWidthLinkR341.cs',STAGE/'scripts/ShelfWidthLinkR341.cs'),('ShelfNotchLinkR286.cs',STAGE/'scripts/width600_native_r339/ShelfNotchLinkR286.cs')]:
    shutil.copy2(source,target/name)
p=target/'OutdoorShadeR145.cs';t=p.read_text(encoding='utf-8')
key='Need(master.ForceRebuild3(false),"Cabinet master rebuild failed");'
assert t.count(key)==1
t=t.replace(key,key+'\n        SyncNarrowShelfWidth(Doc("箱体横层板L.SLDPRT"),w,changes,row);SyncShelfNotchHeight(Doc("箱体横层板L.SLDPRT"),changes,row);\n        var narrowShelfRight=Doc("箱体横层板R.SLDPRT");Need(narrowShelfRight.ForceRebuild3(false)&&narrowShelfRight.Extension.GetWhatsWrongCount()==0,"Mirrored narrow shelf failed");')
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorWidth600R348.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
write(STAGE/'evidence/native_two_door_r348_build.json',{'status':'BUILT_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'baseline':'width600_native_r343','reason':'Snapshot audits every packaged native document including suppressed shelf seeds','sources':{str(p):sha(p) for p in target.glob('*.cs')}})
