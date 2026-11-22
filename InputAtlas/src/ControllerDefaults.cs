using System;
using System.IO;
namespace InputAtlas {
public static class ControllerDefaults {
 // Pause remains effective until a deliberate app launch or a new watchdog session.
 public static void Resume(string root){foreach(string name in new[]{"gamepad.paused","controller.stop"}){string path=Path.Combine(root,name);if(File.Exists(path))File.Delete(path);}}
}
}
