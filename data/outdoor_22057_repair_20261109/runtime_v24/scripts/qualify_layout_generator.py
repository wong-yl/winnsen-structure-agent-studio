"""Freeze the three audited layouts and every runtime verification dependency."""
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from generate_model import STAGE, read, sha, write
from layout_contract import INVESTIGATION_RANGES, validate_specification
from generate_layout_model import counts


cases = []
for name in ('minimum_170_contract_r281.json', 'editor_r283_browser.json', 'portal_contract_r283.json', 'saved_reload_r301_acceptance.json', 'native_scope_equivalence_r303.json'):
    report = read(STAGE / 'evidence' / name)
    assert report['status'] == 'PASS'
    for path, digest in report.get('sources', {}).items():
        assert sha(path) == digest
for tag, spec_file in (
    ('pilot_r188', 'pilot_r188_spec.json'),
    ('boundary_low_r246', 'boundary_low_r188_spec.json'),
    ('boundary_high_r296', 'boundary_high_r281_spec.json'),
):
    expected = validate_specification(read(STAGE / 'evidence' / spec_file))
    tool = STAGE / ('OutdoorLayoutR188.exe' if tag == 'pilot_r188' else 'OutdoorLayoutExpandedR296.exe' if tag == 'boundary_high_r296' else 'OutdoorLayoutExpandedR246.exe')
    path = STAGE / 'evidence' / (tag + '_result.json')
    result = read(path)
    assert result['status'] == 'PASS' and result['specification'] == expected
    solids, documents = counts(expected)
    assert (result['visible_solids'], result['native_documents']) == (solids, documents)
    assert len(result['native_phases']) == 3
    assert len({read(p)['process_id'] for p in result['native_phases']}) == 3
    for p in result['native_phases']:
        phase, launch = read(p), read(str(p) + '.launch.json')
        assert phase['exit_app_called'] and launch['tool_sha256'] == sha(tool)
        assert launch['contract_sha256'] == sha(result['contract']) == phase['contract_sha256']
        assert phase['cases'][0]['snapshot']['visible_solids'] == solids
        assert phase['cases'][0]['health_pass']
    final_case = read(result['native_phases'][-1])['cases'][0]
    root = Path(result['models'])
    assert {p.name: sha(p) for p in root.iterdir() if p.is_file() and not p.name.startswith('~$')} == final_case['final_identity']
    for p in result['native_phases']:
        export = read(p)['cases'][0]['export']
        assert sha(export['path']) == export['sha256']
    saved = read(result['native_phases'][0])['cases'][0]
    assert saved['saved_file_reload_before_export'] and saved['saved_reload_files_unchanged']
    assert len(result['checks']) == 4
    for p in result['checks']:
        assert read(p)['status'] == 'PASS'
    supplemental = []
    if tag == 'pilot_r188':
        additional = STAGE / 'evidence/pilot_r188_additional_punch_geometry_r201.json'
        audit = read(additional)
        assert audit['status'] == 'PASS' and len(audit['checks']) == 19
        assert all(check['pass_'] for check in audit['checks'])
        assert str(Path(result['native_phases'][1])) in audit['sources']
        for source, digest in audit['sources'].items():
            assert sha(source) == digest
        supplemental.append(str(additional))
        supplemental.append(str(STAGE / 'evidence/pilot_r188_compatibility_r289.json'))
    elif tag == 'boundary_low_r246':
        supplemental.append(str(STAGE / 'evidence/boundary_low_r246_compatibility_r282.json'))
    else:
        supplemental.extend(str(STAGE / 'evidence' / name) for name in ('same_height_shelf_pairing_r270.json', 'shelf_notch_local_r287_corrected.json', 'shelf_suppression_acceptance_r295.json'))
    for item in supplemental:
        audit = read(item)
        assert audit['status'] == 'PASS'
        for source, digest in audit.get('sources', {}).items():
            assert sha(source) == digest
    cases.append({'specification': expected, 'result': str(path), 'sha256': sha(path),
                  'supplemental_checks': supplemental,
                  'evidence_sha256': {str(p): sha(p) for p in result['native_phases'] + result['checks'] + [result['contract']] + supplemental}})

seed_path = STAGE / 'seed_manifest.json'
seed = read(seed_path)
for name, digest in seed['files'].items():
    assert sha(Path(seed['root']) / name) == digest

support = [
    'OutdoorLayoutR188.exe', 'OutdoorLayoutExpandedR246.exe', 'OutdoorLayoutExpandedR301.exe', 'OutdoorLayoutExpandedR296.exe', 'SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll',
    'evidence/saved_reload_r301_acceptance.json', 'evidence/native_scope_equivalence_r303.json', 'OutdoorLayoutExpandedR303.exe',
    'seed_manifest.json', 'independent_size_rules.json', 'assembly_size_rules.json',
    'scripts/generate_model.py', 'scripts/generate_layout_model.py', 'scripts/portal_generate.py',
    'scripts/layout_contract.py', 'scripts/prepare_layout_case.py', 'scripts/run_native.mjs',
    'scripts/cleanup_owned_residual_poll_r4.ps1', 'scripts/audit_layout_r178.py',
    'scripts/audit_layout_r231.py',
    'scripts/audit_layout_r259.py',
    'scripts/audit_layout_r270.py', 'scripts/audit_layout_r280.py', 'scripts/audit_layout_r282.py', 'scripts/audit_layout_r289.py',
    'scripts/audit_shade_r178.py', 'scripts/shade_geometry_lib_r145.py',
    'scripts/audit_shade_r201.py', 'scripts/audit_shade_r239.py',
    'OutdoorLayoutOfflineR247.exe', 'scripts/run_layout_offline_r189.mjs', 'scripts/verify_layout_download.py',
    'scripts/compare_step_pair_cached.py', 'scripts/qualify_layout_generator.py',
]
support += [str(p.relative_to(STAGE)) for p in (STAGE / 'scripts').glob('*R145.cs')]
support += ['scripts/' + name for name in ('LayoutR167.cs', 'LayoutBuildR170.cs', 'LayoutFeaturesR170.cs')]
support += [str(p.relative_to(STAGE)) for p in (STAGE / 'scripts/expanded_native_r246').glob('*.cs')]
support += [str(p.relative_to(STAGE)) for p in (STAGE / 'scripts/expanded_native_r301').glob('*.cs')]
support += [str(p.relative_to(STAGE)) for p in (STAGE / 'scripts/expanded_native_r296').glob('*.cs')]
support += [str(p.relative_to(STAGE)) for p in (STAGE / 'scripts/expanded_native_r303').glob('*.cs')]
support += [str(p.relative_to(STAGE)) for p in (STAGE / 'scripts/offline_r247').glob('*.cs')]
baseline_path = Path(seed['root']).parent / 'evidence/source_requal_r146_sizing_survey.json'
baseline_step = Path(read(baseline_path)['cases'][0]['export']['path'])
dependencies = {baseline_path.resolve(), baseline_step.resolve()}

# The shared lock helper imports the indoor worker; freeze its actual local imports too.
platform = Path(r'D:\Winnsen_Structure_Agent_Studio')
pending = [platform / 'tools' / name for name in (
    'run_16029_native_worker.mjs', 'run_outdoor_22057_portal_worker.mjs',
    'lib/outdoor_22057_layout.mjs', 'lib/outdoor_22057_tasks.mjs', 'lib/outdoor_22057_portal.mjs',
)]
while pending:
    path = pending.pop().resolve()
    if path in dependencies:
        continue
    assert path.is_relative_to(platform)
    dependencies.add(path)
    for relative in re.findall(r"(?:from\s*|import\s*\(\s*|import\s*)['\"](\.[^'\"]+)['\"]", path.read_text(encoding='utf-8-sig')):
        imported = (path.parent / relative).resolve()
        assert imported.is_file(), str(imported)
        pending.append(imported)

qualification = {
    'status': 'PASS', 'release_id': 'OUT22057-LAYOUT-20261017-v15',
    'checked_utc': datetime.now(timezone.utc).isoformat(),
    'purpose': 'engineering_assistance_models',
    'input_range_mm': {key: list(value) for key, value in INVESTIGATION_RANGES.items()},
    'default_specification': {'height': 1939, 'width': 950, 'depth': 550, 'doors': 14},
    'layout': {'enabled': True, 'version': 'outdoor-dual-column-v1', 'gap_mm': 6,
               'min_door_height_mm': 170, 'min_bottom_door_height_mm': 170,
               'max_column_doors': 11, 'max_total_doors': 22,
               'height_dependent_column_limit': 'min(11,floor((H-115)/176))', 'height_closure_required': True},
    'cases': cases,
    'coverage': {
        'method': 'combined_dimension_boundaries_and_asymmetric_row_extremes_with_mandatory_per_task_native_audit',
        'runtime_audit_required': True, 'runtime_independent_processes': 3,
        'saved_file_reload_before_export_required': True,
        'out_of_range_rejected': True, 'unverified_tasks_downloadable': False,
        'rules': {'row_pitch_budget': 'H-115', 'first_door_bottom': 23, 'last_door_top': 'H-98',
                  'door_width': 'W/2-26', 'lock_center': 'door_center-15',
                  'shade_width': 'W-160', 'connector_width': 'W-121',
                  'long_press_width': 'W', 'led_width': 'W-222',
                  'cover_width': 'W/2-100', 'cover_opening_width': 'W/2-115',
                  'cover_opening_center_pitch': 'W/2+45', 'cover_side_overlap_mm': 7.5,
                  'divider_pilot_depth_pitch': 'D-350',
                  'shelf_channel_hole_z': -96.35,
                  'shelf_channel_axial_gap_mm': .5,
                  'fixed_parts': 'standard columns, short solar strips, purchased parts'},
        'boundary_risks_checked': ['unique lock and shelf bores', 'hinge and pin axes',
                                   'shelf notch material and height linkage', 'shelf and frame interference', 'adjacent lock plate interference',
                                   'shelf and lock plate interference', 'canopy mounting',
                                   'cover and LED clearance', 'base mounting',
                                   'fixed geometry', 'saved and independently reopened solids'],
    },
    'seed_manifest_sha256': sha(seed_path),
    'accepted_layout_decision': {'minimum_door_height_mm': 170, 'retain_existing_lock_and_shelf_structure': True,
                                 'supersedes_130_mm_option': True, 'compact_hole_relocation_enabled': False},
    'native_tool_routing': {'exact_reviewed_1939_950_550_L450_600_756_R170_200_300_400_724': 'OutdoorLayoutR188.exe', 'two_doors_without_internal_shelves': 'OutdoorLayoutExpandedR301.exe', 'other_layouts_with_internal_shelves': 'OutdoorLayoutExpandedR303.exe', 'minimum_height_checked_before_native_launch_mm': 170},
    'support_sha256': {p: sha(STAGE / p) for p in sorted(set(support))},
    'external_support_sha256': {str(p): sha(p) for p in sorted(dependencies)},
    'production_release': False,
}
assert not (STAGE / 'qualification.json').exists()
write(STAGE / 'qualification.json', qualification)
print(json.dumps({'status': 'PASS', 'cases': len(cases),
                  'qualification_sha256': sha(STAGE / 'qualification.json')}))
