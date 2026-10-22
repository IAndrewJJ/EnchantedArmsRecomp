# Dot-source to use project-local Windows build tools in this process.
$taskProjectDirectory = Split-Path $PSScriptRoot -Parent
$taskToolchainDirectory = Join-Path $taskProjectDirectory '.local/toolchain'
$taskMsvcDirectory = Join-Path $taskToolchainDirectory 'msvc/VC/Tools/MSVC/14.44.35207'
$taskWindowsSdkDirectory = Join-Path $taskToolchainDirectory 'windows-sdk/microsoft.windows.sdk.cpp/c'
$taskWindowsSdkLibraries = Join-Path $taskToolchainDirectory 'windows-sdk/microsoft.windows.sdk.cpp.x64/c'
$taskSdkVersion = '10.0.26100.0'
$taskLlvmDirectory = Join-Path $taskToolchainDirectory 'llvm-20.1.8/bin'
$taskCmakeDirectory = Join-Path $taskToolchainDirectory 'cmake/cmake-4.4.4-windows-x86_64/bin'
$taskNinjaDirectory = Join-Path $taskToolchainDirectory 'ninja'
$taskSdkBinDirectory = Join-Path $taskWindowsSdkDirectory "bin/$taskSdkVersion/x64"
if (-not (Test-Path -LiteralPath (Join-Path $taskLlvmDirectory 'clang++.exe'))) {
    throw 'Workspace-local LLVM is not available.'
}
$env:PATH = (@($taskLlvmDirectory, $taskCmakeDirectory, $taskNinjaDirectory, $taskSdkBinDirectory) -join ';') + ';' + $env:PATH
$env:INCLUDE = (@(
    (Join-Path $taskMsvcDirectory 'include'),
    (Join-Path $taskWindowsSdkDirectory "Include/$taskSdkVersion/ucrt"),
    (Join-Path $taskWindowsSdkDirectory "Include/$taskSdkVersion/shared"),
    (Join-Path $taskWindowsSdkDirectory "Include/$taskSdkVersion/um"),
    (Join-Path $taskWindowsSdkDirectory "Include/$taskSdkVersion/winrt"),
    (Join-Path $taskWindowsSdkDirectory "Include/$taskSdkVersion/cppwinrt")
) -join ';')
$env:LIB = (@(
    (Join-Path $taskMsvcDirectory 'lib/x64'),
    (Join-Path $taskWindowsSdkLibraries 'um/x64'),
    (Join-Path $taskWindowsSdkLibraries 'ucrt/x64')
) -join ';')
$env:VCToolsInstallDir = $taskMsvcDirectory + '/'
$env:WindowsSdkDir = $taskWindowsSdkDirectory + '/'
$env:WindowsSDKVersion = $taskSdkVersion + '/'
