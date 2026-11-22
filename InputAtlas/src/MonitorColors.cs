using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace InputAtlas {
public sealed class MonitorPalette {
 public Color Background,Foreground,Label,Divider;public string Source;
 public static string Hex(Color color){return "#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2");}
 static Color Blend(Color a,Color b,double t){return Color.FromArgb((int)Math.Round(a.R+(b.R-a.R)*t),(int)Math.Round(a.G+(b.G-a.G)*t),(int)Math.Round(a.B+(b.B-a.B)*t));}
 public static MonitorPalette Create(Color background,Color foreground,string source){return new MonitorPalette{Background=background,Foreground=foreground,Label=Blend(background,foreground,.82),Divider=Blend(background,foreground,.22),Source=source};}
 static double Linear(byte component){double v=component/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
 public static MonitorPalette Automatic(Color background,string source){double luminance=.2126*Linear(background.R)+.7152*Linear(background.G)+.0722*Linear(background.B);return Create(background,luminance>.179?Color.Black:Color.White,source);}
 public static MonitorPalette Custom(MonitorOptions options){return Create(ColorTranslator.FromHtml(MonitorOptions.ColorHex(options.TaskbarBackground,"#141414")),ColorTranslator.FromHtml(MonitorOptions.ColorHex(options.TaskbarForeground,"#F0F0F0")),"custom");}
 public static MonitorPalette Theme(bool light){return Automatic(light?Color.FromArgb(243,243,243):Color.FromArgb(16,16,16),"theme");}
}
public sealed class MonitorColorResolver {
 [StructLayout(LayoutKind.Sequential)]struct Rect {public int Left,Top,Right,Bottom;public int Width{get{return Right-Left;}}public int Height{get{return Bottom-Top;}}}
 [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 Color? cached;IntPtr sampledShell;long sampledAt;bool previousLight;
 static bool LightTheme(){try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return Convert.ToInt32(key.GetValue("SystemUsesLightTheme",0))!=0;}catch{return false;}}
 public static Color? SampleTaskbar(IntPtr shell,IntPtr widget){
  Rect bar,own=new Rect();if(shell==IntPtr.Zero||!IsWindowVisible(shell)||!GetWindowRect(shell,out bar)||bar.Width<100||bar.Height<8||bar.Height>200)return null;
  bool hasOwn=widget!=IntPtr.Zero&&GetWindowRect(widget,out own);if(!hasOwn)own=new Rect{Left=bar.Left+bar.Width*3/4,Right=bar.Left+bar.Width*3/4};
  // Tray-side background strips keep task-button highlights and central icon glyphs outside the sample.
  int[] xs={own.Right+4,own.Right+9,own.Right+17,own.Right+35,own.Right+57,own.Right+79,own.Right+101,own.Right+123};int edge=Math.Max(3,bar.Height/10);int[] ys={bar.Top+edge,bar.Bottom-edge-1};var colors=new List<uint>();int left=Math.Max(bar.Left,xs.Min()),right=Math.Min(bar.Right,xs.Max()+1),top=ys.Min(),bottom=ys.Max()+1;if(right<=left)return null;
  try{using(var capture=new Bitmap(right-left,bottom-top))using(var graphics=Graphics.FromImage(capture)){graphics.CopyFromScreen(left,top,0,0,capture.Size,CopyPixelOperation.SourceCopy);foreach(int x in xs)foreach(int y in ys){if(x<=bar.Left+2||x>=bar.Right-2)continue;var at=new Point{X=x,Y=y};IntPtr under=WindowFromPoint(at);if(under==IntPtr.Zero||GetAncestor(under,2)!=shell)continue;Color pixel=capture.GetPixel(x-left,y-top);colors.Add((uint)(pixel.R|(pixel.G<<8)|(pixel.B<<16)));}}}catch(System.ComponentModel.Win32Exception){return null;}
  if(colors.Count<3)return null;var group=colors.GroupBy(c=>c).OrderByDescending(g=>g.Count()).First();if(group.Count()<3)return null;uint value=group.Key;return Color.FromArgb((int)(value&255),(int)((value>>8)&255),(int)((value>>16)&255));
 }
 public MonitorPalette Resolve(MonitorOptions options,IntPtr shell,IntPtr widget,bool allowSample){
  if(options.TaskbarColorMode==1)return MonitorPalette.Custom(options);bool light=LightTheme();long now=Stopwatch.GetTimestamp();
  if(sampledShell!=shell||previousLight!=light){cached=null;sampledAt=0;sampledShell=shell;previousLight=light;}
  if(allowSample&&shell!=IntPtr.Zero&&(sampledAt==0||(now-sampledAt)/(double)Stopwatch.Frequency>=5)){sampledAt=now;var color=SampleTaskbar(shell,widget);if(color.HasValue)cached=color;}
  return cached.HasValue?MonitorPalette.Automatic(cached.Value,"sampled"):MonitorPalette.Theme(light);
 }
}
}
