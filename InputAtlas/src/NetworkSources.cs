using System;using System.Linq;using System.Text;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Threading;
namespace InputAtlas {
public sealed class NetworkOrigin {
 public string Id="unassigned",Name="Unassigned",Kind="unknown",Revision="";
 public static NetworkOrigin Wifi(byte[] ssid,string profile=""){string name;try{name=new UTF8Encoding(false,true).GetString(ssid);}catch{name=String.IsNullOrWhiteSpace(profile)?Encoding.Default.GetString(ssid):profile;}return new NetworkOrigin{Id="wifi:"+BitConverter.ToString(ssid).Replace("-",""),Name=name,Kind="wifi"};}
 public static NetworkOrigin Wifi(string ssid){return Wifi(Encoding.UTF8.GetBytes(ssid));}
}
public sealed class NetworkSourceProbe:IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct Notification {public uint Source,Code;public Guid Interface;public uint Size;public IntPtr Data;}
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Notify(ref Notification data,IntPtr context);
 [DllImport("wlanapi.dll")]static extern uint WlanOpenHandle(uint version,IntPtr reserved,out uint negotiated,out IntPtr client);
 [DllImport("wlanapi.dll")]static extern uint WlanCloseHandle(IntPtr client,IntPtr reserved);
 [DllImport("wlanapi.dll")]static extern uint WlanQueryInterface(IntPtr client,ref Guid adapter,uint opcode,IntPtr reserved,out uint size,out IntPtr data,out uint type);
 [DllImport("wlanapi.dll")]static extern void WlanFreeMemory(IntPtr data);
 [DllImport("wlanapi.dll")]static extern uint WlanRegisterNotification(IntPtr client,uint source,[MarshalAs(UnmanagedType.Bool)]bool ignore,Notify callback,IntPtr context,IntPtr reserved,out uint previous);
 readonly object gate=new object();readonly Dictionary<Guid,long> changes=new Dictionary<Guid,long>();readonly string session=Guid.NewGuid().ToString("N");readonly Notify callback;IntPtr handle;DateTime retry;volatile bool disposed;int signaled;string registrationError="";public string LastError="";
 public NetworkSourceProbe(){callback=Changed;Open();}
 void Open(){if(handle!=IntPtr.Zero||DateTime.UtcNow<retry)return;retry=DateTime.UtcNow.AddSeconds(15);try{uint version;uint error=WlanOpenHandle(2,IntPtr.Zero,out version,out handle);if(error!=0){handle=IntPtr.Zero;registrationError="Wi-Fi source API: "+error;return;}uint old;error=WlanRegisterNotification(handle,8,true,callback,IntPtr.Zero,IntPtr.Zero,out old);registrationError=error==0?"":"Wi-Fi change notifications: "+error;}catch(Exception ex){registrationError=ex.Message;}}
 void Changed(ref Notification data,IntPtr context){if(disposed||data.Source!=8||!(data.Code==9||data.Code==10||data.Code==11||data.Code==13||data.Code==14||data.Code==20||data.Code==21))return;lock(gate){long n;changes.TryGetValue(data.Interface,out n);changes[data.Interface]=n+1;}Interlocked.Exchange(ref signaled,1);}
 public bool ConsumeChange(){return Interlocked.Exchange(ref signaled,0)!=0;}
 string Revision(Guid adapter){lock(gate){long n;changes.TryGetValue(adapter,out n);return session+":"+n;}}
 public Dictionary<string,NetworkOrigin> Capture(IEnumerable<MonitorMetrics.IfRow> rows){LastError="";var result=new Dictionary<string,NetworkOrigin>();foreach(var row in rows.Where(MonitorMetrics.Physical)){var origin=new NetworkOrigin();if(row.OperStatus==1){if(row.Type==71)origin=Wireless(row.Guid);else{string id=row.NetworkGuid==Guid.Empty?row.Guid.ToString("D"):row.NetworkGuid.ToString("D");origin=new NetworkOrigin{Id=(row.Type==6?"wired:":"mobile:")+id,Name=(row.Type==6?"Ethernet":"Mobile")+" · "+row.Alias+" · "+id.Substring(0,6),Kind=row.Type==6?"wired":"mobile"};}}origin.Revision=Revision(row.Guid);result[row.Guid.ToString("D")]=origin;}return result;}
 NetworkOrigin Wireless(Guid adapter){Open();if(handle==IntPtr.Zero){LastError=registrationError;return new NetworkOrigin();}IntPtr data=IntPtr.Zero;try{uint size,type;uint error=WlanQueryInterface(handle,ref adapter,7,IntPtr.Zero,out size,out data,out type);if(error!=0){if(error!=5023)LastError="Wi-Fi source query: "+error;return new NetworkOrigin();}if(size<556||Marshal.ReadInt32(data,0)!=1)return new NetworkOrigin();int n=Marshal.ReadInt32(data,520);if(n<=0||n>32)return new NetworkOrigin();byte[] ssid=new byte[n];Marshal.Copy(IntPtr.Add(data,524),ssid,0,n);var origin=NetworkOrigin.Wifi(ssid,Marshal.PtrToStringUni(IntPtr.Add(data,8),256).TrimEnd('\0'));if(registrationError!="")LastError=registrationError;return origin;}catch(Exception ex){LastError=ex.Message;return new NetworkOrigin();}finally{if(data!=IntPtr.Zero)WlanFreeMemory(data);}}
 public void Dispose(){disposed=true;if(handle!=IntPtr.Zero){uint old;try{WlanRegisterNotification(handle,0,true,null,IntPtr.Zero,IntPtr.Zero,out old);}catch{}WlanCloseHandle(handle,IntPtr.Zero);handle=IntPtr.Zero;}GC.KeepAlive(callback);}
}
}
