import shutil
import subprocess
from generate_model import STAGE,sha,write
target=STAGE/'scripts/block_fix_probe_r328';assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r324',target)
p=target/'FloorFastenersR317.cs';text=p.read_text(encoding='utf-8')
text=text.replace('using SolidWorks.Interop.sldworks;','using SolidWorks.Interop.sldworks;\nusing SolidWorks.Interop.swconst;')
text=text.replace('floor.SketchAddConstraints("sgFIXED");','var fixedBlock=s.RelationManager.AddRelation(new object[]{b.block},(int)swConstraintType_e.swConstraintType_BLOCKFIXEDLOCK);Need(fixedBlock!=null,"Native fixed block relation rejected");')
p.write_text(text,encoding='utf-8')
p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8').replace('else if(mode=="layout_probe")LayoutProbe(c,row);','else if(mode=="layout_probe")Width600Probe(c,row);');p.write_text(text,encoding='utf-8')
p=target/'Width600ProbeR311.cs';text=(STAGE/'scripts/Width600ProbeR311.cs').read_text(encoding='utf-8').replace('"箱体内顶板.SLDPRT","盖板.SLDPRT"','"箱体底板.SLDPRT","箱体内顶板.SLDPRT","盖板.SLDPRT"');p.write_text(text,encoding='utf-8')
tool=STAGE/'OutdoorBlockFixProbeR328.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
p=STAGE/'scripts/run_block_fix_probe_r328.mjs';p.write_text((STAGE/'scripts/run_width600_probe_r311.mjs').read_text(encoding='utf-8').replace('OutdoorWidth600ProbeR311.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/native_block_fix_probe_r328_build.json',{'tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
