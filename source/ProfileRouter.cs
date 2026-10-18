using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Text.RegularExpressions;
namespace SenseBridge {
public sealed class GameSession:IDisposable {public GameProfile Profile;public Process Process;public string Key;public DateTime Started;public int Pid;public void Dispose(){if(Process!=null)Process.Dispose();}}
public static class ProfileRouter {
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 public static int ForegroundPid(){uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);return (int)pid;}
 public static GameSession Choose(List<GameSession> sessions,int foreground,string current){return sessions.FirstOrDefault(s=>s.Pid==foreground);}
 public static GameProfile Match(List<GameProfile> profiles,string executable){
  if(string.IsNullOrEmpty(executable))return null;
  string path=Path.GetFullPath(executable),name=Path.GetFileNameWithoutExtension(path);
  foreach(var profile in profiles){
   if(!string.IsNullOrEmpty(profile.EngineExecutable)&&string.Equals(Path.GetFullPath(profile.EngineExecutable),path,StringComparison.OrdinalIgnoreCase))return profile;
   if(Store.Supported(profile)&&!string.IsNullOrEmpty(profile.EngineExecutable)&&name.StartsWith("bb-probe",StringComparison.OrdinalIgnoreCase)&&path.StartsWith(Store.AdapterRoot+"\\",StringComparison.OrdinalIgnoreCase))return profile;
  }
  if(Regex.IsMatch(name,"crash|report|unins|setup|helper|launcher|anticheat",RegexOptions.IgnoreCase))return null;
  return profiles.FirstOrDefault(p=>string.IsNullOrEmpty(p.EngineExecutable)&&!string.IsNullOrEmpty(p.SteamAppId)&&SteamLibrary.Contains(p,path));
 }
 public static GameSession Find(List<GameProfile> profiles,string current){return FindForeground(profiles,ForegroundPid());}
 public static GameSession FindForeground(List<GameProfile> profiles,int pid){
  if(pid<=0)return null;Process process=null;
  try{process=Process.GetProcessById(pid);var profile=Match(profiles,process.MainModule.FileName);if(profile==null)return null;
   DateTime start=process.StartTime.ToUniversalTime();var result=new GameSession{Profile=profile,Process=process,Pid=pid,Started=start,Key=profile.Id+":"+pid+":"+start.Ticks};process=null;return result;
  }catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}catch(System.ComponentModel.Win32Exception){return null;}finally{if(process!=null)process.Dispose();}
 }
}
}
