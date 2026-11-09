"""Task entry point: publish only audited and independently reopened models."""
import argparse
import hashlib
import json
import re
import zipfile
from pathlib import Path
from generate_model import STAGE, read, sha, write
from generate_layout_model import generate
from layout_contract import validate_specification

parser=argparse.ArgumentParser()
parser.add_argument('--task-file',required=True)
parser.add_argument('--release-dir',required=True)
args=parser.parse_args()
task=read(args.task_file)
config=task['generator']
assert re.fullmatch(r'OUT22057-[a-f0-9-]{36}',task['id'])
assert Path(config['source_stage']).resolve()==STAGE
assert sha(__file__)==config['script_sha256']
assert sha(STAGE/'qualification.json')==config['qualification_sha256']
qualification=read(STAGE/'qualification.json')
assert qualification['status']=='PASS' and task['state']=='running' and config['enabled']
spec=task['specification']
assert validate_specification(spec)==spec
for key in ['height','width','depth']:
    lo,hi=qualification['input_range_mm'][key]
    assert config['ranges_mm'][key]==[lo,hi] and lo<=spec[key]<=hi
for relative,digest in qualification['support_sha256'].items():
    p=Path(relative)
    assert not p.is_absolute() and '..' not in p.parts and sha(STAGE/p)==digest,'Generator identity changed: '+relative
for absolute,digest in qualification['external_support_sha256'].items():
    assert Path(absolute).is_absolute() and sha(absolute)==digest,'Verification dependency changed: '+absolute
release=Path(args.release_dir).resolve()
work=release/'generated'/task['id']
work.mkdir(parents=True,exist_ok=False)
tag='portal_'+task['id'][9:].replace('-','')
result=generate(spec,tag)
root=Path(result['models'])
source=read(STAGE/'seed_manifest.json')
assets={f'models/{p.name}':p for p in root.iterdir() if p.is_file() and p.suffix.lower() in ['.sldprt','.sldasm','.step']}
assert sum(Path(n).suffix.lower() in ['.sldprt','.sldasm'] for n in assets)==result['native_documents']
assets['verification/size_request.json']=root/'size_request.json'
assets['verification/generated_layout_manifest.json']=root/'generated_layout_manifest.json'
native_paths=[Path(p) for p in result['native_phases']]
for p in native_paths+[Path(p) for p in result['checks']]:assets['verification/'+p.name]=p
step=Path(read(native_paths[-1])['cases'][0]['export']['path'])
assets['geometry/whole_assembly.step']=step
assets['verification/qualification.json']=STAGE/'qualification.json'
manifest={n:{'sha256':sha(p),'bytes':p.stat().st_size} for n,p in assets.items()}
archive=work.with_suffix('.zip')
assert not archive.exists()
narrow_lamp_note=''
if spec['width'] < 900:
    led_count=max(2,int((spec['width']-470-53.5)//75)+1)
    led_count-=led_count%2
    led_length=75*(led_count-1)+30
    narrow_lamp_note=f'本规格采用两根{led_length:g} mm适配短灯条，每根{led_count}颗灯珠，灯珠间距75 mm；16 mm截面、1.8 mm底板及灯珠尺寸保持原尺寸。灯条文件名中的930保留作标准引用标识，实际长度以此说明和模型为准。塑料罩与压条随柜宽配套。\n窄柜的锁控板安装柱与后加强筋定位孔按柜宽联动；锁控板及支架保持原尺寸，顶板安装柱保持4 mm直径、10 mm长度及每组160 mm孔距。\n'
readme=f'''22057 户外柜工程辅助模型

本次规格：{spec['height']}高 × {spec['width']}宽 × {spec['depth']}深 mm；左{len(spec['columns'][0]['doors'])}门、右{len(spec['columns'][1]['doors'])}门，共{spec['doors']}门。
每列门序从下向上，成品门高及6 mm门缝见 verification/size_request.json。
解压全部文件，使用SOLIDWORKS 2020打开 models/{source['top']}。
文件名沿用标准模型名称，实际尺寸以本任务和模型内容为准。
左右立柱保持原标准几何，短压条只移动位置；遮阳板、连接架、长压板、灯罩壳随柜宽联动。
{narrow_lamp_note}
已完成{result['native_documents']}份原生文档健康与包内引用、{result['visible_solids']}个实体、门高与锁孔配对、层板及门轴接口、雨棚直接安装、盖板及灯罩间隙检查，并在三个独立原生进程中保存和重开比较。
用于结构工程师继续修改基础模型。
'''
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for name,path in assets.items():z.write(path,name)
    z.writestr('00_使用说明.txt',readme)
    z.writestr('manifest.json',json.dumps(manifest,ensure_ascii=False,indent=2))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    for name,identity in manifest.items():
        data=z.read(name)
        assert len(data)==identity['bytes'] and hashlib.sha256(data).hexdigest()==identity['sha256']
receipt={'status':'PASS','taskId':task['id'],'specification':spec,'archive':str(archive.relative_to(release)),'bytes':archive.stat().st_size,'sha256':sha(archive),'native_result':str(STAGE/'evidence'/f'{tag}_result.json'),'production_release':False}
write(work/'result.json',receipt)
print(json.dumps(receipt,ensure_ascii=False))
