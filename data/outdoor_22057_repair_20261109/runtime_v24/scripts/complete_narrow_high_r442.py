"""Repair a copied saved checkpoint, then complete all unchanged acceptance stages."""
import shutil
import stat
from generate_model import STAGE,NODE,read,write,sha,native,run
from generate_layout_model import finish

tag='width600_high_r442'
progress=STAGE/'evidence/narrow_cases_r442_progress.json';assert not progress.exists()
source=read(STAGE/'evidence/width600_high_r432_save_generate.json')
assert source['status']=='failed' and source['cases'][0]['preaudit_candidate_saved_not_accepted']
old=STAGE/'generated_width600_high_r432'
assert {p.name:sha(p) for p in old.iterdir() if p.is_file() and not p.name.startswith('~$')}==source['cases'][0]['final_identity']
root=STAGE/('generated_'+tag);assert not root.exists();shutil.copytree(old,root)
for p in root.iterdir():
    if p.is_file():p.chmod(stat.S_IREAD|stat.S_IWRITE)
identity={p.name:sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')}
assert identity==source['cases'][0]['final_identity']
contract=read(STAGE/'evidence/width600_high_r432_contract.json');case=contract['cases'][0]
case.update(id=tag,package_root=str(root),relocated_root=str(root),hinge_pitch_repair_source={'evidence':str(STAGE/'evidence/width600_high_r432_save_generate.json'),'sha256':sha(STAGE/'evidence/width600_high_r432_save_generate.json'),'root':str(old)},
            checkpoint_evidence=str(STAGE/'evidence/width600_high_r432_save_generate.json'),
            checkpoint_sha256=sha(STAGE/'evidence/width600_high_r432_save_generate.json'))
contract_path=STAGE/'evidence'/(tag+'_contract.json');assert not contract_path.exists();write(contract_path,contract)
write(STAGE/'evidence/hinge_checkpoint_copy_r442.json',{'status':'PASS_IDENTICAL_ISOLATED_CHECKPOINT_COPY','source':str(old),'target':str(root),'files_sha256':identity,'source_evidence':case['checkpoint_evidence'],'source_evidence_sha256':case['checkpoint_sha256']})
state={'status':'RUNNING','reused_low':'width600_low_r410','high':tag,'preserved_failures':['width600_high_r411','width600_high_r419','width600_high_r426','width600_high_r432']};write(progress,state)
try:
    saved,value=native(contract_path.name,'generate',tag+'_save')
    assert value['status']=='generate_finished'
    result=finish(contract_path,saved,tag);assert result['status']=='PASS'
    state['high_status']='PASS';write(progress,state)
    run([NODE,str(STAGE/'scripts/run_led_transition_probe_r377.mjs'),'width600_low_r343_contract.json','layout_probe','led_transition_r377'],STAGE/'evidence/led_transition_r377_runner.log')
    run(['C:/Users/Administrator/AppData/Local/Programs/Python/Python310/python.exe','-B',str(STAGE/'scripts/analyze_led_transitions_r378.py')],STAGE/'evidence/led_transition_r378_analysis.log')
    state['status']='PASS_TWO_CASES_AND_LOCAL_LED_TRANSITIONS'
except Exception as exc:
    state.update(status='FAIL',error=repr(exc));raise
finally:write(progress,state)
print(state)
