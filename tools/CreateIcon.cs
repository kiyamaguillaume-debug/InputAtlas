using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

// Build-time vector drawing. No graphics library is loaded by the dashboard.
class CreateIcon {
 static GraphicsPath Rounded(float x,float y,float w,float h,float r){var p=new GraphicsPath();p.AddArc(x,y,2*r,2*r,180,90);p.AddArc(x+w-2*r,y,2*r,2*r,270,90);p.AddArc(x+w-2*r,y+h-2*r,2*r,2*r,0,90);p.AddArc(x,y+h-2*r,2*r,2*r,90,90);p.CloseFigure();return p;}
 static byte[] Draw(int size){
  using(var large=new Bitmap(size*4,size*4,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(large)){
   g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size*4/256f,size*4/256f);g.Clear(Color.Transparent);
   using(var tile=Rounded(6,6,244,244,52))using(var bg=new LinearGradientBrush(new Point(0,0),new Point(256,256),Color.FromArgb(47,47,47),Color.FromArgb(20,20,20)))using(var edge=new Pen(Color.FromArgb(86,86,86),2)){g.FillPath(bg,tile);g.DrawPath(edge,tile);}
   var points=new[]{new PointF(43,139),new PointF(79,139),new PointF(101,80),new PointF(132,183),new PointF(157,118),new PointF(209,118)};
   using(var line=new LinearGradientBrush(new Point(40,0),new Point(210,0),Color.FromArgb(54,223,194),Color.FromArgb(76,165,255)))using(var pen=new Pen(line,17)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLines(pen,points);}
   using(var dot=new SolidBrush(Color.FromArgb(161,128,255)))g.FillEllipse(dot,187,48,20,20);
   using(var final=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var f=Graphics.FromImage(final))using(var stream=new MemoryStream()){
    f.CompositingMode=CompositingMode.SourceCopy;f.InterpolationMode=InterpolationMode.HighQualityBicubic;f.PixelOffsetMode=PixelOffsetMode.HighQuality;f.DrawImage(large,new Rectangle(0,0,size,size));final.Save(stream,ImageFormat.Png);return stream.ToArray();
   }
  }
 }
 static void Main(string[] args){var sizes=new[]{256,128,64,48,40,32,24,20,16};var images=new List<byte[]>();foreach(int size in sizes)images.Add(Draw(size));Directory.CreateDirectory(args[0]);File.WriteAllBytes(Path.Combine(args[0],"InputAtlas.png"),images[0]);
  using(var writer=new BinaryWriter(File.Create(Path.Combine(args[0],"InputAtlas.ico")))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(var image in images)writer.Write(image);}
 }
}
