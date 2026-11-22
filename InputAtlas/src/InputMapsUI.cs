using System;using System.IO;using System.Linq;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Data;
namespace InputAtlas {public partial class MainWindow {
 int inputMapKind,inputMapGeneration,mouseHeatPalette;double mouseHeatEmphasis=MouseHeatScale.DefaultEmphasis,keyboardHeatEmphasis=MouseHeatScale.DefaultEmphasis;string inputMapDisplay="all";
 async void InputMapsPage(){
  int request=++inputMapGeneration;Heading("Keyboard and mouse maps","键盘与鼠标热图");
  var selectors=new WrapPanel();
  var selector=new ComboBox{ItemsSource=zh?new[]{"键盘键位热图","鼠标点击位置热图","鼠标按键统计"}:new[]{"Keyboard key heatmap","Mouse click heatmap","Mouse button counts"},SelectedIndex=inputMapKind,Width=235,Margin=new Thickness(0,0,12,8)};
  selector.SelectionChanged+=(s,e)=>{if(selector.SelectedIndex<0)return;inputMapKind=selector.SelectedIndex;SaveSettings();Render();};selectors.Children.Add(selector);page.Children.Add(selectors);
  var loading=Txt(T("Loading recorded input…","正在读取输入统计…"),12);page.Children.Add(loading);
  DateTime end=to.SelectedDate.Value.Date,start=mode.SelectedIndex==4?new DateTime(1970,1,1):Store.Period(end,mode.SelectedIndex);
  try{
   var maps=await Task.Run(()=>InputMapStore.Read(Store.Root,start,end,0));if(request!=inputMapGeneration||tabs.SelectedIndex!=6)return;page.Children.Remove(loading);
   AddNote("This view uses the selected day, week, month, year or all-time period.","此页按所选的日、周、月、年或总计周期汇总。");
   if(inputMapKind==0){
    Heading("Keystrokes in this view: "+maps.KeyTotal.ToString("N0"),"当前视图按键数："+maps.KeyTotal.ToString("N0"));InputHeatPalette();var keyboard=new KeyboardHeatView(maps.Keys,zh,mouseHeatPalette,keyboardHeatEmphasis);InputHeatEmphasis(true,keyboard.SetEmphasis);page.Children.Add(keyboard);AddNote("Relative key use: 100% is the most-used key in this view. Hover a key for its recorded count.","相对按键频次：当前视图使用最多的键为 100%；悬停查看原始次数。");
   }else{
    if(inputMapKind==1){
     var ids=new[]{"all"}.Concat(maps.Displays.Select(r=>Convert.ToString(r["display"]))).ToArray();var labels=new[]{T("All displays · normalized","全部屏幕 · 按比例合并")}.Concat(maps.Displays.Select((r,i)=>Convert.ToString(r["display"]).StartsWith("WhatPulse display",StringComparison.Ordinal)?T("Unassigned screen positions","未分配屏幕的位置"):T("Display ","屏幕 ")+(i+1)+(Store.Number(r["width"])>0?" · "+r["width"]+" × "+r["height"]:""))).ToArray();
     var display=new ComboBox{ItemsSource=labels,SelectedIndex=Math.Max(0,Array.IndexOf(ids,inputMapDisplay)),MinWidth=260,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,10)};display.SelectionChanged+=(s,e)=>{if(display.SelectedIndex<0)return;inputMapDisplay=ids[display.SelectedIndex];SaveSettings();Render();};page.Children.Add(display);
     InputHeatPalette();var map=new MouseHeatView(maps,inputMapDisplay,zh,mouseHeatPalette,mouseHeatEmphasis);InputHeatEmphasis(false,map.SetEmphasis);page.Children.Add(map);
     AddNote("Relative density: 100% is the busiest area in this view. Darker means fewer clicks; brighter means more.","相对点击密度：当前图最密集处为 100%；颜色越暗点击越少，越亮点击越多。");
     AddNote("Hover a position to see the recorded clicks in that cell.","将指针移到图上，可查看对应网格记录的点击次数。");
    }
    Heading("Mouse buttons · all displays","鼠标按键 · 全部屏幕");MouseButtonTable(maps);AddNote("Scroll steps: "+maps.Scrolls.ToString("N0"),"滚轮步数："+maps.Scrolls.ToString("N0"));
   }
   Heading("Input measurement","输入计量设置");int dpi=800;try{dpi=(int)Store.Number(Store.ReadJson(Path.Combine(Store.Root,"native-input-settings.json"))["MouseDpi"]);}catch{}
   var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(Txt(T("Mouse DPI: ","鼠标 DPI："),13));var field=new TextBox{Text=dpi.ToString(),Width=100};row.Children.Add(field);var save=new Button{Content=T("Apply","应用"),Margin=new Thickness(10,0,0,0),Padding=new Thickness(16,5,16,5)};
   save.Click+=(s,e)=>{int value;if(!Int32.TryParse(field.Text,out value))return;value=Math.Max(100,Math.Min(64000,value));string p=Path.Combine(Store.Root,"native-input-settings.json"),tmp=p+".tmp";File.WriteAllText(tmp,Store.Json.Serialize(new{MouseDpi=value}));if(File.Exists(p))File.Replace(tmp,p,null);else File.Move(tmp,p);field.Text=value.ToString();};row.Children.Add(save);page.Children.Add(row);
   AddNote("Native distance is estimated from raw mouse movement and DPI. Words are estimated at Space/Enter boundaries.","独立采集的距离根据鼠标原始移动量与 DPI 估算，词数按空格／回车边界估算。");
  }catch(Exception ex){if(request==inputMapGeneration&&tabs.SelectedIndex==6)loading.Text=T("Map read status: ","热图读取状态：")+ex.Message;}
 }
 void InputHeatPalette(){var palette=new WrapPanel{Margin=new Thickness(0,0,0,12)};var rainbow=Txt(T("Rainbow (RGB)","红绿蓝配色"),13);rainbow.Margin=new Thickness(0,6,14,0);palette.Children.Add(rainbow);var thermal=new CheckBox{Name="InputHeatPalette",Content=T("Thermal · purple / orange / yellow","热成像 · 深紫／橘黄"),IsChecked=mouseHeatPalette==1,Style=(Style)Application.Current.FindResource("ThemeSwitch"),VerticalAlignment=VerticalAlignment.Center};thermal.Click+=(s,e)=>{mouseHeatPalette=thermal.IsChecked==true?1:0;SaveSettings();Render();};palette.Children.Add(thermal);page.Children.Add(palette);}
 void InputHeatEmphasis(bool keyboard,Action<double> paint){var contrast=new WrapPanel{Margin=new Thickness(0,0,0,10),VerticalAlignment=VerticalAlignment.Center};var title=Txt(keyboard?T("Key emphasis","键位突出程度"):T("Hotspot emphasis","热点突出程度"),13);title.Margin=new Thickness(0,5,14,0);contrast.Children.Add(title);var low=Txt(T("More detail","更多细节"),12);low.Margin=new Thickness(0,5,8,0);contrast.Children.Add(low);
  var slider=new Slider{Name=keyboard?"KeyboardHeatEmphasis":"MouseHeatEmphasis",Minimum=MouseHeatScale.MinimumEmphasis,Maximum=MouseHeatScale.MaximumEmphasis,Value=MouseHeatScale.Clamp(keyboard?keyboardHeatEmphasis:mouseHeatEmphasis),TickFrequency=.05,IsSnapToTickEnabled=true,Width=220,VerticalAlignment=VerticalAlignment.Center,ToolTip=keyboard?T("Move right to emphasize frequently pressed keys.","向右移动，更突出按得最多的键。"):T("Move right to emphasize the most frequently clicked areas.","向右移动，更突出点击最频繁的位置。")};contrast.Children.Add(slider);var high=Txt(keyboard?T("Frequent keys","突出高频键"):T("Stronger hotspots","突出热点"),12);high.Margin=new Thickness(8,5,12,0);contrast.Children.Add(high);var reset=new Button{Content=T("Reset","重置"),Padding=new Thickness(12,3,12,3)};reset.Click+=(s,e)=>slider.Value=MouseHeatScale.DefaultEmphasis;contrast.Children.Add(reset);page.Children.Add(contrast);
  var update=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};update.Tick+=(s,e)=>{update.Stop();paint(keyboard?keyboardHeatEmphasis:mouseHeatEmphasis);SaveSettings();};slider.ValueChanged+=(s,e)=>{if(keyboard)keyboardHeatEmphasis=MouseHeatScale.Clamp(slider.Value);else mouseHeatEmphasis=MouseHeatScale.Clamp(slider.Value);update.Stop();update.Start();};slider.Unloaded+=(s,e)=>{update.Stop();SaveSettings();};
 }
 DataGrid MapTable(System.Collections.IEnumerable rows){return new DataGrid{ItemsSource=rows,IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,MaxHeight=340,MinHeight=80,MinColumnWidth=120,EnableRowVirtualization=true,Margin=new Thickness(0,0,0,16)};}
 void MouseButtonTable(InputMapData maps){
  double total=maps.Buttons.Values.Sum();var ids=new[]{1,2,3,4,5}.Concat(maps.Buttons.Keys.Where(k=>k>5).OrderBy(k=>k));string[] names=zh?new[]{"左键","右键","中键","侧键 1","侧键 2"}:new[]{"Left button","Right button","Middle button","Side button 1","Side button 2"};
  var rows=ids.Where(id=>id<=5).Select(id=>{double n;maps.Buttons.TryGetValue(id,out n);return new{Button=names[id-1],Count=n.ToString("N0"),Share=(total>0?n/total:0).ToString("P1")};}).ToList();double other=maps.Buttons.Where(r=>r.Key>5).Sum(r=>r.Value);if(other>0)rows.Add(new{Button=T("Other buttons","其他按钮"),Count=other.ToString("N0"),Share=(total>0?other/total:0).ToString("P1")});
  var table=MapTable(rows);table.Columns.Add(new DataGridTextColumn{Header=T("Button","鼠标按钮"),Binding=new Binding("Button"),MinWidth=220,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});table.Columns.Add(new DataGridTextColumn{Header=T("Count","次数"),Binding=new Binding("Count"),Width=150});table.Columns.Add(new DataGridTextColumn{Header=T("Share","占比"),Binding=new Binding("Share"),Width=110});page.Children.Add(table);
 }
}}
