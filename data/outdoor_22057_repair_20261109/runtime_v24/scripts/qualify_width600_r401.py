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
    recovery=None
    if result.get('saved_failure_recovery'):
        reference=result['saved_failure_recovery'];assert sha(reference['path'])==reference['sha256']
        recovery=read(reference['path'])
        assert recovery['status']=='PASS' and recovery['kind']=='SAVED_NATIVE_WITH_FAILED_SAME_PROCESS_RELOAD_AND_TWO_INDEPENDENT_REOPENS'
        assert dims==[2200,600,700] and recovery['contract']==result['contract']
        assert recovery['preserved_failed_save_phase']==phases[0] and recovery['independent_reopens']==phases[1:]
        assert recovery['native_save_phase_status_remains']=='failed'
        assert recovery['checks']==result['checks'] and len(recovery['checks'])>=10
        for p,h in recovery['sources'].items():assert sha(p)==h,p
    phase_tools=set()
    for index,p in enumerate(phases):
        value,launch=read(p),read(str(p)+'.launch.json');case=value['cases'][0]
        assert value['exit_app_called'] and value['contract_sha256']==launch['contract_sha256']==sha(result['contract'])
        tool=Path(launch['tool_path']);assert tool.parent==STAGE and sha(tool)==launch['tool_sha256']
        phase_tools.add(str(tool));assert case['health_pass'] and case['snapshot']['visible_solids']==result['visible_solids']
        assert case['snapshot']['invalid_bodies']==0 and len(case['snapshot']['documents'])==result['native_documents']
        assert all(d['issues']==0 for d in case['snapshot']['documents'])
        if recovery and index==0:
            assert value['status']=='failed' and 'Unhealthy native document:' in value['error']
            assert case['snapshot']==case['snapshot_before_saved_reload'] and 'export' not in case
            assert case['final_identity']==recovery['candidate_files_sha256']
        else:
            assert value['status']==('generate_finished' if index==0 else 'verify_finished')
            assert sha(case['export']['path'])==case['export']['sha256']
            assert case['export']['saved'] and case['export']['errors']==case['export']['warnings']==0
            if index:assert case['files_unchanged'] and not case['native_save_performed']
    assert len(phase_tools)==1
    tool=Path(next(iter(phase_tools)));tools.append(tool.name);support.add(tool.name)
    source=STAGE/'scripts'/('width600_native_r'+tool.stem.split('R')[-1]);assert source.is_dir()
    support.update(str(p.relative_to(STAGE)) for p in source.glob('*.cs'))
    first=read(phases[0])['cases'][0]
    assert first['native_save_performed'] and first['saved_reload_files_unchanged']
    if not recovery:assert first['saved_file_reload_before_export']
    root=Path(result['models'])
    assert {p.name:sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')}==read(phases[-1])['cases'][0]['final_identity']
    checks=list(result['checks'])
    supplemental=STAGE/'evidence'/(tag+'_auxiliary_supplement.json')
    if supplemental.exists():checks.append(str(supplemental))
    assert len(checks)>=8
    for p in checks:
        check=read(p);assert check['status']=='PASS',p
        for source,h in check.get('sources',{}).items():assert sha(source)==h,source
    files=phases+checks+[result['contract']]+[str(p)+suffix for p in phases for suffix in ('.launch.json','.after.json')]
    if recovery:files.extend([result['saved_failure_recovery']['path'],*recovery['sources']])
    for p in checks:files.extend(read(p).get('sources',{}))
    checkpoint=read(result['contract'])['cases'][0].get('hinge_pitch_repair_source')
    if checkpoint:
        assert sha(checkpoint['evidence'])==checkpoint['sha256']
        failed=read(checkpoint['evidence'])
        assert failed['status']=='failed' and 'actual=527 invalid=0' in failed['error']
        source_case=failed['cases'][0]
        assert source_case['preaudit_candidate_saved_not_accepted'] and source_case['dimensions_mm']==dims
        source_root=Path(checkpoint['root'])
        assert {p.name:sha(p) for p in source_root.iterdir() if p.is_file() and not p.name.startswith('~$')}==source_case['final_identity']
        assert first['validated_saved_hinge_checkpoint']==checkpoint
        files.extend([checkpoint['evidence'],checkpoint['evidence']+'.launch.json',checkpoint['evidence']+'.after.json'])
    cases.append({'specification':spec,'result':str(result_path),'sha256':sha(result_path),'evidence_sha256':{str(p):sha(p) for p in files}})
for name in ['width_contract_r321.json','width_portal_contract_r329.json']:
    value=read(STAGE/'evidence'/name);assert value['status']=='PASS'
    support.add('evidence/'+name)
seed=read(STAGE/'seed_manifest.json')
for name,h in seed['files'].items():assert sha(Path(seed['root'])/name)==h,name
support.update(['scripts/qualify_width600_r401.py','scripts/audit_saved_snapshot_r444.py','scripts/complete_saved_recovery_r444.py','scripts/audit_layout_r368.py','scripts/audit_shade_r369.py','scripts/audit_narrow_lamp_r370.py','scripts/audit_rear_rib_r386.py','scripts/audit_roof_board_r398.py','scripts/audit_cover_fasteners_r404.py',
                'scripts/audit_narrow_auxiliary_r346.py','scripts/audit_narrow_auxiliary_r425.py','scripts/audit_narrow_auxiliary_r437.py','evidence/parent_v15_identity.json','OutdoorLayoutOfflineR400.exe',
                'scripts/run_layout_offline_r400.mjs'])
support.update(str(p.relative_to(STAGE)) for p in (STAGE/'scripts/offline_r400').glob('*.cs'))
transitions=read(STAGE/'evidence/led_transitions_r378_result.json')
assert transitions['status']=='PASS_LOCAL_NOT_SAVED'
assert [c['width_mm'] for c in transitions['checks']]==[748.5,898.5]
transition_files=[STAGE/'evidence/led_transitions_r378_result.json',Path(transitions['native'])]
assert sha(transitions['native'])==transitions['native_sha256']
transition_native=read(transitions['native'])
assert transition_native['status']=='layout_probe_finished' and transition_native['exit_app_called']
assert transition_native['cases'][0]['files_unchanged'] and not transition_native['cases'][0]['native_save_performed']
for p in transitions['checks']:
    assert sha(p['geometry'])==p['sha256']
    check=read(p['geometry']);assert check['status']=='PASS_LOCAL_NOT_SAVED'
    transition_files.append(Path(p['geometry']))
    for path,digest in check['sources'].items():
        assert sha(path)==digest,path
        transition_files.append(Path(path))
for suffix in ['.launch.json','.after.json']:transition_files.append(Path(transitions['native']+suffix))
transition_launch=read(transitions['native']+'.launch.json')
assert sha(transition_launch['tool_path'])==transition_launch['tool_sha256']
support.update([Path(transition_launch['tool_path']).name,'scripts/analyze_short_led_r365.py','scripts/analyze_led_transitions_r378.py'])
support.update(str(p.relative_to(STAGE)) for p in transition_files)
ui=read(STAGE/'evidence/width600_progress_r362.json')['ui']
for path,digest in ui['source_sha256'].items():assert sha(path)==digest,path
assert read(ui['browser_evidence'])['status']=='PASS_UI_ONLY'
support.update(str(p.relative_to(STAGE)) for p in (STAGE/'portal_studio/lib').glob('*.mjs'))
support.update(['evidence/width600_progress_r362.json','evidence/studio_ui_browser_r357.json'])
qualification={**previous,'status':'PASS','release_id':'OUT22057-WIDTH600-20261018-v16',
    'checked_utc':datetime.now(timezone.utc).isoformat(),'input_range_mm':{k:list(v) for k,v in INVESTIGATION_RANGES.items()},
    'cases':cases,'parent_qualification':{'path':str(old/'qualification.json'),'sha256':sha(old/'qualification.json'),
        'inherited_cases':previous['cases'],'reuse_scope':'width 900-1200 uses unchanged v15 native generators, source seed, controls and geometry auditors',
        'unchanged_runtime_files':{p:sha(STAGE/p) for p in unchanged}},
    'native_tool_routing':{'width_below_900_two_doors':tools[0],'width_below_900_with_shelves':tools[1],
        'width_900_to_1200':previous['native_tool_routing']},
    'support_sha256':{p:sha(STAGE/p) for p in sorted(support)},
    'external_support_sha256':{**previous['external_support_sha256'],str(old/'qualification.json'):sha(old/'qualification.json')},
    'ui_source_sha256':ui['source_sha256'],'local_transition_evidence':transitions,'production_release':False}
qualification['coverage']={**previous['coverage'],'additional_narrow_boundaries':2,'inherited_width_range_mm':[900,1200],
    'narrow_width_range_mm':[600,900],'runtime_narrow_fastener_and_auxiliary_audit_required':True,
    'narrow_changes':'M4 and M6 mounting coordinates; roof relief body scope; complete door auxiliary row; shelf auxiliary bores and three notches; original shelf-rib length W/2-7 and pilot pitch W/2-175 restored after instance replacement',
    'fixed_parts':'standard columns and short solar press strips preserved; approved short LEDs retain cross section and LED package geometry',
    'narrow_lamp':'shell W-222, opening W-470, plastic cover W-443, retainer W-440; two 75mm pitch LED rows with even package count',
    'rear_rib_pilot_linkage':'x=+-max(75,W/2-300), top y=H-118.8, bottom y=60 with 30mm pitch; all real bores, 3mm material rims, original 50mm ribs and neighbor clearances audited per task',
    'roof_controller_mounts':'center=+-min(260,W/2-125); original 160mm mounting pitch, 4mm posts, 10mm length, standard controllers/brackets and original native mates retained; joined roof, coaxial support and neighboring parts audited per task',
    'hinge_support_tabs':'original D5@草图2=W/2-175; each plate remains one joined body; all three 5x2x2mm tabs, unique shelf pairing and notch fit audited per task',
    'local_count_transitions_mm':[748.5,898.5]}
assert not (STAGE/'qualification.json').exists()
write(STAGE/'qualification.json',qualification)
print('PASS: width600 qualification frozen')
