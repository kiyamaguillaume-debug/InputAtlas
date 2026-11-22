using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace InputAtlas {
public class Series {
 public string Name;public double[] V;public Color Color;public string IconKey,Family;
 public Series(string n,IEnumerable<double> v,Color c){Name=n;V=v.ToArray();Color=c;}
}
public static class Palette {
 public static Color Parse(string c){return (Color)ColorConverter.ConvertFromString(c);}
 public static Color Blue=Parse("#3B66F5"),Teal=Parse("#12B8A6"),Purple=Parse("#9460F5"),Pink=Parse("#EA5297"),Amber=Parse("#D99C15"),Gray=Parse("#9BA9BF");
 public static string[] Colors={"#3B66F5","#12B8A6","#9460F5","#EA5297","#93B62D","#D99C15","#42A4CF","#AF6B50","#78879F"};
 public static Color App(string s){
  string key=s.ToLowerInvariant().Replace(".exe","");string[] known={"chatgpt","msedge","weixin","explorer","powerpnt","deepseek harness","other","zotero","shell experience host","verge-mihomo","uu","qqmusic","lumi.launcher","lm studio","gameviewserver","doubao"};
  string[] fixedColors={"#3B66F5","#9460F5","#D99C15","#12B8A6","#EA5297","#42A4CF","#78879F","#AF6B50","#93B62D","#175D9B","#E56650","#75A332","#795BA9","#BE4B86","#269A8B","#AA772C"};int index=Array.IndexOf(known,key);if(index>=0)return Parse(fixedColors[index]);
  uint hash=2166136261;foreach(char c in key)hash=(hash^c)*16777619;double hue=hash%360/60.0,x=1-Math.Abs(hue%2-1);double r=0,g=0,b=0;if(hue<1){r=1;g=x;}else if(hue<2){r=x;g=1;}else if(hue<3){g=1;b=x;}else if(hue<4){g=x;b=1;}else if(hue<5){r=x;b=1;}else{r=1;b=x;}return Color.FromRgb((byte)(45+r*150),(byte)(45+g*150),(byte)(45+b*150));
 }
}
public class Plot : FrameworkElement {
 public string Title="",Kind="bars",Unit="count";public string[] Labels=new string[0];public string[] Tips;
 public List<Series> Series=new List<Series>();public bool Stacked=false;public double MinimumMaximum;
 class Hit{public Rect R;public string T;}
 List<Hit> hits=new List<Hit>();ToolTip tip=new ToolTip();string previousTip="";
 static Brush TextBrush {get{return Theme.Text;}}
 public Plot(){Height=345;MinWidth=500;Margin=new Thickness(0,4,0,12);MouseMove+=Hover;MouseLeave+=(s,e)=>{tip.IsOpen=false;previousTip="";};Unloaded+=(s,e)=>{tip.IsOpen=false;};}
 protected override Size MeasureOverride(Size a){return new Size(double.IsInfinity(a.Width)?900:a.Width,Height);}
 static Brush Brush(Color c){var b=new SolidColorBrush(c);b.Freeze();return b;}
 public static string Format(double v,string unit,bool compact=false){
  if(unit=="bytes"){double n=Math.Abs(v);return n>=1073741824?(v/1073741824).ToString("0.00")+" GB":n>=1048576?(v/1048576).ToString("0.0")+" MB":n>=1024?(v/1024).ToString("0.0")+" KB":v.ToString("0")+" B";}
  if(unit=="time"){if(v<60)return v.ToString("0")+"s";return ((long)v/3600).ToString()+"h "+(((long)v%3600)/60).ToString()+"m";}
  if(unit=="distance"){double m=v*.0254;return m>=1000?(m/1000).ToString("0.00")+" km":m>=1?m.ToString("0.0")+" m":(m*100).ToString("0")+" cm";}
  if(compact&&Math.Abs(v)>=1000000)return (v/1000000).ToString("0.0")+"M";if(compact&&Math.Abs(v)>=2000)return (v/1000).ToString("0.0")+"k";return v.ToString("N0");
 }
 static void Text(DrawingContext dc,string text,double x,double y,double size=12,Brush brush=null){var f=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(size>=17?"Segoe UI Semibold":"Segoe UI"),size,brush??TextBrush,1.0);dc.DrawText(f,new Point(x,y));}
 void Hover(object s,MouseEventArgs e){var p=e.GetPosition(this);var h=hits.LastOrDefault(v=>v.R.Contains(p));if(h==null){tip.IsOpen=false;previousTip="";return;}if(previousTip==h.T&&tip.IsOpen)return;tip.Content=h.T;tip.PlacementTarget=this;tip.Placement=System.Windows.Controls.Primitives.PlacementMode.Mouse;tip.IsOpen=true;previousTip=h.T;}
 string Tooltip(int i){return (Tips!=null&&i<Tips.Length?Tips[i]:Labels[i])+"\n"+String.Join("\n",Series.Select(s=>s.Name+": "+Format(s.V[i],Unit)));}
 protected override void OnRender(DrawingContext dc){
  hits.Clear();double w=ActualWidth,h=ActualHeight;if(w<120||h<100)return;
  dc.DrawRoundedRectangle(Theme.Card,new Pen(Theme.Get("Stroke"),.6),new Rect(.5,.5,w-1,h-1),14,14);Text(dc,Title,20,16,17);
  if(Labels.Length==0||Series.Count==0){Text(dc,Theme.Chinese?"此时段暂无记录":"No recorded data for this period",24,105,14);return;}
  if(Kind=="pie"){DrawPie(dc,w,h);return;}
  double legendX=22,legendY=47;foreach(var s in Series){double iconWidth=s.Family==GamepadPresentation.Combined?42:20;double space=s.IconKey==null?30+new FormattedText(s.Name,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,TextBrush,1.0).Width:28+iconWidth;if(legendX+space>w-20){legendY+=22;legendX=22;}dc.DrawRoundedRectangle(Brush(s.Color),null,new Rect(legendX,legendY+3,10,10),3,3);if(s.IconKey==null)Text(dc,s.Name,legendX+16,legendY);else GamepadGlyph.Draw(dc,s.IconKey,s.Family,new Rect(legendX+17,legendY-2,iconWidth,20),Theme.Text);legendX+=space;}
  double left=86,top=legendY+36,bottom=h-45,right=w-24,ph=bottom-top,pw=right-left;
  double max=Stacked||Kind=="area"?Enumerable.Range(0,Labels.Length).Max(i=>Series.Sum(s=>s.V[i])):Series.Max(s=>s.V.Length==0?0:s.V.Max());
  if(max<=0)max=Unit=="time"?60:1;max*=1.17;max=Math.Max(max,MinimumMaximum);
  for(int tick=0;tick<=4;tick++){double y=bottom-ph*tick/4;dc.DrawLine(new Pen(Theme.Get("GridLine"),1),new Point(left,y),new Point(right,y));Text(dc,Format(max*tick/4,Unit,true),8,y-8,11,Theme.Muted);}
  int n=Labels.Length;double step=pw/n;int skip=Math.Max(1,(int)Math.Ceiling(n/(pw/70)));
  for(int i=0;i<n;i++){if(i%skip==0||i==n-1)Text(dc,Labels[i],left+step*(i+.5)-16,bottom+10,10);hits.Add(new Hit{R=new Rect(left+step*i,top,step,ph),T=Tooltip(i)});}
  if(Kind=="area"){
   int samples=Math.Max(n,Math.Min(500,n*9));var smooth=new List<double[]>();
   foreach(var s in Series){double[] arr=new double[samples];for(int k=0;k<samples;k++){double pos=(double)k/(samples-1)*Math.Max(0,n-1);int i=Math.Min((int)pos,Math.Max(0,n-2));double t=pos-i;t=t*t*(3-2*t);arr[k]=n==1?s.V[0]:s.V[i]+(s.V[i+1]-s.V[i])*t;}smooth.Add(arr);}
   double[] baseY=new double[samples];for(int j=0;j<Series.Count;j++){
    var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(left,bottom-baseY[0]/max*ph),true,true);for(int k=0;k<samples;k++)c.LineTo(new Point(left+(double)k/(samples-1)*pw,bottom-(baseY[k]+smooth[j][k])/max*ph),true,false);for(int k=samples-1;k>=0;k--)c.LineTo(new Point(left+(double)k/(samples-1)*pw,bottom-baseY[k]/max*ph),true,false);}g.Freeze();dc.DrawGeometry(Brush(Series[j].Color),null,g);for(int k=0;k<samples;k++)baseY[k]+=smooth[j][k];
   }
  }else{
   for(int i=0;i<n;i++){double cumulative=0;for(int j=0;j<Series.Count;j++){
    var s=Series[j];double value=s.V[i],bw=step*.72/(Stacked?1:Series.Count);double x=left+step*i+step*.14+(Stacked?0:j*bw),bh=Math.Max(0,value/max*ph),y=bottom-(Stacked?cumulative:0)/max*ph-bh;
    if(bh>0)dc.DrawRectangle(Brush(s.Color),null,new Rect(x,y,Math.Max(1,bw-2),bh));
    if(!Stacked&&value>0&&bw>14)Text(dc,Format(value,Unit,true),x,y-18-(Unit=="bytes"?j*13:0),bw<30?9:11);cumulative+=value;
   }if(Stacked&&cumulative>0)Text(dc,Format(cumulative,Unit,true),left+step*i+step*.14,bottom-cumulative/max*ph-18,11);}
  }
 }
 void DrawPie(DrawingContext dc,double w,double h){
  var values=Series.Select(s=>s.V.Sum()).ToArray();double total=values.Sum();if(total<=0){Text(dc,Theme.Chinese?"暂无数据":"No data",24,90,14);return;}
  Point center=new Point(w*.42,h*.56);double radius=Math.Min(w*.28,h*.32),angle=-90;
  for(int i=0;i<values.Length;i++){
   double sweep=values[i]/total*360;if(sweep<=0)continue;double end=angle+sweep;
   if(sweep>=359.999)dc.DrawEllipse(Brush(Series[i].Color),null,center,radius,radius);
   else {var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(center,true,true);c.LineTo(new Point(center.X+radius*Math.Cos(angle*Math.PI/180),center.Y+radius*Math.Sin(angle*Math.PI/180)),true,false);c.ArcTo(new Point(center.X+radius*Math.Cos(end*Math.PI/180),center.Y+radius*Math.Sin(end*Math.PI/180)),new Size(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false);}dc.DrawGeometry(Brush(Series[i].Color),new Pen(Theme.Card,2),g);}
   double middle=(angle+sweep/2)*Math.PI/180;Text(dc,Series[i].Name+"\n"+Format(values[i],Unit)+"\n"+(100*values[i]/total).ToString("0.0")+"%",center.X+radius*.55*Math.Cos(middle)-28,center.Y+radius*.55*Math.Sin(middle)-23,11,Theme.Dark?Brushes.White:Brushes.Black);angle=end;
  }
 }
}
public class Heatmap : FrameworkElement {
 public List<Record> Rows;public string Metric,Title,Unit;public Color Color;public DateTime End;public int Mode;
 class Cell { public Rect R;public string Tip; }
 List<Cell> cells=new List<Cell>();ToolTip tip=new ToolTip();
 public Heatmap(){Height=230;MinWidth=0;Margin=new Thickness(0,4,0,12);MouseMove+=(s,e)=>{Point p=e.GetPosition(this);var c=cells.LastOrDefault(v=>v.R.Contains(p));if(c==null){tip.IsOpen=false;return;}tip.Content=c.Tip;tip.PlacementTarget=this;tip.Placement=System.Windows.Controls.Primitives.PlacementMode.Mouse;tip.IsOpen=true;};MouseLeave+=(s,e)=>tip.IsOpen=false;Unloaded+=(s,e)=>tip.IsOpen=false;}
 protected override Size MeasureOverride(Size a){return new Size(double.IsInfinity(a.Width)?900:a.Width,Height);}
 static void Text(DrawingContext dc,string s,double x,double y,int size=11){dc.DrawText(new FormattedText(s,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(size>=17?"Segoe UI Semibold":"Segoe UI"),size,size>=17?Theme.Text:Theme.Muted,1.0),new Point(x,y));}
 public int CellCount {get{return cells.Count;}}
 public bool CellsFit {get{return cells.All(c=>c.R.Left>=0&&c.R.Right<=ActualWidth&&Math.Abs(c.R.Width-c.R.Height)<.001);}}
 protected override void OnRender(DrawingContext dc){cells.Clear();if(ActualWidth<100)return;dc.DrawRoundedRectangle(Theme.Card,new Pen(Theme.Get("Stroke"),.6),new Rect(.5,.5,ActualWidth-1,ActualHeight-1),14,14);Text(dc,Title,20,16,17);if(Rows==null)return;
  int mode=Mode==4?1:Mode;DateTime from=Mode==0?End.AddDays(-370):Mode==1||Mode==4?End.AddDays(-364):Mode==2?End.AddMonths(-23):End.AddYears(-9);if(Rows.Count>0&&Rows.Min(r=>r.Day)<from)from=Rows.Min(r=>r.Day);
  var groups=Rows.GroupBy(r=>Store.Period(r.Day,mode)).ToDictionary(g=>g.Key,g=>g.Sum(r=>r.Get(Metric)));var dates=new List<DateTime>();for(var d=Store.Period(from,mode);d<=Store.Period(End,mode);d=Store.Next(d,mode))dates.Add(d);
  if(Mode==4){double sum=0;foreach(var d in dates){double v;groups.TryGetValue(d,out v);sum+=v;groups[d]=sum;}}
  int offset=mode==0?((int)dates[0].DayOfWeek+6)%7:0;
  int columns=mode==0?(dates.Count+offset+6)/7:dates.Count;
  double max=groups.Count==0?0:groups.Values.Max(),ox=mode==0?52:24,oy=66;
  double stride=Math.Min(18,Math.Max(1,(ActualWidth-ox-20)/Math.Max(1,columns))),side=stride*.77;int rowcount=7;
  if(mode==0){string[] weekdays=Theme.Chinese?new[]{"一","二","三","四","五","六","日"}:new[]{"Mon","Tue","Wed","Thu","Fri","Sat","Sun"};for(int j=0;j<7;j++)Text(dc,weekdays[j],14,oy+j*stride,Math.Min(11,(int)Math.Max(8,stride)));}
  string lastMonth="";double lastLabelX=-100;
  for(int i=0;i<dates.Count;i++){
   double value;groups.TryGetValue(dates[i],out value);int column=mode==0?(i+offset)/7:i;int dayrow=mode==0?(i+offset)%7:0;
   string month=mode==3?dates[i].Year.ToString():dates[i].ToString(Theme.Chinese?"M月":"MMM",CultureInfo.InvariantCulture);if(month!=lastMonth&&ox+column*stride-lastLabelX>28){Text(dc,month,ox+column*stride,oy+rowcount*stride+9,10);lastMonth=month;lastLabelX=ox+column*stride;}
   int filled=max<=0?0:(int)Math.Ceiling(value/max*rowcount);for(int r=0;r<(mode==0?1:rowcount);r++){
    int yrow=mode==0?dayrow:r;bool active=mode==0?value>0:r>=rowcount-filled;
    double f=mode==0&&max>0?Math.Max(.18,value/max):1;var c=active?Theme.Blend(Color,f):Theme.Empty;
    Rect rect=new Rect(ox+column*stride,oy+yrow*stride,side,side);dc.DrawRoundedRectangle(new SolidColorBrush(c),null,rect,Math.Min(3,side/4),Math.Min(3,side/4));cells.Add(new Cell{R=rect,Tip=dates[i].ToString("yyyy-MM-dd")+(Mode==4?(Theme.Chinese?" · 累计":" · cumulative total"):mode==0?"":Theme.Chinese?" · 周期总计":" · period total")+"\n"+Title+": "+Plot.Format(value,Unit)});
   }
  }
  Text(dc,"0",ActualWidth-175,24);for(int i=0;i<5;i++)dc.DrawRoundedRectangle(new SolidColorBrush(Theme.Blend(Color,(i+1)/5.0)),null,new Rect(ActualWidth-150+i*16,25,13,10),2,2);Text(dc,Plot.Format(max,Unit,true),ActualWidth-65,24);
 }
}
}
