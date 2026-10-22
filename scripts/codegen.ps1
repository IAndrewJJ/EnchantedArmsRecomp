param([string]$SdkRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $SdkRoot) {
    $SdkRoot = Join-Path $projectRoot '.local/toolchain/rexglue-sdk-0.10.0/win-amd64'
}
$recompiler = Join-Path $SdkRoot 'bin/rexglue.exe'
$gameExecutable = Join-Path $projectRoot '.local/game/default.xex'
if (-not (Test-Path -LiteralPath $recompiler)) { throw "ReXGlue executable missing: $recompiler" }
if (-not (Test-Path -LiteralPath $gameExecutable)) { throw "Game executable missing: $gameExecutable" }
$expectedHash = '79E95EA830119123C2FD9C7B08FB890C4CF40CC6B8422B3568A820B6B3779F22'
if ((Get-FileHash -LiteralPath $gameExecutable -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'This executable does not match the game revision used for the function hints.'
}
Push-Location $projectRoot
try {
    & $recompiler codegen 'enchantedarmsrecomp_manifest.toml'
    if ($LASTEXITCODE -ne 0) { throw "ReXGlue code generation failed: $LASTEXITCODE" }
} finally {
    Pop-Location
}
