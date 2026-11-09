import json
from generate_model import STAGE,PYTHON,read,write,sha,run

source=STAGE/'evidence/led_transition_r377_layout_probe.json'
value=read(source);case=value['cases'][0]
assert value['status']=='layout_probe_finished' and case['files_unchanged'] and not case['native_save_performed']
checks=[]
for cycle in case['transition_cycles']:
    width=cycle['dimensions_mm'][1];label=str(width).replace('.','p')
    wrapper=STAGE/'evidence'/f'led_transition_{label}_r378_native.json';assert not wrapper.exists()
    write(wrapper,{'status':'layout_probe_finished','source':str(source),'source_sha256':sha(source),'cases':[cycle]})
    output=STAGE/'evidence'/f'led_transition_{label}_r378_geometry.json'
    run([PYTHON,str(STAGE/'scripts/analyze_short_led_r365.py'),str(wrapper),str(output)],output.with_suffix('.log'))
    check=read(output);assert check['status']=='PASS_LOCAL_NOT_SAVED'
    checks.append({'width_mm':width,'geometry':str(output),'sha256':sha(output),'checks':len(check['checks'])})
assert [r['width_mm'] for r in checks]==[748.5,898.5]
out=STAGE/'evidence/led_transitions_r378_result.json';assert not out.exists()
write(out,{'status':'PASS_LOCAL_NOT_SAVED','native':str(source),'native_sha256':sha(source),'checks':checks,'scope':'Only short lamp count transitions; not whole-cabinet acceptance'})
print('PASS: local native lamp transitions at 748.5 and 898.5 mm')
