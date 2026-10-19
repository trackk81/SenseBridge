"""Isolated real-process updater smoke test. Never touches a user's installation."""
from pathlib import Path
import tempfile,subprocess,json,hashlib,time,os
root=Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='SenseBridge-process-test-') as temp:
 base=Path(temp);install=base/'install';work=base/'work';payload=work/'payload'
 install.mkdir();payload.mkdir(parents=True)
 csc=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
 for folder,value in ((install,'old'),(payload,'new')):
  source=base/(value+'.cs')
  source.write_text('using System;using System.IO;using System.Threading;class App{static void Main(string[] a){if(a.Length>0){Thread.Sleep(4000);return;}File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"started.txt"),"'+value+'");}}')
  subprocess.run([str(csc),'/nologo','/target:winexe','/out:'+str(folder/'SenseBridge.exe'),str(source)],check=True)
 import shutil
 for folder in (install,payload,work):shutil.copy2(root/'SenseBridge.Updater.exe',folder/'SenseBridge.Updater.exe')
 (install/'profiles').mkdir();(install/'profiles/games.json').write_text('preserve-me')
 parent=subprocess.Popen([str(install/'SenseBridge.exe'),'--wait'])
 # .NET DateTime ticks, exact same property used by the application.
 ps=f"(Get-Process -Id {parent.pid}).StartTime.ToUniversalTime().Ticks"
 ticks=int(subprocess.check_output(['powershell.exe','-NoProfile','-Command',ps],text=True).strip())
 files=[{'Path':p.name,'Size':p.stat().st_size,'Sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in payload.iterdir()]
 manifest={'Format':1,'Version':'0.1.6','MinimumVersion':'0.1.6','PackageUrl':'https://github.com/trackk81/SenseBridge/releases/download/v0.1.6/SenseBridge-0.1.6-update-win-x64.zip','Size':1,'Sha256':'0'*64,'Files':files}
 plan={'InstallRoot':str(install),'PayloadRoot':str(payload),'ParentPid':parent.pid,'ParentStartTicks':ticks,'Manifest':manifest}
 (work/'plan.json').write_text(json.dumps(plan))
 helper=subprocess.Popen([str(work/'SenseBridge.Updater.exe'),str(work/'plan.json')])
 assert helper.wait(timeout=25)==0
 parent.wait(timeout=5)
 for _ in range(50):
  if (install/'started.txt').exists():break
  time.sleep(.1)
 assert (install/'started.txt').read_text()=='new'
 assert (install/'profiles/games.json').read_text()=='preserve-me'
 assert (work/'backup/SenseBridge.exe').is_file()
 print('PASS: updater waits for original process, replaces binaries, preserves profile, saves backup and restarts new app')
