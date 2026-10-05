param([switch]$ValidateOnly,[switch]$Portable,[string]$PortableDirectory="")
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dataDir = Join-Path $env:LOCALAPPDATA 'SteamMonitorLauncher'
if ($Portable) { $dataDir = if ($PortableDirectory) { [IO.Path]::GetFullPath($PortableDirectory) } else { Join-Path $root 'Portable' } }
if ($ValidateOnly) { $dataDir = Join-Path (Split-Path $root) 'validation-output' }
$appDir = if ($Portable) { $dataDir } else { Join-Path $dataDir 'bin' }
$logDir = Join-Path $dataDir 'logs'
$target = Join-Path $appDir 'SteamMonitorLauncher.exe'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$log = Join-Path $logDir 'install.log'
$stage = $null
$transcribing = $false
try {
    Start-Transcript -Path $log -Force | Out-Null
    $transcribing = $true
    Write-Host 'Steam Monitor Launcher 1.12.1 - Install / Repair'
    $required = @('Launcher.cs','Native.cs','SteamLibrary.cs','OtherLibraries.cs','Updater.cs','LibraryPlus.cs','Watcher.cs','Interface.cs','Diagnostics.cs','Exclusions.cs','Features.cs','Displays.cs','app.manifest','Launcher.ico','LauncherPaused.ico','Assets\Launcher.png','Uninstall.ps1','Update.ps1','version.txt')
    foreach ($name in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) {
            throw "Missing $name. Extract the ENTIRE ZIP into a new folder before running setup."
        }
    }
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler is missing. See install.log for details.' }
    New-Item -ItemType Directory -Path $appDir -Force | Out-Null
    $stage = Join-Path $appDir ('build-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    if ($Portable -or $ValidateOnly) { Set-Content -LiteralPath (Join-Path $stage 'portable.flag') -Value 'portable' -Encoding ASCII }
    $candidate = Join-Path $stage 'SteamMonitorLauncher.exe'
    $arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+',
        ('/out:' + $candidate),('/win32manifest:' + (Join-Path $root 'app.manifest')),
        ('/win32icon:' + (Join-Path $root 'Launcher.ico')),
        ('/resource:' + (Join-Path $root 'Launcher.ico') + ',Launcher.ico'),
        ('/resource:' + (Join-Path $root 'LauncherPaused.ico') + ',LauncherPaused.ico'),
        ('/resource:' + (Join-Path $root 'Assets\Launcher.png') + ',LauncherHeader.png'),
        '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Xml.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll')
    foreach ($name in @('Launcher.cs','Native.cs','SteamLibrary.cs','OtherLibraries.cs','Updater.cs','LibraryPlus.cs','Watcher.cs','Interface.cs','Diagnostics.cs','Exclusions.cs','Features.cs','Displays.cs')) { $arguments += Join-Path $root $name }
    Write-Host 'Building a fresh executable (including same-version repairs)...'
    $output = & $compiler @arguments 2>&1
    $compileExit = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($compileExit -ne 0 -or -not (Test-Path -LiteralPath $candidate)) { throw "Compilation failed (exit $compileExit). The previous installation has not been replaced." }
    [Reflection.AssemblyName]::GetAssemblyName($candidate) | Out-Null
    Write-Host 'Checking startup, icon loading and settings-window construction...'
    $check = Start-Process -FilePath $candidate -ArgumentList '--self-test' -PassThru
    if (-not $check.WaitForExit(30000)) { $check.Kill(); throw 'The new executable did not finish its startup check. Existing files were not replaced.' }
    if ($ValidateOnly -and (Test-Path (Join-Path $stage 'logs'))) { Copy-Item -Path (Join-Path $stage 'logs\*') -Destination $logDir -Force }
    if ($check.ExitCode -ne 0) { if ($ValidateOnly) { Get-Content -LiteralPath (Join-Path $logDir 'startup-check.log') -ErrorAction SilentlyContinue | Write-Host }; throw 'Startup check failed. See startup-check.log in the logs folder. Existing files were not replaced.' }
    if ($ValidateOnly) { Write-Host 'Windows build and startup checks passed.'; exit 0 }
    # Stop only this application's installed/extracted executables after a successful build check.
    $prefix = [IO.Path]::GetFullPath($appDir).TrimEnd('\') + '\'
    $sourceExe = [IO.Path]::GetFullPath((Join-Path $root 'SteamMonitorLauncher.exe'))
    foreach ($p in @(Get-Process -Name 'SteamMonitorLauncher*' -ErrorAction SilentlyContinue)) {
        try { $exePath = $p.Path } catch { continue }
        if ($exePath -and ($exePath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or $exePath.Equals($sourceExe,[StringComparison]::OrdinalIgnoreCase))) {
            Write-Host ('Closing prior launcher: ' + $p.Id)
            Stop-Process -Id $p.Id -ErrorAction Stop
            if (-not $p.WaitForExit(10000)) { throw 'The old launcher could not be stopped. Exit it from its tray icon, then run repair again.' }
        }
    }
    $backup = $target + '.previous'
    if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination $backup -Force }
    try {
        Copy-Item -LiteralPath $candidate -Destination $target -Force
        if ((Get-FileHash -LiteralPath $candidate).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Installed executable verification failed.' }
    } catch {
        if (Test-Path -LiteralPath $backup) { Copy-Item -LiteralPath $backup -Destination $target -Force }
        throw
    }
    # Put an actual executable beside setup as well. It redirects to the installed copy.
    try { if (-not $Portable) { Copy-Item -LiteralPath $candidate -Destination $sourceExe -Force } } catch { Write-Warning ('Installed successfully, but could not place the extra EXE in the extracted folder: ' + $_.Exception.Message) }
    if (-not $Portable) { Copy-Item -LiteralPath (Join-Path $root 'Uninstall.ps1') -Destination (Join-Path $dataDir 'Uninstall.ps1') -Force }
    if (-not $Portable) { try {
        $shell = New-Object -ComObject WScript.Shell
        foreach ($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) {
            if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
            $link = $shell.CreateShortcut((Join-Path $folder 'Steam Monitor Launcher.lnk'))
            $link.TargetPath = $target
            $link.WorkingDirectory = $appDir
            $link.IconLocation = $target + ',0'
            $link.Description = 'Steam Monitor Launcher - settings and background monitor control'
            $link.Save()
        }
        $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamMonitorLauncher'
        New-Item -Path $uninstallKey -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'Steam Monitor Launcher' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '1.12.1' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $appDir -PropertyType String -Force | Out-Null
        $uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $dataDir 'Uninstall.ps1') + '"'
        New-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
    } catch { Write-Warning ('App installed, but shortcut/uninstall registration was incomplete: ' + $_.Exception.Message) } }
    Copy-Item -LiteralPath (Join-Path $root 'Update.ps1') -Destination (Join-Path $dataDir 'Update.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $root 'version.txt') -Destination (Join-Path $dataDir 'version.txt') -Force
    if ($Portable) { Set-Content -LiteralPath (Join-Path $dataDir 'portable.flag') -Value 'portable' -Encoding ASCII }
    Write-Host ('Installed: ' + $target)
    Write-Host 'Saved monitor preferences were retained.'
    Start-Process -FilePath $target
} catch {
    Write-Host ('ERROR: ' + $_.Exception.Message)
    if ($transcribing) { Stop-Transcript | Out-Null; $transcribing=$false }
    if (-not $ValidateOnly) { Start-Process notepad.exe -ArgumentList ('"' + $log + '"') }
    exit 1
} finally {
    if ($transcribing) { Stop-Transcript | Out-Null }
    if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
