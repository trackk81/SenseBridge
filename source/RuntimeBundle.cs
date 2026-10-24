using System;using System.IO;using System.IO.Compression;using System.Linq;using System.Reflection;using System.Collections.Generic;
namespace SenseBridge {
// The application ships a transparent ZIP resource so the existing 0.1.6 updater
// can deliver dependencies by replacing its already-allowlisted SenseBridge.exe.
// No network download, executable content from presets, or user data replacement.
public static class RuntimeBundle {
 public static void Ensure(string root){
  using(var source=typeof(RuntimeBundle).Assembly.GetManifestResourceStream("SenseBridge.Runtime.zip")){
   if(source==null)throw new FileNotFoundException("Bundled runtime is missing");
   using(var zip=new ZipArchive(source,ZipArchiveMode.Read)){
    var manifest=zip.GetEntry("manifest.json");if(manifest==null||manifest.Length>1024*1024)throw new InvalidDataException("Runtime manifest");
    List<UpdateFile> files;using(var r=new StreamReader(manifest.Open()))files=UpdatePackage.Json().Deserialize<List<UpdateFile>>(r.ReadToEnd());
    if(files==null||files.Count<1||files.Count>400||files.Any(f=>f==null||string.IsNullOrEmpty(f.Path)||f.Sha256==null||f.Sha256.Length!=64||!f.Sha256.All(Uri.IsHexDigit))||zip.Entries.Count!=files.Count+1||zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count||files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=files.Count)throw new InvalidDataException("Runtime bounds");
    foreach(var f in files){if(f.Size<0||f.Size>UpdatePackage.MaxSize||!(f.Path.StartsWith("Runtime/",StringComparison.Ordinal)||f.Path.StartsWith("Ac6Runtime/",StringComparison.Ordinal))||!UpdatePackage.Allowed(f.Path)||zip.GetEntry(f.Path)==null||zip.GetEntry(f.Path).Length!=f.Size)throw new InvalidDataException("Runtime path or size");}
    if(files.Sum(f=>f.Size)>UpdatePackage.MaxExpanded)throw new InvalidDataException("Runtime expanded size");
    var changed=files.Where(f=>!File.Exists(UpdatePackage.SafePath(root,f.Path))||UpdatePackage.Hash(UpdatePackage.SafePath(root,f.Path))!=f.Sha256).ToList();if(changed.Count==0)return;
    string temp=Path.Combine(Path.GetFullPath(root),".runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);var done=new List<UpdateFile>();var existing=new HashSet<string>();
    bool preserveBackup=false;
    try{
     foreach(var f in changed){string p=Path.Combine(temp,f.Path);Directory.CreateDirectory(Path.GetDirectoryName(p));using(var input=zip.GetEntry(f.Path).Open())using(var output=File.Create(p))input.CopyTo(output);if(UpdatePackage.Hash(p)!=f.Sha256)throw new InvalidDataException("Runtime checksum");}
     foreach(var f in changed){string dest=UpdatePackage.SafePath(root,f.Path),p=Path.Combine(temp,f.Path),backup=Path.Combine(temp,"backup",f.Path);Directory.CreateDirectory(Path.GetDirectoryName(dest));if(File.Exists(dest)){Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Replace(p,dest,backup);existing.Add(f.Path);}else File.Move(p,dest);done.Add(f);}
    }catch{foreach(var f in done.AsEnumerable().Reverse()){try{string dest=UpdatePackage.SafePath(root,f.Path);if(existing.Contains(f.Path))File.Copy(Path.Combine(temp,"backup",f.Path),dest,true);else File.Delete(dest);}catch{preserveBackup=true;}}throw;}
    finally{if(!preserveBackup)Directory.Delete(temp,true);}
   }
  }
 }
}
}
