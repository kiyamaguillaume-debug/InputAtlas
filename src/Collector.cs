using System;using System.Globalization;namespace InputAtlas {
// Foreground focus/idle accounting. Input and traffic are collected independently.
public static class Collector {
 static double Epoch(DateTime at){return new DateTimeOffset(at).ToUnixTimeMilliseconds()/1000.0;}
 public static void RecordActivity(Sqlite db,DateTime from,DateTime end,string app,double idle){double elapsed=(end-from).TotalSeconds;if(elapsed<=0)return;if(elapsed>6){db.Query("INSERT INTO collector_events(at,kind,detail) VALUES(?, 'activity_gap', ?)",Epoch(end),Store.Json.Serialize(new{start=Epoch(from),end=Epoch(end),seconds=elapsed}));return;}
  DateTime cursor=from,cutoff=idle>=0?end.AddSeconds(-idle+60):from;bool known=idle>=0;while(cursor<end){DateTime next=new DateTime(cursor.Year,cursor.Month,cursor.Day,cursor.Hour,0,0).AddHours(1);if(next>end)next=end;double seconds=(next-cursor).TotalSeconds,focus=known&&app!="Unknown"&&app!="LockApp"?Math.Max(0,(cutoff<next?cutoff:next).Subtract(cursor).TotalSeconds):0;focus=Math.Min(seconds,focus);double away=known?seconds-focus:0,unknown=known?0:seconds;string day=cursor.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
   db.Query("INSERT OR IGNORE INTO daily(day) VALUES(?)",day);db.Query("UPDATE daily SET uptime_seconds=uptime_seconds+?,focus_seconds=focus_seconds+?,idle_seconds=idle_seconds+?,unclassified_seconds=unclassified_seconds+? WHERE day=?",seconds,focus,away,unknown,day);
   if(focus>0){db.Query("INSERT OR IGNORE INTO application_activity(day,application) VALUES(?,?)",day,app);db.Query("UPDATE application_activity SET active_seconds=active_seconds+? WHERE day=? AND application=?",focus,day,app);db.Query("INSERT OR IGNORE INTO application_hourly_activity(day,hour,application) VALUES(?,?,?)",day,cursor.Hour,app);db.Query("UPDATE application_hourly_activity SET active_seconds=active_seconds+? WHERE day=? AND hour=? AND application=?",focus,day,cursor.Hour,app);}cursor=next;
  }
 }
}
}
