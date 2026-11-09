"""Accept only the affected high boundary, then the two local LED transitions."""
from generate_model import STAGE, NODE, read, write, run
from generate_layout_model import generate

progress = STAGE / 'evidence/narrow_cases_r426_progress.json'
assert not progress.exists()
assert read(STAGE / 'evidence/width600_low_r410_result.json')['status'] == 'PASS'
state = {'status': 'RUNNING', 'reused_low': 'width600_low_r410',
         'preserved_failures': ['width600_high_r411', 'width600_high_r419'],
         'high': 'width600_high_r426'}
write(progress, state)
try:
    result = generate(read(STAGE / 'evidence/width600_high_r308_specification.json'), 'width600_high_r426')
    assert result['status'] == 'PASS'
    state['high_status'] = 'PASS'
    write(progress, state)
    run([NODE, str(STAGE / 'scripts/run_led_transition_probe_r377.mjs'),
         'width600_low_r343_contract.json', 'layout_probe', 'led_transition_r377'],
        STAGE / 'evidence/led_transition_r377_runner.log')
    run(['C:/Users/Administrator/AppData/Local/Programs/Python/Python310/python.exe', '-B',
         str(STAGE / 'scripts/analyze_led_transitions_r378.py')],
        STAGE / 'evidence/led_transition_r378_analysis.log')
    state['status'] = 'PASS_TWO_CASES_AND_LOCAL_LED_TRANSITIONS'
except Exception as exc:
    state.update(status='FAIL', error=repr(exc))
    raise
finally:
    write(progress, state)
print(state)
