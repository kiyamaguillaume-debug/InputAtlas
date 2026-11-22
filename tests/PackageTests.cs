using System;using System.IO;using System.Linq;using System.Collections.Generic;using InputAtlas;
class PackageTests {
 static int checks;static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static double Scalar(string sql){using(var db=new Sqlite(Path.Combine(Store.Root,"inputatlas.sqlite")))return Store.Number(db.Query(sql)[0].Values.First());}
 static void Key(NativeInputModel model,int vk,int scan,DateTime at){model.Key(1,vk,scan,0,at,"Demo Editor");model.Key(1,vk,scan,1,at,"Demo Editor");}
 static int Main(string[] args){try{
  if(args.Length!=1||args[0].IndexOf("tests",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Isolated tests path required");
  Environment.SetEnvironmentVariable("INPUTATLAS_DATA_DIR",Path.GetFullPath(args[0]));NativeRuntime.Configure();
  var fresh=Store.Load(DateTime.Today,DateTime.Today,true);Check(fresh.Daily.Count==0,"fresh database starts empty");
  Check(!MonitorOptions.Load(Store.Root).OnTaskbar,"taskbar attachment is opt-in");
  Check(InputMapStore.Read(Store.Root,DateTime.Today,DateTime.Today).KeyTotal==0,"empty input-map reader");
  string original=NativeRuntime.Name("basic-collector");Environment.SetEnvironmentVariable("INPUTATLAS_DATA_DIR",Path.Combine(args[0],"other"));Check(original!=NativeRuntime.Name("basic-collector"),"profile IPC identities are separate");Environment.SetEnvironmentVariable("INPUTATLAS_DATA_DIR",Path.GetFullPath(args[0]));
  DateTime at=DateTime.Today.AddHours(12);var model=new NativeInputModel();Key(model,65,30,at);Key(model,32,57,at);
  model.Key(1,17,29,0,at,"Demo Editor");model.Key(1,67,46,0,at,"Demo Editor");model.Key(1,67,46,0,at,"Demo Editor");model.Key(1,67,46,1,at,"Demo Editor");model.Key(1,17,29,1,at,"Demo Editor");
  model.Click(at,"Demo Editor",1,"Demo display",20,15);model.Click(at,"Demo Editor",2,"Demo display",70,40);model.Scroll(at,"Demo Editor",120);
  Check(model.TotalKeys==4&&model.TotalClicks==2,"key-repeat filtering and mouse edges");
  using(var writer=new NativeInputStore(Path.Combine(Store.Root,"inputatlas.sqlite"))){writer.Display("Demo display",1920,1080,0,0);writer.Flush(model,at.AddSeconds(-2),at,"Demo Editor",0,null,1);}
  Check(Scalar("SELECT SUM(keys) FROM daily")==4&&Scalar("SELECT SUM(clicks) FROM daily")==2,"input totals persist");
  Check(Scalar("SELECT SUM(count) FROM native_shortcuts WHERE combo='1|67'")==1,"Ctrl+C combination persists once");
  var maps=InputMapStore.Read(Store.Root,at,at);Check(maps.KeyTotal==4&&maps.Buttons[1]==1&&maps.Buttons[2]==1,"map and button counts match recorded input");
  var source=NetworkOrigin.Wifi("Demo Wi-Fi");source.Revision="session:0";var previous=new Dictionary<string,object>{{"sampled_at",10.0},{"source_id",source.Id},{"revision",source.Revision}};
  Check(NetworkSourceStore.Attribute(source,previous,10,2)==source.Id,"stable network source is attributed");source.Revision="session:1";
  Check(NetworkSourceStore.Attribute(source,previous,10,2)=="unassigned","network reconnection boundary is discarded");
  Check(NetworkSourceStore.Attribute(null,previous,10,2)=="unassigned","unknown source is discarded");
  using(var db=new Sqlite(Path.Combine(Store.Root,"inputatlas.sqlite"),true)){NetworkSourceStore.Register(db,source,at);NetworkSourceStore.Allocate(db,"unassigned",at,1000,1000);Check(db.Query("SELECT * FROM network_source_daily").Count==0,"unassigned bytes create no totals");}
  Check(Scalar("SELECT SUM(download_bytes+upload_bytes) FROM daily")==0,"discarded network bytes leave dashboard unchanged");
  string settings=Path.Combine(Store.Root,"settings.json");File.WriteAllText(settings,"{\"language\":1}");NativeRuntime.Configure();Check(File.ReadAllText(settings)=="{\"language\":1}"&&Scalar("SELECT SUM(keys) FROM daily")==4,"initialization preserves existing settings and counters");
  var edges=new PadEdges();edges.Read(PadButtons.Xbox(0,0,0));var buttons=edges.Read(PadButtons.Xbox(0xC0,0,0));Check(buttons.Contains("L3 / LS")&&buttons.Contains("R3 / RS"),"Xbox stick clicks remain countable");Check(edges.Read(PadButtons.Xbox(0xC0,0,0)).Count==0,"held controller buttons do not repeat");
  using(var db=new Sqlite(Path.Combine(Store.Root,"inputatlas.sqlite"))){Check(NetworkSourceStore.Conserved(db),"source and dashboard network totals agree");Check(Convert.ToString(db.Query("PRAGMA integrity_check")[0]["integrity_check"])=="ok","database integrity");}
  File.WriteAllText(Path.Combine(Store.Root,"test-result.json"),"{\"ok\":true,\"checks\":"+checks+"}");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
