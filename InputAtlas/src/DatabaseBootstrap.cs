using System;using System.IO;using System.Threading;
namespace InputAtlas {
public static class DatabaseBootstrap {
 public static void Ensure(string root){Directory.CreateDirectory(root);using(var gate=new Mutex(false,NativeRuntime.Name("initialize"))){bool owner=false;try{try{owner=gate.WaitOne(20000);}catch(AbandonedMutexException){owner=true;}if(!owner)throw new IOException("InputAtlas database initialization is busy. Try again in a moment.");
  using(var db=new Sqlite(Path.Combine(root,"inputatlas.sqlite"),true,true)){db.Query("PRAGMA journal_mode=WAL");db.Query("BEGIN IMMEDIATE");try{
   db.Query("CREATE TABLE IF NOT EXISTS daily(day TEXT PRIMARY KEY,clicks REAL NOT NULL DEFAULT 0,keys REAL NOT NULL DEFAULT 0,download_bytes REAL NOT NULL DEFAULT 0,upload_bytes REAL NOT NULL DEFAULT 0,words REAL NOT NULL DEFAULT 0,uptime_seconds REAL NOT NULL DEFAULT 0,distance_inches REAL NOT NULL DEFAULT 0,focus_seconds REAL NOT NULL DEFAULT 0,idle_seconds REAL NOT NULL DEFAULT 0,unclassified_seconds REAL NOT NULL DEFAULT 0)");
   db.Query("CREATE TABLE IF NOT EXISTS application_activity(day TEXT NOT NULL,application TEXT NOT NULL,active_seconds REAL NOT NULL DEFAULT 0,distance_inches REAL NOT NULL DEFAULT 0,download_bytes REAL NOT NULL DEFAULT 0,upload_bytes REAL NOT NULL DEFAULT 0,PRIMARY KEY(day,application))");
   db.Query("CREATE TABLE IF NOT EXISTS application_hourly_activity(day TEXT NOT NULL,hour INTEGER NOT NULL,application TEXT NOT NULL,active_seconds REAL NOT NULL DEFAULT 0,distance_inches REAL NOT NULL DEFAULT 0,download_bytes REAL NOT NULL DEFAULT 0,upload_bytes REAL NOT NULL DEFAULT 0,PRIMARY KEY(day,hour,application))");
   db.Query("CREATE TABLE IF NOT EXISTS collector_events(id INTEGER PRIMARY KEY,at REAL,kind TEXT,detail TEXT)");NetworkSourceStore.Initialize(db);
   db.Query("CREATE TABLE IF NOT EXISTS app_meta(name TEXT PRIMARY KEY,value TEXT)");db.Query("INSERT OR IGNORE INTO app_meta VALUES('schema_version','1')");db.Query("COMMIT");
  }catch{db.Query("ROLLBACK");throw;}}
  using(var db=new Sqlite(Path.Combine(root,"gamepad.sqlite"),true,true)){
   db.Query("PRAGMA journal_mode=WAL");db.Query("CREATE TABLE IF NOT EXISTS controller_presses(day TEXT NOT NULL,hour INTEGER NOT NULL,device TEXT NOT NULL,controller TEXT NOT NULL,button TEXT NOT NULL,application TEXT NOT NULL,presses INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(day,hour,device,button,application))");db.Query("CREATE INDEX IF NOT EXISTS controller_by_day ON controller_presses(day)");
  }
  WriteDefault(Path.Combine(root,"native-input-settings.json"),"{\"MouseDpi\":800}");WriteDefault(Path.Combine(root,"controller-settings.json"),"{\"mode\":\"both\"}");WriteDefault(Path.Combine(root,"settings.json"),"{\"language\":0,\"mode\":0,\"tab\":0,\"clicks\":true,\"keys\":true,\"words\":true,\"dark\":false,\"heatMode\":0,\"mouseHeatPalette\":1,\"mouseHeatEmphasis\":0.65,\"keyboardHeatEmphasis\":0.65,\"inputMapKind\":0,\"inputMapDisplay\":\"all\",\"networkSourceId\":\"all\"}");string monitor=Path.Combine(root,"monitor-settings.json");if(!File.Exists(monitor))new MonitorOptions().Save(root);
 }finally{if(owner)gate.ReleaseMutex();}}}
 static void WriteDefault(string file,string content){if(!File.Exists(file))File.WriteAllText(file,content);}
}
}
