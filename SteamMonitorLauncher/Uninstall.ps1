$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$base = Join-Path $env:LOCALAPPDATA 'SteamMonitorLauncher'
$bin = Join-Path $base 'bin'
$choice = [Windows.Forms.MessageBox]::Show('Remove Steam Monitor Launcher, its Windows startup entry and shortcuts? Games will remain running. Settings and logs will be kept unless you choose to remove them next.','Uninstall Steam Monitor Launcher','YesNo','Question','Button2')
if ($choice -ne 'Yes') { exit }
$deleteSettings = [Windows.Forms.MessageBox]::Show('Also remove saved monitor settings, custom-game entries and exclusions? Settings backup exports will not be removed.','Remove preferences?','YesNo','Question','Button2') -eq 'Yes'
try {
    $prefix = [IO.Path]::GetFullPath($bin).TrimEnd('\') + '\'
    foreach ($process in @(Get-Process -Name 'SteamMonitorLauncher*' -ErrorAction SilentlyContinue)) {
        if ($process.Path -and $process.Path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $process.Id -ErrorAction Stop
            [void]$process.WaitForExit(10000)
        }
    }
    Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'SteamMonitorLauncher' -ErrorAction SilentlyContinue
    foreach ($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) {
        $shortcut = Join-Path $folder 'Steam Monitor Launcher.lnk'
        if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
    }
    if (Test-Path -LiteralPath $bin) {
        foreach ($file in @(Get-ChildItem -LiteralPath $bin -File)) {
            if ($file.Name -match '^SteamMonitorLauncher(?:-\d+\.\d+)?\.exe(?:\.previous)?$') { Remove-Item -LiteralPath $file.FullName -Force }
        }
    }
    foreach ($name in @('Update.ps1','version.txt')) {
        $file = Join-Path $base $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    if ($deleteSettings) {
        foreach ($name in @('settings.xml','settings.xml.tmp')) {
            $file = Join-Path $base $name
            if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
        }
    }
    Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamMonitorLauncher' -Force -ErrorAction SilentlyContinue
    [Windows.Forms.MessageBox]::Show('Launcher removed. Logs and any exported/backed-up settings were retained. Delete the extracted setup folder if you no longer need it.','Uninstall complete') | Out-Null
} catch {
    [Windows.Forms.MessageBox]::Show(('Removal could not finish: ' + $_.Exception.Message),'Uninstall error') | Out-Null
    exit 1
}
