using System;
using System.Drawing;
using System.Windows.Forms;
namespace SenseBridge {
sealed class SettingsForm:Form {
 ComboBox language;CheckBox debug;
 public bool ResetRequested;public string LanguageChoice{get{return new[]{"auto","ko","en","ja"}[language.SelectedIndex];}}public bool DebugChoice{get{return debug.Checked;}}
 public SettingsForm(){
  Text=L.T("프로그램 설정");ClientSize=new Size(440,290);Font=new Font("Malgun Gothic",10);AutoScaleMode=AutoScaleMode.Dpi;BackColor=Color.FromArgb(24,28,40);ForeColor=Color.WhiteSmoke;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
  Controls.Add(new Label{Text=L.T("언어"),Bounds=new Rectangle(24,28,130,30)});
  language=new ComboBox{Bounds=new Rectangle(165,24,250,30),DropDownStyle=ComboBoxStyle.DropDownList};language.Items.AddRange(new object[]{L.T("자동 (Windows)"),"한국어","English","日本語"});language.SelectedIndex=Math.Max(0,Array.IndexOf(new[]{"auto","ko","en","ja"},AppPreferences.Current.Language));Controls.Add(language);
  debug=new CheckBox{Text=L.T("디버그 실행 버튼 표시"),Checked=AppPreferences.Current.DebugButton,Bounds=new Rectangle(24,77,390,32)};Controls.Add(debug);
  var reset=new Button{Text=L.T("모든 설정 초기화"),Bounds=new Rectangle(24,131,390,36),FlatStyle=FlatStyle.Flat};Controls.Add(reset);
  Controls.Add(new Label{Text=L.T("전용 지원 게임은 유지됩니다. 추가한 게임·연결 경로·설정을 지웁니다. 게임 파일과 세이브는 유지됩니다."),Bounds=new Rectangle(24,177,390,50),Font=new Font("Malgun Gothic",9)});
  var save=new Button{Text=L.T("저장"),Bounds=new Rectangle(200,241,100,32),FlatStyle=FlatStyle.Flat};save.Click+=delegate{DialogResult=DialogResult.OK;Close();};Controls.Add(save);
  var cancel=new Button{Text=L.T("닫기"),Bounds=new Rectangle(314,241,100,32),FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;
  reset.Click+=delegate{if(MessageBox.Show(this,L.T("추가한 글로벌 게임과 모든 연결 경로·설정을 초기화할까요? 전용 지원 게임은 목록에 남으며, 효과는 중지됩니다."),L.T("모든 설정 초기화"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes){ResetRequested=true;DialogResult=DialogResult.OK;Close();}};
 }
}
}
