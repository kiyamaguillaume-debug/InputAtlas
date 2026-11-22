using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace InputAtlas {
public static class MonitorWorker {
 [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 static void Heartbeat(string root,bool stopped,MonitorSnapshot state){string file=Path.Combine(root,"monitor-heartbeat.json"),temp=file+".tmp";File.WriteAllText(temp,MonitorOptions.Json.Serialize(new{pid=Process.GetCurrentProcess().Id,at=DateTime.Now.ToString("o"),stopped=stopped,attached=state!=null&&state.Attached,status=state==null?"starting":state.TaskbarStatus}));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
 [STAThread]public static int Main(string[] args){
  bool testMode=args.Length==2&&args[0]=="--test-root";if(testMode){string data=Path.GetFullPath(args[1]);if(data.IndexOf("tests",StringComparison.OrdinalIgnoreCase)<0)return 2;Environment.SetEnvironmentVariable("INPUTATLAS_DATA_DIR",data);}NativeRuntime.Configure();string root=Store.Root;
  bool owner;using(var mutex=new Mutex(true,MonitorOptions.Name(root,"worker"),out owner)){if(!owner)return 0;
   MonitorSnapshot latest=null;
   try{
    if(args.Length==2&&args[0]=="--probe"){var probe=new MonitorMetrics(root);var first=probe.Sample(new MonitorOptions());Thread.Sleep(1000);File.WriteAllText(args[1],MonitorOptions.Json.Serialize(new{rowSize=Marshal.SizeOf(typeof(MonitorMetrics.IfRow)),first=first,second=probe.Sample(new MonitorOptions())}));return 0;}
    var settings=MonitorOptions.Load(root);if(!settings.Enabled)return 0;try{SetProcessDpiAwarenessContext((IntPtr)(-4));}catch{}
    Application.EnableVisualStyles();var context=new ApplicationContext();var metrics=new MonitorMetrics(root);var heartbeat=Stopwatch.StartNew();
    using(var channel=new MonitorChannel(root,true))using(var view=new MonitorTaskbar(root,()=>{File.WriteAllText(Path.Combine(root,"monitor-open.request"),DateTime.Now.ToString("o"));using(var process=Process.Start(new ProcessStartInfo(Path.Combine(NativeRuntime.Base,"InputAtlas.exe"),testMode?"--preview":""){WorkingDirectory=NativeRuntime.Base,UseShellExecute=false})){} }))using(var timer=new System.Windows.Forms.Timer()){
     Action tick=()=>{try{settings=MonitorOptions.Load(root);if(!settings.Enabled||File.Exists(Path.Combine(root,"monitor.stop"))){context.ExitThread();return;}timer.Interval=settings.Interval==500||settings.Interval==2000?settings.Interval:1000;latest=metrics.Sample(settings);view.Update(settings,latest);latest.TaskbarBackground=view.BackgroundColor;latest.TaskbarForeground=view.ForegroundColor;latest.TaskbarColorSource=view.ColorSource;latest.TaskbarColorMode=settings.TaskbarColorMode;latest.TaskbarWidth=view.DisplayWidth;latest.TaskbarMinWidth=view.MinimumWidth;latest.Attached=view.Attached;latest.DoubleClickReady=view.DoubleClickReady;latest.TaskbarStatus=view.Status;channel.Write(latest);if(heartbeat.Elapsed.TotalSeconds>=10){Heartbeat(root,false,latest);if(testMode)view.SavePreview(Path.Combine(root,"taskbar-preview.png"));heartbeat.Restart();}}catch(Exception ex){File.AppendAllText(Path.Combine(root,"monitor.log"),DateTime.Now.ToString("o")+" "+ex.Message+Environment.NewLine);}};
     timer.Interval=settings.Interval;timer.Tick+=(s,e)=>tick();tick();Heartbeat(root,false,latest);timer.Start();Application.Run(context);timer.Stop();
    }return 0;
   }catch(Exception ex){File.AppendAllText(Path.Combine(root,"monitor.log"),DateTime.Now.ToString("o")+" "+ex+Environment.NewLine);return 1;}finally{try{Heartbeat(root,true,latest);}catch{}}
  }
 }
}
}
