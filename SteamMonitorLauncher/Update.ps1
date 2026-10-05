param([Parameter(Mandatory=$true)][string]$PackagePath,[string]$ExpectedVersion="")
$ErrorActionPreference = 'Stop'
$work = $null
$updateLock = $null
try {
    $portableMode = Test-Path -LiteralPath (Join-Path $PSScriptRoot 'portable.flag')
    $data = if ($portableMode) { $PSScriptRoot } else { Join-Path $env:LOCALAPPDATA 'SteamMonitorLauncher' }
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    try { $updateLock = [IO.File]::Open((Join-Path $data 'update.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw 'Another update is already running. Wait for it to finish.' }
    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) { throw 'Update ZIP was not found.' }
    if ((Get-Item -LiteralPath $PackagePath).Length -gt 50MB) { throw 'Update ZIP is larger than the 50 MB limit.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.Windows.Forms
    $work = Join-Path $data ('updates\' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
    try {
        if ($zip.Entries.Count -gt 300) { throw 'Too many files in this update.' }
        $total = 0L
        $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        $prefix = [IO.Path]::GetFullPath($work).TrimEnd('\') + '\'
        foreach ($entry in $zip.Entries) {
            $total += $entry.Length
            if ($total -gt 100MB) { throw 'Expanded update exceeds 100 MB.' }
            $relative = $entry.FullName.Replace('/', '\')
            if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':')) { throw 'Invalid path in update ZIP.' }
            $destination = [IO.Path]::GetFullPath((Join-Path $work $relative))
            if (-not $destination.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe path in update ZIP.' }
            if (-not $seen.Add($destination)) { throw 'Duplicate path in update ZIP.' }
            if ($relative.EndsWith('\')) { New-Item -ItemType Directory -Path $destination -Force | Out-Null; continue }
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$false)
        }
    } finally { $zip.Dispose() }
    $root = Join-Path $work 'SteamMonitorLauncher'
    foreach ($required in @('version.txt','Install.ps1','Update.ps1','Launcher.cs','Updater.cs','LibraryPlus.cs','Interface.cs','SteamLibrary.cs','OtherLibraries.cs','SteamUnlockedLibrary.cs','Watcher.cs','Native.cs','Features.cs','Displays.cs','Exclusions.cs','Diagnostics.cs','app.manifest','Launcher.ico','LauncherPaused.ico','Assets\Launcher.png','Uninstall.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $required) -PathType Leaf)) { throw "Incomplete update package: missing $required" }
    }
    $versionText = (Get-Content -LiteralPath (Join-Path $root 'version.txt') -Raw).Trim()
    if ($versionText -notmatch '^\d{1,4}\.\d{1,4}\.\d{1,4}$') { throw 'Invalid package version.' }
    if ($ExpectedVersion -and $versionText -ne $ExpectedVersion) { throw "Downloaded package version does not match the release feed." }
    $incoming = [version]$versionText
    $currentPath = Join-Path $data 'version.txt'
    $current = [version]'0.0.0'
    if (Test-Path -LiteralPath $currentPath) { $current = [version]((Get-Content -LiteralPath $currentPath -Raw).Trim()) }
    if ($incoming -lt $current) { throw "This package ($incoming) is older than the installed version ($current)." }
    $verb = if ($incoming -eq $current) { 'Repair' } else { 'Update to' }
    $answer = [Windows.Forms.MessageBox]::Show("$verb version $incoming ?`nInstalled: $current`n`nThis runs code from the selected ZIP. Continue only with a launcher package you trust.`nYour monitor settings, exclusions and custom games will be retained.",'Steam Monitor Updater','YesNo','Question')
    if ($answer -ne [Windows.Forms.DialogResult]::Yes) { exit 0 }
    $config = Join-Path $data 'settings.xml'
    if (Test-Path -LiteralPath $config) {
        $backups = Join-Path $data 'backups'
        New-Item -ItemType Directory -Path $backups -Force | Out-Null
        Copy-Item -LiteralPath $config -Destination (Join-Path $backups ('settings-before-update-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff') + '.xml'))
    }
    Write-Host "$verb $incoming - validating and building before replacing the installed app..."
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if ($portableMode) { & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Install.ps1') -Portable -PortableDirectory $data }
    else { & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Install.ps1') }
    if ($LASTEXITCODE -ne 0) { throw 'Update installation failed. Review the installation log. The installer checks the new build before replacing the existing executable.' }
    Write-Host 'Update completed. The launcher has restarted.'
} catch {
    $message = $_.Exception.Message
    Write-Host ('Update failed: ' + $message) -ForegroundColor Red
    try { Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.MessageBox]::Show($message,'Update failed','OK','Error') | Out-Null } catch {}
    exit 1
} finally {
    if ($work -and (Test-Path -LiteralPath $work)) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
    if ($updateLock) { $updateLock.Dispose() }
}
