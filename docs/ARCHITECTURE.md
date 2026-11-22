# Architecture and measurement

## Components

| Executable | Responsibility |
| --- | --- |
| InputAtlas.exe | WPF dashboard; reads SQLite, changes settings, starts workers |
| InputAtlas.Collector.exe | Raw keyboard/mouse input, shortcuts, active/idle accounting and network counters |
| InputAtlas.Gamepad.Collector.exe | Sony HID reports, Xbox XInput, device matching and button counters |
| InputAtlas.Monitor.exe | CPU/memory/network sampling, tray icon and optional taskbar display |
| InputAtlas.Watchdog.exe | Worker liveness checks and sleep/wake recovery |

All components use the same selected data profile. Named mutexes, events and monitor shared memory are scoped by a hash of its absolute path. Default profile: `%LOCALAPPDATA%\InputAtlas`. `INPUTATLAS_DATA_DIR` overrides the path; `portable.mode` beside the binaries selects `data` beside the binaries. Configure every component consistently.

Fresh initialization is serialized, creates the base SQLite schema, and writes defaults only when absent. SQLite uses WAL. `inputatlas.sqlite` holds activity/input/network data; `gamepad.sqlite` holds controller presses. Dashboard queries use read-only database connections. Keep the profile on a local disk.

## Counters

- Keyboard: Raw Input key-down edges, ignoring held-key autorepeat. Key identifiers are virtual-key codes with selected physical distinctions, such as left/right modifiers and keypad Enter. Shortcut counts do not preserve keystroke order or typed strings.
- Mouse: button-down events; position is stored as a 128×72 grid per display configuration. Distance estimates use raw relative movement divided by configured DPI. Wheel steps are accumulated independently.
- Words: estimated at Space/Enter boundaries, not linguistic word segmentation.
- Focus: last input within 60 seconds. Normal approximately 2-second intervals are split at hour/day boundaries. Gaps over 6 seconds are skipped rather than assigning suspended time to active use.
- Network: changes in physical-interface byte counters, not payload-only internet bytes. Per-adapter state and Wi-Fi/network identity must agree across samples. Unknown, changed-source, reset-counter or long intervals are skipped. Daily totals and the sum of known sources share the same allocation path. The first sample establishes a baseline.
- Wi-Fi identities use SSID bytes, so access points with the same SSID are grouped. Wired networks use Windows network/interface identity; these may change when Windows recreates a network profile.
- Per-application network attribution is read from Windows network usage data by an explicitly requested PowerShell cache update. Results may lag and need available Windows data/permissions. This breakdown applies to all network sources together.
- CPU: `100 × (Δkernel + Δuser − Δidle) / (Δkernel + Δuser)` using GetSystemTimes. Kernel includes idle. This measures busy time, without nominal-frequency weighting. The first sample is a baseline.
- Memory: physical RAM use reported by GlobalMemoryStatusEx, not process-private memory.
- Controllers: rising button edges with trigger thresholds/hysteresis. L3/R3 count; analog axes do not. Xbox virtual devices count under Xbox. If a mapping layer exposes both the real PlayStation and virtual Xbox device, select Xbox-only mode to avoid counting both input paths.

## UI and limits

Input maps use RGB or thermal color lookup tables and adjustable relative-to-peak intensity. Their colors are relative to the selected period, not an absolute comparison across periods. Hover details show recorded grid/key counts. Daily bar charts show the most recent 10 selected days.

Taskbar attachment manipulates the Explorer taskbar window hierarchy and is experimental, especially on Windows 11 and third-party taskbar replacements. It is disabled by default. Collection and the ordinary dashboard are usable without attachment.

The beta includes format readers for historical imports but no automated import utility. Historical aggregate totals cannot reconstruct event locations or keys that the original database did not retain.
