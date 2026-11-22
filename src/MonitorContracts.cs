using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace InputAtlas {
public sealed class MonitorOptions {
 public bool Enabled=true,OnTaskbar=false,Download=true,Upload=true,Cpu=true,Memory=true,DailyTraffic=false;
 public int Interval=1000,Language=0,TaskbarWidth=0,TrafficPeriod=0,PerformanceDigits=0,TaskbarColorMode=0;public string Adapter="auto",TaskbarBackground="#141414",TaskbarForeground="#F0F0F0";
 public void Normalize(){Interval=Interval==500||Interval==2000?Interval:1000;TaskbarWidth=TaskbarWidth<=0?0:Math.Max(140,Math.Min(1200,TaskbarWidth));TrafficPeriod=Math.Max(0,Math.Min(4,TrafficPeriod));PerformanceDigits=Math.Max(0,Math.Min(1,PerformanceDigits));TaskbarColorMode=Math.Max(0,Math.Min(1,TaskbarColorMode));TaskbarBackground=ColorHex(TaskbarBackground,"#141414");TaskbarForeground=ColorHex(TaskbarForeground,"#F0F0F0");if(String.IsNullOrWhiteSpace(Adapter))Adapter="auto";}
 public string PerformanceText(double? value){return value.HasValue?value.Value.ToString(PerformanceDigits==1?"0.0":"0")+"%":"—";}
 public static string ColorHex(string value,string fallback){int parsed;string raw=(value??"").Trim().TrimStart('#');return raw.Length==6&&Int32.TryParse(raw,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out parsed)?"#"+parsed.ToString("X6"):fallback;}
 public static string PeriodLabel(int period,bool chinese,bool compact=false){period=Math.Max(0,Math.Min(4,period));return (chinese?(compact?new[]{"今","周","月","年","总"}:new[]{"今日","本周","本月","本年","总计"}):compact?new[]{"Day","Wk","Mo","Yr","All"}:new[]{"Today","This week","This month","This year","All time"})[period];}
 public static string TrafficTitle(int period,bool chinese,bool upload){period=Math.Max(0,Math.Min(4,period));return chinese?PeriodLabel(period,true)+(upload?"上传":"下载"):new[]{"Today's","This week's","This month's","This year's","All-time"}[period]+(upload?" uploads":" downloads");}
 public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 public static MonitorOptions Load(string root){try{string text;using(var file=new FileStream(Path.Combine(root,"monitor-settings.json"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(file,Encoding.UTF8))text=reader.ReadToEnd();var result=Json.Deserialize<MonitorOptions>(text)??new MonitorOptions();result.Normalize();return result;}catch{return new MonitorOptions();}}
 public void Save(string root){Normalize();string p=Path.Combine(root,"monitor-settings.json"),tmp=p+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(tmp,Json.Serialize(this),Encoding.UTF8);try{for(int attempt=0;;attempt++){try{if(File.Exists(p))File.Replace(tmp,p,null,true);else File.Move(tmp,p);return;}catch(IOException){if(attempt>=7)throw;System.Threading.Thread.Sleep(15*(attempt+1));}catch(UnauthorizedAccessException){if(attempt>=7)throw;System.Threading.Thread.Sleep(15*(attempt+1));}}}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 public static string Name(string root,string purpose){using(var hash=SHA256.Create())return "Local\\InputAtlasMonitor-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant()))).Replace("-","").Substring(0,16)+"-"+purpose;}
 public bool AnyItem {get{return Download||Upload||Cpu||Memory||DailyTraffic;}}
}
public sealed class MonitorAdapter {public string Id,Name,Description;public bool Connected;}
public sealed class MonitorSnapshot {
 public string At="",Adapter="",AdapterId="",TaskbarStatus="",Error="",TaskbarBackground="",TaskbarForeground="",TaskbarColorSource="";public int TaskbarColorMode;
 public double? Cpu,MemoryPercent,Download,Upload;
 public double TrafficDownload,TrafficUpload;public int TrafficPeriod,TaskbarWidth,TaskbarMinWidth;
 public ulong MemoryUsed,MemoryTotal;public double TodayDownload,TodayUpload;
 public List<MonitorAdapter> Adapters=new List<MonitorAdapter>();public bool Attached,TaskbarEnabled,DoubleClickReady;public int Pid,Items,Interval;public string AdapterSetting="";
}
public static class MonitorRuntime {
 public static void Ensure(string root,bool force=false){
  var options=MonitorOptions.Load(root);string stop=Path.Combine(root,"monitor.stop");
  if(!options.Enabled){if(!File.Exists(stop))File.WriteAllText(stop,"paused by user");return;}
  if(File.Exists(Path.Combine(root,"maintenance.flag")))return;
  if(File.Exists(stop))File.Delete(stop);
  try{using(var worker=System.Threading.Mutex.OpenExisting(MonitorOptions.Name(root,"worker"))){bool idle=false;try{idle=worker.WaitOne(0);}catch(System.Threading.AbandonedMutexException){idle=true;}if(!idle)return;worker.ReleaseMutex();}}catch(System.Threading.WaitHandleCannotBeOpenedException){}
  string hb=Path.Combine(root,"monitor-heartbeat.json"),settings=Path.Combine(root,"monitor-settings.json");
  if(!force&&File.Exists(hb)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(hb)).TotalSeconds<20&&(!File.Exists(settings)||File.GetLastWriteTimeUtc(settings)<=File.GetLastWriteTimeUtc(hb)))return;
  string exe=Path.Combine(NativeRuntime.Base,"InputAtlas.Monitor.exe");if(!File.Exists(exe))return;
  using(var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=false,CreateNoWindow=true})){}
 }
}
}
