using System;using System.Linq;using System.Collections.Generic;
namespace InputAtlas {
public static class ShortcutCodec {
 public static int Modifier(int key){return key==17||key==162||key==163?1:key==16||key==160||key==161?2:key==18||key==164||key==165?4:key==91||key==92?8:0;}
 public static int Bits(int mask){int n=0;for(;mask!=0;mask>>=1)n+=mask&1;return n;}
 public static string Encode(int mask,int key){return mask+"|"+key;}
 public static bool Decode(string combo,out int mask,out int key){mask=key=0;var p=combo.Split('|');return p.Length==2&&Int32.TryParse(p[0],out mask)&&Int32.TryParse(p[1],out key)&&mask>0&&mask<16&&key>=0;}
 public static string KeyName(int key){
  if(key>=65&&key<=90||key>=48&&key<=57)return ((char)key).ToString();if(key>=96&&key<=105)return "Num "+(key-96);if(key>=112&&key<=135)return "F"+(key-111);
  switch(key){case 8:return "Backspace";case 9:return "Tab";case 13:return "Enter";case 16:case 160:case 161:return "Shift";case 17:case 162:case 163:return "Ctrl";case 18:case 164:case 165:return "Alt";case 19:return "Pause";case 20:return "Caps Lock";case 27:return "Esc";case 32:return "Space";case 33:return "Page Up";case 34:return "Page Down";case 35:return "End";case 36:return "Home";case 37:return "←";case 38:return "↑";case 39:return "→";case 40:return "↓";case 44:return "Print Screen";case 45:return "Insert";case 46:return "Delete";case 91:case 92:return "Win";case 93:return "Menu";case 106:return "Num *";case 107:return "Num +";case 109:return "Num -";case 110:return "Num .";case 111:return "Num /";case 144:return "Num Lock";case 145:return "Scroll Lock";case 166:return "Browser Back";case 167:return "Browser Forward";case 173:return "Mute";case 174:return "Volume Down";case 175:return "Volume Up";case 176:return "Media Next";case 177:return "Media Previous";case 178:return "Media Stop";case 179:return "Play / Pause";case 186:return ";";case 187:return "=";case 188:return ",";case 189:return "-";case 190:return ".";case 191:return "/";case 192:return "`";case 219:return "[";case 220:return "\\";case 221:return "]";case 222:return "'";case 269:return "Num Enter";default:return null;}
 }
 public static string Label(string combo){int mask,key;if(!Decode(combo,out mask,out key))return null;var names=new List<string>();if((mask&1)!=0)names.Add("Ctrl");if((mask&4)!=0)names.Add("Alt");if((mask&2)!=0)names.Add("Shift");if((mask&8)!=0)names.Add("Win");if(key!=0){string name=KeyName(key);if(name==null)return null;names.Add(name);}return names.Count>=2?String.Join(" + ",names):null;}
}
// Holds physical edges in memory; persisted output is only hourly aggregates.
public sealed class ShortcutTracker {
 readonly Dictionary<string,int> held=new Dictionary<string,int>();int pending;DateTime pendingAt;string pendingApp="";
 int Mask(){int mask=0;foreach(int key in held.Values)mask|=ShortcutCodec.Modifier(key);return mask;}
 public void Reset(){held.Clear();pending=0;}
 public void Forget(long device,int key){held.Remove(device+"|"+key);pending=0;}
 public void Edge(long device,int key,bool down,DateTime at,string app,Action<DateTime,string,string> add){
  string id=device+"|"+key;int mod=ShortcutCodec.Modifier(key);
  if(down){if(held.ContainsKey(id))return;held[id]=key;int mask=Mask();
   if(mod!=0){pending=ShortcutCodec.Bits(mask)>=2&&!held.Values.Any(k=>ShortcutCodec.Modifier(k)==0)?mask:0;pendingAt=at;pendingApp=app;}
   else{pending=0;if(mask!=0&&ShortcutCodec.KeyName(key)!=null)add(at,app,ShortcutCodec.Encode(mask,key));}
  }else{if(!held.ContainsKey(id))return;if(mod!=0&&pending!=0){add(pendingAt,pendingApp,ShortcutCodec.Encode(pending,0));pending=0;}held.Remove(id);}
 }
}
}
