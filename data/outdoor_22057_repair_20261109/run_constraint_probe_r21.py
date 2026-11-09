"""Run a fresh isolated diagnostic; keep the failed guard in force."""
import json
import sys
from pathlib import Path
WORK = Path(__file__).resolve().parent
sys.path.insert(0, str(WORK / 'runtime_v21/scripts'))
from generate_layout_model import generate
task = json.loads((WORK.parent / 'outdoor_22057_tasks/OUT22057-8bdbcd9b-3e71-4ae1-bea6-4c6b96940ff2.json').read_text('utf-8-sig'))
generate(task['specification'], 'probe_22057_1939_860_550_l7_r9_r21')
