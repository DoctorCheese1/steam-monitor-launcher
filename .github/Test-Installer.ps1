$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '..\SteamMonitorLauncher'
$test=Join-Path $env:RUNNER_TEMP ('launcher-install-'+[guid]::NewGuid().ToString('N'))
$package=Join-Path $test 'package'
$data=Join-Path $test 'portable'
New-Item -ItemType Directory -Path $package -Force | Out-Null
try {
    Copy-Item -Path (Join-Path $root '*') -Destination $package -Recurse
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\validation-output\SteamMonitorLauncher.exe') -Destination (Join-Path $package 'SteamMonitorLauncher.exe')
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'Install.ps1') -Portable -PortableDirectory $data -NoLaunch
    if($LASTEXITCODE -ne 0){throw 'Prebuilt installation failed'}
    $target=Join-Path $data 'SteamMonitorLauncher.exe'
    $hash=(Get-FileHash $target).Hash
    $settings=Join-Path $data 'settings.xml'
    Set-Content -LiteralPath $settings -Value '<Settings><Monitor>retained-monitor</Monitor></Settings>'
    $settingsHash=(Get-FileHash $settings).Hash
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'Install.ps1') -Portable -PortableDirectory $data -NoLaunch
    if($LASTEXITCODE -ne 0){throw 'Same-version prebuilt repair failed'}
    if((Get-FileHash (Join-Path $data 'previous-build\SteamMonitorLauncher.exe')).Hash -ne $hash){throw 'Previous build backup is incorrect'}
    . (Join-Path $package 'Recovery.ps1')
    Set-Content -LiteralPath $target -Value 'simulated broken installation'
    Restore-LauncherBuild $data $data $true
    if((Get-FileHash $target).Hash -ne $hash){throw 'Build recovery did not restore the previous executable'}
    if((Get-FileHash $settings).Hash -ne $settingsHash){throw 'Settings changed during repair/recovery'}
    Write-Host 'PASS: prebuilt installation, same-version repair, previous-build recovery, settings retained.'
} finally { Remove-Item -LiteralPath $test -Recurse -Force -ErrorAction SilentlyContinue }
