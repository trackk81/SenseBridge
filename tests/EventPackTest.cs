using System;using System.IO;using System.Linq;using System.Collections.Generic;using SenseBridge;
class EventPackTest {
 static void MustReject(GamePreset p,string root){try{Presets.Validate(p,root);}catch(Exception){return;}throw new Exception("Invalid pack accepted");}
 static void Main(string[] args){
  string temp=Path.Combine(Path.GetTempPath(),"sensebridge-event-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   var p=Presets.Read(args[0],temp);var games=Store.BuiltInGames();games.Add(new GameProfile{Id="steam-1030300",Name="Silksong",SteamAppId="1030300",Adapter="global-audio",InstallDirectory=@"G:\SteamLibrary\steamapps\common\Hollow Knight Silksong"});
   var result=Presets.Import(temp,games,p);if(result.Count!=2||result[1].Adapter!="unity-audio-v1"||result[1].InstallDirectory!=games[1].InstallDirectory||Store.BuiltInGames().Count!=1||Store.BuiltInGames()[0].Name!="Bloodborne")throw new Exception("Import merge or built-in regression");
   var again=Presets.Capture(temp,result[1]);Presets.Validate(again,temp);if(again.Assets[0].Sha256!=p.Assets[0].Sha256||again.Unity.Rules.Count!=p.Unity.Rules.Count)throw new Exception("Roundtrip");
   if(again.Unity.InputComponent!=p.Unity.InputComponent||again.Unity.ActionSetField!=p.Unity.ActionSetField||!again.Unity.Rules.Select(r=>r.TriggerAction).SequenceEqual(p.Unity.Rules.Select(r=>r.TriggerAction)))throw new Exception("Trigger mapping lost in roundtrip");
   if(p.Unity.StateRules!=null){if(again.Unity.StateRules.Count!=p.Unity.StateRules.Count||again.Unity.ActorComponent!=p.Unity.ActorComponent)throw new Exception("State rule roundtrip");var state=p.Unity.StateRules[0];int duration=state.Milliseconds;state.Milliseconds=9999;MustReject(p,temp);state.Milliseconds=duration;string stateName=state.State;state.State="bad\tstate";MustReject(p,temp);state.State=stateName;}
   string id=p.Unity.PackId;p.Unity.PackId="../escape";MustReject(p,temp);p.Unity.PackId=id;
   string name=p.Unity.Rules[0].Name;p.Unity.Rules[0].Name="bad\tentry";MustReject(p,temp);p.Unity.Rules[0].Name=name;
   int samples=p.Unity.Rules[0].Samples;p.Unity.Rules[0].Samples=int.MaxValue;MustReject(p,temp);p.Unity.Rules[0].Samples=samples;
   byte[] bank=Convert.FromBase64String(p.Assets[0].Data);string encoded=p.Assets[0].Data,hash=p.Assets[0].Sha256;bank[24]=255;bank[25]=127;p.Assets[0].Data=Convert.ToBase64String(bank);p.Assets[0].Sha256=Presets.Hash(bank);MustReject(p,temp);p.Assets[0].Data=encoded;p.Assets[0].Sha256=hash;
   Console.WriteLine("PASS: external pack import/merge, roundtrip, built-in remains Bloodborne only, path/rule/sample/amplitude bounds");
  }finally{Directory.Delete(temp,true);}
 }
}
