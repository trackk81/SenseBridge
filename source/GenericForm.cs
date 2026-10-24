using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace SenseBridge {
static class GenericEngine {
 public static Process Worker; public static string Last=L.T("공통 엔진 대기 중");static volatile bool ready;
 public static string Config=Path.Combine(Store.Root,@"profiles\generic.ini");
 public static bool Running {get {try{return Worker!=null&&!Worker.HasExited;}catch{return false;}}}
 public static ProcessStartInfo Info(string args){var p=new ProcessStartInfo(Path.Combine(Store.Root,"SenseBridge.Engine.exe"),args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};p.WorkingDirectory=Store.Root;string toolchain=Environment.GetEnvironmentVariable("SENSEBRIDGE_TOOLCHAIN");p.EnvironmentVariables["PATH"]=Store.Root+";"+(!string.IsNullOrEmpty(toolchain)?Path.Combine(toolchain,"bin")+";":"")+Environment.GetEnvironmentVariable("PATH");return p;}
 public static void Stop(){if(Running){File.WriteAllText(Config+".stop","");if(!Worker.WaitForExit(2500))throw new Exception(L.T("공통 엔진 종료를 기다리는 중입니다. 다시 중지해 주세요."));}if(Worker!=null){Worker.Dispose();Worker=null;}Last=L.T("공통 엔진 중지됨 · 트리거 해제");}
 public static void Start(){Stop();Store.Disable();System.Threading.Thread.Sleep(600);if(File.Exists(Config+".stop"))File.Delete(Config+".stop");ready=false;Last=L.T("USB 컨트롤러와 오디오 연결 중…");Worker=new Process{StartInfo=Info("\""+Config+"\" "+Process.GetCurrentProcess().Id)};Worker.OutputDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null){Last=e.Data;if(e.Data.StartsWith("READY |"))ready=true;}};Worker.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null)Last=e.Data;};Worker.Start();Worker.BeginOutputReadLine();Worker.BeginErrorReadLine();var deadline=DateTime.UtcNow.AddSeconds(5);while(!ready&&Running&&DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(50);if(!ready){string failure=Last;Stop();throw new Exception(failure);}}
}
sealed class GenericForm:Form {
 ComboBox source,left,right;ValueSlider gain,lf,rf;Label status;
 sealed class Output {public string Id,Name;public override string ToString(){return Name;}}
 public GenericForm(string config=null){if(config==null)config=GenericEngine.Config;Text=L.T("글로벌 상세 옵션");ClientSize=new Size(600,470);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(24,28,40);ForeColor=Color.WhiteSmoke;Font=new Font("Malgun Gothic",10);AutoScaleMode=AutoScaleMode.Dpi;
  Label(L.T("오디오 출력 장치"),24,22,450,26);source=Combo(24,52,552);source.Items.Add(new Output{Id="",Name=L.T("Windows 기본 출력 장치")});source.SelectedIndex=0;
  Label(L.T("선택한 장치의 전체 소리에 반응합니다."),24,92,552,25);
  Label(L.T("햅틱 강도"),24,138,200,26);gain=Number(230,126,0,4,2);gain.Width=346;
  Label(L.T("L2 트리거"),24,188,150,26);Label(L.T("R2 트리거"),310,188,150,26);
  left=Combo(24,218,180);right=Combo(310,218,180);foreach(var c in new[]{left,right})c.Items.AddRange(new object[]{L.T("끄기"),L.T("지속 저항"),L.T("걸림·끊김")});lf=Number(24,258,0,8,3);rf=Number(310,258,0,8,3);lf.Width=266;rf.Width=266;
  Label(L.T("일반 게임의 발사 지점은 변경하지 않습니다."),24,336,552,26);
  status=Label("",24,371,552,26);
  var save=new Button{Text=L.T("저장"),Bounds=new Rectangle(356,418,105,32),FlatStyle=FlatStyle.Flat};Controls.Add(save);
  var close=new Button{Text=L.T("닫기"),Bounds=new Rectangle(471,418,105,32),FlatStyle=FlatStyle.Flat,DialogResult=DialogResult.Cancel};Controls.Add(close);CancelButton=close;
  var d=Store.ReadIni(config);gain.Value=Store.Number(d,"gain",2,0,4);
  left.SelectedIndex=(int)Store.Number(d,"left_preferred_mode",Store.Number(d,"left_mode",1,0,2),0,2);right.SelectedIndex=(int)Store.Number(d,"right_preferred_mode",Store.Number(d,"right_mode",1,0,2),0,2);lf.Value=Store.Number(d,"left_force",3,0,8);rf.Value=Store.Number(d,"right_force",3,0,8);
  save.Click+=delegate{try{var v=Store.ReadIni(config);string old=v.ContainsKey("source")?v["source"]:"";v["source"]=((Output)source.SelectedItem).Id;v["gain"]=gain.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);v["left_preferred_mode"]=left.SelectedIndex.ToString();v["right_preferred_mode"]=right.SelectedIndex.ToString();bool enabled=Store.Number(v,"triggers",1,0,1)>0;v["left_mode"]=enabled?v["left_preferred_mode"]:"0";v["right_mode"]=enabled?v["right_preferred_mode"]:"0";v["left_force"]=lf.Value.ToString();v["right_force"]=rf.Value.ToString();Store.WriteIni(config,v);if(old!=v["source"]&&GenericEngine.Running&&GenericEngine.Config==config)GenericEngine.Stop();DialogResult=DialogResult.OK;Close();}catch(Exception ex){status.Text=ex.Message;}};
  Shown+=async delegate{try{
   string result=await Task.Run(()=>{using(var p=Process.Start(GenericEngine.Info("--devices"))){string text=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(L.T("오디오 장치 확인 실패"));return text;}});
   if(IsDisposed)return;
   foreach(var line in result.Split('\n')){var pair=line.Trim().Split('\t');if(pair.Length==2){var item=new Output{Id=pair[0],Name=pair[1]};source.Items.Add(item);if(d.ContainsKey("source")&&d["source"]==item.Id)source.SelectedItem=item;}}
   if(d.ContainsKey("source")&&d["source"]!=""&&source.SelectedIndex==0){source.Items.Add(new Output{Id=d["source"],Name=L.T("연결되지 않은 저장 장치")});source.SelectedIndex=source.Items.Count-1;}
  }catch(Exception ex){if(!IsDisposed)status.Text=ex.Message;}};
 }
 Label Label(string t,int x,int y,int w,int h){var c=new Label{Text=L.T(t),Bounds=new Rectangle(x,y,w,h)};Controls.Add(c);return c;}
 ComboBox Combo(int x,int y,int w){var c=new ComboBox{Bounds=new Rectangle(x,y,w,30),DropDownStyle=ComboBoxStyle.DropDownList};Controls.Add(c);return c;}
 ValueSlider Number(int x,int y,int min,int max,int v){var c=new ValueSlider(max,v,max==4?2:0,""){Bounds=new Rectangle(x,y,266,68)};Controls.Add(c);return c;}
}
}
