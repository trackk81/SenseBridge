using System;using System.Collections.Generic;using System.Drawing;using System.Globalization;using System.IO;using System.Web.Script.Serialization;using System.Windows.Forms;
namespace SenseBridge {
static class Ac6Support {
 public static string Config{get{return File.Exists(Path.Combine(Ac6Data.DirectoryFor(Store.Root),"manifest.json"))?Path.Combine(Ac6Data.DirectoryFor(Store.Root),"config.ini"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"SenseBridge","ac6-options.ini");}}
 public static bool Is(GameProfile g){return g!=null&&g.SteamAppId=="1888160"&&(File.Exists(Path.Combine(Store.Root,"profiles","ac6-local-support.marker"))||(g.Adapter==Ac6Data.Adapter&&File.Exists(Path.Combine(Ac6Data.DirectoryFor(Store.Root),"manifest.json"))));}
 public static void Apply(GameProfile g){var d=Store.ReadIni(Config);d["enabled"]="1";d["haptics"]=g.UseHaptics?"1":"0";d["triggers"]=g.UseTriggers?"1":"0";d["speaker"]=g.UseSpeaker?"1":"0";Store.WriteIni(Config,d);}
 public static void Stop(){if(File.Exists(Config)){var d=Store.ReadIni(Config);d["enabled"]="0";Store.WriteIni(Config,d);}}
}
public sealed class Ac6AudioForm:Form {
 public Ac6AudioForm(bool speaker){Text=L.T("상세 옵션");ClientSize=new Size(380,165);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(20,23,31);ForeColor=Color.WhiteSmoke;Font=new Font("Malgun Gothic",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
 var d=Store.ReadIni(Ac6Support.Config);string key=speaker?"speaker_volume":"gain";
 var title=new Label{Text=L.T(speaker?"스피커 음량":"햅틱 강도"),Bounds=new Rectangle(20,20,200,30)};
 var v=new NumericUpDown{Bounds=new Rectangle(230,20,125,30),DecimalPlaces=speaker?0:1,Increment=speaker?1:.1M,Minimum=0,Maximum=speaker?35:4,Value=(decimal)Store.Number(d,key,speaker?20:1.5M,0,speaker?35:4)};
 var b=new Button{Text=L.T("설정 적용"),Bounds=new Rectangle(225,95,130,35)};Controls.AddRange(new Control[]{title,v,b});b.Click+=delegate{var values=Store.ReadIni(Ac6Support.Config);values[key]=v.Value.ToString(CultureInfo.InvariantCulture);Store.WriteIni(Ac6Support.Config,values);Close();};
 }
}
public sealed class Ac6TriggerForm:Form {
 readonly ComboBox weapons=new ComboBox();readonly TrackBar start=new TrackBar();readonly Label value=new Label();
 readonly string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"SenseBridge","ac6-trigger-settings.json");
 readonly JavaScriptSerializer json=new JavaScriptSerializer(); bool loading;
 string T(string ko,string en,string ja){string l=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;return l=="ko"?ko:l=="ja"?ja:en;}
 Dictionary<string,Dictionary<string,int>> Read(){if(!File.Exists(path))return new Dictionary<string,Dictionary<string,int>>();return json.Deserialize<Dictionary<string,Dictionary<string,int>>>(File.ReadAllText(path))??new Dictionary<string,Dictionary<string,int>>();}
 string Key(){string text=weapons.Text.Split('|')[0].Trim();uint id;return uint.TryParse(text,out id)&&id>0?text:null;}
 public Ac6TriggerForm(){
 Text=T("아머드 코어 · 트리거 상세 옵션","Armored Core · Trigger details","アーマード・コア · トリガー詳細");ClientSize=new Size(540,260);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(20,23,31);ForeColor=Color.WhiteSmoke;Font=new Font("Segoe UI",11);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;
 weapons.Bounds=new Rectangle(24,22,490,32);weapons.Items.AddRange(new object[]{"110110000 | TURNER","110300000 | HU-BEN","110510000 | HALDEMAN","110720000 | COQUILLETT","111300000 | GU-Q1","120100000 | BU-TT/A","110010000","111100000"});Controls.Add(weapons);
 Controls.Add(new Label{Text=T("저항 시작 위치","Resistance start","抵抗の開始位置"),Bounds=new Rectangle(24,86,210,30)});
 start.Bounds=new Rectangle(235,78,240,48);start.Minimum=0;start.Maximum=90;start.TickStyle=TickStyle.None;start.SmallChange=1;start.LargeChange=5;Controls.Add(start);value.Bounds=new Rectangle(470,86,60,28);Controls.Add(value);
 Controls.Add(new Label{Text=T("0% = 당기기 시작 · 무기 ID 직접 입력 가능","0% = start of travel · weapon ID can be entered","0% = 引き始め · 武器IDを直接入力可能"),Bounds=new Rectangle(24,139,490,48)});
 var save=new Button{Text=T("적용","Apply","適用"),Bounds=new Rectangle(390,202,124,34)};Controls.Add(save);
 var reset=new Button{Text=T("기본값","Default","初期値"),Bounds=new Rectangle(250,202,124,34)};Controls.Add(reset);reset.Click+=delegate{start.Value=8;};
 start.ValueChanged+=delegate{value.Text=start.Value+"%";};
 weapons.SelectedIndexChanged+=delegate{LoadValue();};weapons.Leave+=delegate{LoadValue();};
 save.Click+=delegate{try{string key=Key();if(key==null)throw new InvalidDataException(T("올바른 무기 ID를 선택하세요.","Select a valid weapon ID.","正しい武器IDを選択してください。"));var d=Read();d[key]=new Dictionary<string,int>{{"start",start.Value}};Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,json.Serialize(d));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};
 weapons.SelectedIndex=0;
 }
 void LoadValue(){if(loading)return;loading=true;try{string key=Key();int n=8;Dictionary<string,int> v;int stored;if(key!=null&&Read().TryGetValue(key,out v)&&v!=null&&v.TryGetValue("start",out stored))n=Math.Max(0,Math.Min(90,stored));start.Value=n;value.Text=n+"%";}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}finally{loading=false;}}
}
}
