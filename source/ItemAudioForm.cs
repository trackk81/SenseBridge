using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
namespace SenseBridge {
sealed class AudioDetailsForm:Form {
 readonly bool speaker;ValueSlider amount;
 public AudioDetailsForm(bool isSpeaker){
  speaker=isSpeaker;Text=speaker?L.T("스피커 상세 옵션"):L.T("오디오 햅틱 상세 옵션");
  ClientSize=new Size(500,215);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
  Font=new Font("Malgun Gothic",10);BackColor=Color.FromArgb(24,28,40);ForeColor=Color.WhiteSmoke;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);AutoScaleMode=AutoScaleMode.Dpi;
  var d=Store.ReadIni(Store.Config);
  Controls.Add(new Label{Text=speaker?L.T("스피커 볼륨 (%)"):L.T("햅틱 강도"),Bounds=new Rectangle(24,20,450,28)});
  amount=new ValueSlider(speaker?100:4,speaker?Store.Number(d,"speaker_volume",25,0,100):Store.Number(d,"haptic_gain",2,0,4),speaker?0:2,speaker?"%":""){Bounds=new Rectangle(24,54,450,70)};Controls.Add(amount);
  var save=new Button{Text=L.T("저장"),Bounds=new Rectangle(264,156,95,34),FlatStyle=FlatStyle.Flat};var cancel=new Button{Text=L.T("닫기"),Bounds=new Rectangle(379,156,95,34),FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.Cancel};Controls.Add(save);Controls.Add(cancel);CancelButton=cancel;
  save.Click+=delegate{try{
   var v=Store.ReadIni(Store.Config);
   if(speaker){v["speaker_volume"]=amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);v["speaker_playback_mode"]="1";if(Store.Number(v,"speaker_mode",0,0,2)>0)v["speaker_mode"]="1";v["isolated_audio"]="1";v["triangle_clip"]="0";v["up_clip"]="1";}
   else{v["tactile_v2"]="1";v["auto_haptics"]="1";v["haptic_gain"]=amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);v["vial_haptics"]="1";v["visceral_haptics"]="1";v["visceral_gain"]="3";}

   Store.WriteIni(Store.Config,v);Store.Atomic(Path.Combine(Store.Root,@"profiles\bloodborne-settings.json"),Store.Json.Serialize(v));DialogResult=DialogResult.OK;Close();
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"SenseBridge");}};
 }
}
}

namespace SenseBridge {
sealed class TriggerDetailsForm:Form {
 public TriggerDetailsForm(){Text=L.T("트리거 상세 옵션");ClientSize=new Size(500,260);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;AutoScaleMode=AutoScaleMode.Dpi;BackColor=Color.FromArgb(24,28,40);ForeColor=Color.WhiteSmoke;Font=new Font("Malgun Gothic",10);
 Controls.Add(new Label{Text=L.T("저항 강도"),Bounds=new Rectangle(24,20,450,26)});
 var strength=new ValueSlider(80,Store.Number(Store.ReadIni(Store.Config),"trigger_strength",50,0,80),0,""){Bounds=new Rectangle(24,52,450,68)};Controls.Add(strength);
 Controls.Add(new Label{Text=L.T("0 = 저항 끄기 · 최대 80 · 기본 50"),Bounds=new Rectangle(24,128,450,26)});
 Controls.Add(new Label{Text=L.T("게임의 공격 입력 시점은 변경하지 않습니다."),Bounds=new Rectangle(24,157,450,26)});
 var reset=new Button{Text=L.T("기본값"),Bounds=new Rectangle(24,207,100,32)};reset.Click+=delegate{strength.Value=50;};Controls.Add(reset);
 var save=new Button{Text=L.T("저장"),Bounds=new Rectangle(264,207,95,32)};save.Click+=delegate{try{var v=Store.ReadIni(Store.Config);v["trigger_strength"]=strength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);Store.WriteIni(Store.Config,v);Store.Atomic(Path.Combine(Store.Root,@"profiles\bloodborne-settings.json"),Store.Json.Serialize(v));DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};Controls.Add(save);
 var close=new Button{Text=L.T("닫기"),Bounds=new Rectangle(379,207,95,32),DialogResult=DialogResult.Cancel};Controls.Add(close);CancelButton=close;
 }
}
}
