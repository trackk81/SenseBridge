using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SenseBridge.Ac6 {
// Offline diagnostic connector, not part of the release UI yet.
public static class Ac6TraceConnection {
 const string ModuleHash="03b12ac44e3f0e4c312381586bb48217685b807e66895a474f1ea2aaf09ce3da";
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr address,UIntPtr length,uint type,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr process,IntPtr address,UIntPtr size,uint type);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr process,IntPtr address,byte[] data,UIntPtr size,out UIntPtr written);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateRemoteThread(IntPtr process,IntPtr attributes,UIntPtr stack,IntPtr start,IntPtr argument,uint flags,IntPtr threadId);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
 [DllImport("kernel32.dll")]static extern bool GetExitCodeThread(IntPtr thread,out uint code);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool GetModuleHandleEx(uint flags,IntPtr address,out IntPtr module);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetModuleFileName(IntPtr module,StringBuilder path,int size);
 static Exception Failure(string operation){return new InvalidOperationException(operation+": "+new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);}
 static string Hash(string path){using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 static ProcessModule Module(Process p,string name){p.Refresh();return p.Modules.Cast<ProcessModule>().FirstOrDefault(m=>string.Equals(m.ModuleName,name,StringComparison.OrdinalIgnoreCase));}
 public static string Inspect(int pid,bool offlineConfirmed) {
  try {
   using(var p=Process.GetProcessById(pid)) {
    string[] processes;
    var all=Process.GetProcesses();
    try{processes=all.Select(x=>x.ProcessName).ToArray();}finally{foreach(var x in all)x.Dispose();}
    // Check protection before attempting to enumerate protected process modules.
    if(processes.Any(Ac6Compatibility.ProtectedName))return "protected-process-active";
    var modules=p.Modules.Cast<ProcessModule>().Select(m=>m.ModuleName).ToArray();
    return Ac6Compatibility.RejectReason(p.MainModule.FileName,Hash(p.MainModule.FileName),offlineConfirmed,true,processes,modules);
   }
  }catch{return "inspection-unavailable";}
 }
 static uint Call(IntPtr process,IntPtr function,byte[] bytes) {
  IntPtr memory=VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)bytes.Length,0x3000,4);
  if(memory==IntPtr.Zero)throw Failure("Allocate diagnostic parameters");
  bool completed=true;
  try {
   UIntPtr written;
   if(!WriteProcessMemory(process,memory,bytes,(UIntPtr)bytes.Length,out written)||written.ToUInt64()!=(ulong)bytes.Length)throw Failure("Write diagnostic parameters");
   IntPtr thread=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,function,memory,0,IntPtr.Zero);
   if(thread==IntPtr.Zero)throw Failure("Start diagnostic function");
   try {
    completed=WaitForSingleObject(thread,10000)==0;
    if(!completed)throw new TimeoutException("Diagnostic call timed out. No automatic retry.");
    uint result;if(!GetExitCodeThread(thread,out result))throw Failure("Diagnostic result");
    return result;
   }finally{CloseHandle(thread);}
  }finally{if(completed)VirtualFreeEx(process,memory,UIntPtr.Zero,0x8000);}
 }
 public static void Start(int pid,string directory,bool offlineConfirmed) {
  if(IntPtr.Size!=8)throw new InvalidOperationException("64-bit host required");
  string reason=Inspect(pid,offlineConfirmed);if(reason!=null)throw new InvalidOperationException(reason);
  string root=Path.GetFullPath(directory),dll=Path.Combine(root,"SenseBridge.Ac6Trace.dll");
  if(Hash(dll)!=ModuleHash)throw new InvalidDataException("Diagnostic module hash mismatch");
  using(var p=Process.GetProcessById(pid)) {
   DateTime start=p.StartTime;
   // Repeat protection and version guard immediately before writable access.
   reason=Inspect(pid,offlineConfirmed);if(reason!=null)throw new InvalidOperationException(reason);
   if(p.StartTime!=start)throw new InvalidOperationException("Game session changed");
   IntPtr handle=OpenProcess(0x43a,false,pid);if(handle==IntPtr.Zero)throw Failure("Open AC6 diagnostic session");
   try {
    var remote=Module(p,Path.GetFileName(dll));
    if(remote==null) {
     IntPtr load=GetProcAddress(GetModuleHandle("kernel32.dll"),"LoadLibraryW"),owner;
     if(load==IntPtr.Zero||!GetModuleHandleEx(6,load,out owner))throw Failure("Locate module loader");
     var ownerPath=new StringBuilder(2048);
     if(GetModuleFileName(owner,ownerPath,ownerPath.Capacity)==0)throw Failure("Locate loader owner");
     var remoteOwner=Module(p,Path.GetFileName(ownerPath.ToString()));
     if(remoteOwner==null)throw new InvalidOperationException("Matching system module missing");
     Call(handle,new IntPtr(remoteOwner.BaseAddress.ToInt64()+load.ToInt64()-owner.ToInt64()),Encoding.Unicode.GetBytes(dll+"\0"));
     remote=Module(p,Path.GetFileName(dll));
    }
    if(remote==null||!string.Equals(Path.GetFullPath(remote.FileName),dll,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected diagnostic module");
    IntPtr local=LoadLibraryEx(dll,IntPtr.Zero,1);
    if(local==IntPtr.Zero)throw Failure("Inspect diagnostic exports");
    long offset;
    try{IntPtr fn=GetProcAddress(local,"Ac6TraceStart");if(fn==IntPtr.Zero)throw new InvalidDataException("Diagnostic export missing");offset=fn.ToInt64()-local.ToInt64();}
    finally{FreeLibrary(local);}
    string log=Path.Combine(root,"ac6-playback-"+pid+"-"+start.ToUniversalTime().Ticks+".csv");
    if(File.Exists(log))log=Path.Combine(root,"ac6-playback-"+pid+"-"+start.ToUniversalTime().Ticks+"-"+Guid.NewGuid().ToString("N")+".csv");
    uint code=Call(handle,new IntPtr(remote.BaseAddress.ToInt64()+offset),Encoding.Unicode.GetBytes(log+"\0"));
    if(code!=0)throw new InvalidOperationException("Diagnostic start rejected: "+code);
   }finally{CloseHandle(handle);}
  }
 }
 public static uint Stop(int pid,string directory,bool offlineConfirmed) {
  string reason=Inspect(pid,offlineConfirmed);if(reason!=null)throw new InvalidOperationException(reason);
  string dll=Path.Combine(Path.GetFullPath(directory),"SenseBridge.Ac6Trace.dll");
  if(Hash(dll)!=ModuleHash)throw new InvalidDataException("Diagnostic module hash mismatch");
  using(var p=Process.GetProcessById(pid)){
   var remote=Module(p,Path.GetFileName(dll));
   if(remote==null)return 0;
   if(!string.Equals(Path.GetFullPath(remote.FileName),dll,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected diagnostic module");
   IntPtr local=LoadLibraryEx(dll,IntPtr.Zero,1);if(local==IntPtr.Zero)throw Failure("Diagnostic exports");
   long offset;
   try{IntPtr fn=GetProcAddress(local,"Ac6TraceStop");if(fn==IntPtr.Zero)throw new InvalidDataException("Stop export missing");offset=fn.ToInt64()-local.ToInt64();}finally{FreeLibrary(local);}
   IntPtr handle=OpenProcess(0x43a,false,pid);if(handle==IntPtr.Zero)throw Failure("Stop diagnostic session");
   try{return Call(handle,new IntPtr(remote.BaseAddress.ToInt64()+offset),new byte[2]);}finally{CloseHandle(handle);}
  }
 }
}
}
