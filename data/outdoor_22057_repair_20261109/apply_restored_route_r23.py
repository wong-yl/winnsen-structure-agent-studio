"""Atomically activate only the locally qualified candidate while CAD queues are idle."""
import hashlib
import json
import os
import subprocess
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

WORK = Path(__file__).resolve().parent
DATA = WORK.parent
def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

gate = DATA / 'outdoor_22057_release/release_gate.json'
candidate = WORK / 'release_gate.r23.candidate.json'
receipt = WORK / 'route_application_r23.json'
assert not receipt.exists()
assert sha(gate) == sha(WORK / 'release_gate.before.json')
for failed in read(WORK / 'r23_protected_failed_tasks.json')['tasks']:
    assert sha(failed['path']) == failed['sha256'] and read(failed['path'])['state'] == 'failed'
config = read(candidate)
assert config['production_release'] is False and config['generation']['source_stage'] == str(WORK / 'runtime_v23')
qualification = WORK / 'runtime_v23/qualification.json'
assert sha(qualification) == config['generation']['qualification_sha256']
assert read(WORK / 'runtime_v23/evidence/runtime_geometry_repair_acceptance.json')['status'] == 'PASS'
for relative, digest in read(qualification)['support_sha256'].items():
    assert sha(WORK / 'runtime_v23' / relative) == digest, relative
for path, digest in read(qualification)['external_support_sha256'].items():
    assert sha(path) == digest, path
assert not (DATA / 'native_model_requests/.native-worker-instance.lock').exists(), 'Native worker lock is occupied'
for path in (DATA / 'outdoor_22057_tasks').glob('OUT22057-*.json'):
    task = read(path)
    assert task.get('state') not in ('created', 'queued', 'claimed', 'running', 'generating', 'validating'), str(path)
for path in (DATA / 'native_model_requests').glob('*.json'):
    if not path.name.startswith(('NATIVE-', 'PARAMETRIC-')):
        continue
    assert read(path).get('nativeBuild', {}).get('state') not in ('created', 'queued', 'claimed', 'planning', 'building', 'running', 'validating'), str(path)
count = int(subprocess.check_output(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
            '@(Get-CimInstance Win32_Process -Filter "Name=\'SLDWORKS.exe\'").Count'], text=True).strip())
assert count == 0
with urllib.request.urlopen('http://127.0.0.1:5180/status.json', timeout=10) as response:
    assert response.status == 200 and json.load(response)['status'] == 'ok'
pending = gate.with_name('release_gate.r23.pending')
assert not pending.exists()
before = sha(gate)
with pending.open('xb') as output:
    output.write(candidate.read_bytes())
    output.flush()
    os.fsync(output.fileno())
assert sha(gate) == before
os.replace(pending, gate)
assert sha(gate) == sha(candidate)
result = {'status': 'APPLIED', 'applied_utc': datetime.now(timezone.utc).isoformat(),
          'gate': str(gate), 'before_sha256': before, 'candidate_sha256': sha(candidate),
          'qualification_sha256': sha(qualification), 'cad_processes': count,
          'queues_idle': True, 'service_healthy': True, 'original_failed_tasks_changed': False,
          'production_release': False, 'portal_acceptance': 'PENDING_FRESH_TASK'}
receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps(result, ensure_ascii=False))
