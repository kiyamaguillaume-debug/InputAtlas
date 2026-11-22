# InputAtlas v0.1.0-beta

First public preview of the standalone C#/WPF dashboard and collectors.

- Fresh-profile database and default-setting initialization.
- Independent InputAtlas executable names, process coordination and data directory.
- Keyboard/mouse maps, shortcuts, controller statistics, network sources and optional taskbar monitor.
- Bilingual UI, light/dark themes, configurable map palettes and emphasis.
- System-assembly build with no NuGet dependencies.

Validation: local x64 builds, isolated fresh-profile and synthetic aggregation tests, UI rendering and background lifecycle smoke checks. Existing hardware support was carried over from the local project; a broad controller/Windows version compatibility matrix is still pending. The taskbar integration is experimental and starts disabled.

No personal history is included. This preview starts with a fresh profile and does not automatically migrate an older installation.
