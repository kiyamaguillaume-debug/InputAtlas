using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
namespace InputAtlas {
// Rows stay alive during refresh. Only changed bound properties notify WPF.
public sealed class ControllerDisplayRow : INotifyPropertyChanged {
 public readonly string Key;readonly Dictionary<string,object> values=new Dictionary<string,object>();
 public event PropertyChangedEventHandler PropertyChanged;
 public ControllerDisplayRow(string key){Key=key;}
 object Get(string property){object value;return values.TryGetValue(property,out value)?value:null;}
 public ControllerDisplayRow Set(string property,object value){if(!Object.Equals(Get(property),value)){values[property]=value;var handler=PropertyChanged;if(handler!=null)handler(this,new PropertyChangedEventArgs(property));}return this;}
 public void CopyFrom(ControllerDisplayRow other){foreach(var value in other.values)Set(value.Key,value.Value);}
 public string Family {get{return Convert.ToString(Get("Family"));}}public string Controller {get{return Convert.ToString(Get("Controller"));}}public string Device {get{return Convert.ToString(Get("Device"));}}public string Api {get{return Convert.ToString(Get("Api"));}}public string Connection {get{return Convert.ToString(Get("Connection"));}}public string Status {get{return Convert.ToString(Get("Status"));}}public string LastButton {get{return Convert.ToString(Get("LastButton"));}}public string Button {get{return Convert.ToString(Get("Button"));}}public string Application {get{return Convert.ToString(Get("Application"));}}public long Presses {get{return Convert.ToInt64(Get("Presses")??0L);}}
}
public sealed class ControllerTableState {
 public readonly ObservableCollection<ControllerDisplayRow> Rows=new ObservableCollection<ControllerDisplayRow>();
 public void Apply(IEnumerable<ControllerDisplayRow> incoming){var next=incoming.ToArray();var keys=new HashSet<string>(next.Select(r=>r.Key));if(keys.Count!=next.Length)throw new InvalidOperationException("Duplicate controller row identity");var existing=Rows.ToDictionary(r=>r.Key);foreach(var row in next){ControllerDisplayRow retained;if(existing.TryGetValue(row.Key,out retained))retained.CopyFrom(row);else Rows.Add(row);}for(int i=Rows.Count-1;i>=0;i--)if(!keys.Contains(Rows[i].Key))Rows.RemoveAt(i);}
}
public partial class MainWindow {
 string controllerLayoutKey="";ControllerTableState controllerDeviceRows,controllerButtonRows,controllerAppRows;
 DataGrid controllerDevicesTable,controllerButtonsTable,controllerAppsTable;TextBlock controllerHealth,controllerError,controllerConnectHint;
 readonly Dictionary<string,Plot> controllerPlots=new Dictionary<string,Plot>();
 string ControllerLayoutKey(){return zh+"|"+dark+"|"+mode.SelectedIndex+"|"+from.SelectedDate+"|"+to.SelectedDate;}
 bool ControllerPageMatches(){return controllerDeviceRows!=null&&controllerLayoutKey==ControllerLayoutKey();}
 static ControllerDisplayRow DisplayRow(string key,params object[] pairs){var row=new ControllerDisplayRow(key);for(int i=0;i<pairs.Length;i+=2)row.Set((string)pairs[i],pairs[i+1]);return row;}
 void ControllerRefreshTick(){if(File.Exists(Path.Combine(Store.Root,"native-ui.stop"))){Close();return;}if(WindowState==WindowState.Minimized||data==null||busy)return;RefreshControllerSummary();if(tabs.SelectedIndex!=4||controllerSource!=null&&controllerSource.IsDropDownOpen)return;if(ControllerPageMatches())UpdateControllerPage();else Render();}
 static bool SameCounts(Dictionary<string,long> a,Dictionary<string,long> b){return a.Count==b.Count&&a.All(p=>b.ContainsKey(p.Key)&&b[p.Key]==p.Value);}
 static bool SamePlot(Plot a,Plot b){return a.Labels.SequenceEqual(b.Labels)&&a.Series.Count==b.Series.Count&&a.Series.Zip(b.Series,(x,y)=>x.V.SequenceEqual(y.V)).All(s=>s);}
 string ControllerDeviceLabel(string id){int slot;if(id.StartsWith("XInput-")&&Int32.TryParse(id.Substring(7),out slot))return T("Legacy slot ","旧槽位 ")+(slot+1);if(id.StartsWith("XboxUnconfirmed-"))return T("Unknown ","未确认 ")+id.Substring(Math.Max(0,id.Length-4));return id.Substring(Math.Max(0,id.Length-8));}
 string ConnectionLabel(string value){return value=="Unknown"||value=="XInput"?T("Unknown","未确认"):value=="Wireless"?T("Wireless","无线"):value;}
 void UpdateControllerPage(){
  if(!ControllerPageMatches())return;
  var devices=new List<ControllerDisplayRow>();bool online=false;string error="";
  try{var state=Store.ReadJson(Path.Combine(Store.Root,"controller-heartbeat.json"));online=!Convert.ToBoolean(state["stopped"])&&(DateTime.Now-DateTime.Parse(Convert.ToString(state["at"]))).TotalSeconds<8;
   foreach(Dictionary<string,object> d in (IEnumerable)state["devices"]){string family=Convert.ToString(d["kind"])=="Xbox"?"Xbox":"PlayStation",id=Convert.ToString(d["id"]);bool connected=!d.ContainsKey("connected")||Convert.ToBoolean(d["connected"]);devices.Add(DisplayRow(id,"Controller",Convert.ToString(d["name"]),"Device",ControllerDeviceLabel(id),"Api",d.ContainsKey("api")?Convert.ToString(d["api"]):(family=="Xbox"?"XInput":"Raw Input"),"Connection",ConnectionLabel(connected&&d.ContainsKey("connection")?Convert.ToString(d["connection"]):"—"),"Family",family,"Status",!online?T("Offline","离线"):!connected?T("Inactive","未活动"):Convert.ToBoolean(d["counting"])?T("Counting","统计中"):T("Paused","暂停"),"LastButton",PadButtons.Label(Convert.ToString(d["lastButton"]),family)));}
   error=Convert.ToString(state["error"])+" "+Convert.ToString(state["rawInputError"]);error=error.Trim();if(Store.Number(state["unrecognizedSonyReports"])>0)error+=(error.Length==0?"":" · ")+T("Some Sony reports were unrecognized.","部分 Sony 报告尚未识别。");
  }catch(IOException){return;}catch(Exception ex){error=ex.Message;}
  controllerDeviceRows.Apply(devices);controllerHealth.Text=online?T("Controller collector online","手柄采集器在线"):T("Controller collection paused / offline","手柄统计已暂停或离线");controllerHealth.Foreground=B(online?"#12B8A6":"#D99C15");controllerDevicesTable.Visibility=devices.Count>0?Visibility.Visible:Visibility.Collapsed;controllerConnectHint.Visibility=devices.Count==0?Visibility.Visible:Visibility.Collapsed;controllerError.Text=error.Length==0?"":T("Input status: ","输入状态：")+error;controllerError.Visibility=error.Length==0?Visibility.Collapsed:Visibility.Visible;
  try{
   var rows=ControllerRows();var current=rows.Where(r=>mode.SelectedIndex==4||r.Day>=Store.Period(to.SelectedDate.Value,mode.SelectedIndex)).ToList();
   foreach(var map in ControllerMaps()){var counts=current.Where(r=>GamepadPresentation.Includes(map.Family,r.Family)).GroupBy(r=>r.Button).ToDictionary(g=>g.Key,g=>g.Sum(r=>r.Presses));if(!map.IsMouseOver&&!SameCounts(map.Counts,counts)){map.Counts=counts;map.InvalidateVisual();}}
   foreach(var pair in controllerPlots){var next=ControllerPlot(rows.Where(r=>GamepadPresentation.Includes(pair.Key,r.Family)).ToList(),pair.Key);var plot=pair.Value;if(!plot.IsMouseOver&&!SamePlot(plot,next)){plot.Labels=next.Labels;plot.Series=next.Series;plot.InvalidateVisual();}}
   controllerButtonRows.Apply(rows.GroupBy(r=>new{r.Family,r.Controller,r.Device,r.Button}).OrderBy(g=>g.Key.Family).ThenByDescending(g=>g.Sum(r=>r.Presses)).Select(g=>DisplayRow(g.Key.Family+"\0"+g.Key.Controller+"\0"+g.Key.Device+"\0"+g.Key.Button,"Family",g.Key.Family,"Controller",g.Key.Controller,"Device",ControllerDeviceLabel(g.Key.Device),"Button",g.Key.Button,"Presses",g.Sum(r=>r.Presses))));
   controllerAppRows.Apply(rows.GroupBy(r=>new{r.Family,r.App}).OrderByDescending(g=>g.Sum(r=>r.Presses)).Select(g=>DisplayRow(g.Key.Family+"\0"+g.Key.App,"Family",g.Key.Family,"Application",g.Key.App,"Presses",g.Sum(r=>r.Presses))));
  }catch(Exception ex){controllerError.Text=T("Read failed: ","读取失败：")+ex.Message;controllerError.Visibility=Visibility.Visible;}
 }
}
}
