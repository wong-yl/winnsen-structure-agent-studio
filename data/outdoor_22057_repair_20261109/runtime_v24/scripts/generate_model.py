"""Generate an isolated, audited native cabinet; preserve all failed checkpoints."""
import argparse
import hashlib
import json
import shutil
import stat
import subprocess
import sys
from pathlib import Path

STAGE=Path(__file__).resolve().parents[1]
PYTHON=r'D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\bin\python.exe'
NODE=r'C:\Program Files\nodejs\node.exe'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,value):Path(p).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
def run(command,log):
    with Path(log).open('w',encoding='utf-8') as f:
        subprocess.run(command,cwd=STAGE,stdout=f,stderr=f,check=True)
def native(contract,mode,tag):
    run([NODE,str(STAGE/'scripts/run_native.mjs'),contract,mode,tag],STAGE/'evidence'/f'{tag}_{mode}.log')
    path=STAGE/'evidence'/f'{tag}_{mode}.json'
    value=read(path)
    assert value['status']==mode+'_finished' and value['exit_app_called']
    return path,value
def audit(path,tag):
    n=read(path);c=n['cases'][0]
    baseline=read(STAGE/'evidence/cabinet_baseline.json')['baseline']
    row=dict(c,label=tag,height_mm=c['dimensions_mm'][0],width_mm=c['dimensions_mm'][1],depth_mm=c['dimensions_mm'][2])
    row['whole_assembly.step']=row.pop('export')
    wrapper=STAGE/'evidence'/f'{tag}_cabinet_input.json'
    write(wrapper,{'root':str(Path(c['snapshot']['documents'][0]['actual_path']).parent),'assembly':{'baseline':baseline,'cases':[row]}})
    cabinet=STAGE/'evidence'/f'{tag}_cabinet.json'
    run([PYTHON,str(STAGE/'scripts/check_cabinet_geometry_r145.py'),wrapper.name,cabinet.name],cabinet.with_suffix('.log'))
    assert read(cabinet)['status']=='PASS'
    shade=STAGE/'evidence'/f'{tag}_shade.json'
    run([PYTHON,str(STAGE/'scripts/audit_shade_r145.py'),str(path),str(shade)],shade.with_suffix('.log'))
    assert read(shade)['status']=='PASS'
    return [cabinet,shade]
def generate(height,width,depth,tag):
    assert tag.replace('_','').isalnum() and tag.isascii() and tag.islower()
    dims=[height,width,depth]
    assert all(isinstance(v,(int,float)) and 0<v<10000 for v in dims)
    source=read(STAGE/'seed_manifest.json')
    for name,digest in source['files'].items():assert sha(Path(source['root'])/name)==digest
    root=STAGE/('generated_'+tag);root.mkdir(exist_ok=False)
    for name in source['files']:
        shutil.copy2(Path(source['root'])/name,root/name)
        (root/name).chmod(stat.S_IREAD|stat.S_IWRITE)
    write(root/'size_request.json',dict(height_mm=height,width_mm=width,depth_mm=depth))
    contract_name=tag+'_contract.json'
    contract={'scope':'22057_SHADE_PORTAL_R145','cases':[{'index':13,'id':tag,'dimensions_mm':dims,'source_root':source['root'],'source_top':source['top'],'top':source['top'],'package_root':str(root),'relocated_root':str(root),'expected_visible_solids':387}]}
    write(STAGE/'evidence'/contract_name,contract)
    saved,saved_value=native(contract_name,'generate',tag+'_save')
    for p in root.iterdir():
        if p.suffix.lower() in ['.sldprt','.sldasm']:p.chmod(stat.S_IREAD)
    checks=[];phases=[saved]
    for phase in ['reopen','reopened']:
        path,value=native(contract_name,'verify',tag+'_'+phase);phases.append(path)
        comparison=STAGE/'evidence'/f'{tag}_{phase}_geometry.json'
        run([PYTHON,str(STAGE/'scripts/compare_step_pair_cached.py'),saved_value['cases'][0]['export']['path'],value['cases'][0]['export']['path'],str(comparison)],comparison.with_suffix('.log'))
        result=read(comparison);assert result['status']=='PASS' and result['before_solids']==result['after_solids']==387
        checks.append(comparison)
        if phase=='reopen':checks+=audit(path,tag+'_'+phase)
    assert len({read(p)['process_id'] for p in phases})==3
    for name,digest in source['files'].items():assert sha(Path(source['root'])/name)==digest
    result={'status':'PASS','dimensions_mm':dims,'models':str(root),'top':source['top'],'native_phases':[str(p) for p in phases],'checks':[str(p) for p in checks],'visible_solids':387,'native_documents':122,'production_release':False}
    write(STAGE/'evidence'/f'{tag}_result.json',result)
    return result
if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for key in ['height','width','depth']:parser.add_argument('--'+key,type=float,required=True)
    parser.add_argument('--tag',required=True)
    args=parser.parse_args()
    print(json.dumps(generate(args.height,args.width,args.depth,args.tag),ensure_ascii=False))
