"""Extend v15 using new narrow native boundaries and targeted fastener regression."""
import argparse
from datetime import datetime, timezone
from pathlib import Path
from generate_model import STAGE, read, sha, write
from generate_layout_model import counts
from layout_contract import INVESTIGATION_RANGES, validate_specification

parser=argparse.ArgumentParser()
parser.add_argument('--low',required=True)
parser.add_argument('--high',required=True)
parser.add_argument('--regression',required=True)
args=parser.parse_args()
parent=read(STAGE/'evidence/parent_v15_identity.json')
old=Path(parent['parent_stage'])
assert sha(old/'qualification.json')==parent['qualification_sha256']
previous=read(old/'qualification.json')
for p,h in previous['support_sha256'].items(): assert sha(old/p)==h,p
for p,h in previous['external_support_sha256'].items(): assert sha(Path(p))==h,p
for case in previous['cases']:
    assert sha(case['result'])==case['sha256']
    for p,h in case['evidence_sha256'].items(): assert sha(p)==h,p
cases=[]
for tag in (args.low,args.high):
    result_path=STAGE/'evidence'/(tag+'_result.json')
    result=read(result_path)
    spec=validate_specification(result['specification'])
    assert result['status']=='PASS' and spec['width']==600
    assert (result['visible_solids'],result['native_documents'])==counts(spec)
    phases=result['native_phases']
    assert len(phases)==3 and len({read(p)['process_id'] for p in phases})==3
    for p in phases:
        phase,launch=read(p),read(str(p)+'.launch.json')
        assert phase['exit_app_called'] and phase['contract_sha256']==launch['contract_sha256']==sha(result['contract'])
        assert launch['tool_sha256']==sha(launch['tool_path'])
        case=phase['cases'][0]
        assert case['health_pass'] and case['snapshot']['visible_solids']==result['visible_solids']
        assert sha(case['export']['path'])==case['export']['sha256']
    first=read(phases[0])['cases'][0]
    assert first['saved_file_reload_before_export'] and first['saved_reload_files_unchanged']
    root=Path(result['models'])
    assert {p.name:sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')}==read(phases[-1])['cases'][0]['final_identity']
    checks=result['checks']
    assert len(checks)>=5
    for p in checks:
        check=read(p);assert check['status']=='PASS',p
        for source,h in check.get('sources',{}).items():assert sha(source)==h,source
    files=phases+checks+[result['contract']]
    cases.append({'specification':spec,'result':str(result_path),'sha256':sha(result_path),'evidence_sha256':{str(p):sha(p) for p in files}})
regression_path=STAGE/'evidence'/args.regression
regression=read(regression_path)
assert regression['status']=='PASS'
for p,h in regression['sources'].items():assert sha(p)==h,p
contract=read(STAGE/'evidence/width_contract_r321.json');assert contract['status']=='PASS'
seed=read(STAGE/'seed_manifest.json')
for name,h in seed['files'].items():assert sha(Path(seed['root'])/name)==h,name
support=set(previous['support_sha256'])
support.update(['OutdoorWidth600R322.exe','OutdoorWidth600R323.exe','OutdoorLayoutOfflineR307.exe',
                'scripts/audit_cover_fasteners_r314.py','scripts/run_layout_offline_r307.mjs',
                'scripts/qualify_width600_generator_r327.py','scripts/verify_width_contract_r321.py',
                'evidence/native_floor_constraints_r322_build.json','evidence/width_contract_r321.json',
                'evidence/parent_v15_identity.json'])
for folder in ('width600_native_r322','width600_native_r323','offline_r307'):
    support.update(str(p.relative_to(STAGE)) for p in (STAGE/'scripts'/folder).glob('*.cs'))
qualification={**previous,'status':'PASS','release_id':'OUT22057-WIDTH600-20261018-v16',
    'checked_utc':datetime.now(timezone.utc).isoformat(),
    'input_range_mm':{k:list(v) for k,v in INVESTIGATION_RANGES.items()},'cases':cases,
    'parent_qualification':{'path':str(old/'qualification.json'),'sha256':sha(old/'qualification.json'),
        'inherited_cases':previous['cases'],'reuse_scope':'unchanged frame, door layout, canopy, fixed parts and service contracts; modified M4/M6 interfaces covered by new native boundaries and targeted regression'},
    'modified_module_regression':{'path':str(regression_path),'sha256':sha(regression_path)},
    'native_tool_routing':{**previous['native_tool_routing'],'two_doors_without_internal_shelves':'OutdoorWidth600R322.exe','other_layouts_with_internal_shelves':'OutdoorWidth600R323.exe'},
    'support_sha256':{p:sha(STAGE/p) for p in sorted(support)},
    'external_support_sha256':{**previous['external_support_sha256'],str(old/'qualification.json'):sha(old/'qualification.json')},
    'production_release':False}
qualification['coverage']={**previous['coverage'],'modified_fasteners':'M4 cover slots/nuts and M6 floor nuts keep standard geometry; installation coordinates follow width',
    'additional_narrow_boundaries':2,'inherited_width_range_mm':[900,1200],'runtime_cover_and_floor_fastener_audit_required':True}
assert not (STAGE/'qualification.json').exists()
write(STAGE/'qualification.json',qualification)
print('PASS: width600 qualification frozen')
