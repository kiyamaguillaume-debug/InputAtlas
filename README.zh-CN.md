# 输入图谱 · InputAtlas

[English](README.md)

使用 C# 和 WPF 开发的 Windows 本地活动统计工具：键鼠统计、键位与点击位置热图、快捷键、手柄按键、网络流量和任务栏监控。

当前为 **v0.1.0-beta 测试版**。已在 Windows 10 22H2 x64 验证；Windows 11，尤其是任务栏嵌入显示，需要补充实机测试。

## 使用

1. 完整解压 `InputAtlas-v0.1.0-beta-win-x64.zip`，双击 `InputAtlas.exe`。
2. 首次启动会在 `%LOCALAPPDATA%\InputAtlas` 自动建立数据库与默认设置。
3. 打开界面时启动后台采集；关闭界面后继续采集。进入“实时监控”页面勾选需要显示的项目，任务栏显示默认关闭。
4. 需要停止全部后台组件时，用 PowerShell 运行发行包中的 `stop-background.ps1`；再次打开界面可恢复。

运行要求：Windows 10 22H2 x64 或更新版本、.NET Framework 4.8。使用系统 SQLite，无需 R、原版 WhatPulse、.NET SDK 或 NuGet 包。

可选开机启动：在发行目录运行 `install-startup.ps1`，添加当前用户的启动快捷方式；`remove-startup.ps1` 用于移除。无需管理员权限，保留本地统计数据。

便携模式：启动前，在程序旁新建空文件 `portable.mode`，数据将保存到同目录的 `data` 文件夹。整个目录应允许写入。高级测试可使用环境变量 `INPUTATLAS_DATA_DIR` 指定独立的绝对路径。

## 功能和统计口径

- 按日、周、月、年、总计查看；日柱形图默认展示范围内最后 10 天。
- 键盘键位热图、鼠标点击位置热图、左右键和其他鼠标按钮统计；支持 RGB 与紫橙黄配色及对比度调节。
- 快捷键按组合统计；保存键位与次数，不保存输入的文本字符串。
- DualSense、Xbox 的 USB/蓝牙按键采集、独立与合并统计。L3/R3 算按键；摇杆位移不计数。
- 网络来源筛选；来源不明或跨网络切换的间隔流量直接舍弃。
- CPU 忙碌时间比例、物理内存使用率、实时网速，以及可选任务栏显示。
- 中英文、明暗主题、窗口尺寸记忆、后台守护与休眠恢复。

鼠标距离依赖设置的 DPI；词数由空格/回车等边界估算。CPU 使用率采用 GetSystemTimes 忙碌时间比例，可能与任务管理器按性能修正的 Processor Utility 不同。网络记录为网卡计数字节，应用流量归属由 Windows 网络缓存提供，存在延迟。详细说明见[架构](docs/ARCHITECTURE.md)与[隐私说明](docs/PRIVACY.md)。

## 编译与验证

在源码根目录使用 Windows PowerShell：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

产物在 `artifacts\InputAtlas`。使用 .NET Framework 4.8 自带的 x64 编译器及系统程序集，不下载外部依赖。测试使用 `artifacts\tests` 中独立的数据目录。

源码和发行包均不含个人历史数据库、Wi-Fi 名称、设备身份缓存、日志或本地设置。可选旧格式读取代码用于兼容；本发行版未包含自动历史导入工具。本项目独立开发，兼容性名称不表示官方关联。

采用 [MIT 许可证](LICENSE)，见[第三方说明](THIRD_PARTY_NOTICES.md)。
