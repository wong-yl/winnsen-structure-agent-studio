"""No-save diagnosis of shelf feature context after replacing shelf instances."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/shelf_context_probe_r416'
assert not target.exists()
shutil.copytree(STAGE/'scripts/width600_native_r409',target)
p=target/'OutdoorShadeR145.cs';text=p.read_text(encoding='utf-8')
key='else if(mode=="layout_probe")LayoutProbe(c,row);'
assert text.count(key)==1
p.write_text(text.replace(key,'else if(mode=="layout_probe")ShelfContextProbe(c,row);'),encoding='utf-8')
(target/'ShelfContextProbeR416.cs').write_text(r'''using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object ShelfProbeState(ModelDoc2 d) {
        var sketchNames=new[]{"草图1","草图5","草图33"};
        return new{rebuilt=d.ForceRebuild3(false),issues=d.Extension.GetWhatsWrongCount(),health=LayoutFeatureHealth(d),details=ShDetails(d),
            parents=Features(d).Where(f=>f.Name=="切除-拉伸2"||f.Name=="切除-拉伸4"||sketchNames.Contains(f.Name)).Select(f=>new{name=f.Name,type=f.GetTypeName2(),parents=(f.GetParents() as Array??new object[0]).Cast<Feature>().Select(v=>v.Name).ToArray()}).ToArray(),
            profiles=sketchNames.Select(n=>new{name=n,center_y_mm=LayoutSketchCenterY(d,n),points=((Array)((Sketch)RmFeature(d,n).GetSpecificFeature2()).GetSketchPoints2()).Cast<SketchPoint>().Select(p=>new[]{p.X,p.Y,p.Z}).ToArray()}).ToArray()};
    }
    static void ShelfContextProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];var before=Identity(Root);
        try {
            var top=OpenTop(row);var shelf=Doc("箱体横层板L.SLDPRT");row["baseline_shelf"]=ShelfProbeState(shelf);Flush("shelf_baseline");
            var dims=((System.Collections.IEnumerable)c["dimensions_mm"]).Cast<object>().Select(Convert.ToDouble).ToArray();
            ApplyInputs(top,dims[0],dims[1],dims[2],row);row["after_inputs_shelf"]=ShelfProbeState(shelf);Flush("shelf_after_inputs");
            var cabinet=Doc("箱体焊接.SLDASM");var parts=((Array)((AssemblyDoc)cabinet).GetComponents(false)).Cast<Component2>().ToArray();
            var seeds=new Dictionary<string,Tuple<double[],double>>();
            foreach(string side in new[]{"L","R"}){
                var seed=parts.Single(p=>p.Name2==(side=="L"?"箱体横层板L焊接-1":"箱体横层板R焊接-2"));
                var leaf=parts.Single(p=>p.Name2.StartsWith(seed.Name2+"/")&&Path.GetFileName(p.GetPathName())=="箱体横层板"+side+".SLDPRT");
                seeds[side]=Tuple.Create((double[])seed.Transform2.ArrayData,LayoutExtremeY(leaf,false));
            }
            var changes=new List<object>();row["shelf_instance_changes"]=changes;
            LayoutSuppress(cabinet,p=>Path.GetFileName(p.GetPathName()).StartsWith("箱体横层板")||Path.GetFileName(p.GetPathName())=="箱体门轴固定板.SLDPRT",new[]{"局部线性阵列1","局部线性阵列2"},changes);
            row["after_suppression_shelf"]=ShelfProbeState(shelf);Flush("shelf_after_suppression");
            foreach(var r in LayoutRows(c).Where(r=>!(bool)r["top_door"])){
                string side=(string)r["side"];double boundary=Convert.ToDouble(r["boundary_mm"]);var seed=seeds[side];var transform=(double[])seed.Item1.Clone();
                transform[10]+=(7+boundary-seed.Item2)/1000;LayoutAdd(cabinet,"箱体横层板"+side+"焊接.SLDASM","Default",transform,(string)r["prefix"]+"_Shelf");
            }
            row["after_replacement_shelf"]=ShelfProbeState(shelf);Flush("shelf_after_replacement");
            var notchChanges=new List<object>();row["notch_retry_changes"]=notchChanges;
            try{SyncShelfNotchHeight(shelf,notchChanges,row);}catch(Exception ex){row["notch_retry_error"]=ex.ToString();}
            row["after_notch_retry_shelf"]=ShelfProbeState(shelf);Flush("shelf_after_notch_retry");
            row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''',encoding='utf-8')
tool=STAGE/'OutdoorShelfContextProbeR416.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
runner=STAGE/'scripts/run_shelf_context_probe_r416.mjs'
assert not runner.exists()
runner.write_text((STAGE/'scripts/run_left_rib_probe_r405.mjs').read_text(encoding='utf-8').replace('OutdoorLeftRibProbeR405.exe',tool.name),encoding='utf-8')
contract=read(STAGE/'evidence/width600_high_r411_contract.json');contract['cases'][0]['id']='shelf_context_r416'
write(STAGE/'evidence/shelf_context_r416_contract.json',contract)
write(STAGE/'evidence/shelf_context_r416_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
