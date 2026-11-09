"""Read-only identity and guard verification for the isolated r19 candidate."""
import hashlib
import json
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v19'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

manifest = read(STAGE / 'repair_manifest.json')
for relative, expected in manifest['identity'].items():
    assert sha(STAGE / relative) == expected, relative
original = read(WORK / 'original_identity.json')
for path, expected in original.items():
    assert sha(path) == expected, path
parent = WORK / 'runtime_v17'
qualification = read(parent / 'qualification.json')
for relative, expected in qualification['support_sha256'].items():
    assert sha(parent / relative) == expected, relative
for path, expected in qualification['external_support_sha256'].items():
    assert sha(path) == expected, path
seed = read(STAGE / 'seed_manifest.json')
for name, expected in seed['files'].items():
    assert sha(Path(seed['root']) / name) == expected, name
def save_gate(path):
    return path.read_bytes().split(b'    static void SaveDocs(', 1)[1].split(b'    static void Persist(', 1)[0]
relative = 'scripts/width600_native_r440/FeedbackCadBaseR145.cs'
assert save_gate(STAGE / relative) == save_gate(WORK / 'runtime_v18' / relative)
candidate = (STAGE / relative).read_text('utf-8')
guard_lines = [line.strip() for line in (parent / relative).read_text('utf-8-sig').splitlines()
               if 'if(!d.GetSaveFlag())continue;if(d.IsOpenedReadOnly()' in line
               or 'Need(!d.IsOpenedReadOnly()&&Within(d.GetPathName(),Root)' in line]
assert len(guard_lines) == 2 and all(line in candidate for line in guard_lines)
for path in [STAGE / 'scripts/run_native.mjs', STAGE / 'scripts/cleanup_owned_residual_poll_r4.ps1']:
    assert sha(path) == sha(parent / path.relative_to(STAGE)), path
assert sha(WORK.parent / 'outdoor_22057_release/release_gate.json') == sha(WORK / 'release_gate.before.json')
diagnostic = read(WORK / 'runtime_v18/evidence/save_probe_r18_generate.json')
rows = diagnostic['cases'][0]['save_preflight_documents']
blockers = [row for row in rows if row['save_flag'] and not row['fixed_purchased']
            and not (row['opened_read_only'] and row['readonly_audit_hit'])
            and (row['opened_read_only'] or not row['within_root'])]
assert blockers and all(row['within_root'] and row['opened_read_only'] for row in blockers)
result = {
    'status': 'STATIC_PASS_RUNTIME_PENDING', 'candidate_manifest_sha256': sha(STAGE / 'repair_manifest.json'),
    'support_checked': len(manifest['identity']), 'original_paths_checked': len(original),
    'original_support_checked': len(qualification['support_sha256']), 'seed_files_checked': len(seed['files']),
    'save_gate_bytes_unchanged_from_r18': True, 'original_save_guards_present': True,
    'runtime_queue_lock_shutdown_unchanged': True, 'release_gate_unchanged': True,
    'diagnostic': {'loaded_documents': len(rows), 'outside': sum(not row['within_root'] for row in rows),
                   'blocking_readonly_documents': len(blockers), 'first_blocker': blockers[0]['path']},
}
output = WORK / 'candidate_r19_preflight.json'
assert not output.exists(), 'Preserve prior evidence'
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
print(json.dumps(result, ensure_ascii=False))
