using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace InputAtlas {
public sealed class ShortcutRow {public string Combination{get;set;}public string Code{get;set;}public long Count{get;set;}public bool ShiftOnly,ModifierOnly;}
public sealed class ShortcutData {public List<ShortcutRow> Rows=new List<ShortcutRow>();public string Started="",LegacyFirst="",LegacyLast="";}
public static class ShortcutStore {
 public static bool Has(Sqlite db,string name){return Store.Number(db.Query("SELECT COUNT(*) n FROM sqlite_master WHERE type='table' AND name=?",name)[0]["n"])>0;}
 public static ShortcutData Read(string root,DateTime start,DateTime end,int source){var data=new ShortcutData();var counts=new Dictionary<string,long>();string a=start.ToString("yyyy-MM-dd"),b=end.ToString("yyyy-MM-dd");using(var db=new Sqlite(Path.Combine(root,"inputatlas.sqlite"))){db.Query("BEGIN");try{
  foreach(string table in new[]{"legacy_shortcuts","native_shortcuts"}){if(table=="legacy_shortcuts"&&source==2||table=="native_shortcuts"&&source==1||!Has(db,table))continue;foreach(var r in db.Query("SELECT combo,SUM(count) n FROM "+table+" WHERE day>=? AND day<=? GROUP BY combo",a,b)){string code=Convert.ToString(r["combo"]);long old;counts.TryGetValue(code,out old);counts[code]=old+(long)Store.Number(r["n"]);}}
  if(source!=1&&Has(db,"native_shortcut_meta")){var m=db.Query("SELECT started_at FROM native_shortcut_meta WHERE id=1");if(m.Count>0)data.Started=Convert.ToString(m[0]["started_at"]);}
  if(source!=2&&Has(db,"legacy_shortcuts")){var m=db.Query("SELECT MIN(day) first,MAX(day) last FROM legacy_shortcuts")[0];data.LegacyFirst=Convert.ToString(m["first"]);data.LegacyLast=Convert.ToString(m["last"]);}
  db.Query("COMMIT");}catch{try{db.Query("ROLLBACK");}catch{}throw;}}
  foreach(var r in counts){int mask,key;string label=ShortcutCodec.Label(r.Key);if(label==null||!ShortcutCodec.Decode(r.Key,out mask,out key))continue;data.Rows.Add(new ShortcutRow{Combination=label,Code=r.Key,Count=r.Value,ShiftOnly=mask==2,ModifierOnly=key==0});}data.Rows=data.Rows.OrderByDescending(r=>r.Count).ThenBy(r=>r.Combination).ToList();return data;
 }
}
}
