"""Create one isolated request; never modifies the qualified source."""
import argparse
import hashlib
import json
import shutil
import stat
from pathlib import Path
from layout_contract import validate_specification

STAGE = Path(__file__).resolve().parents[1]


def prepare(spec, tag):
    assert tag.isascii() and tag.islower() and tag.replace('_', '').isalnum()
    spec = validate_specification(spec)
    seed = json.loads((STAGE / 'seed_manifest.json').read_text(encoding='utf-8-sig'))
    root = STAGE / ('generated_' + tag)
    root.mkdir(exist_ok=False)
    for name, expected in seed['files'].items():
        source = Path(seed['root']) / name
        assert hashlib.sha256(source.read_bytes()).hexdigest() == expected, name
        shutil.copy2(source, root / name)
        (root / name).chmod(stat.S_IREAD | stat.S_IWRITE)
    (root / 'size_request.json').write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding='utf-8')
    contract = {'scope': '22057_SHADE_PORTAL_R145', 'cases': [{
        'index': 13, 'id': tag, 'dimensions_mm': [spec[key] for key in ('height', 'width', 'depth')],
        'columns': spec['columns'], 'source_root': seed['root'], 'source_top': seed['top'],
        'top': seed['top'], 'package_root': str(root), 'relocated_root': str(root)}]}
    path = STAGE / 'evidence' / (tag + '_contract.json')
    assert not path.exists()
    path.write_text(json.dumps(contract, ensure_ascii=False, indent=2), encoding='utf-8')
    return path


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('specification')
    parser.add_argument('tag')
    args = parser.parse_args()
    print(prepare(json.loads(Path(args.specification).read_text(encoding='utf-8-sig')), args.tag))
