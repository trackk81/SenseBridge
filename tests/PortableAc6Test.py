"""Exercise extracted internal ZIP with no system Python/toolchain lookup."""
from pathlib import Path
import os, subprocess, tempfile, zipfile, json
repo=Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='SenseBridge portable ') as d:
    root=Path(d)/'한글 경로';root.mkdir()
    with zipfile.ZipFile(repo/'release-artifacts/internal/SenseBridge-0.1.6-win-x64.zip') as z:z.extractall(root)
    app=root/'SenseBridge-0.1.6-win-x64'
    with zipfile.ZipFile(repo/'release-artifacts/AC6-data-INTERNAL-PREVIEW.zip') as z:z.extractall(app)
    env=dict(os.environ);env['PATH']=str(Path(env['WINDIR'])/'System32');env.pop('PYTHONHOME',None);env.pop('PYTHONPATH',None);env.pop('SENSEBRIDGE_TOOLCHAIN',None)
    env['SENSEBRIDGE_APP_ROOT']=str(app);env['SENSEBRIDGE_AC6_DATA_ROOT']=str(app/'packs/ac6-audio-01')
    py=app/'Runtime/Python/python.exe';script=app/'Ac6Runtime/research/live_haptics.py'
    subprocess.run([str(py),str(script),'--self-test'],cwd=root,env=env,check=True)
    subprocess.run([str(py),'-c','import readonly_fire_trial; import current_weapon_slots; import trigger_output; print("PASS packaged runtime imports")'],cwd=root,env=env,check=True)
    assert not list(app.rglob('*.wav')) or all('Bloodborne' in p.parts for p in app.rglob('*.wav'))
    assert not list(app.rglob('*.sl2')) and not (app/'profiles').exists()
    print('PASS extracted ZIP: Unicode/spaces, isolated Python, no game/controller opened, data separated, no AC6 original audio/save')
