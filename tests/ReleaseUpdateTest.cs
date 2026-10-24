using System;using System.IO;using System.Linq;using SenseBridge;
class ReleaseUpdateTest {
 static void Main(string[] args){
  string dir=Path.Combine(Path.GetTempPath(),"SenseBridge-release-check-"+Guid.NewGuid().ToString("N"));
  try{
   var m=UpdatePackage.Json().Deserialize<UpdateManifest>(File.ReadAllText(args[0]));UpdatePackage.Validate(m);UpdatePackage.Extract(args[1],dir,m);
   if(System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(dir,"SenseBridge.exe")).Version.ToString(3)!=m.Version)throw new Exception("Wrong application version");
   if(m.Files.Any(f=>f.Path.StartsWith("profiles/")||f.Path.StartsWith("packs/")||f.Path.EndsWith("dualsense.ini")))throw new Exception("User data in update");
   Console.WriteLine("PASS: release manifest, all extracted hashes, application version and protected-file exclusion ("+m.Files.Count+" files)");
  }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
}
