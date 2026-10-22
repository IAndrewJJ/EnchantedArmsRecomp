$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$launcher = Join-Path $projectDirectory 'EnchantedArmsLauncher.exe'
if (-not (Test-Path -LiteralPath $launcher)) { & (Join-Path $PSScriptRoot 'build-launcher.ps1') }
Start-Process -FilePath $launcher -ArgumentList '--play' -WorkingDirectory $projectDirectory -WindowStyle Hidden
