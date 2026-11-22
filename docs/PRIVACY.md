# Local data and privacy

InputAtlas collects usage statistics while its background workers run. The profile can contain:

- Day/hour counters, key identifiers, shortcut combinations and application process names.
- Mouse button counts and coarse click-position grids for each screen configuration.
- Network byte totals, Wi-Fi SSIDs/network profile identifiers and source-switch events.
- Controller device identifiers, connection metadata and button counts by application.
- Settings, diagnostic logs and worker heartbeats.

These records can reveal activity patterns. They are saved in the selected local profile; this code has no telemetry endpoint or history uploader. It does not save screen images, window contents, clipboard contents or typed text strings. A click map contains positions, not a screenshot. Recording keys/combinations is still sensitive, so review your profile before sharing it.

Closing the dashboard leaves collection running. Use the bundled `stop-background.ps1` to stop all components. Reopening the dashboard resumes collection. Startup registration is optional. Remove it with `remove-startup.ps1`.

To erase your collected records: stop all components, close the dashboard, and remove your selected profile directory. To back up an active WAL-mode SQLite database, use SQLite's backup API or stop its writers before copying all profile files. Copying only the main database while workers write may omit recent WAL data.

The repository and release archives contain code, documentation and binaries only. `.gitignore` excludes databases, logs, caches, settings and generated test/build outputs. Verify file contents before making a public commit; ignore rules do not remove already committed data.
