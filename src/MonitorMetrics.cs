using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Text;
namespace InputAtlas {
public sealed class MonitorChannel:IDisposable {
 readonly string name;MemoryMappedFile map;Mutex mutex;
 public MonitorChannel(string root,bool writer){name=MonitorOptions.Name(root,"data");mutex=new Mutex(false,MonitorOptions.Name(root,"data-lock"));if(writer)map=MemoryMappedFile.CreateOrOpen(name,65536);}
 bool Enter(){try{return mutex.WaitOne(40);}catch(AbandonedMutexException){return true;}}
 public void Write(MonitorSnapshot value){byte[] data=Encoding.UTF8.GetBytes(MonitorOptions.Json.Serialize(value));if(data.Length>65000||!Enter())return;try{using(var v=map.CreateViewStream()){var w=new BinaryWriter(v);w.Write(data.Length);w.Write(data);w.Flush();}}finally{mutex.ReleaseMutex();}}
 public MonitorSnapshot Read(){if(!Enter())return null;try{try{using(var file=MemoryMappedFile.OpenExisting(name,MemoryMappedFileRights.Read))using(var view=file.CreateViewStream(0,65536,MemoryMappedFileAccess.Read)){var reader=new BinaryReader(view);int length=reader.ReadInt32();if(length<=0||length>65000)return null;return MonitorOptions.Json.Deserialize<MonitorSnapshot>(Encoding.UTF8.GetString(reader.ReadBytes(length)));}}catch(FileNotFoundException){return null;}}finally{mutex.ReleaseMutex();}}
 public void Dispose(){if(map!=null)map.Dispose();mutex.Dispose();}
}
public sealed class MonitorMetrics {
 [StructLayout(LayoutKind.Sequential)] struct CpuTimes {public uint Low,High;public ulong Value {get{return ((ulong)High<<32)|Low;}}}
 [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out CpuTimes idle,out CpuTimes kernel,out CpuTimes user);
 [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPage,AvailPage,TotalVirtual,AvailVirtual,Extended;}
 [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus state);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct IfRow {
  public ulong Luid;public uint Index;public Guid Guid;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)]public string Alias;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)]public string Description;
  public uint AddressLength;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Address;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] PermanentAddress;
  public uint Mtu,Type,Tunnel,Media,PhysicalMedium,Access,Direction;public byte Flags;
  public uint OperStatus,AdminStatus,MediaState;public Guid NetworkGuid;public uint ConnectionType;
  public ulong TransmitSpeed,ReceiveSpeed,InOctets,InUnicastPackets,InOtherPackets,InDiscards,InErrors,InUnknown,InUnicastOctets,InMulticastOctets,InBroadcastOctets,OutOctets,OutUnicastPackets,OutOtherPackets,OutDiscards,OutErrors,OutUnicastOctets,OutMulticastOctets,OutBroadcastOctets,Queue;
 }
 [DllImport("iphlpapi.dll")] static extern uint GetIfTable2(out IntPtr table);
 [DllImport("iphlpapi.dll")] static extern uint GetIfEntry2(ref IfRow row);
 [DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr table);
 readonly string root;readonly Stopwatch clock=Stopwatch.StartNew();double previousAt=-1,scannedAt=-100,todayAt=-100;ulong idle,kernel,user;
 string selected="";ulong receive,send,luid;List<IfRow> rows=new List<IfRow>();double todayDown,todayUp,trafficDown,trafficUp;int trafficPeriod=-1;DateTime today=DateTime.MinValue;
 public sealed class TrafficTotals {public double TodayDownload,TodayUpload,Download,Upload;}
 public static DateTime TrafficStart(DateTime end,int period){end=end.Date;if(period==1)return end.AddDays(-(((int)end.DayOfWeek+6)%7));if(period==2)return new DateTime(end.Year,end.Month,1);if(period==3)return new DateTime(end.Year,1,1);if(period==4)return DateTime.MinValue;return end;}
 public static TrafficTotals ReadTraffic(string dataRoot,DateTime end,int period){
  var result=new TrafficTotals();string file=Path.Combine(dataRoot,"inputatlas.sqlite");if(!File.Exists(file))return result;
  using(var db=new Sqlite(file)){string day=end.Date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),start=TrafficStart(end,period).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);var rows=db.Query("SELECT SUM(CASE WHEN day=? THEN download_bytes ELSE 0 END) today_download,SUM(CASE WHEN day=? THEN upload_bytes ELSE 0 END) today_upload,SUM(download_bytes) downloads,SUM(upload_bytes) uploads FROM daily WHERE day>=? AND day<=?",day,day,start,day);if(rows.Count>0){var row=rows[0];result.TodayDownload=Store.Number(row["today_download"]);result.TodayUpload=Store.Number(row["today_upload"]);result.Download=Store.Number(row["downloads"]);result.Upload=Store.Number(row["uploads"]);}}
  return result;
 }
 public MonitorMetrics(string dataRoot){root=dataRoot;}
 public static bool Physical(IfRow row){return (row.Flags&1)!=0&&(row.Flags&2)==0&&(row.Type==6||row.Type==71||row.Type==243||row.Type==244);}
 public static List<IfRow> Enumerate(){IntPtr table;uint result=GetIfTable2(out table);if(result!=0)throw new IOException("Network counters: "+result);try{uint count=(uint)Marshal.ReadInt32(table);int size=Marshal.SizeOf(typeof(IfRow));var list=new List<IfRow>();for(uint i=0;i<count;i++)list.Add((IfRow)Marshal.PtrToStructure(IntPtr.Add(table,8+checked((int)i*size)),typeof(IfRow)));return list;}finally{FreeMibTable(table);}}
 public static double? CpuDelta(ulong lastIdle,ulong lastKernel,ulong lastUser,ulong newIdle,ulong newKernel,ulong newUser){if(newIdle<lastIdle||newKernel<lastKernel||newUser<lastUser)return null;double total=(newKernel-lastKernel)+(double)(newUser-lastUser);if(total<=0)return null;return Math.Max(0,Math.Min(100,100*(total-(newIdle-lastIdle))/total));}
 public static double? Rate(ulong before,ulong after,double elapsed){return elapsed>0&&elapsed<=10&&after>=before?(double?)(after-before)/elapsed:null;}
 public MonitorSnapshot Sample(MonitorOptions options){
  double at=clock.Elapsed.TotalSeconds;var snapshot=new MonitorSnapshot{At=DateTime.Now.ToString("o"),Pid=Process.GetCurrentProcess().Id,Interval=options.Interval,TaskbarEnabled=options.OnTaskbar,AdapterSetting=options.Adapter,Items=(options.Download?1:0)|(options.Upload?2:0)|(options.Cpu?4:0)|(options.Memory?8:0)|(options.DailyTraffic?16:0)};
  CpuTimes a,b,c;if(GetSystemTimes(out a,out b,out c)){if(previousAt>=0)snapshot.Cpu=CpuDelta(idle,kernel,user,a.Value,b.Value,c.Value);idle=a.Value;kernel=b.Value;user=c.Value;}
  var mem=new MemoryStatus{Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};if(GlobalMemoryStatusEx(ref mem)){snapshot.MemoryTotal=mem.TotalPhys;snapshot.MemoryUsed=mem.TotalPhys-mem.AvailPhys;snapshot.MemoryPercent=mem.TotalPhys==0?null:(double?)(100.0*snapshot.MemoryUsed/mem.TotalPhys);}
  try{
   if(at-scannedAt>=10||selected!=options.Adapter){rows=Enumerate();scannedAt=at;}
   snapshot.Adapters=rows.Where(Physical).Select(r=>new MonitorAdapter{Id=r.Guid.ToString("D"),Name=r.Alias,Description=r.Description,Connected=r.OperStatus==1}).ToList();
   IfRow choice=options.Adapter=="auto"?rows.Where(r=>Physical(r)&&r.OperStatus==1).OrderByDescending(r=>r.TransmitSpeed).FirstOrDefault():rows.FirstOrDefault(r=>r.Guid.ToString("D")==options.Adapter&&Physical(r));
   if(choice.Luid!=0&&GetIfEntry2(ref choice)==0&&choice.OperStatus==1){snapshot.Adapter=choice.Alias;snapshot.AdapterId=choice.Guid.ToString("D");if(luid==choice.Luid&&selected==options.Adapter&&previousAt>=0){snapshot.Download=Rate(receive,choice.InOctets,at-previousAt);snapshot.Upload=Rate(send,choice.OutOctets,at-previousAt);}receive=choice.InOctets;send=choice.OutOctets;luid=choice.Luid;}else{luid=0;scannedAt=-100;}
   selected=options.Adapter;
  }catch(Exception ex){snapshot.Error=ex.Message;luid=0;}
  if(DateTime.Today!=today||trafficPeriod!=options.TrafficPeriod||at-todayAt>=5){today=DateTime.Today;todayAt=at;trafficPeriod=options.TrafficPeriod;try{var total=ReadTraffic(root,today,trafficPeriod);todayDown=total.TodayDownload;todayUp=total.TodayUpload;trafficDown=total.Download;trafficUp=total.Upload;}catch(Exception ex){snapshot.Error=ex.Message;trafficPeriod=-1;}}
  snapshot.TodayDownload=todayDown;snapshot.TodayUpload=todayUp;snapshot.TrafficDownload=trafficDown;snapshot.TrafficUpload=trafficUp;snapshot.TrafficPeriod=trafficPeriod;previousAt=at;return snapshot;
 }
 public static string Speed(double? rate){if(!rate.HasValue)return "—";double v=rate.Value;return v>=1048576?(v/1048576).ToString("0.0")+" MB/s":v>=1024?(v/1024).ToString("0.0")+" KB/s":v.ToString("0")+" B/s";}
 public static string Bytes(double value){return value>=1073741824?(value/1073741824).ToString("0.00")+" GB":value>=1048576?(value/1048576).ToString("0.0")+" MB":value>=1024?(value/1024).ToString("0.0")+" KB":value.ToString("0")+" B";}
}
}
