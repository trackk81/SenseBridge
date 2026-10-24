using System;using System.IO;using System.Linq;using System.Collections.Generic;using SenseBridge;
class PresetTest {
 static void Check(bool c,string s){if(!c)throw new Exception(s);}
 static void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected,"Invalid preset accepted");}
 static string HashFile(string p){return Presets.Hash(File.ReadAllBytes(p));}
 public static void Main(string[] args){
  string root=Path.Combine(Path.GetTempPath(),"SenseBridge-PresetTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var global=new GameProfile{Id="test-global",Name="Test 게임 日本語",Adapter="global-audio",SteamAppId="12345",EngineExecutable=@"X:\Private\game.exe",UseTriggers=true,UseHaptics=true};
   var cfg=new Dictionary<string,string>{{"gain","1.75"},{"left_preferred_mode","2"},{"right_preferred_mode","0"},{"left_force","7"},{"right_force","3"},{"source","private-device-id"}};
   Store.WriteIni(Presets.ConfigPath(root,global),cfg);string path=Path.Combine(root,"global.sensebridge.json");Presets.Export(root,global,path);
   string json=File.ReadAllText(path);Check(!json.Contains("private-device-id")&&!json.Contains("Private")&&!json.Contains("EngineExecutable"),"Private local paths leaked");
   var p=Presets.Read(path,root);Check(p.Title==global.Name&&p.Triggers&&p.Haptics&&!p.Speaker,"Global flags/title");
   var imported=Presets.Import(root,new List<GameProfile>(),p);Check(imported.Count==1&&imported[0].EngineExecutable==null,"New import must require local mapping");
   var d=Store.ReadIni(Presets.ConfigPath(root,imported[0]));Check(d["gain"]=="1.75"&&d["left_mode"]=="2"&&d["source"]==""&&d["enabled"]=="0","Settings round trip");
   imported[0].EngineExecutable=@"Z:\Existing\local.exe";var second=Presets.Import(root,imported,p);Check(second.Count==1&&second[0].EngineExecutable==imported[0].EngineExecutable,"Preserve matched local path; no duplicate");
   p.Version=99;Reject(()=>Presets.Validate(p,root));p.Version=1;
   p.Settings["gain"]="NaN";Reject(()=>Presets.Import(root,second,p));p.Settings["gain"]="99";Reject(()=>Presets.Validate(p,root));p.Settings["gain"]="1.75";
   p.Settings["source"]="attack";Reject(()=>Presets.Validate(p,root));p.Settings.Remove("source");
   p.Adapter="external-dll";Reject(()=>Presets.Validate(p,root));p.Adapter="global-audio";
   Console.WriteLine("PASS: global round trip, UTF-8, flags, local path/device privacy, duplicate merge, invalid values/version/adapter");
   if(args.Length==0)return;
   string pkg=args[0];File.Copy(Path.Combine(pkg,"SenseBridge.Bloodborne.dll"),Path.Combine(root,"SenseBridge.Bloodborne.dll"));
   foreach(string file in Directory.GetFiles(Path.Combine(pkg,@"Bloodborne\runtime"),"*",SearchOption.AllDirectories)){string target=Path.Combine(root,file.Substring(pkg.Length+1));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(file,target);}
   var bb=new GameProfile{Id="bloodborne",Name="Bloodborne",Adapter="bloodborne-usb-v4",UseTriggers=true,UseHaptics=true,UseSpeaker=true};
   path=Path.Combine(root,"bloodborne.sensebridge.json");Presets.Export(root,bb,path);var b=Presets.Read(path,root);Check(b.Assets.Count==11,"Bank and ten effects required");
   string bank=Path.Combine(root,@"Bloodborne\runtime\auto-haptics.bin"),before=HashFile(bank);
   var all=Presets.Import(root,new List<GameProfile>{bb},b);Check(all.Count==1&&all[0].UseSpeaker,"Built-in merge");Check(HashFile(bank)==before,"Bank bytes changed");
   foreach(var asset in b.Assets)Check(HashFile(Path.Combine(root,@"Bloodborne\runtime",asset.Name.Replace('/',Path.DirectorySeparatorChar)))==asset.Sha256,"Resource changed");
   string previous=File.ReadAllText(Path.Combine(root,@"profiles\games.json"));
   var first=b.Assets[0];string old=first.Name;first.Name="../../outside.dll";Reject(()=>Presets.Import(root,all,b));first.Name=old;
   old=first.Sha256;first.Sha256="invalid";Reject(()=>Presets.Import(root,all,b));first.Sha256=old;
   Check(File.ReadAllText(Path.Combine(root,@"profiles\games.json"))==previous&&HashFile(bank)==before,"Failed validation changed installation");
   old=b.AdapterSha256;b.AdapterSha256="wrong-version";Reject(()=>Presets.Validate(b,root));b.AdapterSha256=old;
   Console.WriteLine("PASS: full Bloodborne JSON round trip, eleven resources preserved, adapter compatibility, traversal/checksum rejection, failed import leaves installation unchanged");
   Console.WriteLine("Export bytes: "+new FileInfo(path).Length);
  }finally{if(root.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("SenseBridge-PresetTest-"))Directory.Delete(root,true);}
 }
}
