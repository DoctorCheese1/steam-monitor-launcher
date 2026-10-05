@echo off
setlocal
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v SteamMonitorLauncher /f >nul 2>&1
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v SteamMonitorLauncher >nul 2>&1
if errorlevel 1 (
  echo Windows startup is disabled. Exit the app from its tray icon to stop it now.
) else (
  echo Could not remove the startup entry. Try the option inside the app.
)
timeout /t 5 >nul
exit /b
