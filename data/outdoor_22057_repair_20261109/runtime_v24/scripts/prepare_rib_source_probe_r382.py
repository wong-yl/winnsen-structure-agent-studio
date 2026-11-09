"""Throwaway source-sketch and physical face diagnosis; never saves CAD."""
import shutil
import subprocess
from generate_model import STAGE, sha, write
target=STAGE/'scripts/rib_source_probe_r382'
assert not target.exists()
shutil.copytree(STAGE/'scripts/rib_phase_probe_r381',target)
p=target/'RibPhaseProbeR381.cs'
t=p.read_text(encoding='utf-8')
start=t.index('    static void RibPhaseProbe(')
t=t[:start]+r'''    static void RibSourceTrace(string label,Dictionary<string,object> row) {
        row["source_"+label]=new[]{"箱体左侧板.SLDPRT","箱体右侧板.SLDPRT","箱体侧板加强筋.SLDPRT","OUT_RightSideRib.SLDPRT"}.Select(n=>{
            var d=Doc(n);return new{name=n,box=RmBox(d),sketches=RibSketches(d),health=LayoutFeatureHealth(d)};}).ToArray();
        RibTrace(label,row);Flush("rib_source/"+label);
    }
    static void RibPhaseProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];PrepareLayout(c,row);var before=Identity(Root);
        try {
            var top=OpenTop(row);RibSourceTrace("opened",row);var changes=new List<object>();row["input_changes"]=changes;
            var master=Doc("防水寄存柜 模型.SLDPRT");Set(master,"D1@草图1",600,changes);Set(master,"D2@草图1",1660,changes);Set(master,"D1@凸台-拉伸1",450,changes);
            Need(master.ForceRebuild3(false),"Master failed");Set(Doc("箱体左侧板.SLDPRT"),"D2@草图68",265,changes);
            top.ForceRebuild3(false);RibSourceTrace("master_only",row);
            foreach(string n in new[]{"箱体左侧板.SLDPRT","箱体右侧板.SLDPRT","OUT_RightSideRib.SLDPRT","箱体右侧板焊接.SLDASM"}){var d=Doc(n);RmActivate(d,"source_rebuild/"+n);d.ForceRebuild3(false);}
            RibSourceTrace("explicit_rebuild",row);
            TrExport(Doc("箱体左侧板.SLDPRT"),"left_panel",row);TrExport(Doc("箱体右侧板.SLDPRT"),"right_panel",row);TrExport(Doc("OUT_RightSideRib.SLDPRT"),"right_rib",row);
            row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
'''
p.write_text(t,encoding='utf-8')
tool=STAGE/'OutdoorRibSourceProbeR382.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]],check=True)
(STAGE/'scripts/run_rib_source_probe_r382.mjs').write_text((STAGE/'scripts/run_rib_phase_probe_r381.mjs').read_text(encoding='utf-8').replace('OutdoorRibPhaseProbeR381.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/rib_source_probe_r382_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
