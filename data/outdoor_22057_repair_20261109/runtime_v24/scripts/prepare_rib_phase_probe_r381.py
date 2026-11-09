"""Throwaway no-save diagnosis of the first phase losing a real rib pilot."""
import shutil
import subprocess
from generate_model import STAGE, sha, write

target = STAGE / 'scripts/rib_phase_probe_r381'
assert not target.exists()
shutil.copytree(STAGE / 'scripts/width600_native_r379', target)
p = target / 'OutdoorShadeR145.cs'
t = p.read_text(encoding='utf-8')
assert t.count('else if(mode=="layout_probe")LayoutProbe(c,row);') == 1
t = t.replace('else if(mode=="layout_probe")LayoutProbe(c,row);', 'else if(mode=="layout_probe"){RelocateTask(c,row);RibPhaseProbe(c,row);}')
p.write_text(t, encoding='utf-8')
p = target / 'LayoutBuildR170.cs'
t = p.read_text(encoding='utf-8')
for key, label in [
 ('LayoutSuppress(top,p=>', 'before_suppress'),
 ('LayoutSuppress(cabinet,p=>', 'after_top_suppress'),
 ('foreach(var r in rows){string side=', 'after_cabinet_suppress'),
 ('ExpectedNativeDocuments=122+rows.Count*3+', 'after_add_layout'),
]:
    assert t.count(key) == 1
    t = t.replace(key, 'RibTrace("' + label + '",row);' + key)
p.write_text(t, encoding='utf-8')
(target / 'RibPhaseProbeR381.cs').write_text(r'''using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object RibSketches(ModelDoc2 d) {
        return Features(d).Where(f=>f.GetTypeName2()=="ProfileFeature").Select(f=>{
            var s=(Sketch)f.GetSpecificFeature2();var arcs=(s.GetSketchSegments() as Array??new object[0]).Cast<SketchSegment>().OfType<SketchArc>().Where(a=>Math.Abs(a.GetRadius()-.001)<1e-7).ToArray();
            if(arcs.Length==0)return null;
            return new{name=f.Name,suppressed=f.IsSuppressed2(1,null),status=s.GetConstrainedStatus(),parents=(f.GetParents() as Array??new object[0]).Cast<Feature>().Select(v=>v.Name).ToArray(),
                circles=arcs.Select(a=>{var p=(SketchPoint)a.GetCenterPoint2();return new{point=RmPoint(new[]{p.X,p.Y,p.Z},s.ModelToSketchTransform.IInverse()),local=new[]{p.X,p.Y,p.Z},radius=a.GetRadius()};}).ToArray(),
                relations=(s.RelationManager.GetRelations(0) as Array??new object[0]).Cast<SketchRelation>().Select(r=>new{type=r.GetRelationType(),entities=(r.GetEntities() as Array??new object[0]).Cast<object>().Select(e=>e is SketchPoint?(object)new{point=new[]{((SketchPoint)e).X,((SketchPoint)e).Y,((SketchPoint)e).Z},id=((SketchPoint)e).GetID()}:e is SketchSegment?(object)new{segment=((SketchSegment)e).GetType(),length=((SketchSegment)e).GetLength()}:new{kind=e==null?"external_null":e.GetType().Name}).ToArray()}).ToArray()};
        }).Where(v=>v!=null).ToArray();
    }
    static void RibTrace(string label,Dictionary<string,object> row) {
        var right=Doc("箱体右侧板焊接.SLDASM");
        var components=((Array)((AssemblyDoc)right).GetComponents(true)).Cast<Component2>().ToArray();
        var panel=components.Single(c=>c.Name2=="箱体右侧板-1");var rib=components.Single(c=>c.Name2=="OUT_RightSideRib-3");
        var pp=(ModelDoc2)panel.GetModelDoc2();var rp=(ModelDoc2)rib.GetModelDoc2();
        row["rib_phase_"+label]=new{mates=LayoutMates(right),states=ShStates(right),health=LayoutFeatureHealth(right),panel_pilots=RightRibRearAxes(panel).Select(a=>a.Point).ToArray(),rib_pilots=RightRibRearAxes(rib).Select(a=>a.Point).ToArray(),panel_sketches=RibSketches(pp),rib_sketches=RibSketches(rp),panel_dimensions=Dims(pp)};
        Flush("rib_phase/"+label);
    }
    static void RibPhaseProbe(Dictionary<string,object> c,Dictionary<string,object> row) {
        Root=(string)c["package_root"];Top=(string)c["top"];PrepareLayout(c,row);var before=Identity(Root);
        try {
            var top=OpenTop(row);RibTrace("opened",row);
            var dims=((IEnumerable)c["dimensions_mm"]).Cast<object>().Select(Convert.ToDouble).ToArray();ApplyInputs(top,dims[0],dims[1],dims[2],row);RibTrace("after_inputs",row);
            BuildLayout(top,c,row);RibTrace("after_layout",row);BuildLayoutFeatures(c,row);RibTrace("after_features",row);RebuildAll(top,row);RibTrace("after_final",row);
            row["native_save_performed"]=false;
        }finally{Sw.CloseAllDocuments(true);CheckIdentity(Root,before);row["files_unchanged"]=true;}
    }
}
''', encoding='utf-8')
tool = STAGE / 'OutdoorRibPhaseProbeR381.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*[str(p) for p in target.glob('*.cs')]], check=True)
runner = (STAGE / 'scripts/run_right_rib_probe_r352.mjs').read_text(encoding='utf-8')
(STAGE / 'scripts/run_rib_phase_probe_r381.mjs').write_text(runner.replace('OutdoorRightRibProbeR352.exe',tool.name),encoding='utf-8')
write(STAGE/'evidence/rib_phase_probe_r381_build.json', {'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
