# Steam Monitor Launcher

Move detected games onto a selected monitor, with background monitoring, per-game profiles, exclusions and Windows startup support.

[Download the latest release](https://github.com/DoctorCheese1/steam-monitor-launcher/releases/latest)

Extract the full release ZIP, then run **Launch Steam Monitor.bat**. The installer builds the Windows application and checks startup before replacing an existing installation. For a portable build, run **Build Portable.bat**.

The online updater uses:

`https://github.com/DoctorCheese1/steam-monitor-launcher/releases/latest/download/latest.json`

Update checks notify at startup and every six hours. Download and install when ready; updates preserve settings. The release feed only becomes available after the first successful release workflow.

See `SteamMonitorLauncher/FEATURE_GUIDE.txt` and `LIVE_UPDATES.txt` for features and instructions.

## Publishing updates

Change the version consistently in the application, installer, manifest and `version.txt`, then push to `main`. GitHub Actions compiles on Windows and runs startup checks before publishing an immutable versioned ZIP and `latest.json`. Existing release versions are never overwritten. Do not reuse version numbers for changed code.

Tests cover startup, embedded icons, serialization and form construction. They do not prove compatibility with every game, exclusive fullscreen mode, monitor arrangement or controller.
