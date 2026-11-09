"""Check the isolated export fix without changing any saved or geometry gates."""
import hashlib
import json
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v24'
PARENT = WORK / 'runtime_v23'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

candidate = read(STAGE / 'repair_manifest.json')
parent = read(PARENT / 'repair_manifest.json')
for root, manifest in ((STAGE, candidate), (PARENT, parent)):
    for relative, digest in manifest['identity'].items():
        assert sha(root / relative) == digest, (str(root), relative)
original = read(WORK / 'original_identity.json')
for path, digest in original.items():
    assert sha(path) == digest, path
qualification = read(WORK / 'runtime_v17/qualification.json')
for relative, digest in qualification['support_sha256'].items():
    assert sha(WORK / 'runtime_v17' / relative) == digest, relative
for path, digest in qualification['external_support_sha256'].items():
    assert sha(path) == digest, path
seed = read(STAGE / 'seed_manifest.json')
for name, digest in seed['files'].items():
    assert sha(Path(seed['root']) / name) == digest, name

for relative in parent['identity']:
    if Path(relative).suffix in ('.py', '.mjs', '.ps1'):
        assert sha(STAGE / relative) == sha(PARENT / relative), relative
base = 'scripts/width600_native_r440/FeedbackCadBaseR145.cs'
old, new = ((root / base).read_text('utf-8-sig') for root in (PARENT, STAGE))
assert old[old.index('    static void SaveDocs('):] == new[new.index('    static void SaveDocs('):]
assert old[:old.index('    static void ExportCurrent(')] == new[:new.index('    static void ExportCurrent(')]
shutdown = 'scripts/width600_native_r440/SavedReloadSessionR301.cs'
assert 'Marshal.ReleaseComObject(oldApplication)' in (STAGE / shutdown).read_text('utf-8-sig')
assert sha(STAGE / 'scripts/cleanup_owned_residual_poll_r4.ps1') == sha(PARENT / 'scripts/cleanup_owned_residual_poll_r4.ps1')
main = (STAGE / 'scripts/width600_native_r440/OutdoorShadeR145.cs').read_text('utf-8-sig')
assert main.count('if(!row.ContainsKey("saved_reload_fresh_process_id"))RestartSavedReloadCad(row);') == 1
assert main.count('saved_reload_independent_session_required') == 1
assert new.count('step_export_preflight') == 1 and 'selection.GetSelectedObjectCount2(-1)==0' in new
assert sha(WORK.parent / 'outdoor_22057_release/release_gate.json') == sha(WORK / 'release_gate.before.json')
result = {'status': 'STATIC_PASS_RUNTIME_PENDING', 'candidate_support_files': len(candidate['identity']),
          'parent_support_files_unchanged': len(parent['identity']), 'protected_original_paths_unchanged': len(original),
          'seed_files_unchanged': len(seed['files']), 'save_gate_unchanged': True,
          'all_audit_sources_and_tolerances_unchanged': True, 'lock_queue_natural_shutdown_unchanged': True,
          'release_gate_unchanged': True, 'production_release': False}
output = WORK / 'candidate_r24_preflight.json'
assert not output.exists()
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps(result, ensure_ascii=False))
