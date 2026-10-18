using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace SenseBridge {
public static class SteamLibrary {
 static string Value(string text,string key){var m=Regex.Match(text,"\""+Regex.Escape(key)+"\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"",RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value.Replace("\\\\","\\").Replace("\\\"","\""):null;}
 public static List<GameProfile> Discover(string steam){
  var result=new List<GameProfile>();if(string.IsNullOrEmpty(steam)||!Directory.Exists(steam))return result;
  var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase){steam};string libraries=Path.Combine(steam,@"steamapps\libraryfolders.vdf");
  if(File.Exists(libraries))try{string text=File.ReadAllText(libraries);foreach(Match m in Regex.Matches(text,"\"path\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"",RegexOptions.IgnoreCase))paths.Add(m.Groups[1].Value.Replace("\\\\","\\"));}catch(IOException){}catch(UnauthorizedAccessException){}
  var ids=new HashSet<string>();
  foreach(string library in paths){string apps=Path.Combine(library,"steamapps");string[] files;try{files=Directory.GetFiles(apps,"appmanifest_*.acf");}catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}
   foreach(string file in files)try{
    string text=File.ReadAllText(file),id=Value(text,"appid"),name=Value(text,"name"),folder=Value(text,"installdir");int number,flags;
    if(!int.TryParse(id,out number)||string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(folder)||!int.TryParse(Value(text,"StateFlags"),out flags)||(flags&4)==0||id=="228980")continue;
    string common=Path.GetFullPath(Path.Combine(apps,"common"))+Path.DirectorySeparatorChar,dir=Path.GetFullPath(Path.Combine(common,folder));
    if(!dir.StartsWith(common,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(dir)||!ids.Add(id))continue;
    result.Add(new GameProfile{Id="steam-"+id,Name=name,Adapter="global-audio",SteamAppId=id,InstallDirectory=dir});
   }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
  }return result.OrderBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
 }
 public static void Import(List<GameProfile> games){
  try{string root=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) as string;
   foreach(var found in Discover(root)){
    var old=games.FirstOrDefault(p=>p.SteamAppId==found.SteamAppId||p.Id==found.Id||!string.IsNullOrEmpty(p.EngineExecutable)&&Contains(found,p.EngineExecutable));
    if(old==null)games.Add(found);else if(!Store.Supported(old)){old.SteamAppId=found.SteamAppId;old.InstallDirectory=found.InstallDirectory;}
   }
  }catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
 }
 public static bool Contains(GameProfile p,string exe){return !string.IsNullOrEmpty(p.InstallDirectory)&&Path.GetFullPath(exe).StartsWith(Path.GetFullPath(p.InstallDirectory).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
 public static bool Ready(GameProfile p){return p!=null&&!string.IsNullOrEmpty(p.SteamAppId)&&!string.IsNullOrEmpty(p.InstallDirectory)&&Directory.Exists(p.InstallDirectory);}
}
}
