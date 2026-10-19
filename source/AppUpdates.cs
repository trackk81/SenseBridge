using System;using System.Diagnostics;using System.IO;using System.Linq;using System.Net;using System.Reflection;using System.Text;using System.Threading;using System.Threading.Tasks;
namespace SenseBridge {
public sealed class ReleaseAsset {public string name{get;set;}public string browser_download_url{get;set;}}
public sealed class UpdateRelease {public string tag_name{get;set;}public bool draft{get;set;}public bool prerelease{get;set;}public ReleaseAsset[] assets{get;set;}}
public static class AppUpdates {
 public static Version Current {get{return typeof(AppUpdates).Assembly.GetName().Version;}}
 public static bool Newer(string candidate,Version current){Version v;return Version.TryParse(candidate,out v)&&new Version(v.Major,v.Minor,Math.Max(0,v.Build))>new Version(current.Major,current.Minor,Math.Max(0,current.Build));}
 static async Task Fetch(string url,string path,long max){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var req=(HttpWebRequest)WebRequest.Create(url);req.UserAgent="SenseBridge/"+Current.ToString(3);req.Accept="application/vnd.github+json";
  using(var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3)))using(timeout.Token.Register(()=>req.Abort()))
  using(var response=(HttpWebResponse)await req.GetResponseAsync()){
   if(response.ResponseUri.Scheme!="https"||response.ContentLength>max)throw new InvalidDataException("Invalid update response");
   using(var input=response.GetResponseStream())using(var output=File.Create(path)){var buffer=new byte[65536];long total=0;int n;while((n=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token))>0){total+=n;if(total>max)throw new InvalidDataException("Update download too large");await output.WriteAsync(buffer,0,n,timeout.Token);}}
  }
 }
 public static async Task<UpdateManifest> Check(){
  string file=Path.GetTempFileName();try{
   await Fetch("https://api.github.com/repos/trackk81/SenseBridge/releases/latest",file,2*1024*1024);
   var release=UpdatePackage.Json().Deserialize<UpdateRelease>(File.ReadAllText(file));
   if(release==null||release.draft||release.prerelease||release.tag_name==null||!release.tag_name.StartsWith("v")||!Newer(release.tag_name.Substring(1),Current))return null;
   string version=release.tag_name.Substring(1),url="https://github.com/trackk81/SenseBridge/releases/download/"+release.tag_name+"/update-manifest.json";
   if(release.assets==null||!release.assets.Any(a=>a.name=="update-manifest.json"&&a.browser_download_url==url))return null;
   await Fetch(url,file,2*1024*1024);var m=UpdatePackage.Json().Deserialize<UpdateManifest>(File.ReadAllText(file));UpdatePackage.Validate(m);
   if(m.Version!=version||new Version(m.MinimumVersion)>Current)throw new InvalidDataException("This release requires a full ZIP installation");
   if(!release.assets.Any(a=>a.browser_download_url==m.PackageUrl))throw new InvalidDataException("Update package asset missing");return m;
  }finally{File.Delete(file);}
 }
 public static async Task<string> Prepare(UpdateManifest m){
  UpdatePackage.Validate(m);if(!Newer(m.Version,Current))throw new InvalidDataException("Update is not newer");
  string dir=Path.Combine(Path.GetTempPath(),"SenseBridge-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  string zip=Path.Combine(dir,"package.zip"),payload=Path.Combine(dir,"payload");
  await Fetch(m.PackageUrl,zip,UpdatePackage.MaxSize);await Task.Run(()=>UpdatePackage.Extract(zip,payload,m));
  if(AssemblyName.GetAssemblyName(Path.Combine(payload,"SenseBridge.exe")).Version.ToString(3)!=m.Version)throw new InvalidDataException("Update executable version mismatch");
  // Run the current trusted helper outside the application directory so it can update itself.
  File.Copy(Path.Combine(Store.Root,"SenseBridge.Updater.exe"),Path.Combine(dir,"SenseBridge.Updater.exe"));
  using(var process=Process.GetCurrentProcess()){
   var plan=new UpdatePlan{InstallRoot=Path.GetFullPath(Store.Root),PayloadRoot=payload,ParentPid=process.Id,ParentStartTicks=process.StartTime.ToUniversalTime().Ticks,Manifest=m};
   File.WriteAllText(Path.Combine(dir,"plan.json"),UpdatePackage.Json().Serialize(plan),new UTF8Encoding(false));
  }
  return dir;
 }
 public static void Launch(string dir){Process.Start(new ProcessStartInfo(Path.Combine(dir,"SenseBridge.Updater.exe"),"\""+Path.Combine(dir,"plan.json")+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});}
}
}
