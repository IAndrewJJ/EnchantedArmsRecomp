param(
    [switch]$CheckOnly,
    [switch]$OriginalRenderer,
    [string]$SdkRoot,
    [ValidateSet('Release', 'RelWithDebInfo')][string]$Configuration = 'RelWithDebInfo',
    [ValidateRange(1, 32)][int]$Jobs = 4
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $SdkRoot) {
    $SdkRoot = Join-Path $projectRoot '.local/toolchain/rexglue-sdk-0.10.0/win-amd64'
    if (-not $OriginalRenderer -and -not $CheckOnly) {
        $python = Join-Path $projectRoot '.local/python/python.exe'
        if (-not (Test-Path -LiteralPath $python)) { throw 'Run scripts/setup.ps1 to prepare portable Python first.' }
        & $python (Join-Path $projectRoot 'tools/prepare_spatial_sdk.py')
        if ($LASTEXITCODE -ne 0) { throw 'Enhanced renderer preparation failed.' }
        $SdkRoot = Join-Path $projectRoot '.local/toolchain/rexglue-spatial-0.10.0/win-amd64'
    }
}
$spatialRenderer = Test-Path -LiteralPath (Join-Path $SdkRoot 'renderer-capabilities.json')
$portableEnvironment = Join-Path $PSScriptRoot 'environment.ps1'
if (Test-Path -LiteralPath (Join-Path $projectRoot '.local/toolchain/llvm-20.1.8/bin/clang++.exe')) {
    . $portableEnvironment
}
$toolStatus = [ordered]@{}
foreach ($toolName in @('cmake', 'ninja', 'clang++')) {
    $command = Get-Command $toolName -ErrorAction SilentlyContinue
    $toolStatus[$toolName] = if ($command) { $command.Source } else { $null }
}
$toolStatus['ReXGlue'] = Test-Path -LiteralPath (Join-Path $SdkRoot 'bin/rexglue.exe')
$toolStatus['MSVC_environment'] = [bool]$env:VCToolsInstallDir
$toolStatus['Windows_SDK_environment'] = [bool]$env:WindowsSdkDir
if ($CheckOnly) {
    $toolStatus | ConvertTo-Json
    if (@('cmake', 'ninja', 'clang++') | Where-Object { -not $toolStatus[$_] }) { exit 1 }
    if (-not $toolStatus['ReXGlue'] -or -not $toolStatus['MSVC_environment'] -or -not $toolStatus['Windows_SDK_environment']) { exit 1 }
    exit 0
}
$missing = @('cmake', 'ninja', 'clang++') | Where-Object { -not $toolStatus[$_] }
if ($missing) { throw "Build tools missing from PATH: $($missing -join ', '). See README.md." }
if (-not $toolStatus['MSVC_environment'] -or -not $toolStatus['Windows_SDK_environment']) {
    throw 'Run this script from Developer PowerShell with the MSVC and Windows SDK environment loaded.'
}
& (Join-Path $PSScriptRoot 'codegen.ps1') -SdkRoot $SdkRoot
$buildName = if ($spatialRenderer) { "$Configuration-spatial" } else { $Configuration }
$buildDirectory = Join-Path $projectRoot ".local/build/$buildName"
$resourceCompiler = if ($taskSdkBinDirectory) { (Join-Path $taskSdkBinDirectory 'rc.exe').Replace('\', '/') } else { 'rc' }
$manifestTool = if ($taskSdkBinDirectory) { (Join-Path $taskSdkBinDirectory 'mt.exe').Replace('\', '/') } else { 'mt' }
& cmake -S $projectRoot -B $buildDirectory -G Ninja `
    "-DCMAKE_BUILD_TYPE=$Configuration" '-DCMAKE_CXX_COMPILER=clang++' `
    "-DCMAKE_PREFIX_PATH=$SdkRoot" '-DCMAKE_EXE_LINKER_FLAGS=-fuse-ld=lld' `
    "-DEA_SPATIAL_FSR=$($spatialRenderer.ToString().ToUpperInvariant())" `
    "-DCMAKE_RC_COMPILER:FILEPATH=$resourceCompiler" "-DCMAKE_MT:FILEPATH=$manifestTool"
if ($LASTEXITCODE -ne 0) { throw "CMake configuration failed: $LASTEXITCODE" }
& cmake --build $buildDirectory --parallel $Jobs
if ($LASTEXITCODE -ne 0) { throw "Native build failed: $LASTEXITCODE" }
$stagingDirectory = Join-Path $projectRoot $(if ($spatialRenderer) { 'bin/graphics-0.3.1' } else { 'bin' })
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
foreach ($pattern in @('enchantedarmsrecomp.exe', 'enchantedarmsrecomp.pdb', '*.dll')) {
    Get-ChildItem -LiteralPath $buildDirectory -Filter $pattern -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stagingDirectory -Force
    }
}
if ($spatialRenderer) {
    # CMake's post-link copy does not run when only a runtime DLL changes.
    # Always stage the selected SDK's verified renderer rather than stale copies.
    foreach ($runtimeName in @('rexruntimerd.dll', 'rexgpu-xenosrd.dll', 'TracyClientrd.dll')) {
        Copy-Item -LiteralPath (Join-Path $SdkRoot "bin/$runtimeName") -Destination $stagingDirectory -Force
    }
    Copy-Item -LiteralPath (Join-Path $SdkRoot 'renderer-capabilities.json') -Destination $stagingDirectory -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'data/gamecontrollerdb.txt') -Destination $stagingDirectory -Force
Write-Output "Native build staged in $stagingDirectory"
