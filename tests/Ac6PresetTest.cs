using System;using System.IO;using System.Linq;using System.Collections.Generic;using SenseBridge;
// Prepared for the execution gate to be lifted. This test has not been run.
class Ac6PresetTest {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid data accepted");}
 public static void Main(string[] args){
  string root=Path.Combine(Path.GetTempPath(),"SenseBridge-Ac6PresetTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var p=Presets.Read(args[0],root);
   var existing=new GameProfile{Id="steam-ac6",SteamAppId="1888160",Name="AC6",Adapter="global-audio",EngineExecutable=@"X:\Games\AC6\armoredcore6.exe"};
   var games=Presets.Import(root,new List<GameProfile>{existing},p);
   Check(games.Count==1&&games[0].EngineExecutable==existing.EngineExecutable,"Merge must preserve local executable");
   Check(!games[0].UseTriggers&&!games[0].UseHaptics&&!games[0].UseSpeaker,"Imported effects must default off");
   var config=Store.ReadIni(Presets.ConfigPath(root,games[0]));Check(config["enabled"]=="0"&&config["speaker"]=="0","Import enabled output");
   var captured=Presets.Capture(root,games[0]);Check(captured.Assets.Count==3&&captured.Ac6.PackId==p.Ac6.PackId,"Native capture failed");
   foreach(var asset in p.Assets)Check(captured.Assets.Single(a=>a.Name==asset.Name).Sha256==asset.Sha256,"Round-trip content changed");
   string before=File.ReadAllText(Path.Combine(root,"profiles","games.json"));
   p.Settings["speaker_volume"]="36";Reject(()=>Presets.Import(root,games,p));p.Settings["speaker_volume"]="20";
   string name=p.Assets[0].Name;p.Assets[0].Name="../outside.dll";Reject(()=>Presets.Import(root,games,p));p.Assets[0].Name=name;
   string hash=p.Assets[0].Sha256;p.Assets[0].Sha256=new string('0',64);Reject(()=>Presets.Import(root,games,p));p.Assets[0].Sha256=hash;
   Check(File.ReadAllText(Path.Combine(root,"profiles","games.json"))==before,"Rejected import changed settings");
   Console.WriteLine("PASS: AC6 merge, disabled defaults, capture hashes, volume/path/hash rejection");
  }finally{if(Path.GetFileName(root).StartsWith("SenseBridge-Ac6PresetTest-"))Directory.Delete(root,true);}
 }
}
