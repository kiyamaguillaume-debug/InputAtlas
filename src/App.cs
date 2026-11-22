using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace InputAtlas {
public partial class MainWindow : Window {
 bool zh=false,ready=false,busy=false,pendingReload=false,dark=false,changingLabels=false;int generation=0;Snapshot data;
 ComboBox language,mode;DatePicker from,to;TabControl tabs;StackPanel page,cards;TextBlock status,notice;CheckBox clicks,keys,words,follow;
 DispatcherTimer timer;string loadedFile="",loadedKey="";string lastWorker="";DateTime workerAttempt=DateTime.MinValue;Process networkWorker;
 TextBlock languageLabel,rangeLabel,viewLabel,sharedLabel;Button refresh;CheckBox darkSwitch;ScrollViewer mainScroll;DispatcherTimer saveTimer;
 int renderedTab=-1;
 string T(string en,string cn){return zh?cn:en;}
 Brush B(string hex){return new SolidColorBrush(Palette.Parse(hex));}
 TextBlock Txt(string s,double size=14){var t=new TextBlock{Text=s,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};t.SetResourceReference(TextBlock.ForegroundProperty,"Ink");return t;}
 public MainWindow(){
  Title="InputAtlas · 输入图谱";using(var icon=typeof(MainWindow).Assembly.GetManifestResourceStream("InputAtlas.Icon.ico")){if(icon!=null)Icon=BitmapFrame.Create(icon,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);}Width=Math.Min(1360,SystemParameters.WorkArea.Width-40);Height=Math.Min(920,SystemParameters.WorkArea.Height-40);MinWidth=1000;MinHeight=660;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI");FontSize=14;Theme.Apply(this,false);
  var outer=new Grid{Margin=new Thickness(24)};outer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(245)});outer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});outer.ColumnDefinitions.Add(new ColumnDefinition());Content=outer;
  var side=new StackPanel();var sideCard=new Border{Child=side,CornerRadius=new CornerRadius(16),Padding=new Thickness(19,22,19,18),VerticalAlignment=VerticalAlignment.Top};sideCard.SetResourceReference(Border.BackgroundProperty,"Panel");outer.Children.Add(sideCard);var brand=Txt("InputAtlas",24);brand.FontWeight=FontWeights.SemiBold;side.Children.Add(brand);var subtitle=Txt("NATIVE DESKTOP · CONTROLLERS",10);subtitle.SetResourceReference(TextBlock.ForegroundProperty,"Muted");subtitle.Margin=new Thickness(0,0,0,24);side.Children.Add(subtitle);
  languageLabel=Txt("Language",12);side.Children.Add(languageLabel);language=new ComboBox{ItemsSource=new[]{"English","Chinese (Simplified)"},SelectedIndex=0,Margin=new Thickness(0,0,0,22)};side.Children.Add(language);
  rangeLabel=Txt("Date range",12);side.Children.Add(rangeLabel);from=new DatePicker{SelectedDate=DateTime.Today.AddDays(-29),Margin=new Thickness(0,0,0,10)};to=new DatePicker{SelectedDate=DateTime.Today,Margin=new Thickness(0,0,0,12)};side.Children.Add(from);side.Children.Add(to);
  follow=new CheckBox{Content="Follow today",IsChecked=true,Margin=new Thickness(0,0,0,22)};side.Children.Add(follow);
  viewLabel=Txt("Statistics view",12);side.Children.Add(viewLabel);mode=new ComboBox{ItemsSource=new[]{"Daily","Weekly","Monthly","Yearly","All time"},SelectedIndex=0,Margin=new Thickness(0,0,0,22)};side.Children.Add(mode);
  refresh=new Button{Content="Refresh",Margin=new Thickness(0,0,0,18)};refresh.Click+=(s,e)=>{StartNetwork(true);Reload(true);};side.Children.Add(refresh);
  darkSwitch=new CheckBox{Content="Dark mode",IsChecked=false,Margin=new Thickness(0,0,0,22),Style=(Style)Application.Current.FindResource("ThemeSwitch")};darkSwitch.Click+=(s,e)=>{dark=darkSwitch.IsChecked==true;Theme.Apply(this,dark);Render();SaveSettings();};side.Children.Add(darkSwitch);
  status=Txt("Loading…",12);side.Children.Add(status);sharedLabel=Txt("SQLite · local data",11);sharedLabel.SetResourceReference(TextBlock.ForegroundProperty,"Muted");sharedLabel.Margin=new Thickness(0,12,0,0);side.Children.Add(sharedLabel);
  var right=new Grid();Grid.SetColumn(right,2);outer.Children.Add(right);right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());
  cards=new StackPanel{Margin=new Thickness(0,0,0,8)};right.Children.Add(cards);
  notice=Txt("",12);notice.SetResourceReference(TextBlock.ForegroundProperty,"Muted");Grid.SetRow(notice,1);right.Children.Add(notice);
  tabs=new TabControl{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Margin=new Thickness(0,8,0,12)};Grid.SetRow(tabs,2);right.Children.Add(tabs);
  foreach(string title in new[]{"Input activity","Network traffic","Heatmaps","Computer activity","Controllers","Live monitor","Input maps","Key combinations"})tabs.Items.Add(new TabItem{Header=title,Padding=new Thickness(13,9,13,9)});tabs.SelectedIndex=0;
  page=new StackPanel();mainScroll=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(mainScroll,3);right.Children.Add(mainScroll);mainScroll.PreviewMouseWheel+=RouteTableWheelToPage;
  language.SelectionChanged+=(s,e)=>{if(changingLabels)return;zh=language.SelectedIndex==1;LocalizeShell();Render();SaveSettings();};mode.SelectionChanged+=(s,e)=>{if(!changingLabels)Reload(true);};from.SelectedDateChanged+=(s,e)=>Reload(true);to.SelectedDateChanged+=(s,e)=>Reload(true);tabs.SelectionChanged+=(s,e)=>{if(e.Source==tabs){Render();mainScroll.ScrollToTop();if(tabs.SelectedIndex==1)StartNetwork(false);SaveSettings();}};
  Loaded+=(s,e)=>{ReadSettings();LocalizeShell();Theme.Apply(this,dark);ready=true;Reload(true);};SourceInitialized+=(s,e)=>Theme.Apply(this,dark);
  saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};saveTimer.Tick+=(s,e)=>{saveTimer.Stop();SaveSettings();};SizeChanged+=(s,e)=>QueueSave();LocationChanged+=(s,e)=>QueueSave();StateChanged+=(s,e)=>QueueSave();Closing+=(s,e)=>SaveSettings();
  timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};timer.Tick+=(s,e)=>{if(WindowState==WindowState.Minimized)return;NativeRuntime.EnsureWorkers();if(follow.IsChecked==true&&to.SelectedDate.HasValue&&to.SelectedDate.Value.Date<DateTime.Today){int days=(to.SelectedDate.Value-from.SelectedDate.Value).Days;to.SelectedDate=DateTime.Today;from.SelectedDate=DateTime.Today.AddDays(-days);}Reload(false);};timer.Start();
  InitControllerRefresh();InitLiveMonitor();Closed+=(s,e)=>{controllerTimer.Stop();timer.Stop();saveTimer.Stop();if(networkWorker!=null)networkWorker.Dispose();};
 }
 void QueueSave(){if(!ready)return;saveTimer.Stop();saveTimer.Start();}
 void RouteTableWheelToPage(object sender,System.Windows.Input.MouseWheelEventArgs e){
  if(e.Handled||e.Delta==0)return;
  DependencyObject node=e.OriginalSource as DependencyObject;bool table=false;
  while(node!=null&&node!=mainScroll){
   if(node is DataGrid){table=true;break;}
   var content=node as ContentElement;
   if(content!=null){var framework=content as FrameworkContentElement;node=ContentOperations.GetParent(content)??(framework==null?null:framework.Parent);}
   else node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
  }
  if(!table)return;
  // Consume the tunneled event before the nested DataGrid ScrollViewer sees it.
  // Reuse the outer viewer's native wheel handling, including system scroll settings.
  e.Handled=true;
  mainScroll.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice,e.Timestamp,e.Delta){RoutedEvent=System.Windows.Input.Mouse.MouseWheelEvent,Source=mainScroll});
 }
 void LocalizeShell(){changingLabels=true;try{Theme.Chinese=zh;int selected=Math.Max(0,mode.SelectedIndex);language.ItemsSource=zh?new[]{"英语","简体中文"}:new[]{"English","Chinese (Simplified)"};language.SelectedIndex=zh?1:0;languageLabel.Text=T("Language","语言");rangeLabel.Text=T("Date range","查看范围");viewLabel.Text=T("Statistics view","统计视图");mode.ItemsSource=zh?new[]{"每日","每周","每月","每年","总计"}:new[]{"Daily","Weekly","Monthly","Yearly","All time"};mode.SelectedIndex=selected;follow.Content=T("Follow today","跟随今天");refresh.Content=T("Refresh data","刷新数据");darkSwitch.Content=T("Dark mode","深色模式");sharedLabel.Text=T("SQLite · local data","SQLite · 本地数据库");}finally{changingLabels=false;}SyncMonitorLanguage();}
 void ReadSettings(){try{var s=Store.ReadJson(Path.Combine(Store.Root,"settings.json"));language.SelectedIndex=(int)Store.Number(s["language"]);mode.SelectedIndex=(int)Store.Number(s["mode"]);tabs.SelectedIndex=(int)Store.Number(s["tab"]);clicksSelected=Convert.ToBoolean(s["clicks"]);keysSelected=Convert.ToBoolean(s["keys"]);wordsSelected=Convert.ToBoolean(s["words"]);if(s.ContainsKey("dark"))dark=Convert.ToBoolean(s["dark"]);darkSwitch.IsChecked=dark;if(s.ContainsKey("heatMode"))heatMode=(int)Store.Number(s["heatMode"]);if(s.ContainsKey("mouseHeatPalette"))mouseHeatPalette=Store.Number(s["mouseHeatPalette"])==1?1:0;if(s.ContainsKey("mouseHeatEmphasis"))mouseHeatEmphasis=MouseHeatScale.Clamp(Store.Number(s["mouseHeatEmphasis"]));if(s.ContainsKey("keyboardHeatEmphasis"))keyboardHeatEmphasis=MouseHeatScale.Clamp(Store.Number(s["keyboardHeatEmphasis"]));if(s.ContainsKey("inputMapKind"))inputMapKind=Math.Max(0,Math.Min(2,(int)Store.Number(s["inputMapKind"])));if(s.ContainsKey("inputMapDisplay"))inputMapDisplay=Convert.ToString(s["inputMapDisplay"]);if(s.ContainsKey("networkSourceId"))networkSourceId=Convert.ToString(s["networkSourceId"]);
   if(s.ContainsKey("windowWidth")&&s.ContainsKey("windowHeight")){double w=Store.Number(s["windowWidth"]),h=Store.Number(s["windowHeight"]);if(!double.IsNaN(w)&&!double.IsNaN(h)&&w>=MinWidth&&h>=MinHeight){Width=Math.Min(w,SystemParameters.VirtualScreenWidth);Height=Math.Min(h,SystemParameters.VirtualScreenHeight);if(s.ContainsKey("windowLeft")&&s.ContainsKey("windowTop")){double left=Store.Number(s["windowLeft"]),top=Store.Number(s["windowTop"]);var screen=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);if(screen.IntersectsWith(new Rect(left,top,Width,Height))){WindowStartupLocation=WindowStartupLocation.Manual;Left=Math.Max(screen.Left,Math.Min(left,screen.Right-160));Top=Math.Max(screen.Top,Math.Min(top,screen.Bottom-100));}}}}
   if(s.ContainsKey("maximized")&&Convert.ToBoolean(s["maximized"]))WindowState=WindowState.Maximized;
  }catch{}}
 void SaveSettings(){if(!ready)return;try{Rect bounds=WindowState==WindowState.Normal?new Rect(Left,Top,ActualWidth,ActualHeight):RestoreBounds;if(bounds.IsEmpty||bounds.Width<MinWidth||bounds.Height<MinHeight)return;string path=Path.Combine(Store.Root,"settings.json"),tmp=path+".tmp";File.WriteAllText(tmp,Store.Json.Serialize(new{language=language.SelectedIndex,mode=mode.SelectedIndex,tab=tabs.SelectedIndex,clicks=clicksSelected,keys=keysSelected,words=wordsSelected,dark=dark,heatMode=heatMode,mouseHeatPalette=mouseHeatPalette,mouseHeatEmphasis=mouseHeatEmphasis,keyboardHeatEmphasis=keyboardHeatEmphasis,inputMapKind=inputMapKind,inputMapDisplay=inputMapDisplay,networkSourceId=networkSourceId,windowWidth=bounds.Width,windowHeight=bounds.Height,windowLeft=bounds.Left,windowTop=bounds.Top,maximized=WindowState==WindowState.Maximized}));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}catch{}}
 public async void Reload(bool force){
  if(!ready)return;if(!from.SelectedDate.HasValue||!to.SelectedDate.HasValue||from.SelectedDate>to.SelectedDate){status.Text=T("Please choose a valid date range.","请选择有效的起止日期。");return;}
  int id=++generation;if(busy){pendingReload=true;return;}
  var a=from.SelectedDate.Value.Date;var b=to.SelectedDate.Value.Date;if(mode.SelectedIndex>0&&mode.SelectedIndex<4)a=Store.Period(a,mode.SelectedIndex);bool all=mode.SelectedIndex==4;string key=a.ToString()+b+all;
  string file=Store.DatabaseStamp(Path.Combine(Store.Root,"inputatlas.sqlite"));if(!force&&file==loadedFile&&loadedKey==key&&tabs.SelectedIndex!=1)return;
  busy=true;try{var next=await Task.Run(()=>Store.Load(a,b,all));if(id!=generation)return;data=next;loadedFile=file;loadedKey=key;Render();if(tabs.SelectedIndex==1)StartNetwork(false);}catch(Exception ex){status.Text=T("Read failed: ","读取失败：")+ex.Message;}finally{busy=false;if(pendingReload){pendingReload=false;Reload(true);}}
 }
 List<Record> Periods(bool ten=false){if(data==null)return new List<Record>();int m=mode.SelectedIndex;var start=m==4&&data.Daily.Count>0?data.Daily.Min(r=>r.Day):from.SelectedDate.Value;
  var p=Store.Group(data.Daily,m,start,to.SelectedDate.Value);if(m==0&&ten&&p.Count>10)p=p.Skip(p.Count-10).ToList();return p;}
 List<Record> Summary(){return Store.SummaryRows(data,mode.SelectedIndex,to.SelectedDate.Value);}
 new string Name(string m){switch(m){case "clicks":return T("Mouse clicks","鼠标点击");case "keys":return T("Keystrokes","键盘按键");case "words":return T("Words","输入词数");case "controller_presses":return T("Controller presses","手柄按键");case "distance_inches":return T("Mouse distance","鼠标距离");case "download_bytes":return T("Downloads","下载");case "upload_bytes":return T("Uploads","上传");case "focus_seconds":return T("Focus time","专注时间");case "idle_seconds":return T("Idle time","空闲时间");case "unclassified_seconds":return T("Unclassified","未分类");default:return m;}}
 string Unit(string m){return m.Contains("bytes")?"bytes":m.Contains("seconds")?"time":m=="distance_inches"?"distance":"count";}
 Color MetricColor(string m){return m=="clicks"?Palette.Teal:m=="keys"?Palette.Blue:m=="words"?Palette.Amber:m=="download_bytes"?Palette.Blue:m=="upload_bytes"?Palette.Purple:m=="focus_seconds"?Palette.Teal:m=="unclassified_seconds"?Palette.Parse("#CBD3E1"):Palette.Gray;}
 Plot MetricPlot(List<Record> rows,string title,params string[] metrics){var p=new Plot{Title=title,Labels=rows.Select(r=>Store.Label(r.Day,mode.SelectedIndex)).ToArray(),Tips=rows.Select(r=>r.Day.ToString("yyyy-MM-dd")).ToArray(),Unit=metrics.Length==0?"count":Unit(metrics[0])};foreach(string m in metrics)p.Series.Add(new Series(Name(m),rows.Select(r=>r.Get(m)),MetricColor(m)));return p;}
 void AddNote(string en,string cn){page.Children.Add(Txt(T(en,cn),12));}
 void Heading(string en,string cn){var t=Txt(T(en,cn),19);t.FontWeight=FontWeights.SemiBold;t.Margin=new Thickness(0,14,0,9);page.Children.Add(t);}
 public void Render(){
  if(data==null)return;int currentTab=tabs.SelectedIndex;double scrollOffset=renderedTab==currentTab?mainScroll.VerticalOffset:0;bool reuseControllerPage=currentTab==4&&renderedTab==4&&ControllerPageMatches();renderedTab=currentTab;if(!reuseControllerPage)page.Children.Clear();cards.Children.Clear();string[] en={"Input activity","Network traffic","Heatmaps","Computer activity","Controllers","Live monitor","Input maps","Key combinations"},cn={"输入统计","网络流量","热图","电脑活动","手柄统计","实时监控","输入热图","快捷键"};for(int i=0;i<8;i++)((TabItem)tabs.Items[i]).Header=zh?cn[i]:en[i];
  status.Text=zh?data.Status.Replace("Collector online","采集器在线").Replace("Collector delayed / offline","采集器延迟／离线").Replace("Collector status unavailable","采集器状态暂不可用").Replace("undated intervals","条未确定日期的记录").Replace("clicks","次点击").Replace("keys","次按键").Replace("kept separately","已单独保留"):data.Status;status.Foreground=data.Healthy?B(dark?"#66DAC4":"#07877C"):B(dark?"#FFC281":"#B85623");notice.Text=T("Local SQLite · native rendering · loaded ","本地 SQLite · 原生绘图 · 读取于 ")+data.Loaded.ToString("HH:mm:ss")+(SourceId()=="all"?"":T(" · Network: "," · 网络来源：")+SourceLabel(SourceId()));
  string[] titles={T("Today","今天"),T("This week","本周"),T("This month","本月"),T("This year","本年"),T("All time","总计")};cards.Children.Add(Txt(titles[mode.SelectedIndex]+(to.SelectedDate.Value.Date==DateTime.Today?"":" · "+to.SelectedDate.Value.ToString("yyyy-MM-dd")),24));
  var summary=Summary();var cardWrap=new WrapPanel();foreach(string m in new[]{"clicks","keys","words","distance_inches","download_bytes","upload_bytes","controller_presses"}){
   var box=new StackPanel{MinWidth=102};var label=Txt(Name(m),11);label.SetResourceReference(TextBlock.ForegroundProperty,"Muted");box.Children.Add(label);var value=m=="controller_presses"?ControllerSummaryText():Txt(Plot.Format(IsNetworkMetric(m)?TrafficTotal(m):summary.Sum(r=>r.Get(m)),Unit(m)),21);value.FontWeight=FontWeights.SemiBold;value.Margin=new Thickness(0);box.Children.Add(value);var card=new Border{Child=box,CornerRadius=new CornerRadius(12),Padding=new Thickness(14,12,14,14),Margin=new Thickness(0,0,10,10)};card.SetResourceReference(Border.BackgroundProperty,"Card");cardWrap.Children.Add(card);
  }cards.Children.Add(cardWrap);RefreshControllerSummary();
  if(tabs.SelectedIndex==0)InputPage();else if(tabs.SelectedIndex==1)NetworkPage();else if(tabs.SelectedIndex==2)HeatmapPage();else if(tabs.SelectedIndex==3)ComputerPage();else if(tabs.SelectedIndex==5)LiveMonitorPage();else if(tabs.SelectedIndex==6)InputMapsPage();else if(tabs.SelectedIndex==7)ShortcutPage();else if(reuseControllerPage)UpdateControllerPage();else GamepadPage();
  if(!reuseControllerPage)Dispatcher.BeginInvoke(new Action(()=>{if(tabs.SelectedIndex==currentTab)mainScroll.ScrollToVerticalOffset(scrollOffset);}),DispatcherPriority.Loaded);
 }
 bool clicksSelected=true,keysSelected=true,wordsSelected=true;
 void InputPage(){
  var metrics=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,6,0,10)};clicks=new CheckBox{Content=Name("clicks"),IsChecked=clicksSelected,Margin=new Thickness(0,0,20,0)};keys=new CheckBox{Content=Name("keys"),IsChecked=keysSelected,Margin=new Thickness(0,0,20,0)};words=new CheckBox{Content=Name("words"),IsChecked=wordsSelected};metrics.Children.Add(clicks);metrics.Children.Add(keys);metrics.Children.Add(words);page.Children.Add(metrics);
  RoutedEventHandler change=(s,e)=>{clicksSelected=clicks.IsChecked==true;keysSelected=keys.IsChecked==true;wordsSelected=words.IsChecked==true;Render();SaveSettings();};clicks.Click+=change;keys.Click+=change;words.Click+=change;
  var chosen=new List<string>();if(clicksSelected)chosen.Add("clicks");if(keysSelected)chosen.Add("keys");if(wordsSelected)chosen.Add("words");page.Children.Add(MetricPlot(Periods(true),T("Input activity","输入统计"),chosen.ToArray()));
  if(mode.SelectedIndex==0)AddNote("Daily bars show the last 10 days within the selected range.","每日柱状图显示所选范围内最后 10 天。");
  Heading("Mouse distance by application","各应用鼠标移动距离");page.Children.Add(AppPlot("distance_inches",data.Hourly,data.Apps));AppTable(mode.SelectedIndex==0?data.Hourly:data.Apps,"distance_inches");
 }
 List<string> AppNames(IEnumerable<Record> rows,string metric){return rows.GroupBy(r=>r.App).OrderByDescending(g=>g.Sum(r=>r.Get(metric))).Take(8).Select(g=>g.Key).ToList();}
 Plot AppPlot(string metric,List<Record> hourly,List<Record> rows){
  bool day=mode.SelectedIndex==0;var source=day?hourly:rows;var names=AppNames(source,metric);bool other=source.Any(r=>!names.Contains(r.App));if(other)names.Add("Other");
  List<Record> periods=Periods();int n=day?24:periods.Count;string[] labels=day?Enumerable.Range(0,24).Select(i=>i.ToString()).ToArray():periods.Select(r=>Store.Label(r.Day,mode.SelectedIndex)).ToArray();
  var p=new Plot{Title=metric=="active_seconds"?T("Application activity","应用活跃时间"):Name(metric),Kind=day?"area":"bars",Stacked=true,Labels=labels,Unit=Unit(metric)};
  foreach(var app in names){var values=new double[n];foreach(var r in source.Where(v=>app=="Other"?!names.Contains(v.App):v.App==app)){int i=day?r.Hour:periods.FindIndex(v=>v.Day==Store.Period(r.Day,mode.SelectedIndex));if(i>=0&&i<n)values[i]+=r.Get(metric);}p.Series.Add(new Series(app,values,Palette.App(app)));}
  if(day)AddNote("Hourly values for "+to.SelectedDate.Value.ToString("yyyy-MM-dd")+"; hover for exact recorded values. Smooth curves are visual interpolation.",to.SelectedDate.Value.ToString("yyyy-MM-dd")+" 的逐小时记录；悬停查看原值，平滑曲线仅用于展示。");return p;
 }
 void AppTable(List<Record> rows,string metric){
  var groups=rows.GroupBy(r=>r.App).OrderByDescending(g=>g.Sum(r=>r.Get(metric))).ToList();double total=groups.Sum(g=>g.Sum(r=>r.Get(metric)));
  var view=groups.Select(g=>new{Application=g.Key,Value=Plot.Format(g.Sum(r=>r.Get(metric)),Unit(metric)),Share=total==0?"0%":(g.Sum(r=>r.Get(metric))/total).ToString("P1")}).ToList();
  var search=new TextBox{Margin=new Thickness(0,0,0,7),Padding=new Thickness(8),ToolTip=T("Search application","搜索应用")};page.Children.Add(search);
  var table=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,MaxHeight=360,MinHeight=100,GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,HeadersVisibility=DataGridHeadersVisibility.Column,ItemsSource=view,EnableRowVirtualization=true};
  table.Columns.Add(new DataGridTextColumn{Header=T("Application","应用程序"),Binding=new System.Windows.Data.Binding("Application"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});table.Columns.Add(new DataGridTextColumn{Header=metric=="active_seconds"?T("Active time","活跃时间"):Name(metric),Binding=new System.Windows.Data.Binding("Value"),Width=170});table.Columns.Add(new DataGridTextColumn{Header=T("Share","占比"),Binding=new System.Windows.Data.Binding("Share"),Width=100});
  search.TextChanged+=(s,e)=>table.ItemsSource=view.Where(v=>v.Application.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();page.Children.Add(table);
 }
 void NetworkPage(){
  NetworkSourcePicker();
  var summary=TrafficSummaryRows();var pie=new Plot{Title=T("Download / upload share","下载／上传占比"),Kind="pie",Labels=new[]{"Total"},Unit="bytes",MinWidth=300,Width=360};foreach(string m in new[]{"download_bytes","upload_bytes"})pie.Series.Add(new Series(Name(m),new[]{summary.Sum(r=>r.Get(m))},MetricColor(m)));
  var bars=MetricPlot(TrafficPeriods(true),T("Downloads and uploads","下载与上传"),"download_bytes","upload_bytes");bars.MinWidth=0;
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(360)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.Children.Add(pie);Grid.SetColumn(bars,1);grid.Children.Add(bars);page.Children.Add(grid);
  if(mode.SelectedIndex==0)AddNote(SourceId()=="all"?"Daily bars show the last 10 days. Cached application traffic can lag behind the live totals.":"Daily bars show the last 10 days.",SourceId()=="all"?"每日柱状图显示最后 10 天。应用流量缓存可能晚于实时总量。":"每日柱状图显示最后 10 天。");
  NetworkSourceBreakdown();if(SourceId()!="all"){AddNote("Application breakdown is available under All networks.","应用流量明细可切回「全部网络」查看。");return;}
  AddNote(data.NetworkStatus, String.Format("Windows 应用流量缓存：{0}/{1} 天。后台独立更新，未使用前台时间分配流量。",data.Cached,data.Requested));Heading("Network traffic by application","各应用网络流量");
  var periods=Periods();var apps=AppNames(data.Network,"traffic_bytes");bool other=data.Network.Any(r=>!apps.Contains(r.App));if(other)apps.Add("Other");var plot=new Plot{Title=T("Application traffic","应用流量"),Kind=periods.Count>1?"area":"bars",Stacked=true,Unit="bytes",Labels=periods.Select(r=>Store.Label(r.Day,mode.SelectedIndex)).ToArray()};
  foreach(var app in apps){var vals=new double[periods.Count];foreach(var r in data.Network.Where(v=>app=="Other"?!apps.Contains(v.App):v.App==app)){int i=periods.FindIndex(v=>v.Day==Store.Period(r.Day,mode.SelectedIndex));if(i>=0)vals[i]+=r.Get("traffic_bytes");}plot.Series.Add(new Series(app,vals,Palette.App(app)));}page.Children.Add(plot);
  if(data.Cached<data.Requested)AddNote("Missing cache days are unavailable, not confirmed zero traffic.","缺少缓存的日期尚无可用数据，不代表当天流量为零。");
  var table=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,MaxHeight=360,ItemsSource=data.Network.GroupBy(r=>r.App).OrderByDescending(g=>g.Sum(r=>r.Get("traffic_bytes"))).Select(g=>new{Application=g.Key,Download=Plot.Format(g.Sum(r=>r.Get("download_bytes")),"bytes"),Upload=Plot.Format(g.Sum(r=>r.Get("upload_bytes")),"bytes"),Total=Plot.Format(g.Sum(r=>r.Get("traffic_bytes")),"bytes")}).ToList(),EnableRowVirtualization=true};
  foreach(var name in new[]{"Application","Download","Upload","Total"})table.Columns.Add(new DataGridTextColumn{Header=name=="Application"?T(name,"应用程序"):name=="Download"?Name("download_bytes"):name=="Upload"?Name("upload_bytes"):T(name,"总流量"),Binding=new System.Windows.Data.Binding(name),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});page.Children.Add(table);
 }
 int heatMode=0;
 void HeatmapPage(){
  var picker=new ComboBox{ItemsSource=zh?new[]{"每日","每周","每月","每年","累计"}:new[]{"Daily","Weekly","Monthly","Yearly","Cumulative"},SelectedIndex=heatMode,Width=180,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,14)};picker.SelectionChanged+=(s,e)=>{heatMode=picker.SelectedIndex;Render();SaveSettings();};page.Children.Add(picker);
  foreach(string metric in new[]{"clicks","keys","words","download_bytes","upload_bytes"}){
   var map=new Heatmap{Rows=IsNetworkMetric(metric)?TrafficRows(heatMode==4):(heatMode==4?data.AllDaily:data.Daily),Title=Name(metric),Metric=metric,Unit=Unit(metric),Color=MetricColor(metric),End=to.SelectedDate.Value,Mode=heatMode};
   page.Children.Add(map);
  }
 }
 void ComputerPage(){
  var uptime=MetricPlot(Periods(true),T("Computer uptime","电脑运行时间"),"focus_seconds","idle_seconds","unclassified_seconds");uptime.Stacked=true;page.Children.Add(uptime);var rows=Summary();
  AddNote("Tracked uptime: "+Plot.Format(rows.Sum(r=>r.Get("uptime_seconds")),"time")+"    Focus: "+Plot.Format(rows.Sum(r=>r.Get("focus_seconds")),"time")+"    Idle: "+Plot.Format(rows.Sum(r=>r.Get("idle_seconds")),"time"),"已记录运行时间："+Plot.Format(rows.Sum(r=>r.Get("uptime_seconds")),"time")+"    专注："+Plot.Format(rows.Sum(r=>r.Get("focus_seconds")),"time")+"    空闲："+Plot.Format(rows.Sum(r=>r.Get("idle_seconds")),"time"));
  Heading("Application activity","应用活动");page.Children.Add(AppPlot("active_seconds",data.Hourly,data.Apps));AppTable(mode.SelectedIndex==0?data.Hourly:data.Apps,"active_seconds");AddNote("Focus means keyboard or mouse input within the last 60 seconds.","专注定义为最近 60 秒内有键盘或鼠标输入。");
 }
 void StartNetwork(bool force){if(NativeRuntime.TestInstance||SourceId()!="all")return;
  if(!ready||tabs.SelectedIndex!=1||!from.SelectedDate.HasValue||!to.SelectedDate.HasValue)return;
  if(networkWorker!=null){try{if(!networkWorker.HasExited)return;}catch{}networkWorker.Dispose();networkWorker=null;}
  string a=from.SelectedDate.Value.ToString("yyyy-MM-dd"),b=to.SelectedDate.Value.ToString("yyyy-MM-dd");if(mode.SelectedIndex==4&&data!=null&&data.Daily.Count>0)a=data.Daily.Min(r=>r.Day).ToString("yyyy-MM-dd");
  if(!force&&lastWorker==a+b&&(DateTime.Now-workerAttempt).TotalMinutes<5)return;lastWorker=a+b;workerAttempt=DateTime.Now;
  try{var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe"),"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(NativeRuntime.Base,"update-network-cache.ps1")+"\" -StartDate "+a+" -EndDate "+b+" -CacheDir \""+Path.Combine(Store.Root,"network-cache")+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Store.Root};networkWorker=Process.Start(info);}catch(Exception ex){notice.Text=T("Network refresh failed: ","网络刷新失败：")+ex.Message;}
 }
 public async Task Capture(string dir){Directory.CreateDirectory(dir);for(int i=0;i<4;i++){tabs.SelectedIndex=i;await Task.Delay(1000);UpdateLayout();var bmp=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(this);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(Path.Combine(dir,"view-"+i+".png")))enc.Save(f);}}
 public async Task Exercise(string dir){
  Directory.CreateDirectory(dir);var checks=new List<string>();
  for(int lang=0;lang<2;lang++){language.SelectedIndex=lang;for(int m=0;m<5;m++){mode.SelectedIndex=m;while(busy||data==null)await Task.Delay(100);for(int tab=0;tab<4;tab++){tabs.SelectedIndex=tab;Render();UpdateLayout();checks.Add("language="+lang+",mode="+m+",tab="+tab);await Task.Delay(50);}}}
  foreach(int h in new[]{0,1,2,3,4}){heatMode=h;tabs.SelectedIndex=2;Render();UpdateLayout();checks.Add("heatmap="+h);}
  mode.SelectedIndex=0;language.SelectedIndex=0;tabs.SelectedIndex=0;
  await Capture(dir);tabs.SelectedIndex=0;File.WriteAllText(Path.Combine(dir,"exercise.json"),Store.Json.Serialize(new{ok=true,checks=checks}));
 }
 public async Task VisualExercise(string dir){
  Directory.CreateDirectory(dir);var checks=new List<string>();while(busy||data==null)await Task.Delay(100);
  foreach(bool night in new[]{false,true}){
   dark=night;darkSwitch.IsChecked=night;Theme.Apply(this,night);
   foreach(int lang in new[]{0,1}){language.SelectedIndex=lang;LocalizeShell();
    foreach(double width in new[]{1000.0,1360.0}){Width=width;await Task.Delay(150);tabs.SelectedIndex=2;heatMode=0;Render();UpdateLayout();await Task.Delay(100);
     var maps=page.Children.OfType<Heatmap>().ToList();if(maps.Count!=5||maps.Any(m=>!m.CellsFit||m.CellCount<365))throw new Exception("Heatmap geometry or square cells failed");
     if(page.Children.OfType<ScrollViewer>().Any())throw new Exception("Nested heatmap scroll viewer consumes mouse wheel");
     mainScroll.ScrollToTop();await Task.Delay(100);var wheel=new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=System.Windows.Input.Mouse.MouseWheelEvent};maps[0].RaiseEvent(wheel);await Task.Delay(100);if(mainScroll.VerticalOffset<=0)throw new Exception("Mouse wheel did not reach page scroll viewer");
     if(!zh&&(languageLabel.Text!="Language"||refresh.Content.ToString().Contains("刷新")||follow.Content.ToString().Contains("跟随")))throw new Exception("Mixed language in English sidebar");
     mainScroll.ScrollToVerticalOffset(200);await Task.Delay(100);double offset=mainScroll.VerticalOffset;Render();UpdateLayout();await Task.Delay(100);if(offset>0&&Math.Abs(mainScroll.VerticalOffset-offset)>2)throw new Exception("Refresh lost scroll position");
     checks.Add("theme="+night+",language="+lang+",width="+width+",squareCellsFit=true,mouseWheelRouted=true,scrollPositionRetained=true");
    }
    await Capture(dir);tabs.SelectedIndex=2;Render();UpdateLayout();await Task.Delay(100);ExportView(Path.Combine(dir,(night?"dark":"light")+(zh?"-zh":"-en")+"-heatmap.png"));
   }
  }
  Width=1280;Height=850;dark=true;darkSwitch.IsChecked=true;Theme.Apply(this,true);language.SelectedIndex=0;tabs.SelectedIndex=2;heatMode=0;Render();SaveSettings();
  var saved=Store.ReadJson(Path.Combine(Store.Root,"settings.json"));if(!Convert.ToBoolean(saved["dark"])||Math.Abs(Store.Number(saved["windowWidth"])-1280)>2)throw new Exception("Theme/window preferences did not persist");
  checks.Add("theme and window dimensions persisted");File.WriteAllText(Path.Combine(dir,"visual-tests.json"),Store.Json.Serialize(new{ok=true,checks=checks}));
 }
 public async Task StyleExercise(string dir){
  Directory.CreateDirectory(dir);var checks=new List<object>();while(busy||data==null)await Task.Delay(100);WindowState=WindowState.Normal;
  if(Icon==null)throw new Exception("Window icon is missing");
  foreach(bool night in new[]{false,true}){dark=night;darkSwitch.IsChecked=night;Theme.Apply(this,night);darkSwitch.ApplyTemplate();UpdateLayout();var track=(Border)darkSwitch.Template.FindName("Track",darkSwitch);var trackColor=((SolidColorBrush)track.Background).Color;if(trackColor!=Palette.Parse(night?"#484848":"#D6D6D6"))throw new Exception("Theme switch color mismatch");
   if(night)foreach(string key in new[]{"Canvas","Panel","Card","Field","Stroke","AccentSoft","Hover","GridLine","Empty","RowAlternate"}){var c=((SolidColorBrush)Theme.Get(key)).Color;if(c.R!=c.G||c.G!=c.B)throw new Exception("Non-neutral dark surface: "+key);}
   foreach(int lang in new[]{0,1}){language.SelectedIndex=lang;LocalizeShell();
    foreach(double width in new[]{1000.0,1360.0}){Width=width;await Task.Delay(100);
     for(int selected=0;selected<4;selected++){tabs.SelectedIndex=selected;Render();UpdateLayout();((TabItem)tabs.Items[selected]).Focus();await Task.Delay(100);UpdateLayout();
      for(int i=0;i<tabs.Items.Count;i++){var tab=(TabItem)tabs.Items[i];var pill=(Border)tab.Template.FindName("Pill",tab);var header=(FrameworkElement)tab.Template.FindName("HeaderContent",tab);if(pill==null||header==null)throw new Exception("Missing tab template parts");
       var rect=pill.TransformToAncestor(tabs).TransformBounds(new Rect(pill.RenderSize));if(rect.Left<-.1||rect.Right>tabs.ActualWidth+.1||rect.Top<-.1||rect.Bottom>tabs.ActualHeight+.1)throw new Exception("Tab pill clipped at index "+i);
       if(pill.BorderBrush==null||((SolidColorBrush)pill.BorderBrush).Color.A==0||pill.BorderThickness.Left<1)throw new Exception("Missing tab outline at "+i);
       var h=header.TransformToAncestor(pill).TransformBounds(new Rect(header.RenderSize));double left=h.Left,right=pill.ActualWidth-h.Right;if(Math.Abs(left-right)>1||left<14)throw new Exception("Asymmetric or clipped header padding at "+i);
       var radius=pill.CornerRadius;if(radius.TopLeft<=0||radius.TopLeft!=radius.TopRight||radius.TopLeft!=radius.BottomLeft||radius.TopLeft!=radius.BottomRight)throw new Exception("Asymmetric rounded corners");
      }
      if(!zh&&(sharedLabel.Text.Contains("R edition")||refresh.Content.ToString().Contains("刷新")))throw new Exception("Obsolete/mixed sidebar label");
      checks.Add(new{dark=night,language=lang,width=width,selected=selected,allTabsFit=true,symmetricPadding=true});
     }
     ExportView(Path.Combine(dir,(night?"dark":"light")+(zh?"-zh":"-en")+"-"+width+".png"));
    }
   }
  }
  File.WriteAllText(Path.Combine(dir,"style-tests.json"),Store.Json.Serialize(new{ok=true,checks=checks}));
 }
 void ExportView(string path){var bmp=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(this);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))enc.Save(f);}
 public async Task RestoreTest(string path){while(busy||data==null)await Task.Delay(100);var saved=Store.ReadJson(Path.Combine(Store.Root,"settings.json"));if(Math.Abs(Width-Store.Number(saved["windowWidth"]))>2||dark!=Convert.ToBoolean(saved["dark"]))throw new Exception("Window/theme not restored");File.WriteAllText(path,Store.Json.Serialize(new{ok=true,width=Width,height=Height,dark=dark,language=language.SelectedIndex}));}
}
public sealed class WindowActivation {
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool AllowSetForegroundWindow(uint pid);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 readonly Window window;WindowState restoredState;public int Restores{get;private set;}public bool Foreground{get;private set;}
 public WindowActivation(Window target){window=target;restoredState=target.WindowState==WindowState.Minimized?WindowState.Normal:target.WindowState;target.StateChanged+=(s,e)=>{if(target.WindowState!=WindowState.Minimized)restoredState=target.WindowState;};}
 public static void Log(string message){try{File.AppendAllText(Path.Combine(Store.Root,"window-activation.log"),DateTimeOffset.Now.ToString("o")+" pid="+Process.GetCurrentProcess().Id+" "+message+Environment.NewLine);}catch{}}
 public static bool HandOffForeground(){bool allowed=false;using(var self=Process.GetCurrentProcess()){string file=self.MainModule.FileName;foreach(var candidate in Process.GetProcessesByName(self.ProcessName)){using(candidate){try{if(candidate.Id==self.Id||!String.Equals(candidate.MainModule.FileName,file,StringComparison.OrdinalIgnoreCase)||candidate.MainWindowHandle==IntPtr.Zero)continue;bool granted=AllowSetForegroundWindow((uint)candidate.Id);allowed|=granted;Log("request owner="+candidate.Id+" foregroundGrant="+granted); }catch(Exception ex){Log("foreground grant: "+ex.Message);}}}}return allowed;}
 public static bool IsForeground(Window target){return GetForegroundWindow()==new System.Windows.Interop.WindowInteropHelper(target).Handle;}
 public void Restore(){
  if(window.Dispatcher.HasShutdownStarted)return;try{if(window.WindowState==WindowState.Minimized)window.WindowState=restoredState;if(!window.IsVisible)window.Show();window.ShowInTaskbar=true;
   if(window.WindowState==WindowState.Normal){var screen=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);var bounds=new Rect(window.Left,window.Top,window.Width,window.Height);if(!screen.IntersectsWith(bounds)){var work=SystemParameters.WorkArea;window.Left=work.Left+Math.Max(0,(work.Width-window.Width)/2);window.Top=work.Top+Math.Max(0,(work.Height-window.Height)/2);}}
   IntPtr handle=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();SetWindowPos(handle,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010|0x0040);bool activated=window.Activate();SetForegroundWindow(handle);Foreground=GetForegroundWindow()==handle;Restores++;Log("restored state="+window.WindowState+" visible="+window.IsVisible+" active="+activated+" foreground="+Foreground);
  }catch(Exception ex){Log("restore failed: "+ex);}
 }
}
public static class Entry {
 [STAThread] public static int Main(string[] args){
  try{
   NativeRuntime.Configure();NativeRuntime.TestInstance=args.Contains("--test-instance");if(args.Contains("--stop-background")){NativeRuntime.StopBackground();return 0;}if(args.Contains("--start-background")){NativeRuntime.Start();return 0;}
   if(args.Contains("--self-test")){SelfTest();return 0;}if(args.Length>0){string uiStop=Path.Combine(Store.Root,"native-ui.stop");if(File.Exists(uiStop))File.Delete(uiStop);}
   bool owner;using(var mutex=new Mutex(true,NativeRuntime.Name("ui"),out owner))using(var show=new EventWaitHandle(false,EventResetMode.AutoReset,NativeRuntime.Name("show"))){
    if(!owner){WindowActivation.HandOffForeground();show.Set();return 0;}if(args.Length==0){NativeRuntime.Start();}var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var w=new MainWindow();var activation=new WindowActivation(w);
    var waiter=ThreadPool.RegisterWaitForSingleObject(show,(state,timedOut)=>{try{if(app.Dispatcher.HasShutdownStarted)return;app.Dispatcher.BeginInvoke(new Action(()=>{activation.Restore();app.Dispatcher.BeginInvoke(new Action(()=>{try{if(!NativeRuntime.TestInstance)NativeRuntime.Start();w.Reload(true);w.OpenLiveMonitorIfRequested();}catch(Exception ex){WindowActivation.Log("refresh after restore failed: "+ex);}}),DispatcherPriority.Background);}),DispatcherPriority.Send);}catch(Exception ex){WindowActivation.Log("show request failed: "+ex.Message);}},null,Timeout.Infinite,false);
    if(args.Length>1&&(args[0]=="--controller-test"||args[0]=="--capture"||args[0]=="--exercise"||args[0]=="--visual-test"||args[0]=="--style-test"||args[0]=="--restore-test"))w.Loaded+=async(s,e)=>{try{await Task.Delay(1500);if(args[0]=="--controller-test")await w.ControllerExercise(args[1]);else if(args[0]=="--exercise")await w.Exercise(args[1]);else if(args[0]=="--visual-test")await w.VisualExercise(args[1]);else if(args[0]=="--style-test")await w.StyleExercise(args[1]);else if(args[0]=="--restore-test")await w.RestoreTest(args[1]);else await w.Capture(args[1]);}catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"exercise-error.log"),ex.ToString());}};
    try{app.Run(w);}finally{waiter.Unregister(null);}
   }return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(Store.Root,"native-error.log"),ex.ToString());MessageBox.Show(ex.Message,"InputAtlas");return 1;}
 }
 static void SelfTest(){
  var s=Store.Load(DateTime.Today.AddDays(-29),DateTime.Today,false);
  for(int mode=0;mode<5;mode++){var grouped=Store.Group(s.Daily,mode,DateTime.Today.AddDays(-29),DateTime.Today);if(Math.Abs(grouped.Sum(r=>r.Get("clicks"))-s.Daily.Sum(r=>r.Get("clicks")))>.01)throw new Exception("Grouping lost data");}
  using(var db=new Sqlite(Path.Combine(Store.Root,"inputatlas.sqlite"))){if(Convert.ToString(db.Query("PRAGMA integrity_check")[0]["integrity_check"])!="ok")throw new Exception("Integrity");bool blocked=false;try{db.Query("UPDATE daily SET clicks=clicks WHERE 0");}catch(IOException){blocked=true;}if(!blocked)throw new Exception("UI database is writable");}
  File.WriteAllText(Path.Combine(Store.Root,"self-test.json"),Store.Json.Serialize(new{ok=true,rows=s.Daily.Count,apps=s.Apps.Count,cachedDays=s.Cached,healthy=s.Healthy,at=DateTime.Now.ToString("o")}));
 }
}
}
