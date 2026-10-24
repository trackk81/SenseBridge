"""Validate a completed --prepare-runtime trial, without launching the app."""
from pathlib import Path
import hashlib,json,zipfile
root=Path(__file__).resolve().parents[1]
target=Path((root.parent/'research/bootstrap-validation-path.txt').read_text(encoding='utf-8-sig'))
assert target.parent==root/'build' and target.name.startswith('portable validation ')
with zipfile.ZipFile(root/'build/runtime-bundle.zip') as z:
    manifest=json.loads(z.read('manifest.json'))
for f in manifest:
    p=target/f['Path']
    assert p.stat().st_size==f['Size'] and hashlib.sha256(p.read_bytes()).hexdigest()==f['Sha256'],f['Path']
for path,text in [('profiles/games.json','preserve-settings'),('packs/keep/data.txt','preserve-pack'),('Bloodborne/runtime/dualsense.ini','preserve-bloodborne')]:
    assert (target/path).read_text()==text,path
print('PASS:',len(manifest),'runtime files; Unicode/spaces path; settings, packs and Bloodborne configuration preserved')
