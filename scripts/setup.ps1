param(
    [Parameter(Mandatory=$true)][string]$Iso,
    [ValidateRange(1,8)][int]$Jobs = 2,
    [string]$BuildLocation,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
# Use this PowerShell's modules even when started from a launcher or PowerShell 7
# environment with a different inherited module search path.
$env:PSModulePath = (Join-Path $PSHOME 'Modules') + [IO.Path]::PathSeparator + $env:PSModulePath
$projectDirectory = Split-Path $PSScriptRoot -Parent
$localDirectory = Join-Path $projectDirectory '.local'
$pythonDirectory = Join-Path $localDirectory 'python'
$python = Join-Path $pythonDirectory 'python.exe'
if (-not (Test-Path -LiteralPath $Iso -PathType Leaf)) { throw 'The selected ISO was not found.' }
if (-not (Test-Path -LiteralPath $python)) {
    Write-Output 'STAGE: Preparing portable Python'
    $lock = Get-Content -LiteralPath (Join-Path $projectDirectory 'launcher/python.lock.json') -Raw | ConvertFrom-Json
    $downloadDirectory = Join-Path $localDirectory 'downloads'
    New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
    $archive = Join-Path $downloadDirectory $lock.name
    $bundled = Join-Path $projectDirectory ("bootstrap/" + $lock.name)
    if (Test-Path -LiteralPath $bundled) {
        if ((Get-FileHash -LiteralPath $bundled -Algorithm SHA256).Hash -ne $lock.sha256) { throw 'Bundled Python checksum mismatch. Re-download the complete launcher kit.' }
        if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $lock.sha256) {
            Copy-Item -LiteralPath $bundled -Destination $archive -Force
        }
    }
    if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $lock.sha256) {
        $partial = $archive + '.partial'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $client = New-Object Net.WebClient
        try { $client.DownloadFile($lock.url, $partial) } finally { $client.Dispose() }
        if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $lock.sha256) { throw 'Python download checksum mismatch.' }
        Move-Item -LiteralPath $partial -Destination $archive -Force
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Path $pythonDirectory -Force | Out-Null
    # Known, checksum-pinned Python archive; write the executable last so retries finish extraction.
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in ($zip.Entries | Sort-Object @{Expression={ $_.FullName -eq 'python.exe' }})) {
            $target = [IO.Path]::GetFullPath((Join-Path $pythonDirectory $entry.FullName))
            if (-not $target.StartsWith($pythonDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe Python archive path.' }
            if ($entry.FullName.EndsWith('/')) { New-Item -ItemType Directory -Path $target -Force | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $zip.Dispose() }
}
if ($BuildLocation) {
    $destination = [IO.Path]::GetFullPath($BuildLocation).TrimEnd('\', '/')
    if (-not [String]::Equals($destination, $projectDirectory, [StringComparison]::OrdinalIgnoreCase)) {
        & $python -u (Join-Path $projectDirectory 'tools/install_location.py') --source $projectDirectory --destination $destination
        if ($LASTEXITCODE -ne 0) { throw 'The build folder could not be prepared.' }
        $destinationArguments = @{ Iso = $Iso; Jobs = $Jobs }
        if ($ValidateOnly) { $destinationArguments.ValidateOnly = $true }
        & (Join-Path $destination 'scripts/setup.ps1') @destinationArguments
        return
    }
}
$arguments = @('-u', (Join-Path $projectDirectory 'tools/setup_game.py'), '--iso', $Iso, '--jobs', [string]$Jobs)
if ($ValidateOnly) { $arguments += '--validate-only' }
& $python @arguments
if ($LASTEXITCODE -ne 0) { throw "Setup failed with exit code $LASTEXITCODE. See the launcher log." }
