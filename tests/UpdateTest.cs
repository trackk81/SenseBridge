using System;using System.IO;using System.IO.Compression;using System.Collections.Generic;using System.Linq;using SenseBridge;
class UpdateTest {
 static void Check(bool c,string message){if(!c)throw new Exception(message);}
 static void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected,"Invalid update accepted");}
 static void Put(string root,string path,string text){string f=Path.Combine(root,path.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(f));File.WriteAllText(f,text);}
 static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"SenseBridge-update-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string payload=Path.Combine(root,"payload"),install=Path.Combine(root,"install"),zip=Path.Combine(root,"update.zip");
   Put(payload,"SenseBridge.exe","new-app");Put(payload,"SenseBridge.Updater.exe","new-updater");
   var m=new UpdateManifest{Format=1,Version="0.1.7",MinimumVersion="0.1.6",PackageUrl="https://github.com/trackk81/SenseBridge/releases/download/v0.1.7/SenseBridge-0.1.7-update-win-x64.zip",Files=new List<UpdateFile>()};
   foreach(string name in new[]{"SenseBridge.exe","SenseBridge.Updater.exe"}){string f=Path.Combine(payload,name);m.Files.Add(new UpdateFile{Path=name,Sha256=UpdatePackage.Hash(f),Size=new FileInfo(f).Length});}
   ZipFile.CreateFromDirectory(payload,zip);m.Size=new FileInfo(zip).Length;m.Sha256=UpdatePackage.Hash(zip);UpdatePackage.Validate(m);
   Check(!AppUpdates.Newer("0.1.6",new Version("0.1.6.0")),"Same version");Check(AppUpdates.Newer("0.1.7",new Version("0.1.6.0")),"New version");Check(!AppUpdates.Newer("0.1.5",new Version("0.1.6.0")),"Downgrade");
   foreach(string p in new[]{"../SenseBridge.exe","/SenseBridge.exe","SenseBridge.exe:stream","profiles/games.json","packs/test/events.bin","Bloodborne/runtime/dualsense.ini","UnityRuntime/../../startup.exe","SenseBridge.exe ","SenseBridge.exe."})Check(!UpdatePackage.Allowed(p),"Protected/traversal path: "+p);
   string hash=m.Sha256;m.Sha256=new string('0',64);Reject(()=>UpdatePackage.Extract(zip,Path.Combine(root,"bad"),m));m.Sha256=hash;
   m.Files.Add(m.Files[0]);Reject(()=>UpdatePackage.Validate(m));m.Files.RemoveAt(m.Files.Count-1);
   UpdatePackage.Extract(zip,Path.Combine(root,"verified"),m);
   Put(install,"SenseBridge.exe","old-app");Put(install,"SenseBridge.Updater.exe","old-updater");Put(install,"profiles/games.json","my-games");Put(install,"packs/test/events.bin","my-pack");Put(install,"Bloodborne/runtime/dualsense.ini","my-options");
   using(var held=new FileStream(Path.Combine(install,"SenseBridge.Updater.exe"),FileMode.Open,FileAccess.Read,FileShare.Read))Reject(()=>UpdatePackage.Apply(install,payload,Path.Combine(root,"rollback"),m));
   Check(File.ReadAllText(Path.Combine(install,"SenseBridge.exe"))=="old-app","Rollback did not restore first file");
   UpdatePackage.Apply(install,payload,Path.Combine(root,"backup"),m);
   Check(File.ReadAllText(Path.Combine(install,"SenseBridge.exe"))=="new-app","Upgrade failed");
   Check(File.ReadAllText(Path.Combine(install,"profiles/games.json"))=="my-games"&&File.ReadAllText(Path.Combine(install,"packs/test/events.bin"))=="my-pack"&&File.ReadAllText(Path.Combine(install,"Bloodborne/runtime/dualsense.ini"))=="my-options","User data changed");
   Console.WriteLine("PASS: version gating, archive hashes, path/protected-file rejection, duplicate rejection, locked-file rollback, update application and user-data preservation");
  }finally{Directory.Delete(root,true);}
 }
}
