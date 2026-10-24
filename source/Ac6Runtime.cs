using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
namespace SenseBridge {
// Local AC6 adapter lifecycle. Imported presets cannot supply these paths.
static class Ac6Runtime {
 static Process audio,trigger;static string session,failed,audioStop,triggerStop;
 static string runtimeRoot;static bool recovering;static int disconnectRetries;static DateTime retryAt;
 static string radioCache;
 static bool ControllerPresent(){try{using(var q=new System.Management.ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_PnPEntity WHERE Status='OK'")){foreach(System.Management.ManagementObject d in q.Get()){using(d){string id=Convert.ToString(d["PNPDeviceID"]);if(id.StartsWith("USB\\VID_054C&PID_0CE6",StringComparison.OrdinalIgnoreCase)||id.StartsWith("USB\\VID_054C&PID_0DF2",StringComparison.OrdinalIgnoreCase))return true;}}}}catch{}return false;}
 static bool Disconnected(){try{return runtimeRoot!=null&&File.ReadAllText(Path.Combine(runtimeRoot,"live-test","engine-output.log")).Contains("ERROR Controller disconnected");}catch{return false;}}
 static void RequestStop(){
  Ac6Support.Stop();
  if(audioStop!=null)File.WriteAllText(audioStop,"stop");
  if(triggerStop!=null)File.WriteAllText(triggerStop,"stop");
 }
 static bool Alive(Process p){try{return p!=null&&!p.HasExited;}catch{return false;}}
 public static bool Running {get{return Alive(audio)&&Alive(trigger);}}
 static Process Launch(string python,string root,string args){
  var info=new ProcessStartInfo(python,args){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true};
  info.EnvironmentVariables["SENSEBRIDGE_APP_ROOT"]=Store.Root;info.EnvironmentVariables["SENSEBRIDGE_AC6_OPTIONS"]=Ac6Support.Config;
  if(root==Path.Combine(Store.Root,"Ac6Runtime")){info.EnvironmentVariables["SENSEBRIDGE_AC6_DATA_ROOT"]=Ac6Data.DirectoryFor(Store.Root);if(radioCache!=null)info.EnvironmentVariables["SENSEBRIDGE_AC6_RADIO_CACHE"]=radioCache;}
  string log=Path.Combine(Store.Root,"profiles",args.Contains("live_haptics.py")?"ac6-audio-error.txt":"ac6-trigger-error.txt");Store.Atomic(log,"");int length=0;
  var child=new Process{StartInfo=info};child.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null||length>=65536)return;length+=e.Data.Length;try{File.AppendAllText(log,e.Data+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}};
  child.Start();child.BeginErrorReadLine();return child;
 }
 static void PrepareRadio(string python,string root,string game){
  string manifest=Path.Combine(Ac6Data.DirectoryFor(Store.Root),"radio-index.json"),hash=UpdatePackage.Hash(manifest);
  radioCache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SenseBridge","AC6Radio",Ac6Data.GameHash,hash);
  Directory.CreateDirectory(radioCache);string ready=Path.Combine(radioCache,"source-index.sha256");
  if(File.Exists(ready)&&File.ReadAllText(ready)==hash&&File.Exists(Path.Combine(radioCache,"radio-candidates.json")))return;
  string args=Q(Path.Combine(root,"research","portable_radio.py"))+" --game "+Q(game)+" --manifest "+Q(manifest)+" --cache "+Q(radioCache)+" --decoder "+Q(Path.Combine(Store.Root,"Runtime","Decoder","vgmstream-cli.exe"))+" --key "+Q(Path.Combine(root,"sd-public.pem"));
  var info=new ProcessStartInfo(python,args){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
  using(var p=new Process{StartInfo=info}){string error="";p.OutputDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null){try{Store.Atomic(Path.Combine(Store.Root,"profiles","ac6-radio-progress.txt"),e.Data);}catch(IOException){}catch(UnauthorizedAccessException){}}};p.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null&&error.Length<8192)error+=e.Data+"\n";};p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();p.WaitForExit();if(p.ExitCode!=0)throw new IOException("AC6 radio preparation failed: "+error);}
  if(!File.Exists(Path.Combine(radioCache,"radio-candidates.json")))throw new IOException("AC6 radio cache missing");Store.Atomic(ready,hash);
 }
 static string Q(string s){if(s.IndexOf('"')>=0)throw new InvalidDataException("Invalid local adapter path");return "\""+s+"\"";}
 public static void Ensure(Process game){
  string key=game.Id+":"+game.StartTime.ToUniversalTime().Ticks;
  if(key==failed)throw new InvalidOperationException("AC6 connection failed; inspect the local runtime log before retrying.");
  if(key==session&&Running)return;
  if(key==session){
   if(!recovering){
    if(!Disconnected()||disconnectRetries>=3){failed=key;RequestStop();throw new InvalidOperationException("AC6 adapter stopped unexpectedly; inspect runtime logs before restarting SenseBridge.");}
    recovering=true;disconnectRetries++;retryAt=DateTime.UtcNow.AddSeconds(5);RequestStop();
   }
   if(DateTime.UtcNow<retryAt||Alive(audio)||Alive(trigger))throw new IOException("Waiting for controller reconnection and output release.");
   if(!ControllerPresent()){retryAt=DateTime.UtcNow.AddSeconds(5);throw new IOException("Waiting for USB DualSense reconnection.");}
   session=null;recovering=false;
  }
  // Never start a second writer while an earlier child is still shutting down.
  if(Alive(audio)||Alive(trigger))throw new InvalidOperationException("Previous AC6 session is still closing.");
  try{
   string local=Path.Combine(Store.Root,"profiles","ac6-local-runtime.json");
   string root,python,traceRoot;
   if(File.Exists(local)&&!File.Exists(Path.Combine(Ac6Data.DirectoryFor(Store.Root),"manifest.json"))){var p=Store.Json.Deserialize<Dictionary<string,string>>(File.ReadAllText(local));root=Path.GetFullPath(p["root"]);python=Path.GetFullPath(p["python"]);traceRoot=Path.GetFullPath(p["traceRoot"]);}
   else{root=Path.Combine(Store.Root,"Ac6Runtime");python=Path.Combine(Store.Root,"Runtime","Python","python.exe");traceRoot=Path.Combine(root,"trace");}
   runtimeRoot=root;radioCache=null;
   if(!File.Exists(python)||!File.Exists(Path.Combine(root,"research","live_haptics.py")))throw new FileNotFoundException("AC6 local runtime is unavailable");
   foreach(string dependency in new[]{"SenseBridge.Events.exe","SDL3.dll"})if(!File.Exists(Path.Combine(Store.Root,dependency)))throw new FileNotFoundException("AC6 application dependency missing: "+dependency);
   string reason=Ac6.Ac6TraceConnection.Inspect(game.Id,true);
   if(reason!=null)throw new InvalidOperationException(reason);
   if(root==Path.Combine(Store.Root,"Ac6Runtime"))PrepareRadio(python,root,Path.GetDirectoryName(game.MainModule.FileName));
   string prefix="ac6-playback-"+game.Id+"-"+game.StartTime.ToUniversalTime().Ticks;
   string trace=Directory.GetFiles(traceRoot,prefix+"*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
   if(trace==null||DateTime.UtcNow-File.GetLastWriteTimeUtc(trace)>TimeSpan.FromSeconds(3)){
    Ac6.Ac6TraceConnection.Start(game.Id,traceRoot,true);
    trace=Directory.GetFiles(traceRoot,prefix+"*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
   }
   if(trace==null)throw new IOException("AC6 trace did not start");
   string stop=Path.Combine(root,"live-test","live.ini.stop");
   if(File.Exists(stop))File.Move(stop,stop+"."+DateTime.UtcNow.Ticks);
   string log=Path.Combine(root,"research","managed-trigger-"+DateTime.UtcNow.Ticks+".jsonl");
   audioStop=stop;triggerStop=log+".stop";
   string parent=Process.GetCurrentProcess().Id.ToString();
   string radio=radioCache!=null||File.Exists(Path.Combine(root,"research","radio-candidates.json"))?" --radio-trial":"";
   audio=Launch(python,root,Q(Path.Combine(root,"research","live_haptics.py"))+" --pid "+game.Id+" --trace "+Q(trace)+radio+" --parent-pid "+parent);
   trigger=Launch(python,root,Q(Path.Combine(root,"research","readonly_fire_trial.py"))+" "+game.Id+" 0 "+Q(log)+" "+parent);
   if(audio.WaitForExit(750)||trigger.WaitForExit(750))throw new IOException("AC6 output process exited during startup; inspect profiles/ac6-audio-error.txt and ac6-trigger-error.txt");
   session=key;
   Store.Atomic(Path.Combine(Store.Root,"profiles","ac6-runtime-status.json"),Store.Json.Serialize(new {Session=key,AudioPid=audio.Id,TriggerPid=trigger.Id,Trace=trace,State="started"}));
  }catch(Exception ex){failed=key;RequestStop();Store.Atomic(Path.Combine(Store.Root,"profiles","ac6-runtime-error.txt"),ex.ToString());throw;}
 }
}
}
