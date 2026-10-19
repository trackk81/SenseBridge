using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

// Generic audio-event transport. No game names, input remapping or hardware ownership.
[BepInPlugin("org.sensebridge.audioevents", "SenseBridge audio events", "0.1.0")]
public sealed class UnityBridge : BaseUnityPlugin {
 sealed class StateRule {public int Id;public string Action;}
 readonly Dictionary<string,StateRule> states=new Dictionary<string,StateRule>();Type actorType;
 sealed class Rule {public int Id,Frequency,Samples;public string Action;}
 static UnityBridge instance;
 readonly Dictionary<string,Rule> rules=new Dictionary<string,Rule>(StringComparer.Ordinal);
 readonly Dictionary<string,int> mapped=new Dictionary<string,int>();
 readonly Dictionary<int,int> seen=new Dictionary<int,int>();
 UdpClient udp; IPEndPoint endpoint; string token="",link,config=""; float reload; UnityEngine.Object input; string inputType="",actionField=""; AudioListener listener; Harmony harmony;
 void Awake(){
  instance=this;link=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"bridge-link.ini");
  harmony=new Harmony("org.sensebridge.audioevents");
  harmony.Patch(AccessTools.Method(typeof(AudioSource),"Play",Type.EmptyTypes),postfix:new HarmonyMethod(typeof(UnityBridge),"Played"));
  harmony.Patch(AccessTools.Method(typeof(AudioSource),"PlayOneShot",new[]{typeof(AudioClip),typeof(float)}),postfix:new HarmonyMethod(typeof(UnityBridge),"OneShot"));
  var stateType=AccessTools.TypeByName("HutongGames.PlayMaker.FsmState");if(stateType!=null)harmony.Patch(AccessTools.Method(stateType,"OnEnter",Type.EmptyTypes),postfix:new HarmonyMethod(typeof(UnityBridge),"StateEntered"));
  Logger.LogInfo("AudioSource Play/PlayOneShot event bridge ready; inactive until SenseBridge connects.");
 }
 static void Played(AudioSource __instance){if(instance!=null)instance.Emit(__instance,__instance.clip,1);}
 static void OneShot(AudioSource __instance,AudioClip clip,float volumeScale){if(instance!=null)instance.Emit(__instance,clip,volumeScale);}
 static Dictionary<string,string> Read(string path){var d=new Dictionary<string,string>();if(File.Exists(path))foreach(var s in File.ReadAllLines(path)){int i=s.IndexOf('=');if(i>0)d[s.Substring(0,i)]=s.Substring(i+1);}return d;}
 void Update(){
  if(Time.unscaledTime<reload)return;reload=Time.unscaledTime+.25f;
  try{
   var l=Read(link);string next;l.TryGetValue("config",out next);if(string.IsNullOrEmpty(next)){token="";return;}
   var initial=Read(next);if(!initial.ContainsKey("enabled")||initial["enabled"]!="1"){token="";return;}
   if(next!=config){
    config=next;rules.Clear();var c=Read(config);string manifest=c["rules"];string target=c["assembly_sha256"];
    using(var sha=SHA256.Create()){var actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"Managed/Assembly-CSharp.dll")))).Replace("-","").ToLowerInvariant();if(actual!=target)throw new InvalidDataException("Game build does not match imported pack");}
    foreach(var line in File.ReadAllLines(manifest)){var p=line.Split('\t');if(p.Length!=4&&p.Length!=5)throw new InvalidDataException("Rule columns");rules.Add(p[0],new Rule{Id=int.Parse(p[1]),Frequency=int.Parse(p[2]),Samples=int.Parse(p[3]),Action=p.Length==5?p[4]:""});}
    states.Clear();actorType=null;if(c.ContainsKey("actor_component")&&c["actor_component"]!="")actorType=Assembly.Load("Assembly-CSharp").GetType(c["actor_component"],false);if(actorType!=null&&!typeof(MonoBehaviour).IsAssignableFrom(actorType))throw new InvalidDataException("Invalid actor component");
    if(c.ContainsKey("states")&&File.Exists(c["states"])){int id=0;foreach(string line in File.ReadAllLines(c["states"])){if(line.Length==0)continue;var cells=line.Split('\t');if(cells.Length!=5)throw new InvalidDataException("State columns");states.Add(cells[0]+"\t"+cells[1],new StateRule{Id=id++,Action=cells[2]});}}
    Logger.LogInfo("State event rules: "+states.Count);
    Logger.LogInfo("Loaded external pack: "+rules.Count+" exact sound identities");
   }
   var d=Read(config);inputType=d.ContainsKey("input_component")?d["input_component"]:"";actionField=d.ContainsKey("action_set_field")?d["action_set_field"]:"";token=d.ContainsKey("token")?d["token"]:"";
   int port;if(token.Length!=32||!d.ContainsKey("enabled")||d["enabled"]!="1"||!int.TryParse(d["port"],out port)||port<1024||port>65535){token="";return;}
   endpoint=new IPEndPoint(IPAddress.Loopback,port);if(udp==null)udp=new UdpClient(AddressFamily.InterNetwork);
   foreach(var state in states.Values)if(!mapped.ContainsKey(state.Action))ResolveTrigger(state.Action);
   if(listener==null)listener=FindAnyObjectByType<AudioListener>();byte[] hello=Encoding.ASCII.GetBytes(token+" HELLO3 "+System.Diagnostics.Process.GetCurrentProcess().Id);udp.Send(hello,hello.Length,endpoint);
  }catch(Exception e){token="";config="";reload=Time.unscaledTime+5;Logger.LogWarning("Pack inactive: "+e.Message);}
 }
 void Emit(AudioSource source,AudioClip clip,float volume){
  // Do not alter game playback, even if a pack or transport fails.
  try{
   if(token.Length!=32||udp==null||!Application.isFocused||Time.timeScale<=0||AudioListener.pause||source==null||clip==null||!source.isActiveAndEnabled||source.mute||source.loop)return;
   Rule r;if(!rules.TryGetValue(clip.name,out r)||clip.frequency!=r.Frequency||Math.Abs(clip.samples-r.Samples)>1)return;
   float gain=source.volume*volume*AudioListener.volume,pitch=Math.Abs(source.pitch);if(float.IsNaN(gain)||float.IsInfinity(gain)||gain<=0||pitch<.25f||pitch>4)return;
   // Avoid multiplying one effect through duplicate wrappers in the same frame.
   int key=unchecked(source.GetInstanceID()*397^r.Id),frame=Time.frameCount,old;
   if(seen.TryGetValue(key,out old)&&old==frame)return;if(seen.Count>4096)seen.Clear();seen[key]=frame;
   if(source.spatialBlend>.01f){if(listener==null)return;float distance=Vector3.Distance(source.transform.position,listener.transform.position);if(distance>=source.maxDistance)return;gain*=Mathf.Clamp01(source.minDistance/Mathf.Max(source.minDistance,distance));}
   string packet=token+" "+r.Id+" "+Mathf.Clamp01(gain).ToString("R",CultureInfo.InvariantCulture)+" "+pitch.ToString("R",CultureInfo.InvariantCulture)+" "+ResolveTrigger(r.Action);
   byte[] bytes=Encoding.ASCII.GetBytes(packet);udp.Send(bytes,bytes.Length,endpoint);
  }catch{ /* A haptic transport failure must never break an AudioSource call. */ }
 }
 static void StateEntered(object __instance){if(instance!=null)instance.EmitState(__instance);}
 void EmitState(object state){
  try{
   if(token.Length!=32||udp==null||states.Count==0||actorType==null||!Application.isFocused||Time.timeScale<=0||AudioListener.pause)return;
   var type=state.GetType();var fsm=AccessTools.Property(type,"Fsm").GetValue(state,null);if(fsm==null)return;var ft=fsm.GetType();string name=(string)AccessTools.Property(type,"Name").GetValue(state,null),fsmName=(string)AccessTools.Property(ft,"Name").GetValue(fsm,null);StateRule rule;if(!states.TryGetValue(fsmName+"\t"+name,out rule))return;
   var owner=AccessTools.Property(ft,"GameObject").GetValue(fsm,null) as GameObject;if(owner==null||owner.GetComponentInParent(actorType)==null)return;
   int mask=ResolveTrigger(rule.Action);if(mask==0)return;byte[] packet=Encoding.ASCII.GetBytes(token+" STATE "+rule.Id+" "+mask);udp.Send(packet,packet.Length,endpoint);
  }catch{ /* State observation must not affect game state transitions. */ }
 }
 int ResolveTrigger(string action){
  if(string.IsNullOrEmpty(action)||string.IsNullOrEmpty(inputType)||string.IsNullOrEmpty(actionField))return 0;
  try{
   if(input==null){var type=Assembly.Load("Assembly-CSharp").GetType(inputType,false);if(type==null||!typeof(MonoBehaviour).IsAssignableFrom(type))return 0;input=FindAnyObjectByType(type);}
   if(input==null)return 0;var actions=AccessTools.Field(input.GetType(),actionField).GetValue(input);if(actions==null)return 0;
   var field=AccessTools.Field(actions.GetType(),action);if(field==null)return 0;var playerAction=field.GetValue(actions);if(playerAction==null||playerAction.GetType().FullName!="InControl.PlayerAction")return 0;
   int mask=BindingResolver.Mask(playerAction),previous;if(!mapped.TryGetValue(action,out previous)||previous!=mask){mapped[action]=mask;Logger.LogInfo("Trigger binding "+action+" mask="+mask+" (1=L2, 2=R2)");}return mask;
  }catch{return 0;}
 }
 void OnDestroy(){token="";if(udp!=null)udp.Close();if(harmony!=null)harmony.UnpatchSelf();if(instance==this)instance=null;}
}
