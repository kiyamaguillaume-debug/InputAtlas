using System;using System.IO;using System.Linq;using System.Diagnostics;using System.Text;using System.Security.Cryptography;using System.Threading;
namespace InputAtlas {
public static class NativeRuntime {
 public static bool BackgroundEnabled,TestInstance;
 public static string Base {get{return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);}}
 public static string Data {get{string path=Environment.GetEnvironmentVariable("INPUTATLAS_DATA_DIR");if(!String.IsNullOrWhiteSpace(path))return Path.GetFullPath(path);return File.Exists(Path.Combine(Base,"portable.mode"))?Path.Combine(Base,"data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InputAtlas");}}
 public static string Name(string purpose){using(var hash=SHA256.Create())return "Local\\InputAtlas-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(Data).ToUpperInvariant()))).Replace("-","").Substring(0,16)+"-"+purpose;}
 public static void Configure(){Store.Root=Data;DatabaseBootstrap.Ensure(Store.Root);Validate();}
 public static void Validate(){if(!String.Equals(Path.GetFullPath(Store.Root).TrimEnd('\\'),Data.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid InputAtlas data directory");}
 public static bool Held(string purpose){try{using(var mutex=Mutex.OpenExisting(Name(purpose))){bool owned=false;try{try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}return !owned;}finally{if(owned)mutex.ReleaseMutex();}}}catch(WaitHandleCannotBeOpenedException){return false;}}
 public static void Start(){Validate();foreach(string file in new[]{"background.paused","watchdog.stop","collector.stop","controller.stop","monitor.stop"}){string path=Path.Combine(Data,file);if(File.Exists(path))File.Delete(path);}BackgroundEnabled=true;ControllerDefaults.Resume(Data);EnsureWorkers();}
 public static void Stop(){Validate();File.WriteAllText(Path.Combine(Data,"gamepad.paused"),"paused by user");File.WriteAllText(Path.Combine(Data,"controller.stop"),"stop");}
 public static void StopBackground(){foreach(string file in new[]{"background.paused","watchdog.stop","collector.stop","controller.stop","monitor.stop"})File.WriteAllText(Path.Combine(Data,file),"stopped by user");}
 public static void EnsureWorkers(bool force=false){Validate();if(!BackgroundEnabled||File.Exists(Path.Combine(Data,"maintenance.flag"))||File.Exists(Path.Combine(Data,"background.paused")))return;Ensure("InputAtlas.Watchdog.exe","watchdog");MonitorRuntime.Ensure(Data,force);if(!File.Exists(Path.Combine(Data,"collector.stop")))Ensure("InputAtlas.Collector.exe","basic-collector");if(!File.Exists(Path.Combine(Data,"gamepad.paused"))&&!File.Exists(Path.Combine(Data,"controller.stop")))Ensure("InputAtlas.Gamepad.Collector.exe","gamepad-collector");}
 static void Ensure(string exe,string purpose){if(Held(purpose))return;string file=Path.Combine(Base,exe);if(!File.Exists(file))throw new FileNotFoundException("Extract the complete InputAtlas release folder before starting.",file);using(var p=Process.Start(new ProcessStartInfo(file){WorkingDirectory=Base,UseShellExecute=false,CreateNoWindow=true})){} }
}
}
