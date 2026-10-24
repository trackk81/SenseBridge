using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace SenseBridge {
public sealed class Ac6PackInfo {public string PackId{get;set;}public string ExecutableSha256{get;set;}}
public sealed class Ac6EventMap {public List<string> labels{get;set;}public Dictionary<string,int> mapping{get;set;}}
public sealed class Ac6RadioRule {public uint event_id{get;set;}public uint target{get;set;}public string language{get;set;}public uint media_id{get;set;}public string source_sha256{get;set;}}
public sealed class Ac6RadioIndex {public List<Ac6RadioRule> rules{get;set;}public Dictionary<string,List<uint>> stop_targets{get;set;}}
public static class Ac6Data {
 public const string Adapter="ac6-audio-v1",Id="ac6-audio-01",GameHash="e003771e5548bca91882c1d7dfe2df3de97cee05c2c989d0c0e34b036ea610d6";
 public static readonly string[] Names={"events.bin","event-map.json","radio-index.json"};
 public static bool Is(GamePreset p){return p!=null&&p.Adapter==Adapter;}
 public static string DirectoryFor(string root){return Path.Combine(root,"packs",Id);}
 static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=20*1024*1024,RecursionLimit=16};}
 static bool Hash(string s){return s!=null&&s.Length==64&&s.All(Uri.IsHexDigit);}
 public static void Validate(GamePreset p){
  if(p.Version!=3||p.SteamAppId!="1888160"||p.Ac6==null||p.Ac6.PackId!=Id||p.Ac6.ExecutableSha256!=GameHash||p.Unity!=null||!string.IsNullOrEmpty(p.AdapterSha256))throw new InvalidDataException("Invalid AC6 data identity");
  if(p.Assets.Count!=Names.Length||p.Assets.Any(a=>a==null||!Names.Contains(a.Name))||p.Assets.Select(a=>a.Name).Distinct().Count()!=Names.Length)throw new InvalidDataException("Invalid AC6 asset names");
  var bytes=new Dictionary<string,byte[]>();long total=0;
  foreach(var a in p.Assets){if(a.Data==null||a.Data.Length>90*1024*1024)throw new InvalidDataException("AC6 asset size");byte[] b=Convert.FromBase64String(a.Data);total+=b.Length;if(total>64*1024*1024||!Hash(a.Sha256)||Presets.Hash(b)!=a.Sha256)throw new InvalidDataException("AC6 checksum");bytes.Add(a.Name,b);}
  var map=Json().Deserialize<Ac6EventMap>(Encoding.UTF8.GetString(bytes["event-map.json"]));
  if(map==null||map.labels==null||map.labels.Count<1||map.labels.Count>1000||map.labels.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>100||s.Any(char.IsControl))||map.mapping==null||map.mapping.Count<1||map.mapping.Count>20000)throw new InvalidDataException("AC6 event map bounds");
  foreach(var e in map.mapping){uint id;if(!uint.TryParse(e.Key,out id)||id==0||e.Value<0||e.Value>=map.labels.Count)throw new InvalidDataException("AC6 event mapping");}
  using(var r=new BinaryReader(new MemoryStream(bytes["events.bin"]))){if(Encoding.ASCII.GetString(r.ReadBytes(8))!="SBEVENT1"||r.ReadUInt32()!=map.labels.Count)throw new InvalidDataException("AC6 bank identity");for(int i=0;i<map.labels.Count;i++){uint frames=r.ReadUInt32(),speaker=r.ReadUInt32(),trigger=r.ReadUInt32();if(frames<1||frames>26400||speaker!=0||trigger!=0||r.BaseStream.Length-r.BaseStream.Position<frames*2L)throw new InvalidDataException("AC6 waveform bounds");for(uint j=0;j<frames;j++){short v=r.ReadInt16();if(v< -4588||v>4588)throw new InvalidDataException("AC6 amplitude limit");}}if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("AC6 trailing samples");}
  var radio=Json().Deserialize<Ac6RadioIndex>(Encoding.UTF8.GetString(bytes["radio-index.json"]));
  if(radio==null||radio.rules==null||radio.rules.Count>20000||radio.stop_targets==null||radio.stop_targets.Count>20000)throw new InvalidDataException("AC6 radio index bounds");
  foreach(var r in radio.rules)if(r==null||r.event_id==0||r.target==0||r.media_id==0||(r.language!="ja"&&r.language!="enus")||!Hash(r.source_sha256))throw new InvalidDataException("AC6 radio identity");
  foreach(var e in radio.stop_targets){uint id;if(!uint.TryParse(e.Key,out id)||e.Value==null||e.Value.Count>1000)throw new InvalidDataException("AC6 radio stop map");}
 }
 public static void Capture(GamePreset p,string root){p.Version=3;p.Adapter=Adapter;p.Ac6=new Ac6PackInfo{PackId=Id,ExecutableSha256=GameHash};foreach(string n in Names){byte[] b=File.ReadAllBytes(Path.Combine(DirectoryFor(root),n));p.Assets.Add(new PresetAsset{Name=n,Sha256=Presets.Hash(b),Data=Convert.ToBase64String(b)});}}
}
}
