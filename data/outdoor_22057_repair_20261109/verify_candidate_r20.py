"""Verify new geometry candidate identity and unchanged acceptance boundaries."""
import hashlib
import json
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v20'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

candidate = read(STAGE / 'repair_manifest.json')
parent = read(WORK / 'runtime_v19/repair_manifest.json')
for stage, manifest in [(STAGE, candidate), (WORK / 'runtime_v19', parent)]:
    for relative, expected in manifest['identity'].items():
        assert sha(stage / relative) == expected, (str(stage), relative)
original = read(WORK / 'original_identity.json')
for path, expected in original.items():
    assert sha(path) == expected, path
qualification = read(WORK / 'runtime_v17/qualification.json')
for relative, expected in qualification['support_sha256'].items():
    assert sha(WORK / 'runtime_v17' / relative) == expected, relative
for path, expected in qualification['external_support_sha256'].items():
    assert sha(path) == expected, path
seed = read(STAGE / 'seed_manifest.json')
for name, expected in seed['files'].items():
    assert sha(Path(seed['root']) / name) == expected, name
for relative in candidate['identity']:
    if Path(relative).suffix in ('.py', '.mjs', '.ps1'):
        assert sha(STAGE / relative) == sha(WORK / 'runtime_v19' / relative), relative
save_source = 'scripts/width600_native_r440/FeedbackCadBaseR145.cs'
assert sha(STAGE / save_source) == sha(WORK / 'runtime_v19' / save_source)
assert sha(WORK.parent / 'outdoor_22057_release/release_gate.json') == sha(WORK / 'release_gate.before.json')
native = read(WORK / 'runtime_v19/evidence/write_state_r19_generate.json')['cases'][0]
blocks = native['cover_fastener_width_linkage']['blocks']
outer = [b for b in blocks if abs(b['before'][9]) > .25]
assert len(outer) == 2 and all(abs(abs(b['before'][9]) - .3125) < 1e-8 and abs(abs(b['after'][9]) - .2675) < 1e-8 for b in outer)
result = {'status': 'STATIC_PASS_RUNTIME_PENDING', 'candidate_support_files': len(candidate['identity']),
          'r19_support_files_unchanged': len(parent['identity']), 'v17_support_files_unchanged': len(qualification['support_sha256']),
          'protected_original_paths_unchanged': len(original), 'seed_files_unchanged': len(seed['files']),
          'all_audit_sources_and_tolerances_unchanged': True, 'save_gate_unchanged': True,
          'lock_queue_shutdown_unchanged': True, 'release_gate_unchanged': True,
          'confirmed_double_nut_translation_mm': 45, 'production_release': False}
output = WORK / 'candidate_r20_preflight.json'
assert not output.exists()
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps(result, ensure_ascii=False))
