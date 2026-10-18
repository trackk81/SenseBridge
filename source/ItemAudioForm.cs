using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
namespace SenseBridge {
sealed class AudioDetailsForm:Form {
 readonly bool speaker;NumericUpDown amount;
 public AudioDetailsForm(bool isSpeaker){
  speaker=isSpeaker;Text=speaker?L.T("스피커 상세 옵션"):L.T("오디오 햅틱 상세 옵션");
  ClientSize=new Size(430,135);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
  Font=new Font("Malgun Gothic",10);BackColor=Color.FromArgb(24,28,40);ForeColor=Color.WhiteSmoke;Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);AutoScaleMode=AutoScaleMode.Dpi;
  var d=Store.ReadIni(Store.Config);
  Controls.Add(new Label{Text=speaker?L.T("스피커 볼륨 (%)"):L.T("햅틱 강도"),Bounds=new Rectangle(24,26,235,28)});
  amount=new NumericUpDown{Bounds=new Rectangle(285,22,120,30),Minimum=0,Maximum=speaker?100:4,DecimalPlaces=speaker?0:2,Increment=speaker?5:.25m,Value=speaker?Store.Number(d,"speaker_volume",25,0,100):Store.Number(d,"haptic_gain",2,0,4)};Controls.Add(amount);
  var save=new Button{Text=L.T("저장"),Bounds=new Rectangle(205,78,95,34),FlatStyle=FlatStyle.Flat};var cancel=new Button{Text=L.T("닫기"),Bounds=new Rectangle(310,78,95,34),FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.Cancel};Controls.Add(save);Controls.Add(cancel);CancelButton=cancel;
  save.Click+=delegate{try{
   var v=Store.ReadIni(Store.Config);
   if(speaker){v["speaker_volume"]=amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);v["speaker_playback_mode"]="1";if(Store.Number(v,"speaker_mode",0,0,2)>0)v["speaker_mode"]="1";v["isolated_audio"]="1";v["triangle_clip"]="0";v["up_clip"]="1";}
   else{v["tactile_v2"]="1";v["auto_haptics"]="1";v["haptic_gain"]=amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);v["vial_haptics"]="1";v["visceral_haptics"]="1";v["visceral_gain"]="3";}

   Store.WriteIni(Store.Config,v);Store.Atomic(Path.Combine(Store.Root,@"profiles\bloodborne-settings.json"),Store.Json.Serialize(v));DialogResult=DialogResult.OK;Close();
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"SenseBridge");}};
 }
}
}
