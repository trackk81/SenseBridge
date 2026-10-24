using System;using System.Diagnostics;using System.IO;using System.Threading;using System.Windows.Forms;using SenseBridge;
static class UpdaterProgram {
 [STAThread] static int Main(string[] args){
  string backup=null;try{
   if(args.Length!=1)throw new ArgumentException("Update plan required");
   string planFile=Path.GetFullPath(args[0]),work=Path.GetDirectoryName(planFile);
   if(!string.Equals(work.TrimEnd('\\'),AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Plan must be beside updater");
   var plan=UpdatePackage.Json().Deserialize<UpdatePlan>(File.ReadAllText(planFile));UpdatePackage.Validate(plan.Manifest);
   if(!string.Equals(Path.GetFullPath(plan.PayloadRoot),Path.Combine(work,"payload"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid staging folder");
   string root=Path.GetFullPath(plan.InstallRoot),exe=UpdatePackage.SafePath(root,"SenseBridge.exe");
   if(!File.Exists(exe)||root.StartsWith(work+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid install directory");
   Process parent=null;try{parent=Process.GetProcessById(plan.ParentPid);}catch(ArgumentException){}
   if(parent!=null)using(parent){if(parent.StartTime.ToUniversalTime().Ticks!=plan.ParentStartTicks||!string.Equals(parent.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update parent mismatch");if(!parent.WaitForExit(60000))throw new IOException("SenseBridge did not close. No files were changed.");}
   // Do not touch loaded modules belonging to another app or a restarted instance.
   foreach(var p in Process.GetProcessesByName("SenseBridge"))using(p){try{if(string.Equals(p.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase))throw new IOException("SenseBridge is still running");}catch(System.ComponentModel.Win32Exception){throw new IOException("Cannot verify running SenseBridge process");}}
   backup=Path.Combine(work,"backup");UpdatePackage.Apply(root,plan.PayloadRoot,backup,plan.Manifest);
   File.WriteAllText(Path.Combine(work,"result.txt"),"Updated to "+plan.Manifest.Version+"\nBackup: "+backup);
   Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=root});return 0;
  }catch(Exception e){string detail=e.Message+(backup==null?"":"\nBackup: "+backup);try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-error.txt"),e.ToString());}catch{}MessageBox.Show(detail,"SenseBridge update",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
 }
}
