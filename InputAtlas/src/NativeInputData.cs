using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Globalization;
namespace InputAtlas {
public sealed class NativeInputOptions {
 public int MouseDpi=800;public static NativeInputOptions Load(string root){try{var v=Store.Json.Deserialize<NativeInputOptions>(File.ReadAllText(Path.Combine(root,"native-input-settings.json")));v.MouseDpi=Math.Max(100,Math.Min(64000,v.MouseDpi));return v;}catch{return new NativeInputOptions();}}
}
public sealed class InputBucket {
 public DateTime At;public string App;public long Keys,Clicks,Words,Scrolls;public double Distance;public Dictionary<int,long> KeyCounts=new Dictionary<int,long>();public Dictionary<string,long> Points=new Dictionary<string,long>(),ShortcutCounts=new Dictionary<string,long>();
}
public sealed class NativeInputModel {
 public readonly Dictionary<string,InputBucket> Pending=new Dictionary<string,InputBucket>();readonly HashSet<string> held=new HashSet<string>();readonly ShortcutTracker shortcuts=new ShortcutTracker();readonly Action<DateTime,string,string> addShortcut;int wordLength;public long TotalKeys,TotalClicks,TotalShortcuts;
 public NativeInputModel(){addShortcut=RecordShortcut;}
 public static int KeyId(int vk,int scan,int flags){if(vk<=0||vk>=255||scan==255)return 0;if(vk==16&&(flags&2)!=0)return 0;if((flags&2)==0){int[] scans={71,72,73,75,76,77,79,80,81,82,83};int[] ids={103,104,105,100,101,102,97,98,99,96,110};int index=Array.IndexOf(scans,scan);if(index>=0)return ids[index];}if(vk==16)return scan==54?161:160;if(vk==17)return (flags&2)!=0?163:162;if(vk==18)return (flags&2)!=0?165:164;if(vk==13&&(flags&2)!=0)return 269;return vk;}
 InputBucket Bucket(DateTime at,string app){string key=at.ToString("yyyy-MM-dd HH",CultureInfo.InvariantCulture)+"|"+app;InputBucket b;if(!Pending.TryGetValue(key,out b)){b=new InputBucket{At=at,App=app};Pending.Add(key,b);}return b;}
 void RecordShortcut(DateTime at,string app,string combo){var b=Bucket(at,app);long n;b.ShortcutCounts.TryGetValue(combo,out n);b.ShortcutCounts[combo]=n+1;TotalShortcuts++;}
 public void ResetEdges(){held.Clear();shortcuts.Reset();wordLength=0;}
 public void PruneReleasedKeys(Func<int,bool> isDown){foreach(string id in held.ToArray()){int pos=id.IndexOf('|'),key=Int32.Parse(id.Substring(pos+1));if(isDown(key==269?13:key))continue;held.Remove(id);shortcuts.Forget(Int64.Parse(id.Substring(0,pos)),key);}wordLength=0;}
 public bool Key(long device,int vk,int scan,int flags,DateTime at,string app){int key=KeyId(vk,scan,flags);if(key==0)return false;string identity=device+"|"+key;if((flags&1)!=0){if(held.Remove(identity))shortcuts.Edge(device,key,false,at,app,addShortcut);return false;}if(!held.Add(identity))return false;shortcuts.Edge(device,key,true,at,app,addShortcut);var b=Bucket(at,app);b.Keys++;TotalKeys++;long n;b.KeyCounts.TryGetValue(key,out n);b.KeyCounts[key]=n+1;if((key>=65&&key<=90)||(key>=48&&key<=57)||(key>=96&&key<=105))wordLength++;else if(key==8)wordLength=Math.Max(0,wordLength-1);else if(key==32||key==13||key==269){if(wordLength>0)b.Words++;wordLength=0;}return true;}
 public void Click(DateTime at,string app,int button,string display,int x,int y){var b=Bucket(at,app);b.Clicks++;TotalClicks++;string k=display+"\t"+Math.Max(0,Math.Min(127,x))+"\t"+Math.Max(0,Math.Min(71,y))+"\t"+button;long n;b.Points.TryGetValue(k,out n);b.Points[k]=n+1;}
 public void Move(DateTime at,string app,double inches){if(inches>0&&inches<100000)Bucket(at,app).Distance+=inches;}
 public void Scroll(DateTime at,string app,int delta){Bucket(at,app).Scrolls+=Math.Max(1,Math.Abs(delta)/120);}
}
public sealed class NativeInputStore:IDisposable {
 readonly Sqlite db;public NativeInputStore(string file){db=new Sqlite(file,true);Initialize(db);}
 public static void Initialize(Sqlite db){
  NetworkSourceStore.Initialize(db);
  db.Query("CREATE TABLE IF NOT EXISTS native_key_heat(day TEXT,hour INTEGER,key_id INTEGER,count INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(day,hour,key_id))");
  db.Query("CREATE TABLE IF NOT EXISTS native_mouse_heat(day TEXT,hour INTEGER,display TEXT,x INTEGER,y INTEGER,button INTEGER,count INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(day,hour,display,x,y,button))");
  db.Query("CREATE TABLE IF NOT EXISTS native_displays(display TEXT PRIMARY KEY,width INTEGER,height INTEGER,left_px INTEGER,top_px INTEGER)");
  db.Query("CREATE TABLE IF NOT EXISTS native_input_totals(day TEXT,hour INTEGER,application TEXT,keys INTEGER DEFAULT 0,clicks INTEGER DEFAULT 0,words INTEGER DEFAULT 0,scrolls INTEGER DEFAULT 0,distance_inches REAL DEFAULT 0,PRIMARY KEY(day,hour,application))");
  db.Query("CREATE TABLE IF NOT EXISTS native_shortcuts(day TEXT,hour INTEGER,application TEXT,combo TEXT,count INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(day,hour,application,combo))");
  db.Query("CREATE TABLE IF NOT EXISTS native_shortcut_meta(id INTEGER PRIMARY KEY,started_at TEXT)");db.Query("INSERT OR IGNORE INTO native_shortcut_meta VALUES(1,?)",DateTime.Now.ToString("o"));
  db.Query("CREATE TABLE IF NOT EXISTS native_network_state(adapter TEXT PRIMARY KEY,luid TEXT,boot REAL,received REAL,sent REAL,sampled_at REAL)");
  db.Query("CREATE TABLE IF NOT EXISTS native_engine_meta(id INTEGER PRIMARY KEY,started_at TEXT,source TEXT)");db.Query("INSERT OR IGNORE INTO native_engine_meta VALUES(1,?,'Raw Input + IP Helper')",DateTime.Now.ToString("o"));
 }
 public void Display(string id,int width,int height,int left,int top){db.Query("INSERT OR REPLACE INTO native_displays VALUES(?,?,?,?,?)",id,width,height,left,top);}
 static string Day(DateTime at){return at.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}
 public void Flush(NativeInputModel model,DateTime previous,DateTime now,string app,double idle,List<MonitorMetrics.IfRow> network,double boot,Dictionary<string,NetworkOrigin> networkOrigins=null){
  db.Query("BEGIN IMMEDIATE");try{
   Collector.RecordActivity(db,previous,now,app,idle);
   foreach(var b in model.Pending.Values){string day=Day(b.At);db.Query("INSERT OR IGNORE INTO daily(day) VALUES(?)",day);db.Query("UPDATE daily SET keys=keys+?,clicks=clicks+?,words=words+?,distance_inches=distance_inches+? WHERE day=?",b.Keys,b.Clicks,b.Words,b.Distance,day);
    db.Query("INSERT OR IGNORE INTO native_input_totals(day,hour,application) VALUES(?,?,?)",day,b.At.Hour,b.App);db.Query("UPDATE native_input_totals SET keys=keys+?,clicks=clicks+?,words=words+?,scrolls=scrolls+?,distance_inches=distance_inches+? WHERE day=? AND hour=? AND application=?",b.Keys,b.Clicks,b.Words,b.Scrolls,b.Distance,day,b.At.Hour,b.App);
    if(b.Distance>0){db.Query("INSERT OR IGNORE INTO application_activity(day,application) VALUES(?,?)",day,b.App);db.Query("UPDATE application_activity SET distance_inches=distance_inches+? WHERE day=? AND application=?",b.Distance,day,b.App);db.Query("INSERT OR IGNORE INTO application_hourly_activity(day,hour,application) VALUES(?,?,?)",day,b.At.Hour,b.App);db.Query("UPDATE application_hourly_activity SET distance_inches=distance_inches+? WHERE day=? AND hour=? AND application=?",b.Distance,day,b.At.Hour,b.App);}
    foreach(var key in b.KeyCounts){db.Query("INSERT OR IGNORE INTO native_key_heat(day,hour,key_id) VALUES(?,?,?)",day,b.At.Hour,key.Key);db.Query("UPDATE native_key_heat SET count=count+? WHERE day=? AND hour=? AND key_id=?",key.Value,day,b.At.Hour,key.Key);}
    foreach(var combo in b.ShortcutCounts){db.Query("INSERT OR IGNORE INTO native_shortcuts(day,hour,application,combo) VALUES(?,?,?,?)",day,b.At.Hour,b.App,combo.Key);db.Query("UPDATE native_shortcuts SET count=count+? WHERE day=? AND hour=? AND application=? AND combo=?",combo.Value,day,b.At.Hour,b.App,combo.Key);}
    foreach(var cell in b.Points){var p=cell.Key.Split('\t');db.Query("INSERT OR IGNORE INTO native_mouse_heat(day,hour,display,x,y,button) VALUES(?,?,?,?,?,?)",day,b.At.Hour,p[0],Int32.Parse(p[1]),Int32.Parse(p[2]),Int32.Parse(p[3]));db.Query("UPDATE native_mouse_heat SET count=count+? WHERE day=? AND hour=? AND display=? AND x=? AND y=? AND button=?",cell.Value,day,b.At.Hour,p[0],Int32.Parse(p[1]),Int32.Parse(p[2]),Int32.Parse(p[3]));}
   }
   if(network!=null)foreach(var row in network.Where(MonitorMetrics.Physical)){string id=row.Guid.ToString("D"),luid=row.Luid.ToString(CultureInfo.InvariantCulture);double stamp=new DateTimeOffset(now).ToUnixTimeMilliseconds()/1000.0;NetworkOrigin origin;if(networkOrigins==null||!networkOrigins.TryGetValue(id,out origin))origin=new NetworkOrigin();var previousOrigins=db.Query("SELECT * FROM network_source_state WHERE adapter=?",id);var previousOrigin=previousOrigins.Count>0?previousOrigins[0]:null;var old=db.Query("SELECT * FROM native_network_state WHERE adapter=?",id);if(old.Count>0){var s=old[0];double elapsed=stamp-Store.Number(s["sampled_at"]),receive=row.InOctets-Store.Number(s["received"]),send=row.OutOctets-Store.Number(s["sent"]);DateTime last=DateTimeOffset.FromUnixTimeSeconds((long)Store.Number(s["sampled_at"])).LocalDateTime;bool same=Day(last)==Day(now);if(Convert.ToString(s["luid"])==luid&&Math.Abs(Store.Number(s["boot"])-boot)<60&&receive>=0&&send>=0&&elapsed>0&&(same||elapsed<=10)){string source=NetworkSourceStore.Attribute(origin,previousOrigin,Store.Number(s["sampled_at"]),elapsed);if(source!="unassigned"){db.Query("INSERT OR IGNORE INTO daily(day) VALUES(?)",Day(now));db.Query("UPDATE daily SET download_bytes=download_bytes+?,upload_bytes=upload_bytes+? WHERE day=?",receive,send,Day(now));NetworkSourceStore.Allocate(db,source,now,receive,send);}}}
    NetworkSourceStore.SaveState(db,id,origin,stamp,previousOrigin,now);
    db.Query("INSERT OR REPLACE INTO native_network_state VALUES(?,?,?,?,?,?)",id,luid,boot,(double)row.InOctets,(double)row.OutOctets,stamp);
   }
   db.Query("COMMIT");model.Pending.Clear();
  }catch{try{db.Query("ROLLBACK");}catch{}throw;}
 }
 public void Dispose(){db.Dispose();}
}
}
