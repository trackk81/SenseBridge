using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace SenseBridge {
public sealed class PresetAsset {public string Name{get;set;}public string Sha256{get;set;}public string Data{get;set;}}
public sealed class GamePreset {
 public string Format{get;set;}public int Version{get;set;}public string Title{get;set;}public string Adapter{get;set;}public string SteamAppId{get;set;}
 public bool Triggers{get;set;}public bool Haptics{get;set;}public bool Speaker{get;set;}
 public UnityAudioPack Unity{get;set;}public Ac6PackInfo Ac6{get;set;}public string AdapterSha256{get;set;}public Dictionary<string,string> Settings{get;set;}public List<PresetAsset> Assets{get;set;}
}
public static class Presets {
 const int MaxJson=100*1024*1024;
 static readonly string[] Effects={"drink.wav","blood-collected.wav","est-drink.wav","heal-switch.wav","healing.wav","visceral-0.wav","visceral-1.wav","visceral-2.wav","visceral-3.wav","visceral-4.wav"};
 static readonly Dictionary<string,decimal[]> BloodRules=new Dictionary<string,decimal[]> {
  {"haptic_gain",new decimal[]{0,4,2}},{"trigger_strength",new decimal[]{0,80,50}},{"fire_percent",new decimal[]{35,85,55}},
  {"speaker_volume",new decimal[]{0,100,25}},{"visceral_sound_mask",new decimal[]{0,31,6}}
 };
 static readonly Dictionary<string,decimal[]> GlobalRules=new Dictionary<string,decimal[]> {
  {"gain",new decimal[]{0,4,2}},{"left_preferred_mode",new decimal[]{0,2,1}},{"right_preferred_mode",new decimal[]{0,2,1}},
  {"left_force",new decimal[]{0,8,3}},{"right_force",new decimal[]{0,8,3}}
 };
 static readonly Dictionary<string,decimal[]> EventRules=new Dictionary<string,decimal[]> {{"gain",new decimal[]{0,4,2}},{"speaker_volume",new decimal[]{0,100,25}},{"left_force",new decimal[]{0,8,3}},{"right_force",new decimal[]{0,8,3}}};
 static readonly Dictionary<string,decimal[]> Ac6Rules=new Dictionary<string,decimal[]> {{"gain",new decimal[]{0,4,1.5M}},{"speaker_volume",new decimal[]{0,35,20}},{"left_force",new decimal[]{0,8,3}},{"right_force",new decimal[]{0,8,3}}};
 static bool Unity(GamePreset p){return p.Adapter=="unity-audio-v1";}
 static bool Native(GamePreset p){return Ac6Data.Is(p);}
 static bool Blood(GamePreset p){return p.Adapter=="bloodborne-usb-v4";}
 static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=MaxJson,RecursionLimit=32};}
 public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 static string Runtime(string root){return Path.Combine(root,@"Bloodborne\runtime");}
 public static string ConfigPath(string root,GameProfile g){return g.Adapter==Ac6Data.Adapter?Path.Combine(Ac6Data.DirectoryFor(root),"config.ini"):EventPacks.Is(g)?EventPacks.Config(root,g):Store.Supported(g)?Path.Combine(Runtime(root),"dualsense.ini"):Path.Combine(root,"profiles","global-"+string.Concat(g.Id.Where(char.IsLetterOrDigit))+".ini");}
 static string[] Names(){return new[]{"auto-haptics.bin"}.Concat(Effects.Select(n=>"effects/"+n)).ToArray();}
 static string AssetPath(string root,string name){return Path.Combine(Runtime(root),name.Replace('/',Path.DirectorySeparatorChar));}
 public static GamePreset Capture(string root,GameProfile game){
  if(game==null)throw new InvalidDataException(L.T("게임을 선택해 주세요."));
  var p=new GamePreset{Format="SenseBridge.GamePreset",Version=EventPacks.Is(game)?2:1,Title=game.Name,Adapter=game.Adapter,SteamAppId=game.SteamAppId,Triggers=game.UseTriggers,Haptics=game.UseHaptics,Speaker=EventPacks.Dedicated(game)&&game.UseSpeaker,Settings=new Dictionary<string,string>(),Assets=new List<PresetAsset>()};
  var values=Store.ReadIni(ConfigPath(root,game));var rules=Blood(p)?BloodRules:Native(p)?Ac6Rules:Unity(p)?EventRules:GlobalRules;
  foreach(var rule in rules){string key=rule.Key;if(!values.ContainsKey(key)&&key.EndsWith("_preferred_mode")){string old=key.Replace("_preferred_mode","_mode");if(values.ContainsKey(old))values[key]=values[old];}p.Settings[key]=Store.Number(values,key,rule.Value[2],rule.Value[0],rule.Value[1]).ToString(CultureInfo.InvariantCulture);}
  if(Blood(p)){
   p.AdapterSha256=Hash(File.ReadAllBytes(Path.Combine(root,"SenseBridge.Bloodborne.dll")));
   foreach(string name in Names()){byte[] data=File.ReadAllBytes(AssetPath(root,name));p.Assets.Add(new PresetAsset{Name=name,Sha256=Hash(data),Data=Convert.ToBase64String(data)});}
  }
  if(Unity(p)){p.Unity=EventPacks.Manifest(root,game);byte[] data=File.ReadAllBytes(Path.Combine(EventPacks.DirectoryFor(root,game.PackId),"events.bin"));p.Assets.Add(new PresetAsset{Name="events.bin",Sha256=Hash(data),Data=Convert.ToBase64String(data)});}
  if(Native(p))Ac6Data.Capture(p,root);
  Validate(p,root);return p;
 }
 public static void Export(string root,GameProfile game,string path){var p=Capture(root,game);string text=Serializer().Serialize(p);if(text.Length>MaxJson)throw new InvalidDataException(L.T("프리셋 파일이 너무 큽니다."));Store.Atomic(Path.GetFullPath(path),text);}
 public static GamePreset Read(string path,string root){
  var info=new FileInfo(path);if(info.Length>MaxJson||info.Length==0)throw new InvalidDataException(L.T("프리셋 파일 크기가 올바르지 않습니다."));
  GamePreset p;try{p=Serializer().Deserialize<GamePreset>(File.ReadAllText(path));}catch(ArgumentException){throw new InvalidDataException(L.T("올바른 SenseBridge 프리셋 JSON이 아닙니다."));}
  Validate(p,root);return p;
 }
 public static void Validate(GamePreset p,string root){
  if(p==null||p.Format!="SenseBridge.GamePreset"||(p.Version!=1&&p.Version!=2&&p.Version!=3))throw new InvalidDataException(L.T("지원하지 않는 프리셋 형식 또는 버전입니다."));
  if(string.IsNullOrWhiteSpace(p.Title)||p.Title.Length>160||p.Title.Any(char.IsControl))throw new InvalidDataException(L.T("게임 제목이 올바르지 않습니다."));
  if(p.Adapter!="bloodborne-usb-v4"&&p.Adapter!="global-audio"&&!Unity(p)&&!Native(p))throw new InvalidDataException(L.T("지원하지 않는 게임 연결 모듈입니다."));
  if(!string.IsNullOrEmpty(p.SteamAppId)&&(p.SteamAppId.Length>12||p.SteamAppId.Any(c=>c<'0'||c>'9')))throw new InvalidDataException(L.T("Steam 게임 ID가 올바르지 않습니다."));
  if(!Blood(p)&&!Unity(p)&&!Native(p)&&p.Speaker)throw new InvalidDataException(L.T("공통 엔진은 이벤트 스피커 재생을 지원하지 않습니다."));
  var rules=Blood(p)?BloodRules:Native(p)?Ac6Rules:Unity(p)?EventRules:GlobalRules;
  if(p.Settings==null||p.Settings.Count!=rules.Count||p.Settings.Keys.Any(k=>!rules.ContainsKey(k)))throw new InvalidDataException(L.T("프리셋 설정 항목이 올바르지 않습니다."));
  foreach(var rule in rules){decimal v;if(!decimal.TryParse(p.Settings[rule.Key],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out v)||v<rule.Value[0]||v>rule.Value[1]||(!rule.Key.Contains("gain")&&decimal.Truncate(v)!=v))throw new InvalidDataException(L.T("프리셋 설정 값이 허용 범위를 벗어났습니다."));}
  if(p.Assets==null)throw new InvalidDataException(L.T("프리셋 리소스 목록이 없습니다."));
  if(Native(p)){Ac6Data.Validate(p);return;}
  if(p.Ac6!=null)throw new InvalidDataException("Unexpected AC6 metadata");
  if(Unity(p)){EventPacks.Validate(p);return;}
  if(p.Version!=1||p.Unity!=null)throw new InvalidDataException("Unexpected event pack metadata");
  if(!Blood(p)){if(p.Assets.Count!=0||!string.IsNullOrEmpty(p.AdapterSha256))throw new InvalidDataException(L.T("공통 프리셋에 전용 리소스를 포함할 수 없습니다."));return;}
  string dll=Path.Combine(root,"SenseBridge.Bloodborne.dll");
  if(!File.Exists(dll)||!string.Equals(Hash(File.ReadAllBytes(dll)),p.AdapterSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(L.T("이 프리셋과 호환되는 Bloodborne 연결 모듈이 필요합니다. 내보낸 PC와 같은 SenseBridge 버전을 사용하세요."));
  var names=Names();if(p.Assets.Count!=names.Length||p.Assets.Any(a=>a==null||!names.Contains(a.Name))||p.Assets.Select(a=>a.Name).Distinct().Count()!=names.Length)throw new InvalidDataException(L.T("프리셋 리소스 이름 또는 개수가 올바르지 않습니다."));
  long total=0;
  foreach(var a in p.Assets){
   if(a.Data==null||a.Data.Length>90*1024*1024)throw new InvalidDataException(L.T("프리셋 리소스가 너무 큽니다."));
   byte[] b=Convert.FromBase64String(a.Data);total+=b.Length;
   if(total>64*1024*1024||Hash(b)!=a.Sha256)throw new InvalidDataException(L.T("프리셋 리소스 무결성 검사에 실패했습니다."));
   if(a.Name=="auto-haptics.bin")ValidateBank(b,AssetPath(root,a.Name));else ValidateWave(b);
  }
 }
 static void ValidateBank(byte[] b,string installed){
  byte[] header=new byte[28];using(var f=File.OpenRead(installed)){if(f.Read(header,0,28)!=28)throw new InvalidDataException("Haptic manifest missing");}
  if(b.Length<28||!b.Take(28).SequenceEqual(header))throw new InvalidDataException(L.T("햅틱 캐시가 연결 모듈과 호환되지 않습니다."));
  using(var r=new BinaryReader(new MemoryStream(b))){r.BaseStream.Position=8;uint count=r.ReadUInt32();if(count>4096)throw new InvalidDataException("Haptic count");r.BaseStream.Position=28;
   for(uint i=0;i<count;i++){uint frames=r.ReadUInt32(),material=r.ReadUInt32();if(frames<1||frames>480000||material<1||material>10||r.BaseStream.Length-r.BaseStream.Position<frames*2)throw new InvalidDataException("Haptic clip bounds");for(uint n=0;n<frames;n++){short v=r.ReadInt16();if(v>4588||v< -4588)throw new InvalidDataException("Haptic amplitude limit");}}
   if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Haptic trailing data");
  }
 }
 static void ValidateWave(byte[] b){
  if(b.Length<44||b.Length>8000000||Encoding.ASCII.GetString(b,0,4)!="RIFF"||Encoding.ASCII.GetString(b,8,4)!="WAVE"||BitConverter.ToUInt32(b,4)!=b.Length-8)throw new InvalidDataException("Invalid WAV resource");
  bool format=false,data=false;uint rate=0;ushort align=0,formatType=0;
  for(long at=12;at+8<=b.Length;){uint n=BitConverter.ToUInt32(b,(int)at+4);long end=at+8+n;if(end>b.Length)throw new InvalidDataException("WAV chunk bounds");string id=Encoding.ASCII.GetString(b,(int)at,4);
   if(id=="fmt "){if(n<16||format)throw new InvalidDataException("WAV format");int o=(int)at+8;ushort type=BitConverter.ToUInt16(b,o),channels=BitConverter.ToUInt16(b,o+2),bits=BitConverter.ToUInt16(b,o+14);rate=BitConverter.ToUInt32(b,o+4);align=BitConverter.ToUInt16(b,o+12);if((type!=1&&type!=3)||channels<1||channels>2||rate<8000||rate>192000||(type==1&&bits!=16)||(type==3&&bits!=32)||align!=channels*bits/8)throw new InvalidDataException("WAV format");formatType=type;format=true;}
   if(id=="data"){if(data||!format||n==0||n%align!=0||n/(double)align/rate>10)throw new InvalidDataException("WAV duration");if(formatType==3)for(long j=at+8;j<end;j+=4){float v=BitConverter.ToSingle(b,(int)j);if(float.IsNaN(v)||float.IsInfinity(v)||v< -1||v>1)throw new InvalidDataException("WAV sample range");}data=true;}at=end+(n&1);
  }if(!format||!data)throw new InvalidDataException("WAV missing chunks");
 }
 public static GameProfile Find(List<GameProfile> games,GamePreset p){return games.FirstOrDefault(g=>(g.Adapter==p.Adapter||(Unity(p)||Native(p))&&g.Adapter=="global-audio")&&(Blood(p)||!string.IsNullOrEmpty(p.SteamAppId)&&g.SteamAppId==p.SteamAppId||string.IsNullOrEmpty(p.SteamAppId)&&string.Equals(g.Name,p.Title,StringComparison.OrdinalIgnoreCase)));}
 public static List<GameProfile> Import(string root,List<GameProfile> current,GamePreset p){
  Validate(p,root);
  var games=Store.Json.Deserialize<List<GameProfile>>(Store.Json.Serialize(current));var g=Find(games,p);
  if(g==null){g=new GameProfile{Id=Blood(p)?"bloodborne":Guid.NewGuid().ToString("N"),Adapter=p.Adapter};games.Add(g);}
  g.Adapter=p.Adapter;g.PackId=Unity(p)?p.Unity.PackId:Native(p)?Ac6Data.Id:null;g.Name=p.Title;g.SteamAppId=p.SteamAppId;g.UseTriggers=Native(p)?false:p.Triggers;g.UseHaptics=Native(p)?false:p.Haptics;g.UseSpeaker=Native(p)?false:p.Speaker;
  var cfg=new Dictionary<string,string>(p.Settings);cfg["enabled"]="0";cfg["triggers"]=p.Triggers?"1":"0";cfg["haptics"]=p.Haptics?"1":"0";
  if(Blood(p)){cfg["speaker_mode"]=p.Speaker?"1":"0";cfg["speaker_playback_mode"]="1";cfg["isolated_audio"]="1";cfg["triangle_clip"]="0";cfg["up_clip"]="1";cfg["tactile_v2"]="1";cfg["auto_haptics"]="1";cfg["vial_haptics"]="1";cfg["visceral_haptics"]="1";cfg["visceral_gain"]="3";cfg["material_audio"]="1";cfg["footstep_audio"]="1";}
  else if(Unity(p)||Native(p)){cfg["speaker"]=p.Speaker?"1":"0";if(Native(p)){cfg["triggers"]=cfg["haptics"]=cfg["speaker"]="0";}}
  else{cfg["source"]="";foreach(var side in new[]{"left","right"})cfg[side+"_mode"]=p.Triggers?cfg[side+"_preferred_mode"]:"0";}
  var writes=new Dictionary<string,byte[]>();var utf8=new UTF8Encoding(false);
  writes[ConfigPath(root,g)]=utf8.GetBytes(string.Join("\n",cfg.Select(k=>k.Key+"="+k.Value))+"\n");
  writes[Path.Combine(root,@"profiles\games.json")]=utf8.GetBytes(Store.Json.Serialize(games));
  if(Native(p)){foreach(var a in p.Assets)writes[Path.Combine(Ac6Data.DirectoryFor(root),a.Name)]=Convert.FromBase64String(a.Data);writes[Path.Combine(Ac6Data.DirectoryFor(root),"manifest.json")]=utf8.GetBytes(Store.Json.Serialize(p.Ac6));}
  if(Blood(p)){writes[Path.Combine(root,@"profiles\bloodborne-settings.json")]=utf8.GetBytes(Store.Json.Serialize(cfg));foreach(var a in p.Assets)writes[AssetPath(root,a.Name)]=Convert.FromBase64String(a.Data);}
  if(Unity(p)){string dir=EventPacks.DirectoryFor(root,g.PackId);writes[Path.Combine(dir,"manifest.json")]=utf8.GetBytes(Store.Json.Serialize(p.Unity));writes[Path.Combine(dir,"states.tsv")]=utf8.GetBytes(string.Join("\n",(p.Unity.StateRules??new List<TriggerStateRule>()).Select(r=>r.Fsm+"\t"+r.State+"\t"+r.Action+"\t"+r.Milliseconds+"\t"+r.Power)));writes[Path.Combine(dir,"events.bin")]=Convert.FromBase64String(p.Assets[0].Data);writes[Path.Combine(dir,"rules.tsv")]=utf8.GetBytes(string.Join("\n",p.Unity.Rules.Select(r=>r.Name+"\t"+r.Id+"\t"+r.Frequency+"\t"+r.Samples+"\t"+(r.TriggerAction??""))));}
  var backups=writes.Keys.ToDictionary(k=>k,k=>File.Exists(k)?File.ReadAllBytes(k):null);var done=new List<string>();
  try{foreach(var w in writes){Write(w.Key,w.Value);done.Add(w.Key);}}catch{foreach(string file in done.AsEnumerable().Reverse()){if(backups[file]==null)File.Delete(file);else Write(file,backups[file]);}throw;}
  return games;
 }
 static void Write(string path,byte[] data){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,data);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
}
