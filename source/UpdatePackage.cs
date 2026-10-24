using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
namespace SenseBridge {
public sealed class UpdateFile {public string Path{get;set;} public string Sha256{get;set;} public long Size{get;set;}}
public sealed class UpdateManifest {public int Format{get;set;} public string Version{get;set;} public string MinimumVersion{get;set;} public string PackageUrl{get;set;} public string Sha256{get;set;} public long Size{get;set;} public List<UpdateFile> Files{get;set;}}
public sealed class UpdatePlan {public string InstallRoot{get;set;} public string PayloadRoot{get;set;} public int ParentPid{get;set;} public long ParentStartTicks{get;set;} public UpdateManifest Manifest{get;set;}}
public static class UpdatePackage {
 public const long MaxSize=200*1024*1024,MaxExpanded=500*1024*1024;
 public static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 public static bool Allowed(string p){
  if(string.IsNullOrEmpty(p)||p.Length>200||p.Contains("\\")||p.Contains(":")||p.StartsWith("/")||p.Split('/').Any(x=>x==""||x=="."||x==".."||x.EndsWith(".")||x.EndsWith(" ")||x.IndexOfAny(Path.GetInvalidFileNameChars())>=0))return false;
  string[] roots={"SenseBridge.exe","SenseBridge.Updater.exe","SenseBridge.Engine.exe","SenseBridge.Events.exe","SenseBridge.Bloodborne.dll","SDL3.dll","libc++.dll","libiconv-2.dll","libwinpthread-1.dll","attach-profile.json","ui.en.json","ui.ja.json","README.md","START-HERE.txt","THIRD-PARTY-NOTICES.txt","PRESET-GUIDE.ko.html"};
  if(roots.Contains(p,StringComparer.Ordinal))return true;
  if(p.StartsWith("Ac6Runtime/research/",StringComparison.Ordinal)&&p.Split('/').Length==3&&p.EndsWith(".py",StringComparison.Ordinal))return true;
  if(p=="Ac6Runtime/trace/SenseBridge.Ac6Trace.dll")return true;
  if(p=="Ac6Runtime/sd-public.pem")return true;
  if(p.StartsWith("Runtime/Decoder/",StringComparison.Ordinal)&&p.Split('/').Length==3&&(p.EndsWith(".dll",StringComparison.Ordinal)||p=="Runtime/Decoder/vgmstream-cli.exe"||p=="Runtime/Decoder/COPYING"||p=="Runtime/Decoder/README.md"))return true;
  if(p.StartsWith("Runtime/Python/",StringComparison.Ordinal)){
   var parts=p.Split('/');string name=parts[parts.Length-1];
   if(parts.Length==3&&new[]{"python.exe","python313.dll","python3.dll","python313.zip","python313._pth","vcruntime140.dll","vcruntime140_1.dll","LICENSE.txt"}.Contains(name))return true;
   if(parts.Length==4&&parts[2]=="DLLs"&&(name.EndsWith(".pyd",StringComparison.Ordinal)||name.EndsWith(".dll",StringComparison.Ordinal)))return true;
  }
  if(p.StartsWith("RELEASE-",StringComparison.Ordinal)&&p.EndsWith(".md",StringComparison.Ordinal)&&!p.Contains('/'))return true;
  if(p=="Bloodborne/runtime/auto-haptics.bin")return true;
  if(p.StartsWith("Bloodborne/runtime/effects/",StringComparison.Ordinal)&&p.EndsWith(".wav",StringComparison.Ordinal)&&p.Split('/').Length==4)return true;
  if(p.StartsWith("licenses/",StringComparison.Ordinal)){string name=Path.GetFileName(p);return p.Split('/').Length<=5&&(name.StartsWith("COPYING",StringComparison.Ordinal)||name=="LICENSE"||name=="README"||name.EndsWith(".txt",StringComparison.Ordinal));}
  if((p.StartsWith("assets/",StringComparison.Ordinal)&&(p.EndsWith(".png",StringComparison.Ordinal)||p.EndsWith(".ico",StringComparison.Ordinal)||p=="assets/README.md"))||(p.StartsWith("examples/",StringComparison.Ordinal)&&p.EndsWith(".sensebridge.json",StringComparison.Ordinal)))return p.Split('/').Length==2;
  if(p=="UnityRuntime/.doorstop_version"||p=="UnityRuntime/doorstop_config.ini"||p=="UnityRuntime/winhttp.dll"||p=="UnityRuntime/BepInEx/plugins/SenseBridge/SenseBridge.UnityBridge.dll")return true;
  return p.StartsWith("UnityRuntime/BepInEx/core/",StringComparison.Ordinal)&&p.EndsWith(".dll",StringComparison.Ordinal)&&p.Split('/').Length==4;
 }
 public static void Validate(UpdateManifest m){
  Version v,min;if(m==null||m.Format!=1||!System.Version.TryParse(m.Version,out v)||!System.Version.TryParse(m.MinimumVersion,out min)||min>v||m.Size<1||m.Size>MaxSize||!Digest(m.Sha256)||m.Files==null||m.Files.Count<1||m.Files.Count>500)throw new InvalidDataException("Invalid update manifest");
  string expected="https://github.com/trackk81/SenseBridge/releases/download/v"+m.Version+"/SenseBridge-"+m.Version+"-update-win-x64.zip";
  if(m.PackageUrl!=expected)throw new InvalidDataException("Unexpected update download location");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
  foreach(var f in m.Files){if(f==null||!Allowed(f.Path)||!Digest(f.Sha256)||f.Size<0||f.Size>MaxSize||!names.Add(f.Path))throw new InvalidDataException("Invalid update file");total+=f.Size;}
  if(total>MaxExpanded||!names.Contains("SenseBridge.exe")||!names.Contains("SenseBridge.Updater.exe"))throw new InvalidDataException("Incomplete or oversized update");
 }
 static bool Digest(string s){return s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);}
 public static string SafePath(string root,string relative){
  if(!Allowed(relative))throw new InvalidDataException("Protected or unsafe update path: "+relative);
  root=Path.GetFullPath(root);string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
  if(!path.StartsWith(root.TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Path outside installation");
  for(string part=path;!string.IsNullOrEmpty(part);part=Path.GetDirectoryName(part))if((File.Exists(part)||Directory.Exists(part))&&(File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Update path contains a link");
  return path;
 }
 public static void Extract(string zip,string payload,UpdateManifest m){
  Validate(m);if(new FileInfo(zip).Length!=m.Size||Hash(zip)!=m.Sha256)throw new InvalidDataException("Update package checksum mismatch");
  Directory.CreateDirectory(payload);var expected=m.Files.ToDictionary(x=>x.Path,StringComparer.Ordinal);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  using(var z=ZipFile.OpenRead(zip)){
   if(z.Entries.Count!=expected.Count)throw new InvalidDataException("Unexpected archive entries");
   foreach(var e in z.Entries){UpdateFile f;if(!expected.TryGetValue(e.FullName,out f)||!seen.Add(e.FullName)||e.Length!=f.Size)throw new InvalidDataException("Unexpected archive file");string dest=SafePath(payload,e.FullName);Directory.CreateDirectory(Path.GetDirectoryName(dest));
    using(var input=e.Open())using(var output=new FileStream(dest,FileMode.CreateNew)){var buffer=new byte[65536];long total=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){total+=n;if(total>f.Size)throw new InvalidDataException("Expanded file too large");output.Write(buffer,0,n);}if(total!=f.Size)throw new InvalidDataException("Truncated update file");}
    if(Hash(dest)!=f.Sha256)throw new InvalidDataException("Update file checksum mismatch");
   }
  }
 }
 // Copy all backups before replacing any file; restore every changed file on failure.
 public static void Apply(string root,string payload,string backup,UpdateManifest m){
  Validate(m);Directory.CreateDirectory(backup);var existing=new HashSet<string>();var changed=new List<UpdateFile>();
  foreach(var f in m.Files){string src=SafePath(payload,f.Path),dst=SafePath(root,f.Path);if(new FileInfo(src).Length!=f.Size||Hash(src)!=f.Sha256)throw new InvalidDataException("Staged file changed");if(Directory.Exists(dst))throw new IOException("A directory blocks an update file");if(File.Exists(dst)){string b=SafePath(backup,f.Path);Directory.CreateDirectory(Path.GetDirectoryName(b));File.Copy(dst,b,false);existing.Add(f.Path);}}
  try{foreach(var f in m.Files){string dst=SafePath(root,f.Path);Directory.CreateDirectory(Path.GetDirectoryName(dst));string tmp=dst+"."+Guid.NewGuid().ToString("N")+".update";try{File.Copy(SafePath(payload,f.Path),tmp,false);if(File.Exists(dst))File.Replace(tmp,dst,null);else File.Move(tmp,dst);changed.Add(f);}finally{if(File.Exists(tmp))File.Delete(tmp);}}}
  catch(Exception original){var failures=new List<string>();for(int i=changed.Count-1;i>=0;i--){var f=changed[i];try{string dst=SafePath(root,f.Path);if(existing.Contains(f.Path))File.Copy(SafePath(backup,f.Path),dst,true);else File.Delete(dst);}catch(Exception e){failures.Add(f.Path+": "+e.Message);}}if(failures.Count>0)throw new IOException("Update failed; recovery files: "+backup+"\n"+string.Join("\n",failures),original);throw;}
 }
 public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=2*1024*1024,RecursionLimit=16};}
}
}
