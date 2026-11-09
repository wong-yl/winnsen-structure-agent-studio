"""Collect independent r19 diagnostics; never qualify a failed candidate."""
import json
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parent
STAGE = WORK / 'runtime_v19'
OUT = WORK / 'layout_diagnostic_r20'
PYTHON = r'D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\bin\python.exe'
native = STAGE / 'evidence/probe_22057_1939_860_550_l7_r9_r19_reopen_verify.json'
contract = STAGE / 'evidence/probe_22057_1939_860_550_l7_r9_r19_contract.json'
assert (OUT / 'clearance_locations.json').is_file()
summary_path = OUT / 'remaining_audits.json'
assert not summary_path.exists()
rows = []
audits = [('audit_shade_r369.py', 'shade'), ('audit_cover_fasteners_r404.py', 'fasteners'),
          ('audit_narrow_auxiliary_r437.py', 'auxiliary'), ('audit_narrow_lamp_r370.py', 'lamp'),
          ('audit_rear_rib_r386.py', 'rear_rib'), ('audit_roof_board_r398.py', 'roof_board')]
for script, label in audits:
    output = OUT / (label + '.json')
    assert not output.exists()
    arguments = [str(native), str(output), str(contract)] if label == 'shade' else [str(native), str(contract), str(output)]
    code = ('import sys,runpy;from pathlib import Path;'
            'sys.path.insert(0,sys.argv.pop(1));import shade_geometry_lib_r145 as g;'
            'g.EVIDENCE=Path(sys.argv.pop(1));script=sys.argv.pop(1);'
            'sys.argv[0]=script;runpy.run_path(script,run_name="__main__")')
    with output.with_suffix('.log').open('w', encoding='utf-8') as log:
        process = subprocess.run([PYTHON, '-X', 'utf8', '-B', '-c', code, str(STAGE / 'scripts'),
                                  str(OUT), str(STAGE / 'scripts' / script), *arguments],
                                 cwd=STAGE, stdout=log, stderr=log)
    report = json.loads(output.read_text('utf-8-sig')) if output.exists() else {}
    row = {'label': label, 'returncode': process.returncode, 'status': report.get('status', 'NO_REPORT'),
           'output': str(output), 'failed': [c for c in report.get('checks', []) if not c.get('pass_', True)],
           'error': report.get('error')}
    rows.append(row)
    summary_path.write_text(json.dumps({'status': 'DIAGNOSTIC_ONLY', 'candidate_accepted': False, 'audits': rows}, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps(row, ensure_ascii=False), flush=True)
