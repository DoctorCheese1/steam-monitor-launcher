param([switch]$Restore)
$ErrorActionPreference = 'Stop'
function Backup-LauncherBuild([string]$data,[string]$app) {
    if (-not (Test-Path -LiteralPath (Join-Path $app 'SteamMonitorLauncher.exe'))) { return }
    $pending = Join-Path $data ('recovery-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $pending | Out-Null
    try {
        Copy-Item -LiteralPath (Join-Path $app 'SteamMonitorLauncher.exe') -Destination (Join-Path $pending 'SteamMonitorLauncher.exe')
        foreach ($name in @('version.txt','Update.ps1','Uninstall.ps1')) {
            $file = Join-Path $data $name
            if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination (Join-Path $pending $name) }
        }
        $backup = Join-Path $data 'previous-build'
        $old = Join-Path $data 'previous-build-old'
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force }
        if (Test-Path -LiteralPath $backup) { Move-Item -LiteralPath $backup -Destination $old }
        Move-Item -LiteralPath $pending -Destination $backup
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
    } finally { if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending -Recurse -Force -ErrorAction SilentlyContinue } }
}
function Restore-LauncherBuild([string]$data,[string]$app,[bool]$noLaunch=$false) {
    $backup = Join-Path $data 'previous-build'
    if (-not (Test-Path -LiteralPath (Join-Path $backup 'SteamMonitorLauncher.exe'))) { throw 'No previous build is available yet. A backup is created when an existing installation is updated.' }
    $checkDir = Join-Path $data ('recovery-check-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $checkDir | Out-Null
    try {
        $exe = Join-Path $checkDir 'SteamMonitorLauncher.exe'
        Copy-Item -LiteralPath (Join-Path $backup 'SteamMonitorLauncher.exe') -Destination $exe
        Set-Content -LiteralPath (Join-Path $checkDir 'portable.flag') -Value portable
        $check = Start-Process -FilePath $exe -ArgumentList '--self-test' -PassThru
        if (-not $check.WaitForExit(30000)) { $check.Kill(); throw 'Previous build validation timed out.' }
        if ($check.ExitCode -ne 0) { throw 'Previous build validation failed; the current app was not replaced.' }
        $target = [IO.Path]::GetFullPath((Join-Path $app 'SteamMonitorLauncher.exe'))
        foreach ($process in @(Get-Process -Name SteamMonitorLauncher -ErrorAction SilentlyContinue)) {
            if ($process.Path -and $process.Path.Equals($target,[StringComparison]::OrdinalIgnoreCase)) { Stop-Process -Id $process.Id; if (-not $process.WaitForExit(10000)) { throw 'Close the running launcher before recovery.' } }
        }
        Copy-Item -LiteralPath $exe -Destination $target -Force
        foreach ($name in @('version.txt','Update.ps1','Uninstall.ps1')) { $file=Join-Path $backup $name; if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination (Join-Path $data $name) -Force } }
        if (Test-Path -LiteralPath (Join-Path $data 'version.txt')) {
            $key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamMonitorLauncher'
            if ((-not (Test-Path (Join-Path $data 'portable.flag'))) -and (Test-Path $key)) { Set-ItemProperty -Path $key -Name DisplayVersion -Value ((Get-Content (Join-Path $data 'version.txt') -Raw).Trim()) }
        }
        if (-not $noLaunch) { Start-Process -FilePath $target }
    } finally { Remove-Item -LiteralPath $checkDir -Recurse -Force -ErrorAction SilentlyContinue }
}
if ($Restore) {
    $lock=$null
    try {
        $data=$PSScriptRoot
        $app=if(Test-Path (Join-Path $data 'portable.flag')){$data}else{Join-Path $data 'bin'}
        $lock=[IO.File]::Open((Join-Path $data 'update.lock'),'OpenOrCreate','ReadWrite','None')
        Add-Type -AssemblyName System.Windows.Forms
        if ([Windows.Forms.MessageBox]::Show('Restore the previous app version? Current settings and game files will be kept.','App recovery','YesNo','Question') -eq 'Yes') { Restore-LauncherBuild $data $app }
    } catch { Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.MessageBox]::Show($_.Exception.Message,'Recovery failed') | Out-Null; exit 1 }
    finally { if($lock){$lock.Dispose()} }
}
