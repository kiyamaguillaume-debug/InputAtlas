using System;using System.IO;using System.Diagnostics;using System.Threading;
namespace InputAtlas {
public static class CollectorWatchdog {
 static string Root{get{return NativeRuntime.Data;}}
 static void Log(string text){try{File.AppendAllText(Path.Combine(Root,"watchdog.log"),DateTimeOffset.Now.ToString("o")+" "+text+Environment.NewLine);}catch{}}
 static void Ensure(string exe,string purpose,string heartbeat,bool paused,RecoveryPolicy policy){if(paused)return;string path=Path.Combine(NativeRuntime.Base,exe);if(NativeRuntime.Held(purpose)){
  try{string file=Path.Combine(Root,heartbeat);if(!File.Exists(file))return;var hb=Store.ReadJson(file);int pid=(int)Store.Number(hb["pid"]);using(var process=Process.GetProcessById(pid)){if(!String.Equals(process.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase))return;DateTime sampled=File.GetLastWriteTimeUtc(file),created=process.StartTime.ToUniversalTime();if(created>sampled.AddSeconds(5)||!policy.ShouldRestart(DateTime.UtcNow,sampled,created))return;process.Kill();process.WaitForExit(5000);Log("Restart stale "+exe+" pid="+pid);}
  }catch(Exception ex){Log("Health check "+exe+": "+ex.Message);return;}
 }
 if(!File.Exists(path))throw new FileNotFoundException("A background component is missing. Extract the complete release folder.",path);using(var process=Process.Start(new ProcessStartInfo(path){WorkingDirectory=NativeRuntime.Base,UseShellExecute=false,CreateNoWindow=true}))Log(exe+" started pid="+process.Id);
 }
 public static int Main(){try{NativeRuntime.Configure();bool owner;using(var mutex=new Mutex(true,NativeRuntime.Name("watchdog"),out owner)){if(!owner)return 0;var policy=new RecoveryPolicy(DateTime.UtcNow);DateTime next=DateTime.MinValue;while(!File.Exists(Path.Combine(Root,"watchdog.stop"))){DateTime now=DateTime.UtcNow;if(policy.Observe(now)){next=DateTime.MinValue;Log("Resume detected; allowing collector recovery grace");}
   if(now>=next){try{if(!File.Exists(Path.Combine(Root,"maintenance.flag"))&&!File.Exists(Path.Combine(Root,"background.paused"))){MonitorRuntime.Ensure(Root);Ensure("InputAtlas.Collector.exe","basic-collector","collector-heartbeat.json",File.Exists(Path.Combine(Root,"collector.stop")),policy);Ensure("InputAtlas.Gamepad.Collector.exe","gamepad-collector","controller-heartbeat.json",File.Exists(Path.Combine(Root,"gamepad.paused"))||File.Exists(Path.Combine(Root,"controller.stop")),policy);}}catch(Exception ex){Log(ex.ToString());}next=DateTime.UtcNow.AddSeconds(30);}Thread.Sleep(1000);
  }}return 0;}catch(Exception ex){Log(ex.ToString());return 1;}}
}
}
