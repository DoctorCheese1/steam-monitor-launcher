param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$PackageUrl,
    [string]$Notes='Launcher update',
    [string]$OutputPath=(Join-Path $PSScriptRoot 'latest.json')
)
$ErrorActionPreference = 'Stop'
$uri = [uri]$PackageUrl
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo) { throw 'Use a public HTTPS ZIP download URL.' }
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) { throw 'Package not found.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
try {
    $entry = @($zip.Entries | Where-Object { $_.FullName.Replace('\','/') -eq 'SteamMonitorLauncher/version.txt' }) | Select-Object -First 1
    if (-not $entry) { throw 'Package version.txt is missing.' }
    if ($entry.Length -gt 128) { throw 'Package version is invalid.' }
    $reader = New-Object IO.StreamReader($entry.Open())
    try { $version = $reader.ReadToEnd().Trim() } finally { $reader.Dispose() }
    if ($version -notmatch '^\d{1,4}\.\d{1,4}\.\d{1,4}$') { throw 'Package version is invalid.' }
} finally { $zip.Dispose() }
$feed = [ordered]@{
    Version = $version
    PackageUrl = $uri.AbsoluteUri
    Sha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Notes = $Notes
}
$feed | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host ('Created: ' + $OutputPath)
Write-Host 'Upload the ZIP first, then upload latest.json to a stable public HTTPS address.'
Write-Host 'This helper creates the feed locally; it does not upload or publish files.'
