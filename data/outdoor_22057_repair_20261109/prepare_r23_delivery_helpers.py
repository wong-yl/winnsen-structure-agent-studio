"""Create separately named R23 acceptance helpers, keeping R22 evidence immutable."""
from pathlib import Path

WORK = Path(__file__).resolve().parent
names = ('prepare_saved_divider_check', 'run_saved_divider_check', 'prepare_restored_route',
         'apply_restored_route', 'finalize_restored_task')
for name in names:
    source = WORK / (name + '_r22.py')
    target = WORK / (name + '_r23.py')
    assert not target.exists(), str(target)
    text = source.read_text('utf-8-sig').replace('runtime_v22', 'runtime_v23').replace('_r22', '_r23').replace('.r22', '.r23').replace('-v22', '-v23').replace('R22', 'R23')
    if name == 'prepare_restored_route':
        before = "qualification = read(WORK / 'runtime_v17/qualification.json')"
        after = """export_acceptance_path = result_path.with_name(result_path.name.removesuffix('_result.json') + '_export_session_acceptance.json')
export_acceptance = read(export_acceptance_path)
assert export_acceptance['status'] == 'PASS' and export_acceptance['native_result_sha256'] == sha(result_path)
for path, digest in export_acceptance['source_sha256'].items():
    assert sha(path) == digest, path
qualification = read(WORK / 'runtime_v17/qualification.json')
qualification['checked_utc'] = datetime.now(timezone.utc).isoformat()"""
        assert text.count(before) == 1
        text = text.replace(before, after)
        text = text.replace("'saved_divider_reopen': saved_mates,", "'export_sessions': export_acceptance, 'export_sessions_sha256': sha(export_acceptance_path),\n           'saved_divider_reopen': saved_mates,")
        text = text.replace("paths = [STAGE / 'repair_manifest.json', receipt_path, result_path, saved_mates_path,", "paths = [STAGE / 'repair_manifest.json', receipt_path, result_path, saved_mates_path, export_acceptance_path,\n         *map(Path, export_acceptance['source_sha256']),")
        text = text.replace("Native save, input limits and all nine audit gates unchanged.", "Saved STEP export uses one fresh owned native session, explicit active top and empty selection; original native save, input limits and all nine audit gates unchanged.")
        text = text.replace("WORK / 'run_saved_divider_check_r23.py'):", "WORK / 'run_saved_divider_check_r23.py', WORK / 'verify_export_sessions_r23.py'):")
    if name == 'finalize_restored_task':
        before = "saved_mates = read(saved_mates_path)"
        after = """export_acceptance_path = native_result_path.with_name(native_result_path.name.removesuffix('_result.json') + '_export_session_acceptance.json')
export_acceptance = read(export_acceptance_path)
assert export_acceptance['status'] == 'PASS' and export_acceptance['native_result_sha256'] == sha(native_result_path)
for path, digest in export_acceptance['source_sha256'].items():
    assert sha(path) == digest, path
saved_mates = read(saved_mates_path)"""
        assert text.count(before) == 1
        text = text.replace(before, after)
        text = text.replace("('runtime_v19', 'runtime_v20', 'runtime_v21')", "('runtime_v19', 'runtime_v20', 'runtime_v21', 'runtime_v22')")
        text = text.replace("'saved_divider_reopen': saved_mates,", "'export_sessions': export_acceptance, 'export_sessions_sha256': sha(export_acceptance_path),\n    'saved_divider_reopen': saved_mates,")
    target.write_text(text, 'utf-8')
    compile(text, str(target), 'exec')
    print(str(target))
