using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
namespace InputAtlas {
public static class GamepadPresentation {
 public const string Combined="Combined";
 public static readonly int[] SummaryIndices={4,5,6,7,8,9,17,18};
 public static bool Includes(string view,string family){return view==Combined||view==family;}
 public static string Title(string family){return family==Combined?(Theme.Chinese?"全部手柄 · PS + Xbox":"All controllers · PS + Xbox"):family=="Xbox"?"Xbox":"PlayStation / PS5";}
 public static string Label(string key,string family){
  if(family!=Combined)return PadButtons.Label(key,family);
  int i=Array.IndexOf(PadButtons.Names,key);
  if(i>=4&&i<=9||i==12||i==13||i==17||i==18)return PadButtons.Label(key,"PlayStation")+" / "+PadButtons.Label(key,"Xbox");
  return PadButtons.Label(key,"PlayStation");
 }
}
public static class GamepadGlyph {
 public static void Text(DrawingContext dc,string text,Point center,double size,Brush ink){var f=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,ink,1);dc.DrawText(f,new Point(center.X-f.Width/2,center.Y-f.Height/2));}
 public static void Draw(DrawingContext dc,string key,string family,Rect box,Brush ink){int i=Array.IndexOf(PadButtons.Names,key);
  if(family==GamepadPresentation.Combined&&i>=4&&i<=7){double part=box.Width*.45;Draw(dc,key,"PlayStation",new Rect(box.X,box.Y,part,box.Height),ink);Draw(dc,key,"Xbox",new Rect(box.Right-part,box.Y,part,box.Height),ink);return;}
  var c=new Point(box.X+box.Width/2,box.Y+box.Height/2);double r=Math.Min(box.Width,box.Height)*.33;var p=new Pen(ink,Math.Max(1.5,box.Width*.055)){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};if(family!="Xbox"&&i>=4&&i<=7){if(i==4){dc.DrawLine(p,new Point(c.X-r,c.Y-r),new Point(c.X+r,c.Y+r));dc.DrawLine(p,new Point(c.X-r,c.Y+r),new Point(c.X+r,c.Y-r));}else if(i==5)dc.DrawEllipse(null,p,c,r,r);else if(i==6)dc.DrawRectangle(null,p,new Rect(c.X-r,c.Y-r,r*2,r*2));else{var g=new StreamGeometry();using(var s=g.Open()){s.BeginFigure(new Point(c.X,c.Y-r*1.13),false,true);s.LineTo(new Point(c.X+r,c.Y+r*.8),true,false);s.LineTo(new Point(c.X-r,c.Y+r*.8),true,false);}dc.DrawGeometry(null,p,g);}return;}
  string label=GamepadPresentation.Label(key,family);var text=new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Semibold"),box.Height*.7,ink,1);double size=box.Height*.7*Math.Min(1,box.Width/Math.Max(1,text.Width));Text(dc,label,c,size,ink);
 }
}
public sealed class GamepadButtonIcon : FrameworkElement {
 public static readonly DependencyProperty ButtonProperty=DependencyProperty.Register("Button",typeof(string),typeof(GamepadButtonIcon),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty FamilyProperty=DependencyProperty.Register("Family",typeof(string),typeof(GamepadButtonIcon),new FrameworkPropertyMetadata("PlayStation",FrameworkPropertyMetadataOptions.AffectsRender));
 public string Button {get{return (string)GetValue(ButtonProperty);}set{SetValue(ButtonProperty,value);}}public string Family {get{return (string)GetValue(FamilyProperty);}set{SetValue(FamilyProperty,value);}}
 public GamepadButtonIcon(){Width=35;Height=24;HorizontalAlignment=HorizontalAlignment.Left;}
 protected override void OnRender(DrawingContext dc){GamepadGlyph.Draw(dc,Button,Family,new Rect(4,0,24,24),Theme.Text);}
}
public sealed class GamepadHeatmap : FrameworkElement {
 public string Family="PlayStation";public Dictionary<string,long> Counts=new Dictionary<string,long>();public long Total {get{return Counts.Where(p=>PadButtons.Countable(p.Key)).Sum(p=>p.Value);}}
 class Hit{public Rect Rect;public string Button;}
 readonly List<Hit> hits=new List<Hit>();readonly ToolTip tip=new ToolTip();double scale,ox,oy;
 public int RenderedKeys {get{return hits.Count;}}public bool KeysFit {get{return hits.All(h=>h.Rect.Left>=0&&h.Rect.Right<=ActualWidth&&h.Rect.Top>=0&&h.Rect.Bottom<=ActualHeight);}}
 public GamepadHeatmap(){Height=460;MinWidth=0;Margin=new Thickness(0,6,0,10);MouseMove+=Hover;MouseLeave+=(s,e)=>tip.IsOpen=false;Unloaded+=(s,e)=>tip.IsOpen=false;}
 protected override Size MeasureOverride(Size available){return new Size(Double.IsInfinity(available.Width)?450:available.Width,Height);}
 void Hover(object sender,MouseEventArgs e){var hit=hits.LastOrDefault(h=>h.Rect.Contains(e.GetPosition(this)));if(hit==null){tip.IsOpen=false;return;}long count=Get(hit.Button);tip.Content=GamepadPresentation.Label(hit.Button,Family)+"  ·  "+count.ToString("N0")+(Theme.Chinese?" 次":" presses");tip.PlacementTarget=this;tip.Placement=System.Windows.Controls.Primitives.PlacementMode.Mouse;if(!tip.IsOpen)tip.IsOpen=true;}
 long Get(string key){long v;return Counts.TryGetValue(key,out v)?v:0;}
 Brush Fill(string key){long max=Counts.Count==0?0:Counts.Values.Max();long value=Get(key);var a=Theme.Empty;var b=Palette.Parse(Family==GamepadPresentation.Combined?"#A178F5":Family=="Xbox"?"#19B780":"#5084F5");double f=value<=0||max<=0?0:.2+.8*Math.Pow((double)value/max,.65);return new SolidColorBrush(Color.FromRgb((byte)(a.R+(b.R-a.R)*f),(byte)(a.G+(b.G-a.G)*f),(byte)(a.B+(b.B-a.B)*f)));}
 void Button(DrawingContext dc,string key,double x,double y,double width=35,double height=35,bool round=true){var rect=new Rect(x-width/2,y-height/2,width,height);var pen=new Pen(Theme.Get("Stroke"),1.5);if(round)dc.DrawEllipse(Fill(key),pen,new Point(x,y),width/2,height/2);else dc.DrawRoundedRectangle(Fill(key),pen,rect,7,7);Brush ink=Theme.Dark||Get(key)>0?Brushes.White:Theme.Text;double glyphWidth=Family==GamepadPresentation.Combined?width-8:26;GamepadGlyph.Draw(dc,key,Family,new Rect(x-glyphWidth/2,y-13,glyphWidth,26),ink);hits.Add(new Hit{Button=key,Rect=new Rect(ox+rect.X*scale,oy+rect.Y*scale,rect.Width*scale,rect.Height*scale)});}
 void Stick(DrawingContext dc,string key,double x,double y){Button(dc,key,x,y,52,52);dc.DrawEllipse(null,new Pen(Theme.Muted,1),new Point(x,y),20,20);}
 void Dpad(DrawingContext dc,double x,double y){Button(dc,PadButtons.Names[0],x,y-29,29,29,false);Button(dc,PadButtons.Names[1],x,y+29,29,29,false);Button(dc,PadButtons.Names[2],x-29,y,29,29,false);Button(dc,PadButtons.Names[3],x+29,y,29,29,false);}
 protected override void OnRender(DrawingContext dc){hits.Clear();if(ActualWidth<180)return;dc.DrawRoundedRectangle(Theme.Card,new Pen(Theme.Get("Stroke"),1),new Rect(.5,.5,ActualWidth-1,Height-1),14,14);
  GamepadGlyph.Text(dc,GamepadPresentation.Title(Family),new Point(ActualWidth/2,27),18,Theme.Text);long actions=Counts.Where(p=>Array.IndexOf(PadButtons.Names,p.Key)>=4&&Array.IndexOf(PadButtons.Names,p.Key)<=7).Sum(p=>p.Value);GamepadGlyph.Text(dc,(Theme.Chinese?"总按键 ":"Total ")+Total.ToString("N0")+(Theme.Chinese?"  ·  动作键 ":"  ·  Actions ")+actions.ToString("N0"),new Point(ActualWidth/2,52),12,Theme.Muted);
  scale=Math.Min((ActualWidth-24)/600,(Height-236)/338);ox=(ActualWidth-600*scale)/2;oy=76;dc.PushTransform(new TranslateTransform(ox,oy));dc.PushTransform(new ScaleTransform(scale,scale));
  var shell=Geometry.Parse("M115,83 C78,86 60,125 47,177 L20,271 C9,317 37,338 71,308 L147,249 C190,264 234,264 270,258 L330,258 C371,264 412,263 453,249 L529,308 C564,338 592,317 580,271 L553,177 C539,125 521,86 485,83 C443,76 418,93 386,94 L214,94 C179,92 158,76 115,83 Z");dc.DrawGeometry(Theme.Get("Field"),new Pen(Theme.Get("Stroke"),2),shell);
  Button(dc,PadButtons.Names[17],145,35,76,30,false);Button(dc,PadButtons.Names[18],455,35,76,30,false);Button(dc,PadButtons.Names[8],145,72,88,25,false);Button(dc,PadButtons.Names[9],455,72,88,25,false);
  if(Family=="Xbox"){Stick(dc,PadButtons.Names[12],142,155);Dpad(dc,237,228);Stick(dc,PadButtons.Names[13],366,228);dc.DrawEllipse(Theme.Get("Panel"),new Pen(Theme.Muted,1.5),new Point(300,126),18,18);GamepadGlyph.Text(dc,"X",new Point(300,126),22,Theme.Muted);Button(dc,PadButtons.Names[10],269,171,26,26);Button(dc,PadButtons.Names[11],331,171,26,26);}else{Dpad(dc,142,173);Stick(dc,PadButtons.Names[12],235,234);Stick(dc,PadButtons.Names[13],365,234);Button(dc,PadButtons.Names[15],300,130,116,50,false);Button(dc,PadButtons.Names[10],225,155,24,24);Button(dc,PadButtons.Names[11],375,155,24,24);Button(dc,PadButtons.Names[14],300,210,31,31);Button(dc,PadButtons.Names[16],300,246,31,18,false);if(Family==GamepadPresentation.Combined){dc.DrawRoundedRectangle(Theme.Get("Panel"),new Pen(Theme.Muted,1),new Rect(277,100,46,28),9,9);GamepadGlyph.Text(dc,"+",new Point(300,114),22,Theme.Muted);}}
  double face=Family==GamepadPresentation.Combined?46:36;Button(dc,PadButtons.Names[7],457,122,face,face);Button(dc,PadButtons.Names[6],414,165,face,face);Button(dc,PadButtons.Names[5],500,165,face,face);Button(dc,PadButtons.Names[4],457,208,face,face);dc.Pop();dc.Pop();
  // Two fixed rows keep all eight key counts readable in the narrower family cards.
  for(int i=0;i<GamepadPresentation.SummaryIndices.Length;i++){string key=PadButtons.Names[GamepadPresentation.SummaryIndices[i]];double width=(ActualWidth-32)/4,x=16+width*(i%4),y=Height-138+(i/4)*49;double labelWidth=Family==GamepadPresentation.Combined?Math.Min(72,width-8):30;GamepadGlyph.Draw(dc,key,Family,new Rect(x+(width-labelWidth)/2,y,labelWidth,20),Theme.Text);GamepadGlyph.Text(dc,Plot.Format(Get(key),"count",true),new Point(x+width/2,y+32),12,Theme.Text);hits.Add(new Hit{Button=key,Rect=new Rect(x,y,width,45)});}
  var end=Fill(Counts.Count==0?"":Counts.OrderByDescending(p=>p.Value).First().Key);dc.DrawRoundedRectangle(new SolidColorBrush(Theme.Empty),null,new Rect(20,Height-31,12,9),2,2);GamepadGlyph.Text(dc,Theme.Chinese?"少":"Less",new Point(49,Height-26),10,Theme.Muted);dc.DrawRoundedRectangle(end,null,new Rect(80,Height-31,12,9),2,2);GamepadGlyph.Text(dc,Theme.Chinese?"多":"More",new Point(111,Height-26),10,Theme.Muted);GamepadGlyph.Text(dc,Theme.Chinese?"只计按下":"Clicks only",new Point(ActualWidth-78,Height-26),10,Theme.Muted);
 }
}
}
