using System;using System.IO;using System.Linq;using System.Globalization;using System.Collections.Generic;
namespace InputAtlas {
public static class NetworkSourceStore {
 public static void Initialize(Sqlite db){
  db.Query("CREATE TABLE IF NOT EXISTS network_sources(id TEXT PRIMARY KEY,name TEXT,kind TEXT,first_seen TEXT,last_seen TEXT)");
  db.Query("CREATE TABLE IF NOT EXISTS network_source_daily(day TEXT,source_id TEXT,download_bytes INTEGER NOT NULL DEFAULT 0,upload_bytes INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(day,source_id))");
  db.Query("CREATE TABLE IF NOT EXISTS network_source_state(adapter TEXT PRIMARY KEY,source_id TEXT,sampled_at REAL,revision TEXT)");
  db.Query("CREATE TABLE IF NOT EXISTS network_source_meta(id INTEGER PRIMARY KEY,started_at TEXT,history_source TEXT,history_basis TEXT,switch_tested INTEGER DEFAULT 0,first_switch_at TEXT)");
  db.Query("CREATE TABLE IF NOT EXISTS network_source_events(at TEXT,adapter TEXT,previous_source TEXT,current_source TEXT,reason TEXT)");
 }
 public static void Register(Sqlite db,NetworkOrigin source,DateTime now){db.Query("INSERT OR IGNORE INTO network_sources VALUES(?,?,?,?,?)",source.Id,source.Name,source.Kind,now.ToString("o"),now.ToString("o"));db.Query("UPDATE network_sources SET name=?,kind=?,last_seen=? WHERE id=?",source.Name,source.Kind,now.ToString("o"),source.Id);}
 static bool SameRevision(string a,string b){int i=a.IndexOf(':'),j=b.IndexOf(':');return i<0||j<0||a.Substring(0,i)!=b.Substring(0,j)||a==b;}
 public static string Attribute(NetworkOrigin source,Dictionary<string,object> previous,double oldCounterAt,double elapsed){if(source==null||source.Kind=="unknown"||previous==null||elapsed<=0||elapsed>10)return "unassigned";if(Math.Abs(Store.Number(previous["sampled_at"])-oldCounterAt)>.001||Convert.ToString(previous["source_id"])!=source.Id||!SameRevision(Convert.ToString(previous["revision"]),source.Revision))return "unassigned";return source.Id;}
 public static void Allocate(Sqlite db,string source,DateTime at,double down,double up){if(source=="unassigned"||String.IsNullOrEmpty(source)||(down==0&&up==0))return;string day=at.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);db.Query("INSERT OR IGNORE INTO network_source_daily(day,source_id) VALUES(?,?)",day,source);db.Query("UPDATE network_source_daily SET download_bytes=download_bytes+?,upload_bytes=upload_bytes+? WHERE day=? AND source_id=?",down,up,day,source);}
 public static void SaveState(Sqlite db,string adapter,NetworkOrigin source,double at,Dictionary<string,object> previous,DateTime now){
  if(source.Kind!="unknown")Register(db,source,now);
  if(previous!=null&&(Convert.ToString(previous["source_id"])!=source.Id||!SameRevision(Convert.ToString(previous["revision"]),source.Revision))){db.Query("INSERT INTO network_source_events VALUES(?,?,?,?,?)",now.ToString("o"),adapter,previous["source_id"],source.Id,Convert.ToString(previous["source_id"])==source.Id?"connection boundary":"source change");if(source.Kind!="unknown")db.Query("UPDATE network_source_meta SET first_switch_at=COALESCE(first_switch_at,?) WHERE id=1 AND history_source<>?",now.ToString("o"),source.Id);}
  db.Query("INSERT OR REPLACE INTO network_source_state VALUES(?,?,?,?)",adapter,source.Id,at,source.Revision);
 }
 public static bool Conserved(Sqlite db){return db.Query("SELECT d.day FROM daily d LEFT JOIN (SELECT day,SUM(download_bytes) down,SUM(upload_bytes) up FROM network_source_daily GROUP BY day) s ON s.day=d.day WHERE d.download_bytes<>COALESCE(s.down,0) OR d.upload_bytes<>COALESCE(s.up,0)").Count==0;}
}
}
