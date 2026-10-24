"""Refresh an existing private staging runtime from reviewed local sources."""
from pathlib import Path
import hashlib,json,shutil,zipfile
root=Path(__file__).resolve().parents[1]
stage=root/'dist/SenseBridge-0.1.8-win-x64'
assert stage.is_dir()
for p in (stage/'Ac6Runtime/research').glob('*.py'):
    source=root.parent/'research'/p.name
    assert source.is_file(), source
    shutil.copy2(source,p)
bundle=root/'build/runtime-bundle.zip'
pending=bundle.with_suffix('.zip.pending')
files=[]
with zipfile.ZipFile(pending,'w',zipfile.ZIP_DEFLATED) as z:
    for folder in (stage/'Ac6Runtime',stage/'Runtime'):
        for p in sorted(folder.rglob('*')):
            if not p.is_file() or '__pycache__' in p.parts or 'live-test' in p.parts:continue
            rel=p.relative_to(stage).as_posix()
            z.write(p,rel)
            files.append({'Path':rel,'Size':p.stat().st_size,'Sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    z.writestr('manifest.json',json.dumps(files))
pending.replace(bundle)
print('Refreshed runtime bundle:',len(files),'files; no application execution')
