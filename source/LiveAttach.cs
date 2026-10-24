using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace SenseBridge {
static class LiveAttach {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int id);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint type,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr h,IntPtr a,UIntPtr n,uint type);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr written);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateRemoteThread(IntPtr h,IntPtr attr,UIntPtr stack,IntPtr start,IntPtr arg,uint flags,IntPtr id);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint timeout);
 [DllImport("kernel32.dll")]static extern bool GetExitCodeThread(IntPtr h,out uint result);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr h,string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibraryEx(string name,IntPtr file,uint flags);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool GetModuleHandleEx(uint flags,IntPtr address,out IntPtr module);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetModuleFileName(IntPtr module,StringBuilder path,int size);
 static Exception Native(string action){return new Exception(action+": "+new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);}
 static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static IntPtr RemoteModule(Process p,string name){p.Refresh();foreach(ProcessModule m in p.Modules)if(string.Equals(m.ModuleName,name,StringComparison.OrdinalIgnoreCase))return m.BaseAddress;return IntPtr.Zero;}
 static uint Call(IntPtr process,IntPtr start,byte[] data){
  if(start==IntPtr.Zero)throw new Exception(L.T("연결 함수가 없습니다."));
  IntPtr memory=VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)data.Length,0x3000,4);if(memory==IntPtr.Zero)throw Native(L.T("연결 메모리"));
  bool finished=true;
  try{UIntPtr wrote;if(!WriteProcessMemory(process,memory,data,(UIntPtr)data.Length,out wrote)||wrote.ToUInt64()!=(ulong)data.Length)throw Native(L.T("연결 데이터"));
   IntPtr thread=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,start,memory,0,IntPtr.Zero);if(thread==IntPtr.Zero)throw Native(L.T("연결 시작"));
   try{finished=WaitForSingleObject(thread,15000)==0;if(!finished)throw new TimeoutException(L.T("연결 응답 시간이 초과됐습니다. 게임을 정상 종료한 뒤 다시 시도해 주세요."));uint result;if(!GetExitCodeThread(thread,out result))throw Native(L.T("연결 결과"));return result;}finally{CloseHandle(thread);}
  }finally{if(finished)VirtualFreeEx(process,memory,UIntPtr.Zero,0x8000);}
 }
 public static bool IsDebug(Process p){return p.MainModule.FileName.StartsWith(Store.AdapterRoot+"\\",StringComparison.OrdinalIgnoreCase);}
 public static void ValidateExecutable(string path){
  if(!File.Exists(Path.Combine(Store.Root,"attach-profile.json"))||!File.Exists(Path.Combine(Store.Root,"SenseBridge.Bloodborne.dll")))throw new Exception(L.T("이 배포판에는 블러드본 전용 연결 자료가 없습니다. README의 블러드본 개발 연결 항목을 확인해 주세요."));
  if(!string.Equals(Path.GetFileName(path),"bb-probe.exe",StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetFileName(Path.GetDirectoryName(path)),"bin",StringComparison.OrdinalIgnoreCase))throw new Exception(L.T("게임 폴더의 bin\\bb-probe.exe를 선택해 주세요."));
  var profile=Store.Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(Store.Root,"attach-profile.json")));
  string root=Path.GetDirectoryName(Path.GetDirectoryName(path));
  if(Hash(path)!=(string)profile["sha256"]||Hash(Path.Combine(root,@"out\boot-linked.bin"))!=(string)profile["boot_sha256"])throw new Exception(L.T("현재 연결 모듈이 지원하는 블러드본 버전과 다릅니다."));
 }
 public static Process Find(GameProfile profile){
  bool steam=SteamLibrary.Ready(profile)&&string.IsNullOrEmpty(profile.EngineExecutable);
  if(!steam&&string.IsNullOrEmpty(profile.EngineExecutable))throw new Exception(L.T("bin\\bb-probe.exe를 먼저 지정해 주세요."));
  string target=steam?null:Path.GetFullPath(profile.EngineExecutable);
  Process found=null;
  foreach(var p in Process.GetProcesses()){
   if(!steam&&!string.Equals(p.ProcessName,Path.GetFileNameWithoutExtension(target),StringComparison.OrdinalIgnoreCase)&&!(Store.Supported(profile)&&p.ProcessName.StartsWith("bb-probe",StringComparison.OrdinalIgnoreCase))){p.Dispose();continue;}
   bool match=false;try{match=steam?SteamLibrary.Contains(profile,p.MainModule.FileName)&&!Regex.IsMatch(p.ProcessName,"crash|report|unins|setup|helper|launcher|anticheat",RegexOptions.IgnoreCase):(Store.Supported(profile)&&p.ProcessName.StartsWith("bb-probe",StringComparison.OrdinalIgnoreCase)&&IsDebug(p))||string.Equals(p.MainModule.FileName,target,StringComparison.OrdinalIgnoreCase);}catch{}
   if(!match){p.Dispose();continue;}
   if(found!=null){if(!Store.Supported(profile)){p.Dispose();continue;}found.Dispose();p.Dispose();throw new Exception(L.T("블러드본이 두 개 실행 중입니다. 사용할 게임 하나만 남겨 주세요."));}found=p;
  }return found;
 }
 public static void Connect(Process p){
  if(IsDebug(p))return;
  var profile=Store.Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(Store.Root,"attach-profile.json")));
  string exe=p.MainModule.FileName,root=Path.GetDirectoryName(Path.GetDirectoryName(exe));
  if(Hash(exe)!=(string)profile["sha256"]||Hash(Path.Combine(root,@"out\boot-linked.bin"))!=(string)profile["boot_sha256"])throw new Exception(L.T("현재 블러드본 버전은 직접 연결 모듈과 일치하지 않습니다."));
  string log=Path.Combine(root,@"user\last_run.log");string text;
  using(var f=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var r=new StreamReader(f))text=r.ReadToEnd();
  if(File.GetLastWriteTime(log)<p.StartTime.AddSeconds(-5))throw new Exception(L.T("현재 게임의 오디오 정보를 찾지 못했습니다. 게임 로딩 후 다시 눌러 주세요."));
  var matches=Regex.Matches(text,@"audio port 0 opened \(type 0, (\d+) ch -> \d+ ch, (float|s16), (\d+) frames\)");
  if(matches.Count==0)throw new Exception(L.T("게임 오디오가 아직 준비되지 않았습니다. 타이틀 화면이 나온 뒤 다시 눌러 주세요."));
  var m=matches[matches.Count-1];int ch=int.Parse(m.Groups[1].Value),frames=int.Parse(m.Groups[3].Value),floating=m.Groups[2].Value=="float"?1:0;
  string dll=Path.Combine(Store.Root,"SenseBridge.Bloodborne.dll");if(!File.Exists(dll))throw new FileNotFoundException(L.T("연결 모듈이 없습니다."),dll);
  IntPtr process=OpenProcess(0x43a,false,p.Id);if(process==IntPtr.Zero)throw Native(L.T("게임 연결"));
  try{
   IntPtr remote=RemoteModule(p,Path.GetFileName(dll));
   if(remote==IntPtr.Zero){
    IntPtr load=GetProcAddress(GetModuleHandle("kernel32.dll"),"LoadLibraryW"),owner;
    if(!GetModuleHandleEx(6,load,out owner))throw Native(L.T("시스템 함수 확인"));var path=new StringBuilder(1024);GetModuleFileName(owner,path,path.Capacity);
    IntPtr target=RemoteModule(p,Path.GetFileName(path.ToString()));if(target==IntPtr.Zero)throw new Exception(L.T("게임의 시스템 모듈을 찾지 못했습니다."));
    Call(process,new IntPtr(target.ToInt64()+load.ToInt64()-owner.ToInt64()),Encoding.Unicode.GetBytes(dll+"\0"));
    remote=RemoteModule(p,Path.GetFileName(dll));if(remote==IntPtr.Zero)throw new Exception(L.T("블러드본 연결 모듈을 로드하지 못했습니다."));
   }
   IntPtr local=LoadLibraryEx(dll,IntPtr.Zero,1);if(local==IntPtr.Zero)throw Native(L.T("연결 모듈 검사"));
   long offset;try{IntPtr start=GetProcAddress(local,"SenseBridgeStart");if(start==IntPtr.Zero)throw new Exception(L.T("연결 모듈 함수가 없습니다."));offset=start.ToInt64()-local.ToInt64();}finally{FreeLibrary(local);}
   byte[] init=new byte[12];Buffer.BlockCopy(new[]{frames,ch,floating},0,init,0,12);
   uint result=Call(process,new IntPtr(remote.ToInt64()+offset),init);
   if(result!=0)throw new Exception(L.T("게임 연결 검사 실패 (")+result+L.T("). 게임 파일은 변경되지 않았습니다."));
  }finally{CloseHandle(process);}
 }
}
}
