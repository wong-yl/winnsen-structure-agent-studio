"""One-time, additive runtime isolation. Original qualification and seed stay untouched."""
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(r'D:\Winnsen_Structure_Agent_Studio')
WORK = ROOT / 'data/outdoor_22057_repair_20261109'
OLD = Path(r'D:\机械结构工程师智能体\outputs\outdoor_22057_width600_portal_20261018_v16')
NEW = WORK / 'runtime_v17'
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def write(p, value): Path(p).write_text(json.dumps(value, ensure_ascii=False, indent=2), 'utf-8')

q = read(OLD / 'qualification.json')
assert not NEW.exists(), 'Preserve existing runtime'
original = {}
for rel, expected in q['support_sha256'].items():
    assert sha(OLD / rel) == expected, 'Original support changed: ' + rel
    original[str(OLD / rel)] = expected
original[str(OLD / 'qualification.json')] = sha(OLD / 'qualification.json')
seed = read(OLD / 'seed_manifest.json')
for name, expected in seed['files'].items():
    p = Path(seed['root']) / name
    assert sha(p) == expected, 'Seed changed: ' + name
    original[str(p)] = expected
drift = []
for p, expected in q['external_support_sha256'].items():
    actual = sha(p)
    if actual != expected: drift.append({'path': p, 'expected': expected, 'actual': actual})
    original[p] = actual
write(WORK / 'original_identity.json', original)
write(WORK / 'dependency_drift.json', drift)
NEW.mkdir()
for rel in q['support_sha256']:
    dest = NEW / rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(OLD / rel, dest)
# Existing small evidence / scripts include supplemental baseline inputs used by the tools.
for folder, pattern in [('evidence', '*.json'), ('scripts', '*.py')]:
    for p in (OLD / folder).glob(pattern):
        dest = NEW / folder / p.name
        if not dest.exists(): shutil.copy2(p, dest)
for p in OLD.glob('*.json'):
    if p.name != 'qualification.json' and not (NEW / p.name).exists(): shutil.copy2(p, NEW / p.name)

worker = (ROOT / 'tools/run_16029_native_worker.mjs').read_text('utf-8-sig')
helpers = worker[worker.index('function isoNow('):worker.index('function requestDigestForPlan(')]
lock = """// Isolated verbatim lock helpers from the qualified shared worker; same dataDir and token protocol.
import { createHash, randomBytes } from 'node:crypto'
import { closeSync, existsSync, fsyncSync, mkdirSync, openSync, readFileSync, renameSync, rmSync, statSync, unlinkSync, writeFileSync } from 'node:fs'
import { hostname } from 'node:os'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
const WORKER_LOCK_STALE_MS = 120000
""" + helpers
(NEW / 'scripts/native_worker_lock.mjs').write_text(lock, 'utf-8')
shutil.copy2(ROOT / 'tools/lib/outdoor_22057_layout.mjs', NEW / 'scripts/outdoor_22057_layout.mjs')
p = NEW / 'scripts/run_native.mjs'
text = p.read_text('utf-8-sig').replace('file:///D:/Winnsen_Structure_Agent_Studio/tools/lib/outdoor_22057_layout.mjs', './outdoor_22057_layout.mjs').replace('file:///D:/Winnsen_Structure_Agent_Studio/tools/run_16029_native_worker.mjs', './native_worker_lock.mjs')
p.write_text(text, 'utf-8')
p = NEW / 'scripts/cleanup_owned_residual_poll_r4.ps1'
text = p.read_text('utf-8-sig').replace(' Stop-Process -Id $ownedPid -Force\n', '').replace('Owned residual still present after bounded polling', 'Owned CAD did not exit naturally within 90 seconds; no termination attempted').replace('stopped verified owned residual and independently polled its absence', 'waited for verified owned CAD to exit naturally')
assert 'Stop-Process' not in text and 'taskkill' not in text
p.write_text(text, 'utf-8-sig')

q['release_id'] = 'OUT22057-RUNTIME-ISOLATION-20261109-v17'
q['runtime_isolation'] = {
    'parent_qualification': str(OLD / 'qualification.json'),
    'parent_sha256': original[str(OLD / 'qualification.json')],
    'scope': 'Inherited geometric qualification; byte-identical native tools, geometry auditors, seed and limits. Only import isolation and non-forced shutdown changed.',
    'lock_helpers_verbatim_sha256': hashlib.sha256(helpers.encode()).hexdigest(),
    'runtime_acceptance': 'PENDING real task; publication remains gated by unchanged per-task native and geometry audits',
    'removed_import_chain': 'run_native -> run_16029_native_worker -> unused indoor generation/publication modules',
    'actual_imports': ['scripts/native_worker_lock.mjs', 'scripts/outdoor_22057_layout.mjs'],
}
# Unrelated indoor modules are no longer imported. Keep all non-platform evidence dependencies.
q['external_support_sha256'] = {p:h for p,h in q['external_support_sha256'].items() if not Path(p).is_relative_to(ROOT)}
q['external_support_sha256'][str(OLD / 'qualification.json')] = original[str(OLD / 'qualification.json')]
q['support_sha256'] = {str(p.relative_to(NEW)):sha(p) for p in sorted(NEW.rglob('*')) if p.is_file()}
write(NEW / 'qualification.json', q)
gate = read(ROOT / 'data/outdoor_22057_release/release_gate.json')
write(WORK / 'release_gate.before.json', gate)
gate['generation'].update(source_stage=str(NEW), script=str(NEW / 'scripts/portal_generate.py'), qualification_sha256=sha(NEW / 'qualification.json'), script_sha256=sha(NEW / 'scripts/portal_generate.py'))
gate['variable_trial_release_id'] = q['release_id']
write(WORK / 'release_gate.candidate.json', gate)
write(WORK / 'runtime_changes.json', {
    'changed_original_support': [p for p,h in read(OLD/'qualification.json')['support_sha256'].items() if sha(NEW/p)!=h],
    'native_tools_unchanged': all(sha(NEW/p)==h for p,h in read(OLD/'qualification.json')['support_sha256'].items() if p.lower().endswith(('.exe','.dll'))),
    'seed_unchanged': sha(NEW/'seed_manifest.json') == sha(OLD/'seed_manifest.json'),
    'lock_helpers_verbatim': helpers in lock,
    'new_support_count': len(q['support_sha256']), 'drift_count': len(drift),
})
print(json.dumps(read(WORK / 'runtime_changes.json'), ensure_ascii=False))
