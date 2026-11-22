using System;
using System.Runtime.InteropServices;
namespace InputAtlas {
public static class XInputConnection {
 [StructLayout(LayoutKind.Sequential)]struct GAMEPAD {public ushort Buttons;public byte LT,RT;public short LX,LY,RX,RY;}
 [StructLayout(LayoutKind.Sequential)]struct CAPS {public byte Type,SubType;public ushort Flags;public GAMEPAD Pad;public ushort LeftMotor,RightMotor;}
 [DllImport("xinput1_4.dll")]static extern uint XInputGetCapabilities(uint index,uint flags,out CAPS capabilities);
 // XInput's wireless flag cannot distinguish Bluetooth from Xbox Wireless.
 // A missing flag is not reliable proof of a physical USB device (virtual pads exist).
 public static string Describe(uint result,ushort flags){return result==0&&(flags&2)!=0?"Wireless":"Unknown";}
 public static string Read(uint index){try{CAPS caps;uint result=XInputGetCapabilities(index,0,out caps);return Describe(result,caps.Flags);}catch(DllNotFoundException){return "Unknown";}catch(EntryPointNotFoundException){return "Unknown";}}
}
}
