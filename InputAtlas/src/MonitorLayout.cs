using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
namespace InputAtlas {
public sealed class MonitorLayout {
 public sealed class Cell {public string Label,Value;public float X,ValueX,LabelWidth,ValueWidth,ColumnWidth;public int Row,Column;}
 public readonly List<Cell> Cells=new List<Cell>();public int Width,MinimumWidth,AutomaticWidth;public float[] Separators;
 static float Measure(Graphics g,Font font,string text){return g.MeasureString(text,font,Int32.MaxValue,StringFormat.GenericTypographic).Width;}
 public static List<KeyValuePair<string,string>> Lines(MonitorOptions options,MonitorSnapshot sample){var lines=new List<KeyValuePair<string,string>>();if(options.Cpu)lines.Add(new KeyValuePair<string,string>("CPU",options.PerformanceText(sample.Cpu)));if(options.Memory)lines.Add(new KeyValuePair<string,string>("RAM",options.PerformanceText(sample.MemoryPercent)));if(options.Download)lines.Add(new KeyValuePair<string,string>("↓",MonitorMetrics.Speed(sample.Download)));if(options.Upload)lines.Add(new KeyValuePair<string,string>("↑",MonitorMetrics.Speed(sample.Upload)));if(options.DailyTraffic){string label=MonitorOptions.PeriodLabel(options.TrafficPeriod,options.Language==1,true);lines.Add(new KeyValuePair<string,string>(label+"↓",sample.TrafficPeriod==options.TrafficPeriod?MonitorMetrics.Bytes(sample.TrafficDownload):"—"));lines.Add(new KeyValuePair<string,string>(label+"↑",sample.TrafficPeriod==options.TrafficPeriod?MonitorMetrics.Bytes(sample.TrafficUpload):"—"));}return lines;}
 public static MonitorLayout Create(Graphics g,Font font,IList<KeyValuePair<string,string>> lines,int customWidth,float scale,int performanceDigits=0){
  var layout=new MonitorLayout();if(lines.Count==0){layout.Separators=new float[0];return layout;}
  int columns=(lines.Count+1)/2;var sizes=new float[columns];var labelWidths=new float[columns];var valueWidths=new float[columns];float padding=8*scale,within=6*scale;
  for(int i=0;i<lines.Count;i++){string label=lines[i].Key,value=lines[i].Value;string reference=label=="CPU"||label=="RAM"?100.0.ToString(performanceDigits==1?"0.0":"0")+"%":label=="↓"||label=="↑"?"1023.9 KB/s":"9999.99 GB";int column=i/2;labelWidths[column]=Math.Max(labelWidths[column],Measure(g,font,label)+1*scale);valueWidths[column]=Math.Max(valueWidths[column],Math.Max(Measure(g,font,value),Measure(g,font,reference))+2*scale);layout.Cells.Add(new Cell{Label=label,Value=value,Row=i%2,Column=column});}
  for(int c=0;c<columns;c++)sizes[c]=labelWidths[c]+within+valueWidths[c];
  foreach(var cell in layout.Cells){cell.LabelWidth=labelWidths[cell.Column];cell.ValueWidth=valueWidths[cell.Column];}
  float content=sizes.Sum();layout.MinimumWidth=(int)Math.Ceiling((padding*2+content+8*scale*(columns-1))/scale);layout.AutomaticWidth=(int)Math.Ceiling((padding*2+content+26*scale*(columns-1))/scale);layout.Width=customWidth<=0?layout.AutomaticWidth:Math.Max(layout.MinimumWidth,Math.Min(1200,customWidth));
  float extra=layout.Width*scale-padding*2-content,gap=columns>1?extra/(columns-1):0,x=padding+(columns==1?extra/2:0);layout.Separators=new float[Math.Max(0,columns-1)];for(int c=0;c<columns;c++){foreach(var cell in layout.Cells.Where(v=>v.Column==c)){cell.X=x;cell.ValueX=x+cell.LabelWidth+within;cell.ColumnWidth=sizes[c];}if(c<columns-1)layout.Separators[c]=x+sizes[c]+gap/2;x+=sizes[c]+gap;}
  return layout;
 }
 public static void Draw(Graphics g,Font font,MonitorLayout layout,int width,int height,float scale,bool light,MonitorPalette palette=null){
  if(palette==null)palette=MonitorPalette.Theme(light);g.Clear(palette.Background);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;float row=height/2f;
  using(var ink=new SolidBrush(palette.Foreground))using(var muted=new SolidBrush(palette.Label))using(var divider=new Pen(palette.Divider))using(var format=new StringFormat(StringFormat.GenericTypographic){Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap}){
   foreach(float x in layout.Separators)g.DrawLine(divider,x,7*scale,x,height-7*scale);
   foreach(var cell in layout.Cells){float y=cell.Row*row;g.DrawString(cell.Label,font,muted,new RectangleF(cell.X,y,cell.LabelWidth+2*scale,row),format);g.DrawString(cell.Value,font,ink,new RectangleF(cell.ValueX,y,cell.ValueWidth+2*scale,row),format);}
  }
 }
}
}
