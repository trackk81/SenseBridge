using System;using System.IO;using SenseBridge;
class PackDownloadTest {
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static void Main(string[] args){
  Check(PackDownloads.Available(new GameProfile{Adapter="global-audio",SteamAppId="1030300"}),"Steam catalog detection");
  Check(PackDownloads.Available(new GameProfile{Adapter="global-audio",EngineExecutable=@"D:\games\Hollow Knight Silksong.exe"}),"Manual executable detection");
  Check(!PackDownloads.Available(new GameProfile{Adapter="unity-audio-v1",SteamAppId="1030300"}),"Installed pack must show settings");
  Check(!PackDownloads.Available(new GameProfile{Adapter="global-audio",SteamAppId="10303000"}),"Unrelated game matched");
  var p=PackDownloads.Verify(args[0],AppDomain.CurrentDomain.BaseDirectory);
  Check(!p.Triggers&&!p.Haptics&&!p.Speaker,"Default effects must be off");
  Check(p.Unity.StateRules.Count==100&&p.Unity.Rules.Count==220,"Catalog content");
  string temp=Path.GetTempFileName();try{File.WriteAllText(temp,"{}");bool rejected=false;try{PackDownloads.Verify(temp,AppDomain.CurrentDomain.BaseDirectory);}catch(InvalidDataException){rejected=true;}Check(rejected,"Tampered download accepted");}finally{File.Delete(temp);}
  Check(Store.BuiltInGames().Count==1&&Store.BuiltInGames()[0].Name=="Bloodborne","Silksong cannot become built-in");
  if(args.Length>1){var remote=PackDownloads.Download(AppDomain.CurrentDomain.BaseDirectory).GetAwaiter().GetResult();Check(remote.Unity.StateRules.Count==100,"Public download");}
  Console.WriteLine("PASS: catalog detection, installed/uninstalled routing, verified pack, tamper rejection, defaults off, Bloodborne-only built-in");
 }
}
