$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler was not found.' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    ('/win32manifest:' + (Join-Path $projectDirectory 'launcher/app.manifest')) `
    ('/out:' + (Join-Path $projectDirectory 'EnchantedArmsLauncher.exe')) `
    (Join-Path $projectDirectory 'launcher/Launcher.cs') `
    (Join-Path $projectDirectory 'launcher/Graphics.cs')
if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
