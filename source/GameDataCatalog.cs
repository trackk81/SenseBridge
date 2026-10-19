using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Net;using System.Threading;using System.Threading.Tasks;
namespace SenseBridge {
public sealed class GameDataEntry {public string Title{get;set;}public string SteamAppId{get;set;}public string Executable{get;set;}public string PackId{get;set;}public string Adapter{get;set;}public string Version{get;set;}public string MinimumAppVersion{get;set;}public string Url{get;set;}public string Sha256{get;set;}public long Size{get;set;}}
public sealed class GameDataIndex {public int Format{get;set;}public List<GameDataEntry> Games{get;set;}}
public static class GameDataCatalog {
 public const string LibraryUrl="https://github.com/trackk81/SenseBridge/releases/tag/game-data";
 public const string IndexUrl="https://raw.githubusercontent.com/trackk81/SenseBridge/main/catalog/index.json";
 static List<GameDataEntry> entries=new List<GameDataEntry>{new GameDataEntry{Title="Hollow Knight: Silksong",SteamAppId="1030300",Executable="Hollow Knight Silksong.exe",PackId="silksong-audio-01",Adapter="unity-audio-v1",Version="1.0",MinimumAppVersion="0.1.5",Url="https://github.com/trackk81/SenseBridge/releases/download/game-data/Silksong-1.0.sensebridge.json",Sha256=PackDownloads.Sha256,Size=11753673}};
 static bool loaded;
 public static void Validate(GameDataIndex index){
  if(index==null||index.Format!=1||index.Games==null||index.Games.Count>1000)throw new InvalidDataException("Invalid game data catalog");
  var ids=new HashSet<string>();foreach(var g in index.Games){Version version,min;
   if(g==null||string.IsNullOrWhiteSpace(g.Title)||g.Title.Length>120||g.SteamAppId==null||!g.SteamAppId.All(char.IsDigit)||g.SteamAppId.Length<1||g.SteamAppId.Length>12||!ids.Add(g.SteamAppId)||!System.Version.TryParse(g.Version,out version)||!System.Version.TryParse(g.MinimumAppVersion,out min)||g.Size<1||g.Size>100*1024*1024||g.Sha256==null||g.Sha256.Length!=64||!g.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid catalog entry");
   EventPacks.DirectoryFor(".",g.PackId);
   string prefix="https://github.com/trackk81/SenseBridge/releases/download/game-data/";
   if(g.Url==null||!g.Url.StartsWith(prefix,StringComparison.Ordinal)||g.Url.Substring(prefix.Length).Any(c=>!(char.IsLetterOrDigit(c)||c=='-'||c=='_'||c=='.'))||!g.Url.EndsWith(".sensebridge.json",StringComparison.Ordinal)||g.Executable==null||Path.GetFileName(g.Executable)!=g.Executable||g.Executable.IndexOfAny(Path.GetInvalidFileNameChars())>=0||!g.Executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid catalog location");
  }
 }
 static void Load(){if(loaded)return;loaded=true;try{var index=Store.Json.Deserialize<GameDataIndex>(File.ReadAllText(Path.Combine(Store.Root,"profiles","catalog-cache.json")));Validate(index);entries=index.Games;}catch{}}
 public static GameDataEntry Find(GameProfile g){Load();if(g==null)return null;return entries.FirstOrDefault(e=>(g.SteamAppId==e.SteamAppId||string.Equals(Path.GetFileName(g.EngineExecutable),e.Executable,StringComparison.OrdinalIgnoreCase))&&new Version(e.MinimumAppVersion)<=AppUpdates.Current);}
 public static async Task Refresh(){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var req=(HttpWebRequest)WebRequest.Create(IndexUrl);req.UserAgent="SenseBridge-catalog";
  using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30)))using(timeout.Token.Register(()=>req.Abort()))using(var response=(HttpWebResponse)await req.GetResponseAsync()){
   if(response.ResponseUri.Scheme!="https"||response.ContentLength>1024*1024)throw new InvalidDataException("Catalog response too large");
   using(var input=response.GetResponseStream())using(var memory=new MemoryStream()){byte[] buf=new byte[16384];int n;while((n=await input.ReadAsync(buf,0,buf.Length,timeout.Token))>0){if(memory.Length+n>1024*1024)throw new InvalidDataException("Catalog response too large");memory.Write(buf,0,n);}string json=System.Text.Encoding.UTF8.GetString(memory.ToArray());var index=Store.Json.Deserialize<GameDataIndex>(json);Validate(index);Store.Atomic(Path.Combine(Store.Root,"profiles","catalog-cache.json"),json);entries=index.Games;loaded=true;}
  }
 }
}
}
