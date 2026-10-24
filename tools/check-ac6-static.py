"""Read-only artifact checks. Does not import game modules or execute binaries."""
import ast, base64, hashlib, json, struct, zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
pack = json.loads((root/'release-artifacts/ArmoredCoreVI-1.0.sensebridge.json').read_text(encoding='utf-8'))
assert not any(pack[k] for k in ('Triggers', 'Haptics', 'Speaker'))
assets = {}
for a in pack['Assets']:
    data = base64.b64decode(a['Data'], validate=True)
    assert hashlib.sha256(data).hexdigest() == a['Sha256']
    assert a['Name'] not in assets
    assets[a['Name']] = data
assert set(assets) == {'events.bin', 'event-map.json', 'radio-index.json'}
mapping = json.loads(assets['event-map.json'])
bank = assets['events.bin']
assert bank[:8] == b'SBEVENT1'
count, = struct.unpack_from('<I', bank, 8)
assert count == len(mapping['labels'])
offset = 12
for _ in range(count):
    frames, speaker, trigger = struct.unpack_from('<III', bank, offset)
    offset += 12
    assert 1 <= frames <= 26400 and speaker == trigger == 0
    samples = struct.unpack_from('<' + 'h'*frames, bank, offset)
    assert all(-4588 <= x <= 4588 for x in samples)
    offset += frames*2
assert offset == len(bank)
assert all(0 < int(k) <= 0xffffffff and 0 <= v < count for k,v in mapping['mapping'].items())
radio = json.loads(assets['radio-index.json'])
for rule in radio['rules']:
    assert set(rule) == {'event_id','target','language','media_id','source_sha256'}
    assert rule['language'] in ('ja','enus')
    assert len(bytes.fromhex(rule['source_sha256'])) == 32
    assert all(0 < rule[k] <= 0xffffffff for k in ('event_id','target','media_id'))
with zipfile.ZipFile(root/'build/runtime-bundle.zip') as bundle:
    manifest = json.loads(bundle.read('manifest.json'))
    assert len(bundle.namelist()) == len(set(bundle.namelist())) == len(manifest)+1
    for f in manifest:
        assert f['Path'].startswith(('Runtime/','Ac6Runtime/'))
        assert not any(p in ('..','.') for p in f['Path'].split('/'))
        data = bundle.read(f['Path'])
        assert len(data) == f['Size'] and hashlib.sha256(data).hexdigest() == f['Sha256']
sources = list((root/'tools').glob('*.py')) + list((root.parent/'research').glob('*.py'))
for path in sources:
    ast.parse(path.read_text(encoding='utf-8-sig'), filename=str(path))
print(json.dumps({'haptic_clips':count,'radio_mappings':len(radio['rules']),
                  'runtime_files':len(manifest),'python_syntax_files':len(sources),
                  'scope':'data hashes, bounds, archive inventory, Python AST only; no C# or application execution'}))
