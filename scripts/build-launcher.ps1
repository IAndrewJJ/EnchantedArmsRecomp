param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
if (-not $OutputPath) { $OutputPath = Join-Path $projectDirectory 'EnchantedArmsLauncher.exe' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler was not found.' }
$release = Get-Content -LiteralPath (Join-Path $projectDirectory 'release.json') -Raw | ConvertFrom-Json
foreach ($value in @($release.version, $release.native_version)) {
    if ($value -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version in release.json.' }
}
if ($release.channel -notin @('preview', 'stable')) { throw 'Invalid release channel.' }
$buildDirectory = Join-Path $projectDirectory '.local/launcher-build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$versionSource = Join-Path $buildDirectory 'ReleaseInfo.g.cs'
$versionCode = @"
using System.Reflection;
[assembly: AssemblyTitle("EnchantedArmsPC")]
[assembly: AssemblyProduct("EnchantedArmsPC")]
[assembly: AssemblyDescription("Unofficial Enchanted Arms PC launcher")]
[assembly: AssemblyVersion("$($release.version).0")]
[assembly: AssemblyFileVersion("$($release.version).0")]
namespace EnchantedArmsPC {
    static class ReleaseInfo {
        internal const string Version = "$($release.version)";
        internal const string NativeVersion = "$($release.native_version)";
        internal const string Channel = "$($release.channel)";
    }
}
"@
[IO.File]::WriteAllText($versionSource, $versionCode, [Text.UTF8Encoding]::new($false))
[xml]$manifest = Get-Content -LiteralPath (Join-Path $projectDirectory 'launcher/app.manifest') -Raw
$manifest.assembly.assemblyIdentity.version = "$($release.version).0"
$manifestPath = Join-Path $buildDirectory 'app.manifest'
$manifest.Save($manifestPath)
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    ('/win32manifest:' + $manifestPath) `
    ('/win32icon:' + (Join-Path $projectDirectory 'launcher/branding/EnchantedArmsPC.ico')) `
    ('/out:' + $OutputPath) `
    $versionSource `
    (Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'launcher') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
