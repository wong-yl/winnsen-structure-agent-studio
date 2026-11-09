"""Exercise the unchanged entrypoint hash gates in disposable non-CAD fixtures."""
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

work = Path(__file__).resolve().parent
runtime = work / 'runtime_v17'
original = json.loads((work.parent / 'outdoor_22057_tasks/OUT22057-8bdbcd9b-3e71-4ae1-bea6-4c6b96940ff2.json').read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p, value): p.write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')
checks = []
for kind in ('support', 'external'):
    fixture = work / ('hash_fixture_' + kind)
    fixture.mkdir()
    (fixture / 'scripts').mkdir()
    for name in ('portal_generate.py', 'generate_model.py', 'generate_layout_model.py', 'prepare_layout_case.py', 'layout_contract.py'):
        shutil.copy2(runtime / 'scripts' / name, fixture / 'scripts' / name)
    sentinel = fixture / 'sentinel.txt'
    sentinel.write_text('qualified', 'utf-8')
    expected = sha(sentinel)
    q = {'status':'PASS', 'input_range_mm':original['generator']['ranges_mm'], 'support_sha256':{}, 'external_support_sha256':{}}
    q['support_sha256' if kind == 'support' else 'external_support_sha256']['sentinel.txt' if kind == 'support' else str(sentinel)] = expected
    write(fixture / 'qualification.json', q)
    task = {**original, 'state':'running', 'generator':{**original['generator'], 'source_stage':str(fixture), 'script_sha256':sha(fixture/'scripts/portal_generate.py'), 'qualification_sha256':sha(fixture/'qualification.json')}}
    write(fixture / 'task.json', task)
    sentinel.write_text('changed', 'utf-8')
    result = subprocess.run([sys.executable, '-X','utf8',str(fixture/'scripts/portal_generate.py'),'--task-file',str(fixture/'task.json'),'--release-dir',str(fixture/'release')], capture_output=True, text=True, encoding='utf-8')
    expected_error = 'Generator identity changed:' if kind == 'support' else 'Verification dependency changed:'
    assert result.returncode != 0 and expected_error in result.stderr
    assert not (fixture / 'release').exists(), 'Must reject before generation'
    checks.append({'kind':kind,'returncode':result.returncode,'expected_error':expected_error,'blocked_before_generation':True})
write(work/'hash_rejection_tests.json', {'status':'PASS','checks':checks})
print(json.dumps(checks))
