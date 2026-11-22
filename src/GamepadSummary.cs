using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace InputAtlas {
public static class ControllerSummaryStore {
 // Aggregate in SQLite instead of loading per-device/application rows into memory.
 public static long Read(string root,DateTime start,DateTime end,bool all){
  string file=Path.Combine(root,"gamepad.sqlite");if(!File.Exists(file))return 0;
  using(var db=new Sqlite(file)){
   var rows=db.Query(all?"SELECT button,SUM(presses) AS total FROM controller_presses WHERE day<=? GROUP BY button":"SELECT button,SUM(presses) AS total FROM controller_presses WHERE day>=? AND day<=? GROUP BY button",all?new object[]{end.ToString("yyyy-MM-dd")}:new object[]{start.ToString("yyyy-MM-dd"),end.ToString("yyyy-MM-dd")});
   return rows.Where(r=>PadButtons.Countable(Convert.ToString(r["button"]))).Sum(r=>(long)Store.Number(r["total"]));
  }
 }
 public static string Stamp(string root){
  return FileStamp(Path.Combine(root,"gamepad.sqlite"))+"|"+FileStamp(Path.Combine(root,"gamepad.sqlite-wal"));
 }
 static string FileStamp(string path){var info=new FileInfo(path);return info.Exists?info.LastWriteTimeUtc.Ticks+":"+info.Length:"missing";}
}
public partial class MainWindow {
 TextBlock controllerSummaryValue;bool controllerSummaryBusy;
 string controllerSummaryScope="",controllerSummaryStamp="";long? controllerSummaryCount;
 bool ControllerSummaryRange(out DateTime start,out DateTime end,out bool all,out string scope){
  start=DateTime.MinValue;end=DateTime.MinValue;all=mode.SelectedIndex==4;scope="";
  if(!from.SelectedDate.HasValue||!to.SelectedDate.HasValue||from.SelectedDate.Value>to.SelectedDate.Value)return false;
  end=to.SelectedDate.Value.Date;start=Store.Period(end,mode.SelectedIndex);
  scope=(all?"all":start.ToString("yyyy-MM-dd"))+"|"+end.ToString("yyyy-MM-dd");return true;
 }
 TextBlock ControllerSummaryText(){
  controllerSummaryValue=Txt("—",21);DateTime start,end;bool all;string scope;
  if(ControllerSummaryRange(out start,out end,out all,out scope)&&scope==controllerSummaryScope&&controllerSummaryCount.HasValue)SetControllerSummary(controllerSummaryCount.Value);
  return controllerSummaryValue;
 }
 void SetControllerSummary(long count){
  controllerSummaryValue.Text=Plot.Format(count,"count");controllerSummaryValue.ToolTip=T("All PS and Xbox button presses in this statistics period.","当前统计周期内 PS 和 Xbox 的全部按键次数。");
 }
 async void RefreshControllerSummary(){
  if(controllerSummaryValue==null||WindowState==WindowState.Minimized)return;
  DateTime start,end;bool all;string scope;if(!ControllerSummaryRange(out start,out end,out all,out scope))return;
  if(scope!=controllerSummaryScope)controllerSummaryValue.Text="—";
  if(controllerSummaryBusy)return;
  controllerSummaryBusy=true;
  try{
   // Include WAL metadata: live collector commits need not change the main file.
   string root=Store.Root,stamp=ControllerSummaryStore.Stamp(root);
   if(scope==controllerSummaryScope&&stamp==controllerSummaryStamp&&controllerSummaryCount.HasValue){SetControllerSummary(controllerSummaryCount.Value);return;}
   long count=await Task.Run(()=>ControllerSummaryStore.Read(root,start,end,all));
   DateTime currentStart,currentEnd;bool currentAll;string currentScope;
   if(!IsLoaded||!ControllerSummaryRange(out currentStart,out currentEnd,out currentAll,out currentScope)||scope!=currentScope)return;
   controllerSummaryScope=scope;controllerSummaryStamp=stamp;controllerSummaryCount=count;SetControllerSummary(count);
  }catch(Exception ex){
   DateTime currentStart,currentEnd;bool currentAll;string currentScope;
   if(IsLoaded&&ControllerSummaryRange(out currentStart,out currentEnd,out currentAll,out currentScope)&&scope==currentScope){controllerSummaryValue.Text="—";controllerSummaryValue.ToolTip=T("Unable to read controller statistics: ","手柄统计读取失败：")+ex.Message;}
  }finally{controllerSummaryBusy=false;}
 }
}
}
