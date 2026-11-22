using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace InputAtlas {
public interface IForegroundIdentity : IDisposable {
 string Name {get;}
 bool IsAlive {get;}
}
// Keep just the current process handle, not an unbounded PID/name dictionary.
// The handle establishes process identity even if Windows reuses a PID.
public sealed class ForegroundResolver : IDisposable {
 readonly Func<uint> pidProvider;
 readonly Func<uint,IForegroundIdentity> opener;
 readonly Func<long> milliseconds;
 uint currentPid;IForegroundIdentity current;long retryAt;
 static readonly Stopwatch Clock=Stopwatch.StartNew();
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 static uint Pid(){uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);return pid;}
 public ForegroundResolver():this(Pid,NativeIdentity.Open,()=>Clock.ElapsedMilliseconds){}
 public ForegroundResolver(Func<uint> pidProvider,Func<uint,IForegroundIdentity> opener,Func<long> milliseconds){this.pidProvider=pidProvider;this.opener=opener;this.milliseconds=milliseconds;}
 public string Read(){
  uint pid=pidProvider();long now=milliseconds();
  if(pid!=currentPid){DisposeIdentity();currentPid=pid;retryAt=0;}
  if(pid==0)return "Unknown";
  if(current!=null){
   try{if(current.IsAlive)return current.Name;}catch{}
   DisposeIdentity();retryAt=0;
  }
  if(now<retryAt)return "Unknown";
  IForegroundIdentity opened=null;
  try{
   opened=opener(pid);
   if(opened!=null&&opened.IsAlive&&!String.IsNullOrWhiteSpace(opened.Name)){current=opened;opened=null;return current.Name;}
  }catch{}finally{if(opened!=null)opened.Dispose();}
  retryAt=now+5000;return "Unknown";
 }
 void DisposeIdentity(){if(current!=null){current.Dispose();current=null;}}
 public void Dispose(){DisposeIdentity();currentPid=0;retryAt=0;}
 sealed class NativeIdentity : IForegroundIdentity {
  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder image,ref uint size);
  [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr process);
  IntPtr handle;public string Name {get;private set;}
  NativeIdentity(IntPtr h,string name){handle=h;Name=name;}
  public bool IsAlive {get{return handle!=IntPtr.Zero&&WaitForSingleObject(handle,0)==0x102;}}
  public static IForegroundIdentity Open(uint pid){
   const uint QueryLimitedAndSynchronize=0x1000|0x100000;IntPtr handle=OpenProcess(QueryLimitedAndSynchronize,false,pid);if(handle==IntPtr.Zero)return null;
   try{var image=new StringBuilder(512);uint length=(uint)image.Capacity;if(!QueryFullProcessImageName(handle,0,image,ref length)){if(Marshal.GetLastWin32Error()!=122)return null;image.Capacity=32768;length=(uint)image.Capacity;if(!QueryFullProcessImageName(handle,0,image,ref length))return null;}var name=Path.GetFileNameWithoutExtension(image.ToString());if(String.IsNullOrWhiteSpace(name))return null;var identity=new NativeIdentity(handle,name);handle=IntPtr.Zero;return identity;}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
  }
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
}
}
