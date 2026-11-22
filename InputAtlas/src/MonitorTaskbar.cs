using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace InputAtlas {
// An owned child window reserves a portion of the Windows 10 task-button area.
// Sampling and painting stay in this process; Explorer hosts only the HWND.
public sealed class MonitorTaskbar:NativeWindow,IDisposable {
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;public int Width {get{return Right-Left;}}public int Height {get{return Bottom-Top;}}}
 [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct PaintInfo {public IntPtr Dc;public int Erase;public Rect Paint;public int Restore,Increment;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Reserved;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string name,string text);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string title);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr SetParent(IntPtr child,IntPtr parent);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr child);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Rect rect,uint points);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll")]static extern bool InvalidateRect(IntPtr window,IntPtr rect,bool erase);
 [DllImport("user32.dll")]static extern IntPtr BeginPaint(IntPtr window,out PaintInfo info);
 [DllImport("user32.dll")]static extern bool EndPaint(IntPtr window,ref PaintInfo info);
 [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
 IntPtr taskbar,parent,taskList;Rect full;int reservedRight,width,height;bool reserved;Bitmap buffer;Font font;float scale=1;MonitorLayout plan;
 readonly Bitmap measurement=new Bitmap(1,1);readonly Font measureFont=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ToolInfo {public uint Size,Flags;public IntPtr Window,Id;public Rect Rect;public IntPtr Instance,Text,Parameter,Reserved;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(uint exStyle,string name,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,ref ToolInfo info);
 [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint GetClassLong(IntPtr window,int index);
 sealed class Layout {public long Parent,TaskList;public uint Pid;public Rect Full;public int ReservedRight;}
 readonly string root;readonly Action open;IntPtr tooltip,tipText;ToolInfo tool;
 MonitorOptions options;MonitorSnapshot sample;string status="";ContextMenuStrip activeMenu;readonly MonitorColorResolver colorResolver=new MonitorColorResolver();MonitorPalette palette=MonitorPalette.Theme(false);
 public string BackgroundColor{get{return MonitorPalette.Hex(palette.Background);}}public string ForegroundColor{get{return MonitorPalette.Hex(palette.Foreground);}}public string ColorSource{get{return palette.Source;}}
 public bool Attached {get;private set;}public string Status {get{return status;}}
 public int DisplayWidth {get;private set;}public int MinimumWidth {get;private set;}
 public bool DoubleClickReady {get{return Handle!=IntPtr.Zero&&(GetClassLong(Handle,-26)&8)!=0;}}
 public void SavePreview(string path){if(buffer!=null)buffer.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
 public MonitorTaskbar(string dataRoot,Action openMain){root=dataRoot;open=openMain;Recover();}
 string LayoutFile {get{return Path.Combine(root,"monitor-layout.json");}}
 void Recover(){try{if(File.Exists(LayoutFile)){var raw=MonitorOptions.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(LayoutFile));var rectangle=(Dictionary<string,object>)raw["Full"];var saved=new Layout{Parent=Convert.ToInt64(raw["Parent"]),TaskList=Convert.ToInt64(raw["TaskList"]),Pid=Convert.ToUInt32(raw["Pid"]),ReservedRight=Convert.ToInt32(raw["ReservedRight"]),Full=new Rect{Left=Convert.ToInt32(rectangle["Left"]),Top=Convert.ToInt32(rectangle["Top"]),Right=Convert.ToInt32(rectangle["Right"]),Bottom=Convert.ToInt32(rectangle["Bottom"])}};IntPtr p=(IntPtr)saved.Parent,t=(IntPtr)saved.TaskList;uint pid;GetWindowThreadProcessId(p,out pid);if(IsWindow(p)&&IsWindow(t)&&GetParent(t)==p&&pid==saved.Pid){var r=ChildRect(t,p);if(r.Right==saved.ReservedRight&&r.Left==saved.Full.Left&&r.Top==saved.Full.Top&&r.Height==saved.Full.Height)SetWindowPos(t,IntPtr.Zero,saved.Full.Left,saved.Full.Top,saved.Full.Width,saved.Full.Height,0x0014);}File.Delete(LayoutFile);}}catch(Exception ex){File.AppendAllText(Path.Combine(root,"monitor.log"),"Layout recovery: "+ex.Message+Environment.NewLine);}}
 void SaveLayout(){uint pid;GetWindowThreadProcessId(parent,out pid);var saved=new Layout{Parent=parent.ToInt64(),TaskList=taskList.ToInt64(),Pid=pid,Full=full,ReservedRight=reservedRight};string text=MonitorOptions.Json.Serialize(saved);if(!File.Exists(LayoutFile)||File.ReadAllText(LayoutFile)!=text)File.WriteAllText(LayoutFile,text);}
 Rect ChildRect(IntPtr window,IntPtr container){Rect r;GetWindowRect(window,out r);MapWindowPoints(IntPtr.Zero,container,ref r,2);return r;}
 public static Rect InspectTaskButtons(){IntPtr shell=FindWindow("Shell_TrayWnd",null),bar=FindWindowEx(shell,IntPtr.Zero,"ReBarWindow32",null),list=FindWindowEx(bar,IntPtr.Zero,"MSTaskSwWClass",null);if(list==IntPtr.Zero)list=FindWindowEx(bar,IntPtr.Zero,"MSTaskListWClass",null);Rect r;GetWindowRect(list,out r);MapWindowPoints(IntPtr.Zero,bar,ref r,2);return r;}
 public void Update(MonitorOptions settings,MonitorSnapshot current){options=settings;sample=current;
  using(var g=Graphics.FromImage(measurement)){var estimate=MonitorLayout.Create(g,measureFont,Lines(),settings.TaskbarWidth,1,settings.PerformanceDigits);DisplayWidth=estimate.Width;MinimumWidth=estimate.MinimumWidth;}
  if(!settings.OnTaskbar||!settings.AnyItem){Restore();palette=colorResolver.Resolve(settings,IntPtr.Zero,IntPtr.Zero,false);status=settings.AnyItem?"hidden":"select-items";return;}
  IntPtr shell=FindWindow("Shell_TrayWnd",null),bar=FindWindowEx(shell,IntPtr.Zero,"ReBarWindow32",null),list=FindWindowEx(bar,IntPtr.Zero,"MSTaskSwWClass",null);
  if(list==IntPtr.Zero)list=FindWindowEx(bar,IntPtr.Zero,"MSTaskListWClass",null);
  if(shell==IntPtr.Zero||bar==IntPtr.Zero||list==IntPtr.Zero){Restore();status="connecting";return;}
  IntPtr responsive;if(SendMessageTimeout(shell,0,IntPtr.Zero,IntPtr.Zero,2,100,out responsive)==IntPtr.Zero){status="connecting";return;}
  if(taskList!=list||parent!=bar||Handle==IntPtr.Zero||!IsWindow(Handle)){Restore();taskbar=shell;parent=bar;taskList=list;CreateHandle(new CreateParams{Caption="InputAtlas live monitor",ClassStyle=8,Style=unchecked((int)0x40000000),ExStyle=0x08000080,Parent=bar,X=0,Y=0,Width=1,Height=1});SetParent(Handle,bar);}
  uint dpi=96;try{dpi=GetDpiForWindow(shell);}catch{}if(dpi==0)dpi=96;float nextScale=dpi/96f;
  if(font==null||Math.Abs(scale-nextScale)>.01){if(font!=null)font.Dispose();scale=nextScale;font=new Font("Segoe UI",12*scale,FontStyle.Regular,GraphicsUnit.Pixel);}
  using(var g=Graphics.FromHwnd(Handle))plan=MonitorLayout.Create(g,font,Lines(),settings.TaskbarWidth,scale,settings.PerformanceDigits);
  int desired=(int)Math.Ceiling(plan.Width*scale);
  Rect currentRect=ChildRect(list,bar);
  if(!reserved||currentRect.Right!=reservedRight||currentRect.Top!=full.Top||currentRect.Height!=full.Height){full=currentRect;reserved=false;}
  int available=(int)Math.Floor(full.Width/scale-160);if(available<plan.MinimumWidth||full.Height>120*nextScale){Restore();status="layout";return;}
  if(plan.Width>available){using(var g=Graphics.FromHwnd(Handle))plan=MonitorLayout.Create(g,font,Lines(),available,scale,settings.PerformanceDigits);desired=(int)Math.Ceiling(plan.Width*scale);}DisplayWidth=plan.Width;MinimumWidth=plan.MinimumWidth;
  if(reserved&&width!=desired){RestoreSize();full=ChildRect(list,bar);}
  width=desired;height=full.Height;reservedRight=full.Right-width;
  SaveLayout();if(currentRect.Left!=full.Left||currentRect.Top!=full.Top||currentRect.Right!=reservedRight||currentRect.Height!=full.Height)SetWindowPos(list,IntPtr.Zero,full.Left,full.Top,full.Width-width,full.Height,0x0014);reserved=true;
  var shown=ChildRect(Handle,parent);if(shown.Left!=reservedRight||shown.Top!=full.Top||shown.Width!=width||shown.Height!=height)SetWindowPos(Handle,IntPtr.Zero,reservedRight,full.Top,width,height,0x0050);else if(!IsWindowVisible(Handle))ShowWindow(Handle,4);
  Attached=GetParent(Handle)==bar&&IsWindowVisible(Handle);status=Attached?"ready":"connecting";palette=colorResolver.Resolve(settings,taskbar,Handle,activeMenu==null||!activeMenu.Visible);UpdateTooltip();InvalidateRect(Handle,IntPtr.Zero,false);
 }
 void RestoreSize(){if(reserved&&IsWindow(taskList)&&IsWindow(parent)){var r=ChildRect(taskList,parent);if(r.Right==reservedRight)SetWindowPos(taskList,IntPtr.Zero,full.Left,full.Top,full.Width,full.Height,0x0014);}reserved=false;}
 public void Restore(){RestoreSize();if(tooltip!=IntPtr.Zero){DestroyWindow(tooltip);tooltip=IntPtr.Zero;}if(tipText!=IntPtr.Zero){Marshal.FreeHGlobal(tipText);tipText=IntPtr.Zero;}if(Handle!=IntPtr.Zero){ShowWindow(Handle,0);DestroyHandle();}if(File.Exists(LayoutFile))File.Delete(LayoutFile);Attached=false;taskbar=parent=taskList=IntPtr.Zero;}
 List<KeyValuePair<string,string>> Lines(){return MonitorLayout.Lines(options,sample);}
 void Paint(IntPtr dc){if(width<1||height<1||sample==null||options==null)return;if(buffer==null||buffer.Width!=width||buffer.Height!=height){if(buffer!=null)buffer.Dispose();buffer=new Bitmap(width,height);}
  using(var g=Graphics.FromImage(buffer))MonitorLayout.Draw(g,font,plan,width,height,scale,false,palette);
  using(var output=Graphics.FromHdc(dc))output.DrawImageUnscaled(buffer,0,0);
 }
 void Change(Action<MonitorOptions> mutate){var settings=MonitorOptions.Load(root);mutate(settings);settings.Save(root);}
 void UpdateTooltip(){bool periodReady=sample.TrafficPeriod==options.TrafficPeriod;string text=(options.Language==1?"实时监控":"Live monitor")+" · "+sample.Adapter+"\r\nCPU "+options.PerformanceText(sample.Cpu)+"\r\nRAM "+options.PerformanceText(sample.MemoryPercent)+" · "+MonitorMetrics.Bytes(sample.MemoryUsed)+" / "+MonitorMetrics.Bytes(sample.MemoryTotal)+"\r\n↓ "+MonitorMetrics.Speed(sample.Download)+"  ↑ "+MonitorMetrics.Speed(sample.Upload)+"\r\n"+MonitorOptions.PeriodLabel(options.TrafficPeriod,options.Language==1)+": ↓ "+(periodReady?MonitorMetrics.Bytes(sample.TrafficDownload):"—")+"  ↑ "+(periodReady?MonitorMetrics.Bytes(sample.TrafficUpload):"—");IntPtr next=Marshal.StringToHGlobalUni(text);IntPtr old=tipText;tipText=next;
  if(tooltip==IntPtr.Zero){tooltip=CreateWindowEx(8,"tooltips_class32",null,0x80000003,0,0,0,0,Handle,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);tool=new ToolInfo{Size=(uint)Marshal.SizeOf(typeof(ToolInfo)),Flags=0x11,Window=Handle,Id=Handle,Text=tipText};SendMessage(tooltip,0x0432,IntPtr.Zero,ref tool);}else{tool.Text=tipText;SendMessage(tooltip,0x0439,IntPtr.Zero,ref tool);}if(old!=IntPtr.Zero)Marshal.FreeHGlobal(old);
 }
 public ContextMenuStrip CreateMenu(){bool zh=options.Language==1;var menu=new ContextMenuStrip();var show=new ToolStripMenuItem(zh?"在任务栏显示":"Show on taskbar"){Checked=options.OnTaskbar,CheckOnClick=true};show.Click+=(s,e)=>Change(o=>o.OnTaskbar=show.Checked);menu.Items.Add(show);
  string[] labels=zh?new[]{"下载速度","上传速度","CPU 占用","内存占用","累计流量"}:new[]{"Download speed","Upload speed","CPU usage","Memory usage","Traffic totals"};bool[] values={options.Download,options.Upload,options.Cpu,options.Memory,options.DailyTraffic};for(int i=0;i<labels.Length;i++){int item=i;var entry=new ToolStripMenuItem(labels[i]){Checked=values[i],CheckOnClick=true};entry.Click+=(s,e)=>Change(o=>{if(item==0)o.Download=entry.Checked;else if(item==1)o.Upload=entry.Checked;else if(item==2)o.Cpu=entry.Checked;else if(item==3)o.Memory=entry.Checked;else o.DailyTraffic=entry.Checked;});menu.Items.Add(entry);}
  var periods=new ToolStripMenuItem(zh?"流量累计周期":"Traffic period");for(int i=0;i<5;i++){int period=i;var item=new ToolStripMenuItem(MonitorOptions.PeriodLabel(i,zh)){Checked=options.TrafficPeriod==i};item.Click+=(s,e)=>Change(o=>o.TrafficPeriod=period);periods.DropDownItems.Add(item);}menu.Items.Add(periods);
  var precision=new ToolStripMenuItem(zh?"CPU／内存精度":"CPU / memory precision");for(int i=0;i<2;i++){int digits=i;var item=new ToolStripMenuItem(zh?(i==0?"整数（12%）":"一位小数（12.3%）"):(i==0?"Integer (12%)":"1 decimal place (12.3%)")){Checked=options.PerformanceDigits==i};item.Click+=(s,e)=>Change(o=>o.PerformanceDigits=digits);precision.DropDownItems.Add(item);}menu.Items.Add(precision);
  var colors=new ToolStripMenuItem(zh?"显示颜色":"Display colors");var follow=new ToolStripMenuItem(zh?"跟随任务栏":"Match taskbar"){Checked=options.TaskbarColorMode==0};follow.Click+=(s,e)=>Change(o=>o.TaskbarColorMode=0);colors.DropDownItems.Add(follow);var custom=new ToolStripMenuItem(zh?"自定义颜色…":"Custom colors…"){Checked=options.TaskbarColorMode==1};custom.Click+=(s,e)=>{Change(o=>o.TaskbarColorMode=1);open();};colors.DropDownItems.Add(custom);menu.Items.Add(colors);
  var widths=new ToolStripMenuItem(zh?"显示宽度":"Display width");var automatic=new ToolStripMenuItem(zh?"自动宽度":"Automatic width"){Checked=options.TaskbarWidth==0};automatic.Click+=(s,e)=>Change(o=>o.TaskbarWidth=0);widths.DropDownItems.Add(automatic);
  widths.DropDownItems.Add(zh?"更紧凑":"More compact",null,(s,e)=>Change(o=>o.TaskbarWidth=Math.Max(MinimumWidth,DisplayWidth-20)));widths.DropDownItems.Add(zh?"更宽松":"More spacious",null,(s,e)=>Change(o=>o.TaskbarWidth=Math.Min(1200,DisplayWidth+20)));widths.DropDownItems.Add(new ToolStripSeparator());foreach(int size in new[]{240,320,400,480,640,800}){int value=size;var item=new ToolStripMenuItem(size.ToString()){Enabled=size>=MinimumWidth,Checked=options.TaskbarWidth==size};item.Click+=(s,e)=>Change(o=>o.TaskbarWidth=value);widths.DropDownItems.Add(item);}widths.DropDownItems.Add(zh?"自定义宽度…":"Custom width…",null,(s,e)=>open());menu.Items.Add(widths);
  var adapters=new ToolStripMenuItem(zh?"网络接口":"Network adapter");var auto=new ToolStripMenuItem(zh?"自动 · 物理网卡":"Automatic · physical adapter"){Checked=options.Adapter=="auto"};auto.Click+=(s,e)=>Change(o=>o.Adapter="auto");adapters.DropDownItems.Add(auto);foreach(var a in sample.Adapters){string id=a.Id;var item=new ToolStripMenuItem(a.Name){Checked=id==options.Adapter};item.Click+=(s,e)=>Change(o=>o.Adapter=id);adapters.DropDownItems.Add(item);}menu.Items.Add(adapters);menu.Items.Add(new ToolStripSeparator());menu.Items.Add(zh?"打开实时监控":"Open live monitor",null,(s,e)=>open());return menu;
 }
 void Menu(){if(activeMenu!=null){if(activeMenu.Visible)return;activeMenu.Dispose();}activeMenu=CreateMenu();activeMenu.Show(Cursor.Position);}
 protected override void WndProc(ref Message message){
  if(message.Msg==0x000F){PaintInfo info;IntPtr dc=BeginPaint(Handle,out info);try{Paint(dc);}finally{EndPaint(Handle,ref info);}message.Result=IntPtr.Zero;return;}
  if(message.Msg==0x0014){message.Result=(IntPtr)1;return;}
  if(message.Msg==0x0205){Menu();return;}if(message.Msg==0x0203){open();return;}
  base.WndProc(ref message);
 }
 public void Dispose(){if(activeMenu!=null){activeMenu.Close();activeMenu.Dispose();activeMenu=null;}Restore();if(buffer!=null)buffer.Dispose();if(font!=null)font.Dispose();measurement.Dispose();measureFont.Dispose();}
}
}
