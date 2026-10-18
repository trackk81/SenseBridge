"""Run from a Windows x64 Python process; no controller or game is opened."""
from pathlib import Path
import ctypes,os,sys,subprocess
root=Path(sys.argv[1]).resolve()
os.environ.pop('SENSEBRIDGE_TOOLCHAIN',None)
os.environ['PATH']=str(Path(os.environ['WINDIR'])/'System32')+os.pathsep+os.environ['WINDIR']
with os.add_dll_directory(str(root)):
    dll=ctypes.WinDLL(str(root/'SenseBridge.Bloodborne.dll'))
    assert dll.SenseBridgeStart
subprocess.run([str(root/'SenseBridge.Engine.exe'),'--self-test'],cwd=root,check=True)
assert not (root/'profiles').exists()
assert not (root/'Bloodborne/tools').exists()
assert not list(root.rglob('*.sav'))
print('PASS: packaged adapter dependencies/export, portable engine, no profiles/debug launcher/saves')
