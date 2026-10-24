"""Read-only staging freshness gate. Never builds, executes or creates ZIPs."""
from pathlib import Path
import hashlib,json,sys,zipfile
root=Path(__file__).resolve().parents[1]
version=sys.argv[1] if len(sys.argv)>1 else '0.1.8'
stage=root/'dist'/('SenseBridge-'+version+'-win-x64')
issues=[]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
for name in ('SenseBridge.exe','SenseBridge.Updater.exe'):
    if not (stage/name).exists() or digest(stage/name)!=digest(root/name):issues.append('staged binary differs: '+name)
for source in (stage/'Ac6Runtime/research').glob('*.py'):
    latest=root.parent/'research'/source.name
    if not latest.exists() or digest(source)!=digest(latest):issues.append('stale staged source: '+source.name)
with zipfile.ZipFile(root/'build/runtime-bundle.zip') as bundle:
    for source in (root.parent/'research').glob('*.py'):
        name='Ac6Runtime/research/'+source.name
        if name in bundle.namelist() and hashlib.sha256(bundle.read(name)).hexdigest()!=digest(source):issues.append('stale embedded source: '+source.name)
exe=root/'SenseBridge.exe'
if any(p.stat().st_mtime>exe.stat().st_mtime for p in (root/'source').rglob('*.cs')):issues.append('C# source is newer than executable; rebuild remains held')
print(json.dumps({'ready':not issues,'issues':issues,'scope':'read-only stage/source consistency; no executable test'},ensure_ascii=False,indent=2))
sys.exit(1 if issues else 0)
