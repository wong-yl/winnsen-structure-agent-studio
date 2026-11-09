"""Restore the previous route after the fresh task was rejected; keep every failure."""
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
task_path = DATA / 'outdoor_22057_tasks/OUT22057-59263d53-54f3-48b0-a263-0e226233069e.json'
receipt_path = WORK / 'route_restored_after_r22_portal_failure.json'
assert not receipt_path.exists()
assert read(task_path)['state'] == 'failed'
assert sha(gate) == sha(WORK / 'release_gate.r22.candidate.json')
for path in (DATA / 'outdoor_22057_tasks').glob('OUT22057-*.json'):
    assert read(path).get('state') not in ('created', 'queued', 'claimed', 'running', 'generating', 'validating'), str(path)
for path in (DATA / 'native_model_requests').glob('*.json'):
    if path.name.startswith(('NATIVE-', 'PARAMETRIC-')):
        assert read(path).get('nativeBuild', {}).get('state') not in ('created', 'queued', 'claimed', 'planning', 'building', 'running', 'validating'), str(path)
assert not (DATA / 'native_model_requests/.native-worker-instance.lock').exists()
count = int(subprocess.check_output(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
            '@(Get-CimInstance Win32_Process -Filter "Name=\'SLDWORKS.exe\'").Count'], text=True).strip())
assert count == 0
with urllib.request.urlopen('http://127.0.0.1:5180/status.json', timeout=10) as response:
    assert response.status == 200 and json.load(response)['status'] == 'ok'
before = sha(gate)
pending = gate.with_name('release_gate.r22.rollback.pending')
with pending.open('xb') as output:
    output.write((WORK / 'release_gate.before.json').read_bytes())
    output.flush()
    os.fsync(output.fileno())
assert sha(gate) == before
os.replace(pending, gate)
assert sha(gate) == sha(WORK / 'release_gate.before.json')
result = {'status': 'RESTORED', 'checked_utc': datetime.now(timezone.utc).isoformat(), 'failed_task': str(task_path),
          'failed_task_sha256': sha(task_path), 'rejected_gate_sha256': before, 'restored_gate_sha256': sha(gate),
          'cad_count': count, 'queues_idle': True, 'service_healthy': True, 'task_and_evidence_preserved': True,
          'reason': 'Fresh portal saved STEP had 398 solids; independent reopen had 399; original comparison rejected',
          'production_release': False}
receipt_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps(result, ensure_ascii=False))
