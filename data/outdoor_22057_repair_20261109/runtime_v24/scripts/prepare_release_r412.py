"""Switch only the outdoor gate and route after the required acceptance evidence."""
import argparse
import os
from datetime import datetime,timezone
from pathlib import Path
from generate_model import STAGE,read,write,sha

parser=argparse.ArgumentParser()
parser.add_argument('target',choices=['fixture','production'])
args=parser.parse_args()
qpath=STAGE/'qualification.json';q=read(qpath)
assert q['status']=='PASS' and q['input_range_mm']['width']==[600,1200]
for p,h in q['support_sha256'].items():assert sha(STAGE/p)==h,p
for p,h in q['external_support_sha256'].items():assert sha(p)==h,p
for p,h in q['ui_source_sha256'].items():assert sha(p)==h,p
platform=Path(r'D:/Winnsen_Structure_Agent_Studio')
root=platform if args.target=='production' else STAGE/'evidence/portal_fixture_width600_r333'
gate_path=root/'data/outdoor_22057_release/release_gate.json'
gate=read(gate_path)
previous=read(platform/'data/outdoor_22057_release/release_gate.json')
generation={**previous['generation'],'enabled':True,'ranges_mm':q['input_range_mm'],
    'source_stage':str(STAGE),'script':str(STAGE/'scripts/portal_generate.py'),
    'qualification_sha256':sha(qpath),'script_sha256':sha(STAGE/'scripts/portal_generate.py')}
gate={**gate,'generation':generation,'variable_trial_release_id':q['release_id'],
    'variable_trial_released_at':datetime.now(timezone.utc).isoformat(),
    'variable_trial_authorized':True,'engineering_assistance':True}
assert gate['packageReady'] and sha(root/'data/outdoor_22057_release'/gate['archive'])==gate['sha256']
backup=STAGE/f'evidence/release_gate_{args.target}_before_r412.json'
assert not backup.exists()
if args.target=='production':
    accepted=read(STAGE/'evidence/downloaded_r400_acceptance.json')
    assert accepted['status']=='PASS' and accepted['specification']['width']==600
    assert accepted['generation_folder_unavailable_during_reopen'] and accepted['generation_folder_restored_unchanged']
    assert sha(accepted['archive'])==accepted['archive_sha256']
    expected=read(STAGE/'evidence/protected_runtime_r402.json')['protected_files']
    server=platform/'tools/serve_16029_review_downloads.mjs'
    assert sha(server)==expected[str(server)] and sha(gate_path)==expected[str(gate_path)]
    server_backup=STAGE/'evidence/serve_16029_review_downloads_before_r412.mjs'
    assert not server_backup.exists();server_backup.write_bytes(server.read_bytes())
    server_bytes=server.read_bytes()
    old="import { createOutdoor22057Routes } from './lib/outdoor_22057_portal.mjs'"
    assert server_bytes.count(old.encode('utf-8'))==1
    new="import { createOutdoor22057Routes } from '"+(STAGE/'portal_studio/lib/outdoor_22057_portal.mjs').as_uri()+"'"
    server_bytes=server_bytes.replace(old.encode('utf-8'),new.encode('utf-8'),1)
    temporary=server.with_name(server.name+'.width600-r412.tmp')
    assert not temporary.exists();temporary.write_bytes(server_bytes);os.replace(temporary,server)
backup.write_bytes(gate_path.read_bytes())
temporary=gate_path.with_name(gate_path.name+'.width600-r412.tmp')
assert not temporary.exists();write(temporary,gate);os.replace(temporary,gate_path)
receipt={'status':'CONFIGURED_PENDING_RUNTIME_CHECK','target':args.target,'gate':str(gate_path),
    'gate_sha256':sha(gate_path),'backup':str(backup),'qualification_sha256':sha(qpath),
    'script_sha256':generation['script_sha256'],'ranges_mm':generation['ranges_mm']}
if args.target=='production':receipt.update(server=str(server),server_sha256=sha(server),server_backup=str(server_backup))
write(STAGE/f'evidence/release_{args.target}_r412.json',receipt)
print(receipt)
