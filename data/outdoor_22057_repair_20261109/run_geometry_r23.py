"""Run the original exact-case full native save/reopen/audit chain on R23."""
import json
import sys
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v23'
assert json.loads((WORK / 'candidate_r23_preflight.json').read_text('utf-8'))['status'] == 'STATIC_PASS_RUNTIME_PENDING'
sys.path.insert(0, str(STAGE / 'scripts'))
from generate_layout_model import generate

task = json.loads((WORK.parent / 'outdoor_22057_tasks/OUT22057-8bdbcd9b-3e71-4ae1-bea6-4c6b96940ff2.json').read_text('utf-8-sig'))
result = generate(task['specification'], 'probe_22057_1939_860_550_l7_r9_r23')
print(json.dumps(result, ensure_ascii=False))
