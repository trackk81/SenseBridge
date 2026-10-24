using System;using System.IO;using System.Net;using System.Threading.Tasks;
namespace SenseBridge {
// Catalog metadata only. Game resources remain separate release assets.
public static class PackDownloads {
 public const string Url="https://github.com/trackk81/SenseBridge/releases/download/v0.1.5/Silksong-1.0.sensebridge.json";
 public const string Sha256="779ddf6cb2e01bdb03508fa4bdc10cd04a5499c52b4795f90037553bb2528b56";
 public static bool Available(GameProfile g){return g!=null&&!EventPacks.Is(g)&&(g.SteamAppId=="1030300"||string.Equals(Path.GetFileName(g.EngineExecutable),"Hollow Knight Silksong.exe",StringComparison.OrdinalIgnoreCase));}
 public static GamePreset Verify(string path,string root){
  if(new FileInfo(path).Length>20*1024*1024||Presets.Hash(File.ReadAllBytes(path))!=Sha256)throw new InvalidDataException("Data pack checksum mismatch. Please download again.");
  var p=Presets.Read(path,root);if(p.Adapter!="unity-audio-v1"||p.SteamAppId!="1030300"||p.Unity.PackId!="silksong-audio-01")throw new InvalidDataException("Unexpected data pack identity");
  p.Triggers=p.Haptics=p.Speaker=false;return p;
 }
 public static async Task<GamePreset> Download(string root){
  string path=Path.Combine(Path.GetTempPath(),"sensebridge-pack-"+Guid.NewGuid().ToString("N")+".json");
  try{
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(Url);request.UserAgent="SenseBridge/0.1.5";request.Timeout=30000;request.ReadWriteTimeout=30000;
   using(var response=(HttpWebResponse)await request.GetResponseAsync()){
    if(response.ResponseUri.Scheme!="https"||response.ContentLength>20*1024*1024)throw new InvalidDataException("Invalid data pack response");
    using(var input=response.GetResponseStream())using(var output=File.Create(path)){
     byte[] buffer=new byte[65536];int count;long total=0;
     while((count=await input.ReadAsync(buffer,0,buffer.Length))>0){total+=count;if(total>20*1024*1024)throw new InvalidDataException("Data pack too large");await output.WriteAsync(buffer,0,count);}
    }
   }
   return Verify(path,root);
  }finally{if(File.Exists(path))File.Delete(path);}
 }
}
}
