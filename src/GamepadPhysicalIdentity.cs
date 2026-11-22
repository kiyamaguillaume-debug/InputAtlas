using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace InputAtlas {
public static class SonyPhysicalIdentity {
 public static string Hash(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
 public static string InterfaceId(string path){return "Sony-"+Hash(path.ToUpperInvariant()).Substring(0,16);}
 public static string FromAddress(string serial){
  string value=new string((serial??"").Where(c=>!Char.IsWhiteSpace(c)&&c!='-'&&c!=':').ToArray()).ToUpperInvariant();
  if(value.Length!=12||value.Any(c=>!Uri.IsHexDigit(c))||value.All(c=>c=='0')||value.All(c=>c=='F'))return null;
  return "SonyPhysical-"+Hash("Sony:"+value).Substring(0,32);
 }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetSerialNumberString(SafeFileHandle handle,byte[] buffer,int length);
 [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetFeature(SafeFileHandle handle,byte[] buffer,int length);
 [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
 [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_FreePreparsedData(IntPtr data);
 [DllImport("hid.dll")]static extern uint HidP_GetCaps(IntPtr data,[Out]byte[] caps);
 public static bool Bluetooth(HidPadIdentity d){return d.Path.IndexOf("VID&",StringComparison.OrdinalIgnoreCase)>=0;}
 // Only discovery queries; no exclusive handle, input reads, output effects or per-frame metadata calls.
 public static string Read(HidPadIdentity d){
  if(d==null||!d.SupportedSony)return null;
  using(var handle=CreateFile(d.Path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)){
   if(handle.IsInvalid)return null;
   var serial=new byte[512];string physical=null;
   if(HidD_GetSerialNumberString(handle,serial,serial.Length))physical=FromAddress(Encoding.Unicode.GetString(serial).Split('\0')[0]);
   // DS5 USB report 0x09 exposes the same hardware address as its Bluetooth serial.
   // Do not query it over Bluetooth: that query can change Bluetooth report mode.
   bool ds5=d.Product==0x0CE6||d.Product==0x0DF2;
   if(ds5&&d.Path.IndexOf("VID_054C&PID_",StringComparison.OrdinalIgnoreCase)>=0){
    IntPtr data;if(HidD_GetPreparsedData(handle,out data)){
     try{var caps=new byte[64];if(HidP_GetCaps(data,caps)==0x00110000){int length=BitConverter.ToUInt16(caps,8);if(length>=7&&length<=4096){var report=new byte[length];report[0]=9;if(HidD_GetFeature(handle,report,report.Length)&&report[0]==9){string verified=FromAddress(String.Concat(Enumerable.Range(1,6).Reverse().Select(i=>report[i].ToString("X2"))));if(verified!=null)physical=verified;}}}}finally{HidD_FreePreparsedData(data);}
    }
   }
   return physical;
  }
 }
}
public sealed class PadIdentityRegistry {
 public const string FileName="controller-identities.json";
 readonly string file;readonly Dictionary<string,string> aliases=new Dictionary<string,string>(StringComparer.Ordinal);
 public PadIdentityRegistry(string path){file=path;if(!File.Exists(file))return;var doc=Store.ReadJson(file);if(!doc.ContainsKey("version")||Convert.ToInt32(doc["version"])!=1)throw new IOException("Unsupported controller identity registry");var entries=doc["aliases"] as Dictionary<string,object>;if(entries==null)throw new IOException("Invalid controller identity aliases");foreach(var entry in entries){string value=Convert.ToString(entry.Value);if(!ValidInterface(entry.Key)||!ValidPhysical(value))throw new IOException("Invalid controller identity entry");aliases.Add(entry.Key,value);}}
 static bool ValidInterface(string id){return id!=null&&id.StartsWith("Sony-")&&id.Length==21&&id.Substring(5).All(Uri.IsHexDigit);}
 static bool ValidPhysical(string id){return id!=null&&id.StartsWith("SonyPhysical-")&&id.Length==45&&id.Substring(13).All(Uri.IsHexDigit);}
 public string Resolve(string id){string value;return id!=null&&aliases.TryGetValue(id,out value)?value:id;}
 public void Register(string oldId,string physical){if(!ValidInterface(oldId)||!ValidPhysical(physical))throw new ArgumentException("Invalid verified controller identity");string prior;if(aliases.TryGetValue(oldId,out prior)){if(prior!=physical)throw new IOException("Controller identity conflict; existing history was retained");return;}aliases.Add(oldId,physical);try{string temp=file+".tmp";File.WriteAllText(temp,Store.Json.Serialize(new{version=1,aliases=aliases}));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}catch{aliases.Remove(oldId);throw;}}
}
public sealed class PadDevice {
 public string IdentityReason="",InputApi="",Id,SourceId,Name,Kind,LastButton="",Connection="";public int Product;public bool Connected,Counting;public int LastReportId,LastReportBytes;public DateTime LastReport,LastPressAt;public long SessionPresses;public readonly PadEdges Edges=new PadEdges();
}
public sealed class PadSourceRouter {
 readonly Dictionary<string,string> active=new Dictionary<string,string>();
 public static bool Fresh(PadDevice d,DateTime now){double age=(now-d.LastReport).TotalSeconds;return d.Connected&&age>=0&&age<=3;}
 public static PadDevice Preferred(IEnumerable<PadDevice> devices,DateTime now){return devices.Where(d=>Fresh(d,now)).OrderByDescending(d=>d.Connection=="USB").ThenByDescending(d=>d.LastReport).FirstOrDefault();}
 public bool Accept(PadDevice device,IEnumerable<PadDevice> devices,DateTime now){var selected=Preferred(devices.Where(d=>d.Id==device.Id),now);if(selected!=device){device.Edges.Reset();return false;}string previous;if(!active.TryGetValue(device.Id,out previous)||previous!=device.SourceId){device.Edges.Reset();active[device.Id]=device.SourceId;}return true;}
 public void ForgetMissing(IEnumerable<PadDevice> devices){var ids=new HashSet<string>(devices.Select(d=>d.Id));foreach(string id in active.Keys.Where(k=>!ids.Contains(k)).ToArray())active.Remove(id);}
}
public sealed class PadDeviceStatus {
 public string id,name,kind,api,connection,lastReport,lastButton,source,identityReason;public bool connected,counting,identityKnown;public int interfaceCount,reportId,reportBytes;public long sessionPresses;
}
public static class PadDeviceGroups {
 public static PadDeviceStatus[] Describe(IEnumerable<PadDevice> devices,DateTime now,bool stopped,Func<PadDevice,bool> enabled){return devices.GroupBy(d=>d.Id).Select(g=>{
  var selected=PadSourceRouter.Preferred(g,now);var latest=selected??g.OrderByDescending(d=>d.LastReport).First();var lastPress=g.OrderByDescending(d=>d.LastPressAt).First();
  return new PadDeviceStatus{id=g.Key,name=latest.Name,kind=latest.Kind,api=latest.InputApi,source=latest.SourceId,identityReason=latest.IdentityReason,connection=selected==null?"":selected.Connection,connected=selected!=null,counting=selected!=null&&!stopped&&enabled(selected),identityKnown=g.Key.StartsWith("SonyPhysical-")||g.Key.StartsWith("XboxPhysical-"),interfaceCount=g.Count(),reportId=latest.LastReportId,reportBytes=latest.LastReportBytes,lastReport=latest.LastReport==DateTime.MinValue?"":latest.LastReport.ToString("o"),lastButton=lastPress.LastButton,sessionPresses=g.Sum(d=>d.SessionPresses)};
 }).ToArray();}
}
}
