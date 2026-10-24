"""Prepare a local-only portable trial. Does not publish or touch user settings."""
from pathlib import Path
import argparse, hashlib, json, shutil, sys, zipfile

def main():
    ap=argparse.ArgumentParser();ap.add_argument('application');ap.add_argument('python_home');ap.add_argument('--version',default='0.1.8');a=ap.parse_args()
    repo=Path(__file__).resolve().parents[1]; work=repo.parent; src=Path(a.application)
    stage=repo/'dist'/('SenseBridge-'+a.version+'-win-x64')
    if stage.exists():raise RuntimeError('Staging already exists; inspect before reusing')
    stage.mkdir(parents=True)
    names=['SenseBridge.Engine.exe','SenseBridge.Events.exe','SenseBridge.Bloodborne.dll','SDL3.dll','libc++.dll','libiconv-2.dll','libwinpthread-1.dll','attach-profile.json','START-HERE.txt','THIRD-PARTY-NOTICES.txt']
    for n in names:shutil.copy2(src/n,stage/n)
    for n in ['SenseBridge.exe','SenseBridge.Updater.exe','ui.en.json','ui.ja.json','README.md','PRESET-GUIDE.ko.html']:shutil.copy2(repo/n,stage/n)
    for n in ['assets','licenses','UnityRuntime']:
        shutil.copytree(src/n,stage/n)
    # Explicit Bloodborne runtime files; no debug launcher, settings, or personal effects.
    if (src/'Bloodborne/runtime').exists():
        for p in (src/'Bloodborne/runtime').rglob('*'):
            if p.is_file() and p.suffix in ('.dll','.bin','.json','.wav'):
                dest=stage/p.relative_to(src);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
    runtime=stage/'Ac6Runtime'; scripts=runtime/'research';scripts.mkdir(parents=True)
    audit=json.loads((work/'research/runtime-dependency-audit.json').read_text())
    for n in sorted(set(audit['modules'])|{'runtime_paths','portable_radio'}):shutil.copy2(work/'research'/(n+'.py'),scripts/(n+'.py'))
    (runtime/'trace').mkdir();shutil.copy2(work/'fire-commit-test/SenseBridge.Ac6Trace.dll',runtime/'trace/SenseBridge.Ac6Trace.dll')
    shutil.copy2(Path('G:/SenseBridge-EldenRing/tools/nuxe-release/Nuxe 1.2.1/res/BinderKeys/ArmoredCore6_PC/Key/sd.pem'),runtime/'sd-public.pem')
    decoder=stage/'Runtime/Decoder';decoder.mkdir(parents=True)
    for p in Path('G:/SenseBridge/tools/vgmstream').iterdir():
        if p.is_file() and (p.suffix=='.dll' or p.name in ('vgmstream-cli.exe','COPYING','README.md')):shutil.copy2(p,decoder/p.name)
    py=stage/'Runtime/Python';py.mkdir(parents=True);home=Path(a.python_home)
    for n in ['python.exe','python313.dll','python3.dll','vcruntime140.dll','vcruntime140_1.dll','LICENSE.txt']:
        shutil.copy2(home/n,py/n)
    shutil.copytree(home/'DLLs',py/'DLLs',ignore=shutil.ignore_patterns('*.ico','*.pdb','*.cat','tcl*','tk*','_tkinter*'))
    with zipfile.ZipFile(py/'python313.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in (home/'Lib').rglob('*.py'):
            rel=p.relative_to(home/'Lib')
            if any(v in ('site-packages','test','tests','idlelib','tkinter','ensurepip','__pycache__') for v in rel.parts):continue
            z.write(p,rel.as_posix())
    (py/'python313._pth').write_text('python313.zip\nDLLs\n../../Ac6Runtime/research\n',encoding='utf-8')
    bundle=repo/'build/runtime-bundle.zip';bundle.parent.mkdir(exist_ok=True);files=[]
    with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED) as z:
        for folder in (runtime,stage/'Runtime'):
            for p in sorted(folder.rglob('*')):
                if p.is_file():
                    rel=p.relative_to(stage).as_posix();z.write(p,rel);files.append({'Path':rel,'Size':p.stat().st_size,'Sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
        z.writestr('manifest.json',json.dumps(files))
    print(stage)
if __name__=='__main__':main()
