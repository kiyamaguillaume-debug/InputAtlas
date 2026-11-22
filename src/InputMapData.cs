using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace InputAtlas {
public sealed class InputMapData {
 public Dictionary<int,double> Keys=new Dictionary<int,double>(),UnmappedKeys=new Dictionary<int,double>(),Buttons=new Dictionary<int,double>();
 public List<Dictionary<string,object>> Cells=new List<Dictionary<string,object>>(),Displays=new List<Dictionary<string,object>>();
 public string Started="",LegacyFirst="",LegacyLast="",CombinedBefore="";public double Clicks,Scrolls,LegacyKeys,LegacyPoints,ExcludedKeys,ExcludedPoints,ExcludedButtons;public int Source;
 public double KeyTotal{get{return Keys.Values.Sum()+UnmappedKeys.Values.Sum();}}
}
public static class InputMapStore {
 static bool Has(Sqlite db,string table){return Store.Number(db.Query("SELECT COUNT(*) n FROM sqlite_master WHERE type='table' AND name=?",table)[0]["n"])>0;}
 static void Add(Dictionary<int,double> values,int key,double n){double old;values.TryGetValue(key,out old);values[key]=old+n;}
 public static InputMapData Read(string root,DateTime start,DateTime end){return Read(root,start,end,0);}
 // 0: combined without overlap; 1: full imported history; 2: native only.
 public static InputMapData Read(string root,DateTime start,DateTime end,int source){
  var data=new InputMapData{Source=source};string a=start.ToString("yyyy-MM-dd"),b=end.ToString("yyyy-MM-dd");
  using(var db=new Sqlite(Path.Combine(root,"inputatlas.sqlite"))){db.Query("BEGIN");try{
   if(source!=1&&Has(db,"native_key_heat")){
    if(Has(db,"native_engine_meta")){var meta=db.Query("SELECT started_at FROM native_engine_meta WHERE id=1");if(meta.Count>0)data.Started=Convert.ToString(meta[0]["started_at"]);}
    foreach(var row in db.Query("SELECT key_id,SUM(count) n FROM native_key_heat WHERE day>=? AND day<=? GROUP BY key_id",a,b))Add(data.Keys,(int)Store.Number(row["key_id"]),Store.Number(row["n"]));
    data.Cells=db.Query("SELECT display,x,y,button,SUM(count) n FROM native_mouse_heat WHERE day>=? AND day<=? GROUP BY display,x,y,button",a,b);
    data.Displays=db.Query("SELECT * FROM native_displays ORDER BY display");
    foreach(var row in data.Cells)Add(data.Buttons,(int)Store.Number(row["button"]),Store.Number(row["n"]));
    data.Scrolls=Store.Number(db.Query("SELECT SUM(scrolls) n FROM native_input_totals WHERE day>=? AND day<=?",a,b)[0]["n"]);
   }
   if(source!=2&&Has(db,"legacy_input_import")){
    var meta=db.Query("SELECT * FROM legacy_input_import WHERE id=1");if(meta.Count>0){var m=meta[0];data.LegacyFirst=Convert.ToString(m["first_day"]);data.LegacyLast=Convert.ToString(m["last_day"]);data.CombinedBefore=Convert.ToString(m["combined_before"]);
     string limit=source==0?" AND (day<? OR (day=? AND hour<?))":"";var cutoff=DateTime.Parse(data.CombinedBefore);object[] args=source==0?new object[]{a,b,cutoff.ToString("yyyy-MM-dd"),cutoff.ToString("yyyy-MM-dd"),cutoff.Hour}:new object[]{a,b};
     foreach(var row in db.Query("SELECT key_id,legacy_key,SUM(count) n FROM legacy_key_heat WHERE key_id>0 AND day>=? AND day<=?"+limit+" GROUP BY key_id,legacy_key",args)){double n=Store.Number(row["n"]);int id=(int)Store.Number(row["key_id"]);Add(id==0?data.UnmappedKeys:data.Keys,id==0?(int)Store.Number(row["legacy_key"]):id,n);data.LegacyKeys+=n;}
     var cells=db.Query("SELECT display,x,y,0 button,SUM(count) n FROM legacy_mouse_heat WHERE day>=? AND day<=?"+limit+" GROUP BY display,x,y",args);data.Cells.AddRange(cells);data.LegacyPoints=cells.Sum(r=>Store.Number(r["n"]));
     foreach(var row in db.Query("SELECT button,SUM(count) n FROM legacy_mouse_buttons WHERE day>=? AND day<=?"+limit+" GROUP BY button",args))Add(data.Buttons,(int)Store.Number(row["button"]),Store.Number(row["n"]));
     data.Scrolls+=Store.Number(db.Query("SELECT SUM(count) n FROM legacy_scrolls WHERE day>=? AND day<=?"+limit,args)[0]["n"]);
     foreach(var id in cells.Select(r=>Convert.ToString(r["display"])).Distinct())data.Displays.Add(new Dictionary<string,object>{{"display",id},{"width",0},{"height",0}});
     if(source==0){string d=cutoff.ToString("yyyy-MM-dd");object[] gap={a,b,d,d,cutoff.Hour};string where=" WHERE day>=? AND day<=? AND (day>? OR (day=? AND hour>=?))";data.ExcludedKeys=Store.Number(db.Query("SELECT SUM(count) n FROM legacy_key_heat"+where,gap)[0]["n"]);data.ExcludedPoints=Store.Number(db.Query("SELECT SUM(count) n FROM legacy_mouse_heat"+where,gap)[0]["n"]);data.ExcludedButtons=Store.Number(db.Query("SELECT SUM(count) n FROM legacy_mouse_buttons"+where,gap)[0]["n"]);}
    }
   }
   data.Clicks=data.Cells.Sum(r=>Store.Number(r["n"]));db.Query("COMMIT");
  }catch{try{db.Query("ROLLBACK");}catch{}throw;}}
  return data;
 }
}
}
