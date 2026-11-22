using System;using System.IO;using System.Reflection;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using InputAtlas;
class UiSmoke {
 static FieldInfo Field(string name){return typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);}
 [STAThread]static int Main(string[] args){int result=0;try{
  if(args.Length!=2||args[0].IndexOf("tests",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Isolated tests profile required");
  Environment.SetEnvironmentVariable("INPUTATLAS_DATA_DIR",Path.GetFullPath(args[0]));NativeRuntime.Configure();NativeRuntime.TestInstance=true;
  Directory.CreateDirectory(args[1]);var app=new Application();var window=new MainWindow();
  window.Loaded+=async(s,e)=>{try{await Task.Delay(800);var tabs=(TabControl)Field("tabs").GetValue(window);tabs.SelectedIndex=0;await Task.Delay(200);var before=(Snapshot)Field("data").GetValue(window);double clicks=0;foreach(var row in before.AllDaily)clicks+=row.Get("clicks");string database=Path.Combine(Store.Root,"inputatlas.sqlite");DateTime mainStamp=File.GetLastWriteTimeUtc(database);using(var writer=new Sqlite(database,true)){string day=DateTime.Today.ToString("yyyy-MM-dd");writer.Query("INSERT OR IGNORE INTO daily(day) VALUES(?)",day);writer.Query("UPDATE daily SET clicks=clicks+7 WHERE day=?",day);if(File.GetLastWriteTimeUtc(database)!=mainStamp)throw new Exception("WAL test changed the main file unexpectedly");window.Reload(false);await Task.Delay(700);var after=(Snapshot)Field("data").GetValue(window);double updated=0;foreach(var row in after.AllDaily)updated+=row.Get("clicks");if(updated!=clicks+7)throw new Exception("Dashboard missed WAL-only changes");}for(int i=0;i<8;i++){tabs.SelectedIndex=i;await Task.Delay(450);window.UpdateLayout();}
   Field("inputMapKind").SetValue(window,1);tabs.SelectedIndex=6;window.Render();await Task.Delay(500);window.UpdateLayout();
   for(int theme=0;theme<2;theme++){Field("dark").SetValue(window,theme==1);Theme.Apply(window,theme==1);window.Render();await Task.Delay(300);window.UpdateLayout();var bmp=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(args[1],theme==0?"light.png":"dark.png")))encoder.Save(file);}
   var page=(StackPanel)Field("page").GetValue(window);foreach(var child in page.Children){var text=child as TextBlock;if(text!=null&&text.Text.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0)throw new Exception(text.Text);}
   File.WriteAllText(Path.Combine(args[1],"ui-result.json"),"{\"ok\":true,\"tabs\":8,\"themes\":2,\"walRefresh\":true}");
  }catch(Exception ex){result=1;File.WriteAllText(Path.Combine(args[1],"ui-error.txt"),ex.ToString());}finally{window.Close();}};
  app.Run(window);return result;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
