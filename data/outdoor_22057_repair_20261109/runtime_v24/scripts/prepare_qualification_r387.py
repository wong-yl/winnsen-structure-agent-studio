"""Prepare the final freezer; execution still requires both whole models to pass."""
from generate_model import STAGE

source=(STAGE/'scripts/qualify_width600_r349.py').read_text(encoding='utf-8')
source=source.replace("assert len(checks)>=6", "assert len(checks)>=8")
source=source.replace("'scripts/qualify_width600_r349.py','scripts/audit_layout_r345.py'", "'scripts/qualify_width600_r387.py','scripts/audit_layout_r368.py','scripts/audit_shade_r369.py','scripts/audit_narrow_lamp_r370.py','scripts/audit_rear_rib_r386.py'")
source=source.replace("'OutdoorLayoutOfflineR307.exe'", "'OutdoorLayoutOfflineR372.exe'")
source=source.replace("'scripts/run_layout_offline_r307.mjs'", "'scripts/run_layout_offline_r372.mjs'")
source=source.replace("'scripts/offline_r307'", "'scripts/offline_r372'")
key="qualification={**previous,'status':'PASS'"
assert source.count(key)==1
source=source.replace(key,"""transitions=read(STAGE/'evidence/led_transitions_r378_result.json')
assert transitions['status']=='PASS_LOCAL_NOT_SAVED'
assert [c['width_mm'] for c in transitions['checks']]==[748.5,898.5]
transition_files=[STAGE/'evidence/led_transitions_r378_result.json',Path(transitions['native'])]
assert sha(transitions['native'])==transitions['native_sha256']
transition_native=read(transitions['native'])
assert transition_native['status']=='layout_probe_finished' and transition_native['exit_app_called']
assert transition_native['cases'][0]['files_unchanged'] and not transition_native['cases'][0]['native_save_performed']
for p in transitions['checks']:
    assert sha(p['geometry'])==p['sha256']
    check=read(p['geometry']);assert check['status']=='PASS_LOCAL_NOT_SAVED'
    transition_files.append(Path(p['geometry']))
    for path,digest in check['sources'].items():
        assert sha(path)==digest,path
        transition_files.append(Path(path))
for suffix in ['.launch.json','.after.json']:transition_files.append(Path(transitions['native']+suffix))
transition_launch=read(transitions['native']+'.launch.json')
assert sha(transition_launch['tool_path'])==transition_launch['tool_sha256']
support.update([Path(transition_launch['tool_path']).name,'scripts/analyze_short_led_r365.py','scripts/analyze_led_transitions_r378.py'])
support.update(str(p.relative_to(STAGE)) for p in transition_files)
ui=read(STAGE/'evidence/width600_progress_r362.json')['ui']
for path,digest in ui['source_sha256'].items():assert sha(path)==digest,path
assert read(ui['browser_evidence'])['status']=='PASS_UI_ONLY'
support.update(str(p.relative_to(STAGE)) for p in (STAGE/'portal_studio/lib').glob('*.mjs'))
support.update(['evidence/width600_progress_r362.json','evidence/studio_ui_browser_r357.json'])
qualification={**previous,'status':'PASS'""")
source=source.replace("'fixed_parts':'standard columns, short press strips and purchased geometry preserved'", "'fixed_parts':'standard columns and short solar press strips preserved; approved short LEDs retain cross section and LED package geometry',\n    'narrow_lamp':'shell W-222, opening W-470, plastic cover W-443, retainer W-440; two 75mm pitch LED rows with even package count',\n    'rear_rib_pilot_linkage':'x=+-max(75,W/2-300), top y=H-118.8, bottom y=60 with 30mm pitch; all real bores, 3mm material rims, original 50mm ribs and neighbor clearances audited per task',\n    'local_count_transitions_mm':[748.5,898.5]")
source=source.replace("'production_release':False}", "'ui_source_sha256':ui['source_sha256'],'local_transition_evidence':transitions,'production_release':False}")
target=STAGE/'scripts/qualify_width600_r387.py'
assert not target.exists()
target.write_text(source,encoding='utf-8')
print('Prepared freezer only; qualification not written')
