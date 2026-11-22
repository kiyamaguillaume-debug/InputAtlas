using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace InputAtlas {
public sealed class Sqlite : IDisposable {
 IntPtr db;
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] name,out IntPtr db,int flags,IntPtr vfs);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int n,out IntPtr stmt,IntPtr tail);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_name(IntPtr stmt,int col);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt,int col);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr stmt,int col);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern double sqlite3_column_double(IntPtr stmt,int col);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr stmt,int i,byte[] val,int n,IntPtr free);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_double(IntPtr stmt,int i,double v);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_null(IntPtr stmt,int i);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr db);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_busy_timeout(IntPtr db,int ms);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_backup_init(IntPtr dest,byte[] destName,IntPtr source,byte[] sourceName);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_backup_step(IntPtr backup,int pages);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_backup_finish(IntPtr backup);
 static byte[] U(string x){return Encoding.UTF8.GetBytes(x+"\0");}
 static string Str(IntPtr p){if(p==IntPtr.Zero)return "";int n=0;while(Marshal.ReadByte(p,n)!=0)n++;byte[] b=new byte[n];Marshal.Copy(p,b,0,n);return Encoding.UTF8.GetString(b);}
 public Sqlite(string path,bool writable=false,bool create=false){int rc=sqlite3_open_v2(U(path),out db,writable?(create?6:2):1,IntPtr.Zero);if(rc!=0){string e=Str(sqlite3_errmsg(db));Dispose();throw new IOException(e);}sqlite3_busy_timeout(db,2000);}
 public List<Dictionary<string,object>> Query(string sql,params object[] args){
  IntPtr s; if(sqlite3_prepare_v2(db,U(sql),-1,out s,IntPtr.Zero)!=0)throw new IOException(Str(sqlite3_errmsg(db)));
  try {
   for(int i=0;i<args.Length;i++) {int rc;var v=args[i];if(v==null)rc=sqlite3_bind_null(s,i+1);else if(v is string){byte[] b=U((string)v);rc=sqlite3_bind_text(s,i+1,b,b.Length-1,new IntPtr(-1));}else rc=sqlite3_bind_double(s,i+1,Convert.ToDouble(v,CultureInfo.InvariantCulture));if(rc!=0)throw new IOException(Str(sqlite3_errmsg(db)));}
   var list=new List<Dictionary<string,object>>();int step;
   while((step=sqlite3_step(s))==100){var r=new Dictionary<string,object>();for(int i=0;i<sqlite3_column_count(s);i++){int t=sqlite3_column_type(s,i);r[Str(sqlite3_column_name(s,i))]=t==5?null:t==1||t==2?(object)sqlite3_column_double(s,i):Str(sqlite3_column_text(s,i));}list.Add(r);}
   if(step!=101)throw new IOException(Str(sqlite3_errmsg(db)));return list;
  }finally{sqlite3_finalize(s);}
 }
 public void Dispose(){if(db!=IntPtr.Zero){sqlite3_close(db);db=IntPtr.Zero;}}
 public void Backup(string path){IntPtr dest;if(sqlite3_open_v2(U(path),out dest,6,IntPtr.Zero)!=0){sqlite3_close(dest);throw new IOException("Cannot open backup destination");}try{IntPtr b=sqlite3_backup_init(dest,U("main"),db,U("main"));if(b==IntPtr.Zero)throw new IOException("Cannot initialize SQLite backup");try{int rc=sqlite3_backup_step(b,-1);if(rc!=101)throw new IOException("SQLite backup failed: "+rc);}finally{sqlite3_backup_finish(b);}}finally{sqlite3_close(dest);}}
}
public class Record {
 public DateTime Day;public int Hour;public string App="";public Dictionary<string,double> V=new Dictionary<string,double>();
 public double Get(string k){double v;return V.TryGetValue(k,out v)?v:0;}
}
public class Snapshot {
 public List<Record> Daily=new List<Record>(),AllDaily=new List<Record>(),Apps=new List<Record>(),Hourly=new List<Record>(),Network=new List<Record>();
 public List<Record> SourceDaily=new List<Record>();public List<Dictionary<string,object>> TrafficSources=new List<Dictionary<string,object>>();public string CurrentNetwork="",NetworkSourceError="";
 public DateTime Loaded;public string Status="",NetworkStatus=""; public bool Healthy;public int Cached,Requested;
}
public static class Store {
 public static string Root=NativeRuntime.Data;
 public static JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
 public static string[] Metrics={"clicks","keys","words","distance_inches","download_bytes","upload_bytes","uptime_seconds","focus_seconds","idle_seconds","unclassified_seconds","active_seconds"};
 public static double Number(object o){return o==null?0:Convert.ToDouble(o,CultureInfo.InvariantCulture);}
 public static Dictionary<string,object> ReadJson(string p){return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(p,Encoding.UTF8));}
 static List<Record> Read(Sqlite db,string sql,params object[] args){return db.Query(sql,args).Select(r=>{
  var x=new Record();object v;if(r.TryGetValue("day",out v))x.Day=DateTime.Parse((string)v,CultureInfo.InvariantCulture);if(r.TryGetValue("application",out v))x.App=Convert.ToString(v);if(r.TryGetValue("hour",out v))x.Hour=(int)Number(v);
  foreach(var m in Metrics)if(r.TryGetValue(m,out v))x.V[m]=Number(v);return x;}).ToList();}
 public static Snapshot Load(DateTime from,DateTime to,bool all){
  var x=new Snapshot();x.Loaded=DateTime.Now;
  using(var db=new Sqlite(Path.Combine(Root,"inputatlas.sqlite"))){
   x.Daily=Read(db,all?"SELECT * FROM daily ORDER BY day":"SELECT * FROM daily WHERE day>=? AND day<=? ORDER BY day",all?new object[0]:new object[]{from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd")});
   x.AllDaily=all?x.Daily:Read(db,"SELECT * FROM daily ORDER BY day");
   if(all&&x.Daily.Count>0)from=x.Daily[0].Day;
   object[] range={from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd")};
   x.Apps=Read(db,"SELECT * FROM application_activity WHERE day>=? AND day<=?",range);
   x.Hourly=Read(db,"SELECT * FROM application_hourly_activity WHERE day=?",to.ToString("yyyy-MM-dd"));
   if(Number(db.Query("SELECT COUNT(*) n FROM sqlite_master WHERE name='network_source_daily'")[0]["n"])>0){x.SourceDaily=Read(db,"SELECT day,source_id application,download_bytes,upload_bytes FROM network_source_daily ORDER BY day");x.TrafficSources=db.Query("SELECT s.id,s.name,s.kind,COALESCE(SUM(d.download_bytes+d.upload_bytes),0) bytes FROM network_sources s LEFT JOIN network_source_daily d ON d.source_id=s.id GROUP BY s.id ORDER BY bytes DESC,s.name");}
   // Collector status reports collection health only. Undated input is discarded.
  }
  try{var hb=ReadJson(Path.Combine(Root,"collector-heartbeat.json"));var at=DateTimeOffset.FromUnixTimeSeconds((long)Number(hb.ContainsKey("sample_at")?hb["sample_at"]:hb["api_at"])).LocalDateTime;x.Healthy=(DateTime.Now-at).TotalSeconds<20&&Convert.ToString(hb["error"])=="";x.Status=(x.Healthy?"Collector online":"Collector delayed / offline")+" · "+at.ToString("yyyy-MM-dd HH:mm:ss")+(x.Status==""?"":"\n"+x.Status);if(hb.ContainsKey("current_networks"))x.CurrentNetwork=String.Join(", ",((System.Collections.IEnumerable)hb["current_networks"]).Cast<object>().Select(Convert.ToString));if(hb.ContainsKey("network_source_error"))x.NetworkSourceError=Convert.ToString(hb["network_source_error"]);}catch{x.Status="Collector status unavailable";}
  for(var d=from.Date;d<=to.Date&&d<=DateTime.Today;d=d.AddDays(1)){
   x.Requested++;string p=Path.Combine(Root,"network-cache",d.ToString("yyyy-MM-dd")+".json");if(!File.Exists(p))continue;
   try{var doc=ReadJson(p);object[] rows=((System.Collections.IEnumerable)doc["rows"]).Cast<object>().ToArray();foreach(Dictionary<string,object> r in rows){var n=new Record{Day=d,App=Convert.ToString(r["application"])};n.V["download_bytes"]=Number(r["download_bytes"]);n.V["upload_bytes"]=Number(r["upload_bytes"]);n.V["traffic_bytes"]=n.Get("download_bytes")+n.Get("upload_bytes");x.Network.Add(n);}x.Cached++;}catch{}
  }
  x.NetworkStatus=String.Format("Windows network cache: {0}/{1} days. Attribution may be delayed; foreground time is not used.",x.Cached,x.Requested);
  try{var s=ReadJson(Path.Combine(Root,"network-cache","status.json"));if(Convert.ToBoolean(s["running"]))x.NetworkStatus+=" Updating in background…";}catch{}
  return x;
 }
 public static DateTime Period(DateTime d,int mode){if(mode==1)return d.Date.AddDays(-(((int)d.DayOfWeek+6)%7));if(mode==2)return new DateTime(d.Year,d.Month,1);if(mode==3)return new DateTime(d.Year,1,1);if(mode==4)return new DateTime(1970,1,1);return d.Date;}
 public static List<Record> SummaryRows(Snapshot snapshot,int mode,DateTime end){var start=Period(end,mode);return snapshot.AllDaily.Where(r=>r.Day<=end.Date&&(mode==4||r.Day>=start)).ToList();}
 public static DateTime Next(DateTime d,int mode){return mode==1?d.AddDays(7):mode==2?d.AddMonths(1):mode==3?d.AddYears(1):d.AddDays(1);}
 public static List<Record> Group(List<Record> rows,int mode,DateTime from,DateTime to,bool pad=true){
  var groups=rows.GroupBy(r=>Period(r.Day,mode)).ToDictionary(g=>g.Key,g=>{var r=new Record{Day=g.Key};foreach(string m in Metrics)r.V[m]=g.Sum(v=>v.Get(m));return r;});
  if(mode==4){if(groups.Count==0)groups[new DateTime(1970,1,1)]=new Record{Day=new DateTime(1970,1,1)};return groups.Values.ToList();}
  var first=Period(from,mode);var end=Period(to,mode);if(pad&&groups.Count>0) {var min=groups.Keys.Min();var before=min;for(int i=0;i<8;i++)before=mode==1?before.AddDays(-7):mode==2?before.AddMonths(-1):mode==3?before.AddYears(-1):before.AddDays(-1);if(first>before)first=before;}
  var result=new List<Record>();for(var d=first;d<=end;d=Next(d,mode)){Record r;if(!groups.TryGetValue(d,out r))r=new Record{Day=d};result.Add(r);if(result.Count>20000)break;}return result;
 }
 public static string Label(DateTime d,int mode){return mode==4?"All time":mode==3?d.ToString("yyyy"):mode==2?d.ToString("yyyy-MM"):mode==1?d.ToString("MM-dd")+" wk":d.ToString("MM-dd");}
}
}
