"""Build full ZIP, program-only update ZIP and update manifest from a clean staging folder.
Usage: python tools/package-release.py 0.1.6
The update ZIP intentionally excludes mutable settings and downloaded packs.
"""
from pathlib import Path
import hashlib,json,re,sys,zipfile,subprocess
version=sys.argv[1]
assert re.fullmatch(r'\d+\.\d+\.\d+',version)
root=Path(__file__).resolve().parents[1]
internal='--internal' in sys.argv[2:]
stage=root/'dist'/f'SenseBridge-{version}-win-x64';out=root/'release-artifacts';out=out/'internal' if internal else out;out.mkdir(parents=True,exist_ok=True)
assert stage.is_dir()
assert (stage/'SenseBridge.Updater.exe').is_file()
if (stage/'Ac6Runtime').is_dir():
 subprocess.run([sys.executable,str(root/'tools/check-ac6-stage.py'),version],check=True)
files=[]
for f in sorted(stage.rglob('*')):
 if not f.is_file() or f.name=='self-test.txt':continue
 rel=f.relative_to(stage).as_posix()
 if rel.startswith('Ac6Runtime/live-test/') or '/__pycache__/' in rel or f.suffix=='.cat':continue
 assert rel.split('/')[0] not in ('profiles','packs','tools'),rel
 assert f.suffix.lower() not in ('.sav','.log','.status','.ps1','.fsb'),rel
 assert f.name not in ('Assembly-CSharp.dll','bridge-link.ini','session.ini','games.json','plan.json'),rel
 files.append((f,rel))
full=out/f'SenseBridge-{version}-win-x64.zip'
update=out/f'SenseBridge-{version}-update-win-x64.zip'
# The 0.1.6 updater cannot accept new runtime directories. These dependencies
# are already delivered in the application's verified embedded runtime bundle.
immutable=[(f,p) for f,p in files if p!='Bloodborne/runtime/dualsense.ini' and not p.startswith(('Runtime/','Ac6Runtime/'))]
def archive(path,items,prefix=''):
 with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,9) as z:
  for f,p in items:z.write(f,prefix+p)
 with zipfile.ZipFile(path) as z:assert z.testzip() is None
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
archive(full,files,stage.name+'/');archive(update,immutable)
manifest={'Format':1,'Version':version,'MinimumVersion':'0.1.6',
 'PackageUrl':f'https://github.com/trackk81/SenseBridge/releases/download/v{version}/{update.name}',
 'Sha256':sha(update),'Size':update.stat().st_size,
 'Files':[{'Path':p,'Sha256':sha(f),'Size':f.stat().st_size} for f,p in immutable]}
manifest_file=out/'update-manifest.json';manifest_file.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
assets=[full,update,manifest_file]
if not internal:assets.append(out/'mingw-w64-libiconv-1.19-1.src.tar.zst')
if internal:(out/'NOT-FOR-RELEASE.txt').write_text(f'Internal preparation only: {version}. No publication. Security review, clean-machine execution and previous-updater migration must pass before release. Full weapon gameplay coverage is not complete.\n')
(out/f'SHA256SUMS-{version}.txt').write_text(''.join(sha(p)+'  '+p.name+'\n' for p in assets),encoding='utf-8')
print(f'Full ZIP: {full.stat().st_size} bytes; update ZIP: {update.stat().st_size} bytes; {len(immutable)} immutable files')
print('Validate update-manifest.json and the update ZIP with UpdatePackage before publishing.')
