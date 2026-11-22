# InputAtlas · 输入图谱

[简体中文](README.zh-CN.md)

A local-first Windows activity dashboard, built with C# and WPF. Track keyboard and mouse activity, visualize input heatmaps, count controller buttons, and monitor network and system usage.

**Status: v0.1.0-beta.** Windows 10 22H2 x64 has been tested. Windows 11 taskbar integration needs additional testing; ordinary dashboard and collection use are separate from taskbar attachment.

## Features

- Daily, weekly, monthly, yearly and all-time summaries.
- Keyboard key heatmaps, mouse click-position heatmaps, and left/right/middle/side button counts.
- RGB and thermal palettes, adjustable heatmap contrast, and hover counts.
- Shortcut counts stored as combinations, without recording typed text.
- PlayStation DualSense and Xbox button statistics; USB and Bluetooth handling, controller heatmaps and combined totals. Stick clicks count; stick movement does not.
- Upload/download totals by Wi-Fi or network source. Unidentified transition intervals are discarded.
- CPU busy time, physical memory use, network speed and traffic totals in an optional taskbar display.
- English and Simplified Chinese, light/dark themes, and remembered window settings.
- SQLite storage, independent background collectors and sleep/wake recovery.

## Run

1. Extract the entire `InputAtlas-v0.1.0-beta-win-x64.zip` archive to a writable folder.
2. Start `InputAtlas.exe`. The first launch creates a fresh profile at `%LOCALAPPDATA%\InputAtlas`.
3. Collection starts with the dashboard and continues after its window closes. The **Live monitor** page controls taskbar display options; taskbar attachment starts disabled.
4. To stop all background collection, run `stop-background.ps1` using PowerShell. Opening the dashboard resumes collection.

Requirements: Windows 10 22H2 x64 or newer and .NET Framework 4.8. The application uses the Windows-provided SQLite engine. No R, original WhatPulse installation, .NET SDK or NuGet packages are needed to run the release.

Optional current-user startup: run `install-startup.ps1` from the extracted release folder. It creates an InputAtlas shortcut in your Startup folder. Run `remove-startup.ps1` to remove that shortcut. Both scripts work without administrator rights. They leave collected data in place.

Portable profile: create an empty `portable.mode` file next to `InputAtlas.exe` before first launch. Data will then be saved in that folder's `data` subdirectory. Keep the whole folder writable. For separate test profiles, set `INPUTATLAS_DATA_DIR` to an absolute directory before launching all components.

## Build and check

From Windows PowerShell in this repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Output: `artifacts\InputAtlas\InputAtlas.exe` and four background executables. The build uses the x64 compiler and desktop assemblies provided by .NET Framework 4.8; it does not download dependencies. Tests create isolated profiles under `artifacts\tests`, verify fresh initialization, persistence, input aggregation, network boundaries and UI rendering. Physical controller and Windows 11 compatibility still require hardware testing.

## Measurement and privacy

See [architecture and measurement](docs/ARCHITECTURE.md), [privacy](docs/PRIVACY.md), and [release notes](docs/RELEASE_NOTES.md). Activity summaries, key identifiers, shortcut combinations, click grids, application process names, network names and controller identifiers can reveal usage patterns. They remain in the local profile. InputAtlas does not contain a telemetry uploader or record screenshots/typed strings. Local user data and original WhatPulse databases are excluded from this repository and release.

The code retains optional legacy-format readers for interoperability. This release contains no automatic historical importer. It is an independent project; WhatPulse, PlayStation and Xbox names identify compatibility only.

## License

[MIT](LICENSE). Copyright 2026 InputAtlas contributors. See [third-party notices](THIRD_PARTY_NOTICES.md).
