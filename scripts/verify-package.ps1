#Requires -Version 7.0
[CmdletBinding()]
param([string]$Path = (Join-Path $PSScriptRoot '../dist/PaneSpace.exe'))

$ErrorActionPreference = 'Stop'
$executable = (Resolve-Path -LiteralPath $Path).Path
$reportPath = Join-Path ([System.IO.Path]::GetTempPath()) ('PaneSpace-package-' + [Guid]::NewGuid() + '.json')
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$process = $null
try {
    $launch = @{ FilePath = $executable; ArgumentList = @('--verify-package', ('"' + $reportPath + '"'));
        PassThru = $true; WindowStyle = 'Hidden' }
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host 'Accept the Windows administrator prompt to check the production executable.'
        $launch.Verb = 'RunAs'
    }
    $process = Start-Process @launch
    if (-not $process.WaitForExit(60000)) {
        throw 'Production executable did not finish its package check within 60 seconds.'
    }
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw 'Production executable did not produce a startup report.'
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $report.Success) { throw "Package check failed: $($report.Error)" }
    if (-not $report.SingleFile -or -not $report.Elevated) { throw 'Production executable must be bundled and elevated.' }
    Write-Host "PASS production EXE: $($report.Runtime), elevated, bundled Core/UI/icon/DWM and recovery companion."
}
finally {
    $identity.Dispose()
    if ($process) { $process.Dispose() }
    if (Test-Path -LiteralPath $reportPath -PathType Leaf) { Remove-Item -LiteralPath $reportPath }
}
