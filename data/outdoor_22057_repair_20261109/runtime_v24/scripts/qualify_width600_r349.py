"""Freeze narrow native evidence while preserving the exact v15 wide generators."""
import argparse
from datetime import datetime, timezone
from pathlib import Path
from generate_model import STAGE, read, sha, write
from generate_layout_model import counts
from layout_contract import INVESTIGATION_RANGES, validate_specification

parser=argparse.ArgumentParser()
parser.add_argument('--low',required=True)
parser.add_argument('--high',required=True)
args=parser.parse_args()
parent=read(STAGE/'evidence/parent_v15_identity.json')
old=Path(parent['parent_stage'])
assert sha(old/'qualification.json')==parent['qualification_sha256']
previous=read(old/'qualification.json')
for p,h in previous['support_sha256'].items():assert sha(old/p)==h,p
for p,h in previous['external_support_sha256'].items():assert sha(p)==h,p
for c in previous['cases']:
    assert sha(c['result'])==c['sha256']
    for p,h in c['evidence_sha256'].items():assert sha(p)==h,p
unchanged=['OutdoorLayoutR188.exe','OutdoorLayoutExpandedR301.exe','OutdoorLayoutExpandedR303.exe',
           'independent_size_rules.json','assembly_size_rules.json','seed_manifest.json',
           'scripts/prepare_layout_case.py','scripts/audit_layout_r289.py','scripts/audit_shade_r201.py','scripts/audit_shade_r239.py']
for p in unchanged:assert sha(STAGE/p)==sha(old/p),p
route=(STAGE/'scripts/run_native.mjs').read_text(encoding='utf-8')
assert "width<900?" in route and "hasShelves?'OutdoorLayoutExpandedR303.exe':'OutdoorLayoutExpandedR301.exe'" in route
cases=[];support=set(previous['support_sha256']);tools=[]
for tag,dims,doors in [(args.low,[1700,600,450],2),(args.high,[2200,600,700],22)]:
    result_path=STAGE/'evidence'/(tag+'_result.json');result=read(result_path)
    spec=validate_specification(result['specification'])
    assert result['status']=='PASS' and result['dimensions_mm']==dims and spec['doors']==doors
    assert (result['visible_solids'],result['native_documents'])==counts(spec)
    phases=result['native_phases'];assert len(phases)==3 and len({read(p)['process_id'] for p in phases})==3
    phase_tools=set()
    for p in phases:
        value,launch=read(p),read(str(p)+'.launch.json');case=value['cases'][0]
        assert value['exit_app_called'] and value['contract_sha256']==launch['contract_sha256']==sha(result['contract'])
        tool=Path(launch['tool_path']);assert tool.parent==STAGE and sha(tool)==launch['tool_sha256']
        phase_tools.add(str(tool));assert case['health_pass'] and case['snapshot']['visible_solids']==result['visible_solids']
        assert sha(case['export']['path'])==case['export']['sha256']
    assert len(phase_tools)==1
    tool=Path(next(iter(phase_tools)));tools.append(tool.name);support.add(tool.name)
    source=STAGE/'scripts'/('width600_native_r'+tool.stem.split('R')[-1]);assert source.is_dir()
    support.update(str(p.relative_to(STAGE)) for p in source.glob('*.cs'))
    first=read(phases[0])['cases'][0]
    assert first['native_save_performed'] and first['saved_file_reload_before_export'] and first['saved_reload_files_unchanged']
    root=Path(result['models'])
    assert {p.name:sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')}==read(phases[-1])['cases'][0]['final_identity']
    checks=list(result['checks'])
    supplemental=STAGE/'evidence'/(tag+'_auxiliary_supplement.json')
    if supplemental.exists():checks.append(str(supplemental))
    assert len(checks)>=6
    for p in checks:
        check=read(p);assert check['status']=='PASS',p
        for source,h in check.get('sources',{}).items():assert sha(source)==h,source
    files=phases+checks+[result['contract']]+[str(p)+suffix for p in phases for suffix in ('.launch.json','.after.json')]
    cases.append({'specification':spec,'result':str(result_path),'sha256':sha(result_path),'evidence_sha256':{str(p):sha(p) for p in files}})
for name in ['width_contract_r321.json','width_portal_contract_r329.json']:
    value=read(STAGE/'evidence'/name);assert value['status']=='PASS'
    support.add('evidence/'+name)
seed=read(STAGE/'seed_manifest.json')
for name,h in seed['files'].items():assert sha(Path(seed['root'])/name)==h,name
support.update(['scripts/qualify_width600_r349.py','scripts/audit_layout_r345.py','scripts/audit_cover_fasteners_r314.py',
                'scripts/audit_narrow_auxiliary_r346.py','evidence/parent_v15_identity.json','OutdoorLayoutOfflineR307.exe',
                'scripts/run_layout_offline_r307.mjs'])
support.update(str(p.relative_to(STAGE)) for p in (STAGE/'scripts/offline_r307').glob('*.cs'))
qualification={**previous,'status':'PASS','release_id':'OUT22057-WIDTH600-20261018-v16',
    'checked_utc':datetime.now(timezone.utc).isoformat(),'input_range_mm':{k:list(v) for k,v in INVESTIGATION_RANGES.items()},
    'cases':cases,'parent_qualification':{'path':str(old/'qualification.json'),'sha256':sha(old/'qualification.json'),
        'inherited_cases':previous['cases'],'reuse_scope':'width 900-1200 uses unchanged v15 native generators, source seed, controls and geometry auditors',
        'unchanged_runtime_files':{p:sha(STAGE/p) for p in unchanged}},
    'native_tool_routing':{'width_below_900_two_doors':tools[0],'width_below_900_with_shelves':tools[1],
        'width_900_to_1200':previous['native_tool_routing']},
    'support_sha256':{p:sha(STAGE/p) for p in sorted(support)},
    'external_support_sha256':{**previous['external_support_sha256'],str(old/'qualification.json'):sha(old/'qualification.json')},
    'production_release':False}
qualification['coverage']={**previous['coverage'],'additional_narrow_boundaries':2,'inherited_width_range_mm':[900,1200],
    'narrow_width_range_mm':[600,900],'runtime_narrow_fastener_and_auxiliary_audit_required':True,
    'narrow_changes':'M4 and M6 mounting coordinates; roof relief body scope; complete door auxiliary row; shelf auxiliary bores and three notches',
    'fixed_parts':'standard columns, short press strips and purchased geometry preserved'}
assert not (STAGE/'qualification.json').exists()
write(STAGE/'qualification.json',qualification)
print('PASS: width600 qualification frozen')
