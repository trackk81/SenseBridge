using System;using System.IO;using System.Net;using System.Threading.Tasks;
namespace SenseBridge {
// Catalog metadata only. Game resources remain separate release assets.
public static class PackDownloads {
 public const string Url="https://github.com/trackk81/SenseBridge/releases/download/game-data/Silksong-1.0.sensebridge.json";
 public const string Sha256="779ddf6cb2e01bdb03508fa4bdc10cd04a5499c52b4795f90037553bb2528b56";
 public static bool Supported(GameProfile g){return g!=null&&(Store.Supported(g)||GameDataCatalog.Listed(g));}
 public static bool Available(GameProfile g){return g!=null&&!EventPacks.Dedicated(g)&&GameDataCatalog.Find(g)!=null;}
 public static GamePreset Verify(string path,string root){return Verify(path,root,GameDataCatalog.Find(new GameProfile{SteamAppId="1030300"}));}
 public static GamePreset Verify(string path,string root,GameDataEntry entry){
  if(entry==null||new FileInfo(path).Length!=entry.Size||Presets.Hash(File.ReadAllBytes(path))!=entry.Sha256)throw new InvalidDataException("Data pack checksum mismatch. Please download again.");
  var p=Presets.Read(path,root);if(p.Adapter!=entry.Adapter||p.SteamAppId!=entry.SteamAppId||(p.Ac6!=null?p.Ac6.PackId:p.Unity!=null?p.Unity.PackId:null)!=entry.PackId)throw new InvalidDataException("Unexpected data pack identity");
  p.Triggers=p.Haptics=p.Speaker=false;return p;
 }
 public static Task<GamePreset> Download(string root){return Download(root,new GameProfile{SteamAppId="1030300"});}
 public static async Task<GamePreset> Download(string root,GameProfile game){
  var entry=GameDataCatalog.Find(game);if(entry==null)throw new InvalidDataException("No compatible game data");
  string path=Path.Combine(Path.GetTempPath(),"sensebridge-pack-"+Guid.NewGuid().ToString("N")+".json");
  try{
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(entry.Url);request.UserAgent="SenseBridge-data";request.Timeout=30000;request.ReadWriteTimeout=30000;
   using(var response=(HttpWebResponse)await request.GetResponseAsync()){
    if(response.ResponseUri.Scheme!="https"||response.ContentLength>entry.Size)throw new InvalidDataException("Invalid data pack response");
    using(var input=response.GetResponseStream())using(var output=File.Create(path)){
     byte[] buffer=new byte[65536];int count;long total=0;
     while((count=await input.ReadAsync(buffer,0,buffer.Length))>0){total+=count;if(total>entry.Size)throw new InvalidDataException("Data pack too large");await output.WriteAsync(buffer,0,count);}
    }
   }
   return Verify(path,root,entry);
  }finally{if(File.Exists(path))File.Delete(path);}
 }
}
}
