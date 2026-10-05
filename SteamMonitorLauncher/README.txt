STEAM MONITOR LAUNCHER 1.12.1 - PROFILES AND TOOLS

INSTALL / UPDATE
Extract the whole ZIP into a new folder and run Launch Steam Monitor.bat.
The repair installer builds a fresh EXE, runs local startup checks, replaces
the installed launcher and creates Desktop and Start menu shortcuts. Existing
settings and exclusions are kept. Older per-game profiles remain disabled until
you enable their override so the previous global behavior does not change.
Windows .NET Framework 4.x and Windows PowerShell are required. No admin install.

QUICK START
Select your global target monitor and keep Auto-move games enabled.
Launch games normally. Closing the settings window keeps the watcher in the
tray. Right-click the tray icon to pause or exit. Windows startup remains an
option in the main window.

NEW TOOLS
Select a game, then click Game settings / Tools.

1. PER-GAME SETTINGS
Open Per-game monitor, borderless and saved layout. Enable Override, choose
that game's monitor and borderless preference, then Save profile. Disable
Override to use the global settings again. Exclusions always take precedence.
If a game's selected display is unavailable, its windows are left alone until
that display returns. Other games continue using their own/default display.

2. SAVED WINDOW LAYOUTS
Arrange a running game window, then choose Save layout from a running window
of this game in Tools, or focus it and press Ctrl+Alt+S. A picker lets you choose
the actual game rather than a splash screen when several windows exist.
Capturing creates a windowed profile with the current monitor, position and
size. Position is stored relative to the monitor work area and clamped to fit
if the display resolution changes. Enable Borderless in the profile if you
prefer filling the display; saved dimensions apply when Borderless is off.
Rendering resolution remains controlled by the game.

3. CUSTOM GAMES
Choose Add custom game EXE; select the actual game's executable. Set a name
and optional launch arguments. It appears in the library, can be launched
here, excluded, and assigned a profile. Edit/remove it from Tools.
Detection matches the exact executable and follows observed child processes.
Required store launchers, DRM/sign-in and anti-cheat still apply. Protected
Xbox/Microsoft Store games may not expose a usable executable and are not
universally supported. A shared launcher EXE can cover multiple games; choose
the game's own EXE when possible. Removing an entry never deletes game files.

4. LIVE STATUS
Tools > Live detection and movement status shows detected windows, exclusions,
paused state, waiting-for-monitor, move sent, verified position, or failure.
After a move request, a later scan reads the window bounds and verifies the
result. A sent request alone is not reported as success. A game's graphics
engine can still reposition it later. When paused, the list reflects the last
scan and will refresh after resuming.

5. MONITOR RECOGNITION
Profiles use a monitor-provided EDID serial identity where available, otherwise
the Windows monitor interface identity, and finally the old display name.
This improves matching after display-number changes and reconnection. Some
monitors/docks expose missing, duplicated or changing identifiers, so a cable,
dock or PC change can still require reselecting the monitor. No universal
physical-display identification is guaranteed. Disconnected targets wait.

6. HOTKEYS
Tools > Hotkey settings enables/disables the following global shortcuts:
Ctrl+Alt+M  Move the focused detected game to its configured display/layout.
Ctrl+Alt+P  Pause/resume automatic movement.
Ctrl+Alt+R  Restore focused game's recorded original position and pause.
Ctrl+Alt+S  Save focused game's current monitor, position and size.
M/R/S skip excluded games and unrelated desktop windows. Focus the game first.
Hotkey registration conflicts are shown in that dialog. Original position is
available for windows the current launcher session has tracked; changing
profiles can reset that history. Hotkey moves while paused are sent immediately,
but automatic verification resumes only when monitoring is enabled.

7. SETTINGS BACKUP / RESTORE
Export settings backup writes an XML file containing global choices, profiles,
custom games and exclusions. Restore replaces those choices after confirmation,
and saves a copy of current settings under settings-before-import-*.xml.
Only import a trusted backup: custom EXE paths/launch arguments are included.
Windows startup keeps its current local setting. Old v1.x backups are supported.
A backup from another PC may need new monitor selections and custom EXE paths.

8. SHORTCUTS / UNINSTALL
Setup creates Steam Monitor Launcher shortcuts on your Desktop and Start menu.
Windows Installed apps also gets an uninstall entry. Tools > Uninstall launcher
or Uninstall Steam Monitor.bat runs the same removal flow. Confirm removal;
preferences are kept by default, with a separate option to delete active settings.
Games are never removed. Logs, backups, and extracted setup files are retained.

EXCLUSIONS
Select a game and click Exclude selected game. Its current position is unchanged
and automatic movement stops for it and its tracked child windows. It remains
launchable. Show excluded games only filters the list; Manage exclusions can
allow a game again even when it is no longer installed. The explicit manual
window picker lets you deliberately move any selected window.

ARK / DISPLAY LIMITS
Use Windowed mode in ARK before asking the launcher to position it. Optional
Borderless fills the chosen monitor without changing the game's rendering
resolution. The ARK setup button supplies optional Unreal launch arguments.
ARK client detection includes ArkAscended.exe and ShooterGame.exe. Dedicated
server EXEs are not included in that fallback. ARK gets up to 15 minutes of
initial checks; other games get two minutes, including later new windows.
Exclusive fullscreen, elevated/protected windows, external services, or a
game's own display logic can resist moving. Initial frames may appear on another
monitor. No injection, anti-cheat bypass or game-config changes are performed.

FILES / LOGS
Installed EXE: %LOCALAPPDATA%\SteamMonitorLauncher\bin\SteamMonitorLauncher.exe
Settings: %LOCALAPPDATA%\SteamMonitorLauncher\settings.xml
Logs: %LOCALAPPDATA%\SteamMonitorLauncher\logs
Run Open Logs.bat for install.log, startup-check.log and watcher.log.
The EXE created beside setup redirects to the persistent installed copy.

VALIDATION
Full source and all assets are included. Source/package checks were performed
here. A Windows compiler/runtime and Steam games were not available in this
workspace. Setup on your PC compiles and checks icons, form construction,
exclusion/profile/custom-game serialization and saved-layout bounds before
replacing the installation. Real monitor behavior, hotkeys and game compatibility
still require Windows testing. Report install.log for build failures and
watcher.log for detection/movement problems.

VERSION 1.12.1 - LIBRARY LAYOUT REPAIR
The Exclude / Manage exclusions buttons now have explicit side-by-side bounds
inside a plain panel; nested-table button autosizing can no longer exceed the
row and hide their labels. Bounds update whenever the window is resized.
The game list uses all of its available height rather than rounding down to
whole rows. Move a running window and ARK windowed setup are now under
Game settings / Tools, freeing two rows of space in the main library.
All v1.8 features and saved preferences remain available.

- Search label and input now share one font, with centered text and measured spacing.

VERSION 1.12.1 - MULTI-LAUNCHER SUPPORT
Automatic local discovery: Steam, Epic Games manifests, GOG installed-game
registry entries and Ubisoft Connect installed-game registry entries.
Click Refresh games after installing a new title. No Steam install is required
for other stores. Search also matches the store name.

Epic and Ubisoft launch using the installed store client. GOG launches a known
registered EXE when available; otherwise launch through GOG while monitoring.
Incomplete Epic installations and unreadable entries are skipped.

For EA, Battle.net, Riot, Amazon, itch.io, or standalone/extracted games:
Game settings / Tools > Add game from any launcher / standalone EXE.
Choose the actual game executable and save it once. This includes extracted
folders such as those you described as SteamUnlocked games; there is no site
integration. Optional launch arguments are supported. Required store clients,
sign-in and normal game launch requirements still apply.

Optional game folder to watch: select ONLY that game's folder to catch separate
startup/game processes anywhere within it. Leave blank for exact EXE plus child
process tracking. Do not select a shared library or launcher folder. Launching
from the original store remains available and often necessary for online games.
Protected Xbox / Microsoft Store executables may not be accessible; universal
automatic discovery of every launcher is not claimed.

All detected/custom games use existing monitor profiles, exclusions, startup,
tray operation and layout controls. Existing settings remain compatible.
Windows compilation and live store/game behavior require testing on Windows.

VERSION 1.12.1 - BUILT-IN ZIP UPDATER
Install this release with Launch Steam Monitor.bat once. Future releases can
be installed from Game settings / Tools > Launcher updater, or the tray menu.
Select the full downloaded ZIP without extracting it. Confirm the version.
Older versions are rejected; the same version can repair an installation.
Settings are backed up before updating; the app restarts after success.
Profiles, exclusions, startup settings and custom games are retained.
The installer compiles and checks startup before replacing the existing EXE.
Update ZIPs must contain SteamMonitorLauncher/version.txt and the full source
package. Packages before this release do not contain the updater metadata.
Only choose trusted release ZIPs: package validation is not a digital signature.
Online checks and verified downloads are now available. Configure your hosted
release feed URL; see LIVE_UPDATES.txt.

VERSION 1.12.1 - LIBRARY, MOVEMENT, PORTABLE AND RECOVERY FEATURES
See FEATURE_GUIDE.txt for the complete new controls and updater instructions.

VERSION 1.12.1 - ONLINE UPDATER
Read LIVE_UPDATES.txt to connect a live release source and publish updates.
