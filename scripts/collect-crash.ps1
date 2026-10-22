param(
    [datetime]$Since = (Get-Date).AddHours(-2),
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$executables = @(
    (Join-Path $projectDirectory 'bin/enchantedarmsrecomp.exe'),
    (Join-Path $projectDirectory 'bin/graphics-0.2.0/enchantedarmsrecomp.exe')
)
$symbolizer = Join-Path $projectDirectory '.local/toolchain/llvm-20.1.8/bin/llvm-symbolizer.exe'
$event = Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000; StartTime=$Since} -ErrorAction SilentlyContinue |
    Where-Object {
        [xml]$candidateXml = $_.ToXml()
        $application = $candidateXml.Event.EventData.Data | Where-Object { $_.Name -eq 'AppPath' }
        $executables -icontains $application.'#text'
    } | Select-Object -First 1
if (-not $event) { throw 'No Windows crash event found for the staged executable in this time range.' }
[xml]$eventXml = $event.ToXml()
$fields = @{}
foreach ($field in $eventXml.Event.EventData.Data) { $fields[$field.Name] = [string]$field.'#text' }
$executable = $fields['AppPath']
$binary = [IO.File]::ReadAllBytes($executable)
$peOffset = [BitConverter]::ToInt32($binary, 0x3C)
$buildStamp = [BitConverter]::ToUInt32($binary, $peOffset + 8)
$eventStamp = [Convert]::ToUInt32($fields['AppTimeStamp'], 16)
$sameBuild = $buildStamp -eq $eventStamp
$report = [ordered]@{
    event_time_utc = $event.TimeCreated.ToUniversalTime().ToString('o')
    executable = $executable
    executable_sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
    exception_code = '0x' + $fields['ExceptionCode']
    fault_module = $fields['ModuleName']
    fault_offset = '0x' + $fields['FaultingOffset']
    event_build_stamp = $fields['AppTimeStamp']
    current_build_stamp = $buildStamp.ToString('x8')
    current_build_matches = $sameBuild
    symbolization = @()
    note = ''
}
if (-not $sameBuild) {
    $report.note = 'The executable has changed since this crash; address symbolization was skipped.'
} elseif ($fields['ModulePath'] -ieq $executable -and (Test-Path -LiteralPath $symbolizer)) {
    $report.symbolization = @(& $symbolizer "--obj=$executable" --relative-address --inlines --verbose $report.fault_offset)
    if ($LASTEXITCODE -ne 0) { throw 'LLVM symbolization failed.' }
} else {
    $report.note = 'The fault is outside this executable, or the local symbolizer is unavailable.'
}
if (-not $OutputPath) {
    $reportDirectory = Join-Path $projectDirectory '.local/logs'
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $OutputPath = Join-Path $reportDirectory ('crash-' + $event.TimeCreated.ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '.json')
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Crash report saved: $OutputPath"
Write-Output "Exception $($report.exception_code) at $($report.fault_module)+$($report.fault_offset)"
if ($report.symbolization) { $report.symbolization } else { $report.note }
