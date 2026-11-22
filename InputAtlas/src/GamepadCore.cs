using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace InputAtlas {
public static class PadPolicy {public static bool Count(string kind,string mode,bool sony){return kind=="Sony"?mode!="xbox":mode!="sony";}}
public sealed class PadSample {public ulong Buttons;public byte LeftTrigger,RightTrigger;}
public static class PadButtons {
 public static readonly string[] Names={"Up","Down","Left","Right","Cross / A","Circle / B","Square / X","Triangle / Y","L1 / LB","R1 / RB","Create / View","Options / Menu","L3 / LS","R3 / RS","PS","Touchpad click","Microphone mute","L2 / LT","R2 / RT"};
 public static ulong Bit(int i){return 1UL<<i;}
 public const ulong CountedButtons=(1UL<<19)-1;
 public static bool Countable(string key){return Array.IndexOf(Names,key)>=0;}
 public static string Family(string controller,string device){return controller.StartsWith("Xbox",StringComparison.OrdinalIgnoreCase)||device.StartsWith("XInput-",StringComparison.OrdinalIgnoreCase)?"Xbox":"PlayStation";}
 public static string Label(string button,string family){switch(Array.IndexOf(Names,button)){case 0:return "↑";case 1:return "↓";case 2:return "←";case 3:return "→";case 4:return family=="Xbox"?"A":"×";case 5:return family=="Xbox"?"B":"○";case 6:return family=="Xbox"?"X":"□";case 7:return family=="Xbox"?"Y":"△";case 8:return family=="Xbox"?"LB":"L1";case 9:return family=="Xbox"?"RB":"R1";case 10:return family=="Xbox"?"▣":"≡";case 11:return "☰";case 12:return family=="Xbox"?"LS":"L3";case 13:return family=="Xbox"?"RS":"R3";case 14:return "PS";case 15:return "▭";case 16:return "♪";case 17:return family=="Xbox"?"LT":"L2";case 18:return family=="Xbox"?"RT":"R2";default:return "—";}}
 public static bool TrySony(int product,byte[] report,out PadSample sample){
  sample=null;if(report==null||report.Length<10)return false;bool dualSense=product==0x0CE6||product==0x0DF2;bool ds4=product==0x05C4||product==0x09CC;if(!dualSense&&!ds4)return false;
  bool simple=dualSense&&report[0]==1&&(report.Length==10||report.Length==78);int b,t;if(dualSense){if(report[0]==0x31&&report.Length>=78){b=9;t=6;}else if(report[0]==1&&(report.Length==10||report.Length==78)){b=5;t=8;}else if(report[0]==1&&report.Length>=64){b=8;t=5;}else return false;}else{if(report[0]==1&&report.Length>=64){b=5;t=8;}else if(report[0]==0x11&&report.Length>=78){b=7;t=10;}else return false;}
  byte a=report[b],c=report[b+1],d=report[b+2];ulong bits=0;int hat=a&15;if(hat==0||hat==1||hat==7)bits|=Bit(0);if(hat==3||hat==4||hat==5)bits|=Bit(1);if(hat==5||hat==6||hat==7)bits|=Bit(2);if(hat==1||hat==2||hat==3)bits|=Bit(3);
  if((a&32)!=0)bits|=Bit(4);if((a&64)!=0)bits|=Bit(5);if((a&16)!=0)bits|=Bit(6);if((a&128)!=0)bits|=Bit(7);
  int[] sourceBits={0,1,4,5,6,7};for(int i=0;i<sourceBits.Length;i++)if((c&(1<<sourceBits[i]))!=0)bits|=Bit(8+i);
  if((d&1)!=0)bits|=Bit(14);if((d&2)!=0)bits|=Bit(15);if(dualSense&&!simple&&(d&4)!=0)bits|=Bit(16);
  sample=new PadSample{Buttons=bits,LeftTrigger=report[t],RightTrigger=report[t+1]};return true;
 }
 public static PadSample Xbox(ushort buttons,byte lt,byte rt){int[] masks={1,2,4,8,0x1000,0x2000,0x4000,0x8000,0x100,0x200,0x20,0x10,0x40,0x80};ulong bits=0;for(int i=0;i<masks.Length;i++)if((buttons&masks[i])!=0)bits|=Bit(i);return new PadSample{Buttons=bits,LeftTrigger=lt,RightTrigger=rt};}
}
public sealed class PadEdges {
 bool initialized,left,right;ulong previous;
 public void Reset(){initialized=false;previous=0;left=right=false;}
 public List<string> Read(PadSample sample){bool baseline=!initialized;left=sample.LeftTrigger>=(baseline||!left?30:20);right=sample.RightTrigger>=(baseline||!right?30:20);ulong next=(sample.Buttons|(left?PadButtons.Bit(17):0)|(right?PadButtons.Bit(18):0))&PadButtons.CountedButtons;ulong pressed=initialized?next&~previous:0;previous=next;initialized=true;var result=new List<string>();for(int i=0;i<PadButtons.Names.Length;i++)if((pressed&PadButtons.Bit(i))!=0)result.Add(PadButtons.Names[i]);return result;}
}
public sealed class PadPress {public DateTime At;public string Device,Controller,Button,App;public long Count=1;}
public sealed class PadDatabase : IDisposable {
 readonly Sqlite db;readonly List<PadPress> pending=new List<PadPress>();public int Pending {get{return pending.Count;}}
 public PadDatabase(string file){db=new Sqlite(file,true,true);db.Query("PRAGMA journal_mode=WAL");db.Query("CREATE TABLE IF NOT EXISTS controller_presses(day TEXT NOT NULL,hour INTEGER NOT NULL,device TEXT NOT NULL,controller TEXT NOT NULL,button TEXT NOT NULL,application TEXT NOT NULL,presses INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(day,hour,device,button,application))");db.Query("CREATE INDEX IF NOT EXISTS controller_by_day ON controller_presses(day)");db.Query("PRAGMA user_version=1");}
 public void Add(PadPress p){pending.Add(p);}
 public void Flush(){if(pending.Count==0)return;db.Query("BEGIN IMMEDIATE");try{foreach(var p in pending){object[] key={p.At.ToString("yyyy-MM-dd"),p.At.Hour,p.Device,p.Controller,p.Button,p.App};db.Query("INSERT OR IGNORE INTO controller_presses(day,hour,device,controller,button,application,presses) VALUES(?,?,?,?,?,?,0)",key);db.Query("UPDATE controller_presses SET presses=presses+? WHERE day=? AND hour=? AND device=? AND button=? AND application=?",p.Count,key[0],key[1],key[2],key[4],key[5]);}db.Query("COMMIT");pending.Clear();}catch{try{db.Query("ROLLBACK");}catch{}throw;}}
 public void Dispose(){try{Flush();}finally{db.Dispose();}}
}
}
