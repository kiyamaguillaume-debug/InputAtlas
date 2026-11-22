using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace InputAtlas {
public sealed class XboxSlotInfo {public int Slot;public uint Result;public ushort Vendor,Product,Flags;}
public sealed class XboxHidInfo {public uint Vendor,Product;public string DeviceId,Connection="Unknown";public IntPtr Handle;}
public sealed class XboxIdentityResult {public int Slot;public string DeviceId,Connection,Reason;}
public sealed class XboxIdentityResolver {
 [StructLayout(LayoutKind.Explicit,Size=32)]struct CAPS {[FieldOffset(2)]public ushort Flags;[FieldOffset(20)]public ushort Vendor;[FieldOffset(22)]public ushort Product;}
 // Optional metadata extension used by SDL. Failure never disables ordinary XInput counting.
 [DllImport("xinput1_4.dll",EntryPoint="#108")]static extern uint XInputGetCapabilitiesEx(uint reserved,uint index,uint flags,out CAPS caps);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetSerialNumberString(SafeFileHandle handle,byte[] buffer,int length);
 [StructLayout(LayoutKind.Sequential)]struct KEY {public Guid Format;public uint Id;}
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_Interface_PropertyW(string path,ref KEY key,out uint type,byte[] buffer,ref uint size,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Locate_DevNodeW(out uint node,string id,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_IDW(uint node,StringBuilder id,uint size,uint flags);
 [DllImport("cfgmgr32.dll")]static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
 sealed class Cache {public XboxHidInfo Info;public DateTime ReadAt;}
 readonly Dictionary<string,Cache> cache=new Dictionary<string,Cache>(StringComparer.OrdinalIgnoreCase);
 public void Invalidate(){cache.Clear();}
 public static string PhysicalId(uint vendor,string serial){string value=new string((serial??"").Where(c=>!Char.IsWhiteSpace(c)&&c!=':'&&c!='-').ToArray()).ToUpperInvariant();if(value.Length<8||value.Length>128||value.Any(c=>!Char.IsLetterOrDigit(c))||value.Distinct().Count()<2)return null;return "XboxPhysical-"+SonyPhysicalIdentity.Hash(vendor.ToString("X4")+":"+value).Substring(0,32);}
 public static XboxSlotInfo[] Slots(){return Enumerable.Range(0,4).Select(i=>{var c=new CAPS();uint rc;try{rc=XInputGetCapabilitiesEx(1,(uint)i,0,out c);}catch(EntryPointNotFoundException){rc=120;}catch(DllNotFoundException){rc=120;}return new XboxSlotInfo{Slot=i,Result=rc,Vendor=c.Vendor,Product=c.Product,Flags=c.Flags};}).ToArray();}
 public static string ClassifyTransport(IEnumerable<string> parents,string path){var chain=parents.Select(p=>p.ToUpperInvariant()).ToArray();if(chain.Any(p=>p.Contains("VIGEM")||p.Contains("VJOY")||p.Contains("VIRTUAL")||p.StartsWith("ROOT\\STEAM")))return "Virtual";if(chain.Any(p=>p.StartsWith("BTHENUM\\")||p.StartsWith("BTHLEDEVICE\\")||p.StartsWith("BTHLE\\"))||path.IndexOf("{00001124-0000-1000-8000-00805f9b34fb}",StringComparison.OrdinalIgnoreCase)>=0)return "Bluetooth";if(chain.Any(p=>p.StartsWith("USB\\VID_"))&&chain.Any(p=>p.StartsWith("USB\\ROOT_HUB")))return "USB";return "Unknown";}
 static string Transport(string path){
  var parents=new List<string>();var key=new KEY{Format=new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"),Id=256};uint size=0,type;
  CM_Get_Device_Interface_PropertyW(path,ref key,out type,null,ref size,0);
  if(size>0&&size<=8192){var buffer=new byte[size];if(CM_Get_Device_Interface_PropertyW(path,ref key,out type,buffer,ref size,0)==0&&type==18){string id=Encoding.Unicode.GetString(buffer).TrimEnd('\0');uint node;if(CM_Locate_DevNodeW(out node,id,0)==0){for(int depth=0;depth<12;depth++){var name=new StringBuilder(1024);if(CM_Get_Device_IDW(node,name,1024,0)!=0)break;parents.Add(name.ToString());uint parent;if(CM_Get_Parent(out parent,node,0)!=0)break;node=parent;}}}}
  return ClassifyTransport(parents,path);
 }
 static XboxHidInfo Read(HidPadIdentity identity){var result=new XboxHidInfo{Vendor=identity.Vendor,Product=identity.Product,Handle=identity.Handle};string serial="";
  using(var handle=CreateFile(identity.Path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)){if(!handle.IsInvalid){var bytes=new byte[512];if(HidD_GetSerialNumberString(handle,bytes,bytes.Length))serial=Encoding.Unicode.GetString(bytes).Split('\0')[0];}}
  result.Connection=Transport(identity.Path);result.DeviceId=result.Connection=="Virtual"?null:PhysicalId(identity.Vendor,serial);return result;
 }
 public static XboxIdentityResult[] Resolve(IEnumerable<XboxSlotInfo> inputSlots,IEnumerable<XboxHidInfo> inputDevices){var slots=inputSlots.ToArray();var devices=inputDevices.ToArray();return slots.Select(slot=>{
  string connection=slot.Result==0&&(slot.Flags&2)!=0?"Wireless":"Unknown",id=null,reason="Metadata unavailable";
  if(slot.Result==0&&slot.Vendor!=0&&slot.Product!=0){
   var matches=devices.Where(d=>d.Vendor==slot.Vendor&&d.Product==slot.Product).ToArray();
   if(slots.Count(s=>s.Result==0&&s.Vendor==slot.Vendor&&s.Product==slot.Product)>1)reason="Multiple XInput slots have the same model";
   else if(matches.Length==0)reason="No matching HID interface";
   else if(matches.Any(d=>String.IsNullOrEmpty(d.DeviceId)))reason="HID identity unavailable or virtual";
   else if(matches.Select(d=>d.DeviceId).Distinct().Count()!=1)reason="Multiple physical devices have the same model";
   else if(matches.Any(d=>d.Connection!="Bluetooth"&&d.Connection!="USB"))reason="Device bus not verified";
   else if((slot.Flags&2)!=0&&matches.Any(d=>d.Connection=="USB"))reason="Wireless receiver identity is not verified";
   else {id=matches[0].DeviceId;reason="Unique model match and HID serial";string[] transports=matches.Select(d=>d.Connection).Distinct().ToArray();if(transports.Length==1){string bus=transports[0];if(bus=="Bluetooth"||bus=="USB"&&(slot.Flags&2)==0)connection=bus;}}
  }
  return new XboxIdentityResult{Slot=slot.Slot,DeviceId=id,Connection=connection,Reason=reason};
 }).ToArray();}
 public XboxIdentityResult[] Scan(IEnumerable<HidPadIdentity> all){var slots=Slots();var candidates=all.Where(d=>d.IsGamepad&&slots.Any(s=>s.Result==0&&s.Vendor==d.Vendor&&s.Product==d.Product)).ToArray();var present=new HashSet<string>(candidates.Select(d=>d.Path),StringComparer.OrdinalIgnoreCase);foreach(var old in cache.Keys.Where(p=>!present.Contains(p)).ToArray())cache.Remove(old);var devices=new List<XboxHidInfo>();foreach(var candidate in candidates){Cache hit;if(!cache.TryGetValue(candidate.Path,out hit)||hit.Info.Handle!=candidate.Handle||hit.Info.DeviceId==null&&(DateTime.UtcNow-hit.ReadAt).TotalSeconds>=30){hit=new Cache{Info=Read(candidate),ReadAt=DateTime.UtcNow};cache[candidate.Path]=hit;}devices.Add(hit.Info);}return Resolve(slots,devices);}
}
}
