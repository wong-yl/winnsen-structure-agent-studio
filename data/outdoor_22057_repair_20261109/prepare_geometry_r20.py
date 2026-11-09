"""Create a fresh candidate for two evidenced geometry linkage defects."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
PARENT = WORK / 'runtime_v19'
STAGE = WORK / 'runtime_v20'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')

diagnostic = read(WORK / 'layout_diagnostic_r20/clearance_locations.json')
assert len(diagnostic['contacts']) == 1
assert diagnostic['contacts'][0]['rib']['instance'].endswith('箱体竖隔板 焊接-1/箱体侧板加强筋-2')
audits = read(WORK / 'layout_diagnostic_r20/remaining_audits.json')['audits']
assert len(audits) == 6 and [r['label'] for r in audits if r['status'] != 'PASS'] == ['fasteners']
assert not STAGE.exists(), 'Preserve existing candidates'
parent_manifest = read(PARENT / 'repair_manifest.json')
STAGE.mkdir()
for relative, digest in parent_manifest['identity'].items():
    assert sha(PARENT / relative) == digest, relative
    target = STAGE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PARENT / relative, target)

source = STAGE / 'scripts/width600_native_r440'
target = source / 'LayoutBuildR170.cs'
text = target.read_text('utf-8-sig')
marker = '    static double LayoutExtremeY('
assert text.count(marker) == 1
helper = '''    static void BindDividerRightRib(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        if(LayoutRows(c).Count==0)return;
        Need(Writable&&Within(Root,Stage)&&System.IO.Path.GetFileName(Root).StartsWith("generated_"),"Divider binding requires a fresh writable candidate");
        var owner=Doc("箱体竖隔板 焊接.SLDASM");RmActivate(owner,"divider_right_rib_binding");var assembly=(AssemblyDoc)owner;
        var parts=((Array)assembly.GetComponents(true)).Cast<Component2>().ToArray();
        var ribs=parts.Where(p=>Path.GetFileName(p.GetPathName())=="箱体侧板加强筋.SLDPRT").ToArray();
        Need(ribs.Length==2&&!parts.Any(p=>Path.GetFileName(p.GetPathName())=="OUT_RightSideRib.SLDPRT"),"Fresh divider must contain its two original rib instances");
        var right=ribs.Single(p=>p.Name2.Split('/').Last()=="箱体侧板加强筋-2");
        var old=(double[])right.Transform2.ArrayData;string configuration=right.ReferencedConfiguration;int constrained=right.GetConstrainedStatus();
        Need(right.IsFixed()&&(right.GetMates()==null||((Array)right.GetMates()).Length==0),"Divider right rib must retain its original fixed placement without mate replacement");
        Need(Math.Abs(old[4]-1)<1e-8&&Math.Abs(old[1])+Math.Abs(old[7])<1e-8,"Divider right rib Y direction differs from row datum");
        var before=parts.ToDictionary(p=>p.Name2,p=>new{path=p.GetPathName(),transform=(double[])p.Transform2.ArrayData,fixed_instance=p.IsFixed(),constrained=p.GetConstrainedStatus()});
        string oldName=right.Name2;string replacement=Path.Combine(Root,"OUT_RightSideRib.SLDPRT");
        Need(!Doc("箱体侧板加强筋.SLDPRT").GetSaveFlag()&&!Doc("OUT_RightSideRib.SLDPRT").GetSaveFlag(),"Refuse replacement with unsaved rib edits");
        string mates=Json.Serialize(LayoutMates(owner));row["divider_right_rib_before"]=new{parts=before,mates=LayoutMates(owner)};Flush("divider_right_rib_before");
        RmActivate(owner,"divider_right_rib_replace");owner.ClearSelection2(true);Need(right.Select4(false,null,false),"Cannot select the single divider right rib");
        bool replaced=assembly.ReplaceComponents2(replacement,configuration,false,0,true);
        row["divider_right_rib_replace"]=new{replaced=replaced,replace_all_instances=false,reattach_mates=true,path=replacement};Flush("divider_right_rib_replace");
        Need(replaced,"Cannot bind divider right rib to right-column reliefs");owner.ClearSelection2(true);RequireCandidateWriteGraph("divider_right_rib_references");
        Need(owner.ForceRebuild3(false)&&owner.Extension.GetWhatsWrongCount()==0,"Divider failed rebuild after single-rib binding");
        var after=((Array)assembly.GetComponents(true)).Cast<Component2>().ToArray();
        Need(after.Length==parts.Length&&after.Count(p=>Path.GetFileName(p.GetPathName())=="箱体侧板加强筋.SLDPRT")==1,"Divider left rib was unexpectedly replaced");
        var bound=after.Single(p=>string.Equals(p.GetPathName(),replacement,StringComparison.OrdinalIgnoreCase));
        Need(bound.ReferencedConfiguration==configuration&&bound.IsFixed()&&bound.GetConstrainedStatus()==constrained,"Divider right rib configuration or constraint changed");
        foreach(var component in after){var expected=before[component==bound?oldName:component.Name2];var actual=(double[])component.Transform2.ArrayData;
            Need(expected.transform.Zip(actual,(a,b)=>Math.Abs(a-b)).Max()<1e-8&&component.IsFixed()==expected.fixed_instance&&component.GetConstrainedStatus()==expected.constrained,"Divider component placement changed during binding");}
        Need(Json.Serialize(LayoutMates(owner))==mates,"Divider mate definitions changed during binding");
        row["divider_right_rib_binding"]=new{path=replacement,before_transform=old,after_transform=bound.Transform2.ArrayData,configuration=configuration,fixed_instance=bound.IsFixed(),constrained=bound.GetConstrainedStatus(),left_rib_retained=true,mates_preserved=true,states=ShStates(owner)};
        Flush("divider_right_rib_binding");
    }
'''
text = text.replace(marker, helper + marker)
target.write_text(text, 'utf-8')

target = source / 'OutdoorShadeR145.cs'
text = target.read_text('utf-8-sig')
old = 'var top=OpenTop(row);var columns=ShColumnBodies();'
new = 'var top=OpenTop(row);if(persist)BindDividerRightRib(c,row);var columns=ShColumnBodies();'
assert text.count(old) == 1
target.write_text(text.replace(old, new), 'utf-8')

target = source / 'CoverFastenersR312.cs'
text = target.read_text('utf-8-sig')
old = '        var inner=Doc("箱体内顶板.SLDPRT");\n'
new = '''        var inner=Doc("箱体内顶板.SLDPRT");
        var seed=(Sketch)RmFeature(inner,"草图21").GetSpecificFeature2();
        var targets=(seed.GetSketchBlockInstances() as Array??new object[0]).Cast<SketchBlockInstance>().ToDictionary(b=>b.Name,b=>{double x=((double[])b.BlockToSketchTransform.ArrayData)[9];Need(Math.Abs(Math.Abs(x)-.1625)<1e-7||Math.Abs(Math.Abs(x)-.3575)<1e-7,"Unexpected seed cover nut datum");return Math.Sign(x)*(Math.Abs(x)>.25?width/2000-.1175:.1625);});
        Need(targets.Count==4&&targets.Values.Distinct().Count()==4,"Four distinct cover nut targets required");
'''
assert text.count(old) == 1
text = text.replace(old, new)
old = 'var p=(double[])b.position.Clone();if(Math.Abs(b.transform[9])>.25)p[0]+=Math.Sign(b.transform[9])*delta;'
new = 'var p=(double[])b.position.Clone();p[0]+=targets[b.block.Name]-b.transform[9];'
assert text.count(old) == 1
text = text.replace(old, new)
old = 'FixCoverProfile(inner,s);Need(s.GetConstrainedStatus()==3,"Cover nut blocks must be constrained");'
new = old + '\n        Need(blocks.All(b=>Math.Abs(((double[])b.block.BlockToSketchTransform.ArrayData)[9]-targets[b.block.Name])<1e-8),"Cover nut absolute datum mismatch");'
assert text.count(old) == 1
target.write_text(text.replace(old, new), 'utf-8')

tool = STAGE / 'OutdoorWidth600R440.exe'
command = [r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:exe', '/platform:x64', '/optimize+',
           '/out:' + str(tool), '/reference:System.Web.Extensions.dll',
           '/reference:' + str(STAGE / 'SolidWorks.Interop.sldworks.dll'),
           '/reference:' + str(STAGE / 'SolidWorks.Interop.swconst.dll'), *map(str, source.glob('*.cs'))]
with (STAGE / 'evidence/geometry_r20_build.log').open('w', encoding='utf-8') as log:
    subprocess.run(command, stdout=log, stderr=log, check=True)
changed = [{'path': rel, 'parent_sha256': digest, 'candidate_sha256': sha(STAGE / rel)}
           for rel, digest in parent_manifest['identity'].items() if sha(STAGE / rel) != digest]
assert {row['path'].replace('\\', '/') for row in changed} == {
    'OutdoorWidth600R440.exe', 'scripts/width600_native_r440/LayoutBuildR170.cs',
    'scripts/width600_native_r440/OutdoorShadeR145.cs', 'scripts/width600_native_r440/CoverFastenersR312.cs'}
write(STAGE / 'repair_manifest.json', {
    'status': 'CANDIDATE_NOT_ACCEPTED', 'parent_manifest': str(PARENT / 'repair_manifest.json'),
    'parent_manifest_sha256': sha(PARENT / 'repair_manifest.json'), 'changes': changed,
    'diagnostics': {str(p): sha(p) for p in [WORK / 'layout_diagnostic_r20/clearance_locations.json', WORK / 'layout_diagnostic_r20/fasteners.json', WORK / 'layout_diagnostic_r20/remaining_audits.json']},
    'scope': 'Bind only central right rib to existing right-row native relief part; cover nut absolute width targets; original CAD rules and all audit gates retained',
    'save_gate_unchanged': True, 'audit_sources_unchanged': True, 'forced_termination': False,
    'build_command': command,
    'identity': {str(p.relative_to(STAGE)): sha(p) for p in STAGE.rglob('*') if p.is_file()},
})
print(json.dumps({'stage': str(STAGE), 'changes': changed, 'gate_changed': False}, ensure_ascii=False))
