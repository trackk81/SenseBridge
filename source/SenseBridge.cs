using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SenseBridge {
public sealed class GameProfile {
 public string Id {get;set;} public string Name {get;set;} public string Executable {get;set;}
 public string Adapter {get;set;}
 public string EngineExecutable {get;set;}
 public string SteamAppId {get;set;} public string InstallDirectory {get;set;}
 public bool UseTriggers {get;set;} public bool UseHaptics {get;set;} public bool UseSpeaker {get;set;}
 public bool HasEffects {get{return UseTriggers||UseHaptics||(Store.Supported(this)&&UseSpeaker);}}
 public override string ToString(){return Name;}
}
public static class Store {
 public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
 public static readonly string AdapterRoot=Path.Combine(Root,"Bloodborne");
 public static readonly string Config=Path.Combine(AdapterRoot,@"runtime\dualsense.ini");
 public static readonly string Library=Path.Combine(Root,@"profiles\games.json");
 public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 public static bool Supported(GameProfile p){return p!=null && p.Adapter=="bloodborne-usb-v4";}
 public static List<GameProfile> BuiltInGames(){return new List<GameProfile>{new GameProfile{Id="bloodborne",Name="Bloodborne",Adapter="bloodborne-usb-v4"}};}
 public static List<GameProfile> EnsureBuiltInGames(List<GameProfile> games){
  if(games==null)games=new List<GameProfile>();
  foreach(var builtin in BuiltInGames())if(!games.Any(p=>p.Adapter==builtin.Adapter))games.Insert(0,builtin);
  return games;
 }
 public static List<GameProfile> LoadGames(){
  var list=EnsureBuiltInGames(File.Exists(Library)?Json.Deserialize<List<GameProfile>>(File.ReadAllText(Library)):null);
  SaveGames(list);return list;
 }
 public static void ResetSettings(string root,string config){
  string profiles=Path.Combine(Path.GetFullPath(root),"profiles");
  if(Directory.Exists(profiles))foreach(string file in Directory.GetFiles(profiles)){
   string name=Path.GetFileName(file);
   if(name=="games.json"||name=="app.json"||name=="bloodborne-settings.json"||name=="generic.ini"||name=="generic.ini.stop"||name.StartsWith("global-",StringComparison.OrdinalIgnoreCase)&&(name.EndsWith(".ini")||name.EndsWith(".ini.stop")))File.Delete(file);
  }
  Atomic(Path.Combine(profiles,"games.json"),Json.Serialize(BuiltInGames()));
  Atomic(config,"enabled=0\ntriggers=0\nhaptics=0\nhaptic_gain=2\ntrigger_strength=50\nfire_percent=55\nspeaker_mode=0\nspeaker_volume=25\nvial_haptics=1\nisolated_audio=1\ntriangle_clip=0\nup_clip=1\nvisceral_sound_mask=6\nvisceral_haptics=1\nspeaker_playback_mode=1\nvisceral_gain=3\n");
 }
 public static void SaveGames(List<GameProfile> list){Atomic(Library,Json.Serialize(list));}
 public static Dictionary<string,string> ReadIni(string path){
  var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  if(File.Exists(path))foreach(var line in File.ReadAllLines(path)){int i=line.IndexOf('=');if(i>0)d[line.Substring(0,i).Trim()]=line.Substring(i+1).Trim();}
  return d;
 }
 public static decimal Number(Dictionary<string,string>d,string k,decimal fallback,decimal min,decimal max){decimal v;return d.ContainsKey(k)&&decimal.TryParse(d[k],NumberStyles.Float,CultureInfo.InvariantCulture,out v)?Math.Max(min,Math.Min(max,v)):fallback;}
 public static void Atomic(string path,string text){
  Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  File.WriteAllText(tmp,text,new System.Text.UTF8Encoding(false));
  try {for(int i=0;;i++){try {if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);break;}catch(IOException){if(i==4)throw;Thread.Sleep(30);}}}
  finally {if(File.Exists(tmp))File.Delete(tmp);}
 }
 public static void WriteIni(string path,Dictionary<string,string>d){Atomic(path,string.Join(Environment.NewLine,d.Select(kv=>kv.Key+"="+kv.Value))+Environment.NewLine);}
 public static void Disable(){var d=ReadIni(Config);d["enabled"]="0";WriteIni(Config,d);}
}
public class Card:Panel {
 public Card(){DoubleBuffered=true;BackColor=Color.FromArgb(24,28,40);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var p=new Pen(Color.FromArgb(43,49,66)))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);}
}
public class TriggerDiagram:Control {
 public int Fire=55;
 public TriggerDiagram(){DoubleBuffered=true;BackColor=Color.FromArgb(24,28,40);}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  int l=20,r=Width-20,y=40;
  using(var track=new Pen(Color.FromArgb(62,68,88),8))g.DrawLine(track,l,y,r,y);
  using(var wall=new Pen(Color.FromArgb(139,122,255),8))g.DrawLine(wall,l+(r-l)*2/10,y,l+(r-l)*5/10,y);
  int x=l+(r-l)*Fire/100;
  using(var b=new SolidBrush(Color.FromArgb(82,222,191)))g.FillEllipse(b,x-6,y-6,12,12);
  using(var white=new SolidBrush(Color.FromArgb(220,225,241)))using(var font=new Font("Malgun Gothic",9)){
   g.DrawString(L.T("당김"),font,white,l,7);g.DrawString(L.T("발사 ")+Fire+"%",font,white,x-30,60);
  }
 }
}
public sealed class MainForm:Form {
 readonly Color bg=Color.FromArgb(15,18,27),fg=Color.FromArgb(235,238,249);
 GameGallery library; Form profileDialog; CheckBox triggers,haptics,audio; Button use,debug,choose,quit,hapticDetails,speakerDetails,add,steamAdd,settings; NotifyIcon tray; Label status,profileInfo;
 readonly System.Windows.Forms.Timer watch=new System.Windows.Forms.Timer{Interval=100};
 GameProfile selected; bool busy,active,exitRequested,loadingProfile,connectedGeneric; string connectedKey,blockedKey; readonly HashSet<string> attached=new HashSet<string>(); DateTime retryAfter; int observedPid=-1,profileRevision,observedRevision=-1;
 public MainForm():this(true){}
 public MainForm(bool promptOnShown){
  Text="SenseBridge 0.1.1";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  ClientSize=new Size(1040,640);MinimumSize=new Size(760,500);FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=true;
  StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=fg;
  Font=new Font("Malgun Gothic",11);AutoScaleMode=AutoScaleMode.Dpi;
  library=new GameGallery{Bounds=new Rectangle(16,70,1008,480),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};
  library.ItemActivated+=delegate{ShowGameOptions();};
  Controls.Add(library);library.SelectedIndexChanged+=delegate{SelectGame();};
  add=new RoundedButton{Text=L.T("+ 게임 추가"),Bounds=new Rectangle(530,20,160,34),Anchor=AnchorStyles.Top|AnchorStyles.Right,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Malgun Gothic",9)};add.Click+=delegate{AddGame();};Controls.Add(add);
  steamAdd=new RoundedButton{Text=L.T("Steam 게임 목록 추가"),Bounds=new Rectangle(704,20,250,34),Anchor=AnchorStyles.Top|AnchorStyles.Right,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Malgun Gothic",9)};steamAdd.Click+=delegate{ImportSteamGames();};Controls.Add(steamAdd);
  settings=new RoundedButton{Text="⚙",AccessibleName=L.T("프로그램 설정"),Bounds=new Rectangle(974,18,42,38),Anchor=AnchorStyles.Top|AnchorStyles.Right,FlatStyle=FlatStyle.Flat,ForeColor=fg,BackColor=bg,Font=new Font("Segoe UI Symbol",16),Cursor=Cursors.Hand};settings.FlatAppearance.BorderSize=0;settings.Click+=delegate{OpenSettings();};Controls.Add(settings);
  Controls.Add(new Label{Text="SENSEBRIDGE",Bounds=new Rectangle(24,22,280,36),ForeColor=fg,Font=new Font("Segoe UI",18,FontStyle.Bold)});
  triggers=Option(L.T("어댑티브 트리거"),260,38);
  haptics=Option(L.T("오디오 햅틱"),260,91);
  audio=Option(L.T("스피커 사용"),260,144);
  haptics.Width=200;audio.Width=200;
  hapticDetails=DetailsButton(94,false);speakerDetails=DetailsButton(147,true);
  use=new RoundedButton{Text=L.T("자동 적용 시작"),Bounds=new Rectangle(20,576,780,42),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(116,96,221),ForeColor=fg,Cursor=Cursors.Hand};
  use.FlatAppearance.BorderSize=0;use.Click+=delegate{UseGame();};Controls.Add(use);
  debug=new RoundedButton{Text=L.T("디버그 실행"),Bounds=new Rectangle(480,282,114,28),FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Malgun Gothic",9),Cursor=Cursors.Hand};
  debug.Click+=delegate{LaunchDebug();};Controls.Add(debug);
  quit=new RoundedButton{Text=L.T("종료하기"),Bounds=new Rectangle(820,576,200,42),Anchor=AnchorStyles.Bottom|AnchorStyles.Right,FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg};quit.Click+=delegate{ExitApp();};Controls.Add(quit);
  choose=new RoundedButton{Text=L.T("실행 파일 지정"),Bounds=new Rectangle(260,282,138,28),FlatStyle=FlatStyle.Flat,BackColor=bg,ForeColor=fg,Font=new Font("Malgun Gothic",9)};choose.Click+=delegate{ChooseExecutable();};Controls.Add(choose);
  status=new Label{Visible=false,Text="",Bounds=new Rectangle(20,550,1000,24),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Font=new Font("Malgun Gothic",9),ForeColor=fg,AutoEllipsis=true};Controls.Add(status);
  profileInfo=new Label{Bounds=new Rectangle(260,208,334,58),Font=new Font("Malgun Gothic",9),ForeColor=fg};Controls.Add(profileInfo);
  triggers.CheckedChanged+=delegate{SaveProfileOptions();};haptics.CheckedChanged+=delegate{SaveProfileOptions();};audio.CheckedChanged+=delegate{SaveProfileOptions();};
  status.Text=L.T("자동 적용 꺼짐");
  tray=new NotifyIcon{Icon=Icon,Text="SenseBridge",Visible=true};
  var menu=new ContextMenuStrip();menu.Items.Add(L.T("열기"),null,delegate{Show();WindowState=FormWindowState.Normal;Activate();});
  menu.Items.Add(L.T("종료"),null,delegate{ExitApp();});tray.ContextMenuStrip=menu;
  tray.DoubleClick+=delegate{Show();WindowState=FormWindowState.Normal;Activate();};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){
   if(!exitRequested&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
   exitRequested=true;active=false;watch.Stop();
   try{GenericEngine.Stop();Store.Disable();}catch(Exception ex){e.Cancel=true;exitRequested=false;Error(ex);return;}
   if(busy){e.Cancel=true;status.Text=L.T("연결 작업 종료 중");return;}
   watch.Dispose();tray.Visible=false;tray.Dispose();
  };
  watch.Tick+=delegate{CheckGame();};
  var startupGames=Store.LoadGames();SteamLibrary.Import(startupGames);Store.SaveGames(startupGames);foreach(var game in startupGames)library.Items.Add(game);
  if(library.Items.Count>0)library.SelectedIndex=0;else{use.Enabled=false;UpdateLayout();}
 }
 CheckBox Option(string label,int x,int y){var c=new CheckBox{Text=label,Bounds=new Rectangle(x,y,334,34),ForeColor=fg};Controls.Add(c);return c;}
 Button DetailsButton(int y,bool speaker){var b=new RoundedButton{Text=L.T("상세 옵션"),Bounds=new Rectangle(480,y,114,28),FlatStyle=FlatStyle.Flat,ForeColor=fg,BackColor=bg,Font=new Font("Malgun Gothic",9)};b.Click+=delegate{using(Form f=Store.Supported(selected)?(Form)new AudioDetailsForm(speaker):new GenericForm(GlobalConfig(selected))){f.ShowDialog(profileDialog??this);}};Controls.Add(b);return b;}
 void ShowGameOptions(){
  if(selected==null||profileDialog!=null)return;
  var controls=new Control[]{triggers,haptics,audio,hapticDetails,speakerDetails,choose,debug,profileInfo};
  using(var f=new Form{Text=selected.Name,ClientSize=new Size(500,320),BackColor=bg,ForeColor=fg,Font=Font,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,AutoScaleMode=AutoScaleMode.None,Icon=Icon}){
   profileDialog=f;float scale;using(var g=CreateGraphics())scale=g.DpiX/96f;
   f.ClientSize=new Size((int)(500*scale),(int)(320*scale));
   Rectangle[] bounds={new Rectangle(24,26,300,36),new Rectangle(24,82,300,36),new Rectangle(24,138,300,36),new Rectangle(350,84,126,32),new Rectangle(350,140,126,32),new Rectangle(24,212,210,34),new Rectangle(280,212,196,34),new Rectangle(24,180,452,28)};
   for(int i=0;i<controls.Length;i++){f.Controls.Add(controls[i]);var r=bounds[i];controls[i].Bounds=new Rectangle((int)(r.X*scale),(int)(r.Y*scale),(int)(r.Width*scale),(int)(r.Height*scale));}
   var close=new RoundedButton{Text=L.T("닫기"),BackColor=bg,ForeColor=fg,FlatStyle=FlatStyle.Flat,Bounds=new Rectangle((int)(350*scale),(int)(270*scale),(int)(126*scale),(int)(32*scale)),DialogResult=DialogResult.OK};f.Controls.Add(close);f.CancelButton=close;
   UpdateLayout();try{f.ShowDialog(this);}finally{profileDialog=null;foreach(var c in controls){c.Visible=false;Controls.Add(c);}UpdateLayout();}
  }
 }
 void ImportSteamGames(){
  if(busy)return;
  try{
   var games=library.Items.Cast<GameProfile>().ToList();int before=games.Count;
   SteamLibrary.Import(games);Store.SaveGames(games);profileRevision++;
   foreach(var game in games)if(!library.Items.Contains(game))library.Items.Add(game);
   if(selected==null&&library.Items.Count>0)library.SelectedIndex=0;
   library.Invalidate();UpdateLayout();
   if(!active)status.Text=L.T("Steam 목록 확인 완료 · 새 항목: ")+(games.Count-before);
  }catch(Exception ex){Error(ex);}
 }
 void AddGame(){
  if(busy)return;
  using(var f=new OpenFileDialog{Title=L.T("추가할 게임 실행 파일 선택"),Filter=L.T("실행 파일 (*.exe)|*.exe"),CheckFileExists=true}){
   if(f.ShowDialog(this)!=DialogResult.OK)return;
   string path=Path.GetFullPath(f.FileName);
   foreach(GameProfile p in library.Items)if(string.Equals(p.EngineExecutable,path,StringComparison.OrdinalIgnoreCase)){library.SelectedItem=p;return;}
   if(string.Equals(Path.GetFileName(path),"bb-probe.exe",StringComparison.OrdinalIgnoreCase)){
    try{LiveAttach.ValidateExecutable(path);var bb=library.Items.Cast<GameProfile>().FirstOrDefault(Store.Supported);if(bb==null){bb=new GameProfile{Id="bloodborne",Name="Bloodborne",Adapter="bloodborne-usb-v4",Executable=path};library.Items.Add(bb);}if(bb!=null){bb.EngineExecutable=path;Store.SaveGames(library.Items.Cast<GameProfile>().ToList());profileRevision++;library.SelectedItem=bb;UpdateLayout();return;}}catch(Exception ex){Error(ex);return;}
   }
   string name=Path.GetFileNameWithoutExtension(path);try{var info=FileVersionInfo.GetVersionInfo(path);if(!string.IsNullOrWhiteSpace(info.ProductName))name=info.ProductName;}catch{}
   var game=new GameProfile{Id=Guid.NewGuid().ToString("N"),Name=name,Executable=path,EngineExecutable=path,Adapter="global-audio"};
   library.Items.Add(game);Store.SaveGames(library.Items.Cast<GameProfile>().ToList());profileRevision++;library.SelectedItem=game;
  }
 }
 void SelectGame(){
  selected=library.SelectedItem as GameProfile;loadingProfile=true;
  try{triggers.Checked=selected!=null&&selected.UseTriggers;haptics.Checked=selected!=null&&selected.UseHaptics;audio.Checked=selected!=null&&selected.UseSpeaker;}finally{loadingProfile=false;}
  UpdateLayout();
 }
 void SaveProfileOptions(){
  if(loadingProfile||selected==null)return;
  selected.UseTriggers=triggers.Checked;selected.UseHaptics=haptics.Checked;selected.UseSpeaker=Store.Supported(selected)&&audio.Checked;library.RefreshCards();
  try{Store.SaveGames(library.Items.Cast<GameProfile>().ToList());profileRevision++;if(active)CheckGame();}catch(Exception ex){Error(ex);}
 }
 static string GlobalConfig(GameProfile p){return Path.Combine(Store.Root,@"profiles\global-"+string.Concat(p.Id.Where(char.IsLetterOrDigit))+".ini");}
 static bool Configured(GameProfile p){return p!=null&&(SteamLibrary.Ready(p)||!string.IsNullOrEmpty(p.EngineExecutable)&&File.Exists(p.EngineExecutable));}
 bool Configured(){return Configured(selected);}
 void UpdateLayout(){
  bool configured=profileDialog!=null&&Configured(),bb=Store.Supported(selected);triggers.Visible=haptics.Visible=configured;use.Visible=true;
  audio.Visible=speakerDetails.Visible=configured&&bb;debug.Visible=configured&&bb&&AppPreferences.Current.DebugButton;hapticDetails.Visible=configured;
  choose.Visible=profileDialog!=null&&selected!=null;
  choose.Text=Configured()?L.T("실행 파일 변경"):L.T("실행 파일 지정");
  profileInfo.Visible=profileDialog!=null&&!Configured();
  if(profileInfo!=null)profileInfo.Text=selected==null?L.T("게임을 추가해 주세요."):!Configured()?L.T("실행 파일을 지정해 주세요."):L.T("체크한 기능은 자동 저장됩니다.");
 }
 void ChooseExecutable(){
  if(busy||selected==null)return;
  using(var dialog=new OpenFileDialog{Title=Store.Supported(selected)?L.T("Bloodborne의 bin\\bb-probe.exe 지정"):L.T("게임 실행 파일 선택"),Filter=Store.Supported(selected)?L.T("Bloodborne 실행 파일 (bb-probe.exe)|bb-probe.exe"):L.T("실행 파일 (*.exe)|*.exe"),CheckFileExists=true,FileName=Store.Supported(selected)?"bb-probe.exe":""}){
   string previous=selected.EngineExecutable;
   string initial=SteamLibrary.Ready(selected)?selected.InstallDirectory:!string.IsNullOrEmpty(previous)?Path.GetDirectoryName(previous):(!string.IsNullOrEmpty(selected.Executable)?Path.Combine(Path.GetDirectoryName(selected.Executable),"bin"):null);
   if(Directory.Exists(initial))dialog.InitialDirectory=initial;
   if(dialog.ShowDialog(profileDialog??this)!=DialogResult.OK)return;
   try{if(Store.Supported(selected))LiveAttach.ValidateExecutable(dialog.FileName);selected.EngineExecutable=Path.GetFullPath(dialog.FileName);Store.SaveGames(library.Items.Cast<GameProfile>().ToList());profileRevision++;UpdateLayout();}
   catch(Exception ex){selected.EngineExecutable=previous;Error(ex);}
  }
 }
 void OpenSettings(){
  if(busy)return;
  watch.Stop();
  using(var dialog=new SettingsForm()){
   dialog.FormClosed+=delegate{if(active&&!exitRequested)watch.Start();};
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   try{
    if(dialog.ResetRequested){
     watch.Stop();active=false;GenericEngine.Stop();Store.Disable();connectedKey=blockedKey=null;retryAfter=DateTime.MinValue;
     Store.ResetSettings(Store.Root,Store.Config);
     AppPreferences.Current=new AppPreferences();L.Change();selected=null;library.Items.Clear();loadingProfile=true;triggers.Checked=haptics.Checked=audio.Checked=false;loadingProfile=false;attached.Clear();
     use.Text=L.T("자동 적용 시작");status.Text=L.T("자동 적용 꺼짐");foreach(var game in Store.LoadGames())library.Items.Add(game);if(library.Items.Count>0)library.SelectedIndex=0;UpdateLayout();
    }else{AppPreferences.Current.Language=dialog.LanguageChoice;AppPreferences.Current.DebugButton=dialog.DebugChoice;AppPreferences.Current.Save();L.Change();UpdateLayout();}
    L.Refresh(this);settings.AccessibleName=L.T("프로그램 설정");library.Invalidate();
    tray.ContextMenuStrip.Items[0].Text=L.T("열기");tray.ContextMenuStrip.Items[1].Text=L.T("종료");
   }catch(Exception ex){Error(ex);}
  }
 }
 void ExitApp(){exitRequested=true;Close();}
 void StopEffects(){GenericEngine.Stop();Store.Disable();connectedKey=null;}
 void UseGame(){
  if(busy)return;
  try{if(active){watch.Stop();StopEffects();active=false;use.Text=L.T("자동 적용 시작");status.Text=L.T("자동 적용 꺼짐");}
  else{active=true;observedPid=-1;retryAfter=DateTime.MinValue;use.Text=L.T("자동 적용 중지");watch.Start();CheckGame();}}catch(Exception ex){Error(ex);}
 }
 async void CheckGame(){
  if(!active||busy||exitRequested)return;
  int foreground=ProfileRouter.ForegroundPid();
  if(foreground!=observedPid)retryAfter=DateTime.MinValue;
  if(DateTime.UtcNow<retryAfter)return;
  if(foreground==observedPid&&observedRevision==profileRevision&&(connectedKey==null||!connectedGeneric||GenericEngine.Running))return;
  observedPid=foreground;observedRevision=profileRevision;
  status.Visible=false;
  busy=true; // Re-entry guard only: polling must not toggle UI enabled states.
  GameSession session=null;
  try{
   session=ProfileRouter.FindForeground(library.Items.Cast<GameProfile>().ToList(),foreground);
   if(session==null||!session.Profile.HasEffects){if(connectedKey!=null||GenericEngine.Running)StopEffects();status.Text=L.T("자동 적용 대기 · 체크한 게임만 사용");return;}
   var profile=session.Profile;var game=session.Process;string key=session.Key;
   string signature=key+":"+profile.UseTriggers+":"+profile.UseHaptics+":"+profile.UseSpeaker;
   if(signature!=connectedKey){StopEffects();ApplyOptions(profile);}
   if(!Store.Supported(profile)){
    if(!GenericEngine.Running){GenericEngine.Config=GlobalConfig(profile);status.Text=L.T("글로벌 엔진 연결 중");await Task.Run(()=>GenericEngine.Start());}
   }else if(!attached.Contains(key)){
    if(key==blockedKey){Store.Disable();return;}
    status.Text=L.T("게임 연결 중");
    try{await Task.Run(()=>LiveAttach.Connect(game));attached.Add(key);}catch(TimeoutException){blockedKey=key;throw;}
   }
   if(exitRequested||!active||ProfileRouter.ForegroundPid()!=foreground||profileRevision!=observedRevision){StopEffects();observedPid=-1;return;}
   connectedGeneric=!Store.Supported(profile);connectedKey=signature;status.Text=L.T("자동 적용 중: ")+profile.Name;
  }catch(Exception ex){try{StopEffects();}catch{}if(!IsDisposed){status.Visible=true;status.Text=ex.Message;observedRevision=-1;retryAfter=DateTime.UtcNow.AddSeconds(5);}}
  finally{if(session!=null)session.Dispose();busy=false;if(!IsDisposed&&exitRequested)Close();}
 }
 void ApplyOptions(GameProfile profile){
   if(!Store.Supported(profile)){
    var path=GlobalConfig(profile);var v=Store.ReadIni(path);v["triggers"]=profile.UseTriggers?"1":"0";v["haptics"]=profile.UseHaptics?"1":"0";
    foreach(string side in new[]{"left","right"}){if(!v.ContainsKey(side+"_preferred_mode"))v[side+"_preferred_mode"]=Store.Number(v,side+"_mode",1,0,2).ToString();v[side+"_mode"]=profile.UseTriggers?v[side+"_preferred_mode"]:"0";}
    Store.WriteIni(path,v);return;
   }
   var d=Store.ReadIni(Store.Config);d["enabled"]=profile.HasEffects?"1":"0";d["triggers"]=profile.UseTriggers?"1":"0";d["haptics"]=profile.UseHaptics?"1":"0";
   if(!d.ContainsKey("vial_haptics"))d["vial_haptics"]="1";
   d["speaker_mode"]=profile.UseSpeaker?"1":"0";d["speaker_playback_mode"]="1";d["isolated_audio"]="1";d["triangle_clip"]="0";d["up_clip"]="1";
   Store.WriteIni(Store.Config,d);Store.Atomic(Path.Combine(Store.Root,@"profiles\bloodborne-settings.json"),Store.Json.Serialize(d));
 }
 void LaunchDebug(){
  if(busy||!Store.Supported(selected))return;
  try{
   using(var game=LiveAttach.Find(selected)){if(game!=null){MessageBox.Show(this,L.T("현재 게임을 정상 종료한 뒤 디버그 실행을 눌러 주세요."),"SenseBridge",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}}
   string script=Path.Combine(Store.AdapterRoot,@"tools\start.ps1");
   if(!File.Exists(script))throw new FileNotFoundException(L.T("디버그 실행 파일이 없습니다."),script);
   if(!active)UseGame();
   Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
  }catch(Exception ex){Error(ex);}
 }
 void Error(Exception ex){MessageBox.Show(this,ex.Message,"SenseBridge",MessageBoxButtons.OK,MessageBoxIcon.Information);}
}
static class Program {
 [STAThread] static void Main(string[] args){
  if(args.Contains("--self-test")){SelfTest();return;}
  bool created;using(var mutex=new Mutex(true,"Local\\SenseBridge.Desktop",out created)){
   if(!created){MessageBox.Show(L.T("SenseBridge가 이미 실행 중입니다. 작업 표시줄 트레이에서 열어 주세요."),"SenseBridge");return;}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{Application.Run(new MainForm());}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"error.log"),ex.ToString());MessageBox.Show(ex.Message,L.T("SenseBridge 실행 오류"));}
  }
 }
 static void SelfTest(){string dir=Path.Combine(Path.GetTempPath(),"sensebridge-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{string p=Path.Combine(dir,"config.ini");Store.Atomic(p,"enabled=1\nfire_percent=55\nfuture_option=keep\n");var d=Store.ReadIni(p);d["enabled"]="0";Store.WriteIni(p,d);var r=Store.ReadIni(p);if(r["future_option"]!="keep"||r["enabled"]!="0"||Store.Number(r,"fire_percent",1,35,85)!=55)throw new Exception("Config roundtrip failed");r["fire_percent"]="1000";if(Store.Number(r,"fire_percent",55,35,85)!=85)throw new Exception("Clamp failed");if(Store.Supported(new GameProfile{Adapter="pending"}))throw new Exception("Unimplemented adapter enabled");var list=new List<GameProfile>{new GameProfile{Id="test",Name=L.T("테스트 게임"),Executable=@"C:\Game Folder\game.exe",Adapter="pending"}};var round=Store.Json.Deserialize<List<GameProfile>>(Store.Json.Serialize(list));if(round[0].Name!=list[0].Name||round[0].Executable!=list[0].Executable)throw new Exception("Profile roundtrip failed");File.WriteAllText(Path.Combine(Store.Root,"self-test.txt"),"PASS: atomic settings/preservation, bounds, unsupported adapter guard, Korean profile roundtrip");}finally{Directory.Delete(dir,true);}}
}
}
