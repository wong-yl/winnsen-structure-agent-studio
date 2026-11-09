"""Retain the three evidenced native divider mates while binding its right rib."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v20'
STAGE = WORK / 'runtime_v22'
def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()
probe_path = WORK / 'runtime_v21/evidence/probe_22057_1939_860_550_l7_r9_r21_save_generate.json'
probe = read(probe_path)['cases'][0]['divider_right_rib_direct_owner_probe']
assert not probe['fixed_instance'] and probe['constrained'] == 3 and probe['mate_count'] == 3
assert not probe['health'] and not probe['original_dirty'] and not probe['replacement_dirty']
assert not STAGE.exists()
manifest = read(PARENT / 'repair_manifest.json')
STAGE.mkdir()
for relative, expected in manifest['identity'].items():
    assert sha(PARENT / relative) == expected
    target = STAGE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PARENT / relative, target)
source = STAGE / 'scripts/width600_native_r440'
target = source / 'LayoutBuildR170.cs'
text = target.read_text('utf-8-sig')
marker = '    static void BindDividerRightRib('
helper = '''    static object DividerMateSignatures(ModelDoc2 owner,string rightName)
    {
        return Features(owner).Where(f=>f.GetTypeName2().StartsWith("Mate")).OrderBy(f=>f.Name).Select(f=>{
            var mate=f.GetSpecificFeature2() as Mate2;var entities=new List<object>();
            if(mate!=null)for(int i=0;i<mate.GetMateEntityCount();i++){
                var entity=(MateEntity2)mate.MateEntity(i);var component=entity.ReferenceComponent as Component2;
                string name=component==null?null:component.Name2;var values=entity.EntityParams as double[]??new double[0];
                var plane=entity.ReferenceType==3&&values.Length>=6?new[]{values[3],values[4],values[5],values.Take(3).Zip(values.Skip(3).Take(3),(a,b)=>a*b).Sum()}:values;
                entities.Add(new{component=name==rightName?"__divider_right_rib__":name,type=entity.ReferenceType,parameters=plane.Select(v=>Math.Round(v,8)).ToArray()});
            }
            return new{name=f.Name,type=f.GetTypeName2(),suppressed=f.IsSuppressed2(1,null),entities=entities};
        }).ToArray();
    }
    static void VerifyDividerRightRib(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        if(LayoutRows(c).Count==0)return;
        var owner=Doc("箱体竖隔板 焊接.SLDASM");var parts=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().ToArray();
        var right=parts.Single(p=>Path.GetFileName(p.GetPathName())=="OUT_RightSideRib.SLDPRT");
        var left=parts.Single(p=>Path.GetFileName(p.GetPathName())=="箱体侧板加强筋.SLDPRT");
        double width=Convert.ToDouble(((IList)c["dimensions_mm"])[1]);var transform=(double[])right.Transform2.ArrayData;
        Need(!right.IsFixed()&&right.GetConstrainedStatus()==3&&((Array)right.GetMates()).Length==3&&!left.IsFixed()&&left.GetConstrainedStatus()==3,"Divider ribs lost their native mate constraints");
        Need(Math.Abs(transform[9]-(width/2000-.0005))<1e-7&&Math.Abs(transform[10])<1e-7&&Math.Abs(transform[11]+.115)<1e-7,"Divider right rib did not follow original cabinet datums");
        Need(Math.Abs(transform[0]-1)+Math.Abs(transform[4]-1)+Math.Abs(transform[8]-1)+Math.Abs(transform[1])+Math.Abs(transform[2])+Math.Abs(transform[3])+Math.Abs(transform[5])+Math.Abs(transform[6])+Math.Abs(transform[7])<1e-7,"Divider right rib orientation changed");
        Need(owner.Extension.GetWhatsWrongCount()==0,"Divider mate graph unhealthy after row linkage");
        row["divider_right_rib_final"]=new{states=ShStates(owner),mates=LayoutMates(owner),health=LayoutFeatureHealth(owner),fully_constrained=true,original_three_mates_retained=true};Flush("divider_right_rib_final");
    }
'''
assert text.count(marker) == 1
text = text.replace(marker, helper + marker)
old = '        Need(right.IsFixed()&&(right.GetMates()==null||((Array)right.GetMates()).Length==0),"Divider right rib must retain its original fixed placement without mate replacement");'
new = '''        row["divider_right_rib_direct_owner_probe"]=new{fixed_instance=right.IsFixed(),constrained=constrained,mate_count=right.GetMates()==null?0:((Array)right.GetMates()).Length,mates=LayoutMates(owner),states=ShStates(owner),health=LayoutFeatureHealth(owner)};Flush("divider_right_rib_direct_owner_probe");
        Need(!right.IsFixed()&&constrained==3&&right.GetMates()!=null&&((Array)right.GetMates()).Length==3&&owner.Extension.GetWhatsWrongCount()==0,"Divider right rib must retain its three original native mates");'''
assert text.count(old) == 1
text = text.replace(old, new)
text = text.replace('string mates=Json.Serialize(LayoutMates(owner));', 'string mates=Json.Serialize(DividerMateSignatures(owner,right.Name2)),dimensions=Json.Serialize(Dims(owner));')
text = text.replace('bound.ReferencedConfiguration==configuration&&bound.IsFixed()&&bound.GetConstrainedStatus()==constrained', 'bound.ReferencedConfiguration==configuration&&!bound.IsFixed()&&bound.GetConstrainedStatus()==constrained&&((Array)bound.GetMates()).Length==3')
text = text.replace('before[component==bound?oldName:component.Name2]', 'before[string.Equals(component.GetPathName(),replacement,StringComparison.OrdinalIgnoreCase)?oldName:component.Name2]')
text = text.replace('Need(Json.Serialize(LayoutMates(owner))==mates,"Divider mate definitions changed during binding");', 'Need(Json.Serialize(DividerMateSignatures(owner,bound.Name2))==mates&&Json.Serialize(Dims(owner))==dimensions,"Divider mate planes, distances or suppression changed during binding");')
text = text.replace('left_rib_retained=true,mates_preserved=true,states=ShStates(owner)', 'left_rib_retained=true,mates_preserved=true,mate_signatures=DividerMateSignatures(owner,bound.Name2),mates=LayoutMates(owner),states=ShStates(owner)')
target.write_text(text, 'utf-8')
target = source / 'OutdoorShadeR145.cs'
text = target.read_text('utf-8-sig')
old = 'RefreshLayoutAssemblyGraph(top,row);row["preaudit_instance_counts"]'
new = 'RefreshLayoutAssemblyGraph(top,row);VerifyDividerRightRib(c,row);row["preaudit_instance_counts"]'
assert text.count(old) == 1
target.write_text(text.replace(old, new), 'utf-8')
tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (STAGE / 'evidence/geometry_r22_build.log').open('w', encoding='utf-8') as log:
    subprocess.run(command, stdout=log, stderr=log, check=True)
changed = [{'path': relative, 'parent_sha256': expected, 'candidate_sha256': sha(STAGE / relative)}
           for relative, expected in read(WORK / 'runtime_v19/repair_manifest.json')['identity'].items() if sha(STAGE / relative) != expected]
assert {row['path'].replace('\\', '/') for row in changed} == {'OutdoorWidth600R440.exe', 'scripts/width600_native_r440/LayoutBuildR170.cs', 'scripts/width600_native_r440/OutdoorShadeR145.cs', 'scripts/width600_native_r440/CoverFastenersR312.cs'}
result = {'status': 'CANDIDATE_NOT_ACCEPTED', 'parent_manifest_sha256': sha(PARENT / 'repair_manifest.json'),
          'diagnostic': str(probe_path), 'diagnostic_sha256': sha(probe_path), 'changes': changed,
          'scope': 'Existing right-row part bound to central right rib with its original three native mates retained; absolute cover nut targets',
          'save_gate_unchanged': True, 'audit_sources_unchanged': True, 'forced_termination': False,
          'build_command': command, 'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()}}
(STAGE / 'repair_manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps({'stage': str(STAGE), 'changes': changed}, ensure_ascii=False))
