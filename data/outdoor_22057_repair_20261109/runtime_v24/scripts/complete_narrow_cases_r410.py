"""Run the two authorized whole-cabinet boundaries, then local LED transitions."""
from generate_model import STAGE,NODE,read,write,run
from generate_layout_model import generate

progress=STAGE/'evidence/narrow_cases_r410_progress.json'
assert not progress.exists()
state={'status':'RUNNING','completed_cases':[],'planned_cases':2}
write(progress,state)
low={'height':1700,'width':600,'depth':450,'columns':[{'side':'L','doors':[{'heightMm':1579}]},{'side':'R','doors':[{'heightMm':1579}]}]}
for spec,tag in [(low,'width600_low_r410'),(read(STAGE/'evidence/width600_high_r308_specification.json'),'width600_high_r411')]:
    result=generate(spec,tag)
    state['completed_cases'].append({'tag':tag,'result':str(STAGE/'evidence'/(tag+'_result.json')),'status':result['status']})
    write(progress,state)
run([NODE,str(STAGE/'scripts/run_led_transition_probe_r377.mjs'),'width600_low_r343_contract.json','layout_probe','led_transition_r377'],STAGE/'evidence/led_transition_r377_runner.log')
run(['C:/Users/Administrator/AppData/Local/Programs/Python/Python310/python.exe','-B',str(STAGE/'scripts/analyze_led_transitions_r378.py')],STAGE/'evidence/led_transition_r378_analysis.log')
state['status']='PASS_TWO_CASES_AND_LOCAL_LED_TRANSITIONS'
state['local_transitions']=str(STAGE/'evidence/led_transitions_r378_result.json')
write(progress,state)
print(state)
