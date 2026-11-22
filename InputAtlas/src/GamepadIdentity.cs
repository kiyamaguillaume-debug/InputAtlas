using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace InputAtlas {
public sealed class HidPadIdentity {
 public IntPtr Handle;public uint Vendor,Product;public ushort Page,Usage;public string Path="";
 public bool IsGamepad {get{return Page==1&&(Usage==4||Usage==5);}}
 public bool SupportedSony {get{return IsGamepad&&Vendor==0x054C&&(Product==0x0CE6||Product==0x0DF2||Product==0x05C4||Product==0x09CC);}}
 // Bluetooth interface paths do not have to contain USB-style VID_/PID_ tokens.
 public static bool Sony(uint vendor,uint product,ushort page,ushort usage){return new HidPadIdentity{Vendor=vendor,Product=product,Page=page,Usage=usage}.SupportedSony;}
}
public static class HidPadDevices {
 [StructLayout(LayoutKind.Sequential)] struct LIST {public IntPtr Handle;public uint Type;}
 [StructLayout(LayoutKind.Explicit,Size=32)] struct INFO {
  [FieldOffset(0)]public uint Size;[FieldOffset(4)]public uint Type;
  [FieldOffset(8)]public uint Vendor;[FieldOffset(12)]public uint Product;[FieldOffset(16)]public uint Version;
  [FieldOffset(20)]public ushort Page;[FieldOffset(22)]public ushort Usage;
 }
 [DllImport("user32.dll",EntryPoint="GetRawInputDeviceList",SetLastError=true)]static extern uint Count(IntPtr list,ref uint n,uint size);
 [DllImport("user32.dll",EntryPoint="GetRawInputDeviceList",SetLastError=true)]static extern uint List([Out]LIST[] list,ref uint n,uint size);
 [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)]static extern uint Info(IntPtr device,uint command,ref INFO info,ref uint size);
 [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint Name(IntPtr device,uint command,StringBuilder value,ref uint size);
 public static HidPadIdentity Read(IntPtr handle){uint size=32;var info=new INFO{Size=32};if(Info(handle,0x2000000B,ref info,ref size)==UInt32.MaxValue||info.Type!=2)return null;uint n=0;Name(handle,0x20000007,null,ref n);if(n==0||n>32768)return null;var name=new StringBuilder((int)n+1);if(Name(handle,0x20000007,name,ref n)==UInt32.MaxValue)return null;return new HidPadIdentity{Handle=handle,Vendor=info.Vendor,Product=info.Product,Page=info.Page,Usage=info.Usage,Path=name.ToString()};}
 public static List<HidPadIdentity> All(){uint n=0,size=(uint)Marshal.SizeOf(typeof(LIST));if(Count(IntPtr.Zero,ref n,size)==UInt32.MaxValue||n>4096)throw new InvalidOperationException("Cannot enumerate raw input devices");var list=new LIST[n];uint got=List(list,ref n,size);if(got==UInt32.MaxValue)throw new InvalidOperationException("Device list changed during enumeration");var result=new List<HidPadIdentity>();for(int i=0;i<got;i++){if(list[i].Type!=2)continue;var identity=Read(list[i].Handle);if(identity!=null)result.Add(identity);}return result;}
}
}
