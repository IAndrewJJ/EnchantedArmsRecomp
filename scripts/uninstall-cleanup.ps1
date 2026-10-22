param(
    [Parameter(Mandatory=$true)][string]$TaskCleanupRoot,
    [Parameter(Mandatory=$true)][string]$TaskToken,
    [Parameter(Mandatory=$true)][int]$TaskWaitPid,
    [Parameter(Mandatory=$true)][long]$TaskStartTicks
)
$ErrorActionPreference = 'Stop'
if ($TaskToken -notmatch '^[a-f0-9]{32}$') { throw 'Invalid uninstall cleanup token.' }
$taskExpectedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('EnchantedArmsPC-uninstall-' + $TaskToken))).TrimEnd('\')
$taskResolvedRoot = (Get-Item -LiteralPath $TaskCleanupRoot -Force).FullName.TrimEnd('\')
if (-not [String]::Equals($taskResolvedRoot, $taskExpectedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target is not the owned temporary helper folder.' }
if ((Get-Item -LiteralPath $TaskCleanupRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cannot clean a linked helper folder.' }
$taskProcess = Get-Process -Id $TaskWaitPid -ErrorAction SilentlyContinue
if ($taskProcess) {
    if ($taskProcess.StartTime.ToUniversalTime().Ticks -ne $TaskStartTicks) { throw 'Cleanup process identity changed.' }
    $taskProcess.WaitForExit()
}
# Exact helper files only. Never recursively remove a temporary directory.
foreach ($taskName in @('EnchantedArmsUninstall.exe', 'request.json', 'cleanup.ps1')) {
    $taskFile = Join-Path $taskExpectedRoot $taskName
    if (Test-Path -LiteralPath $taskFile) { Remove-Item -LiteralPath $taskFile -Force }
}
Remove-Item -LiteralPath $taskExpectedRoot -Force
