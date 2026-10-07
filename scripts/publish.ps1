#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishDirectory = Join-Path $projectRoot 'dist'
Push-Location -LiteralPath $projectRoot
try {
    if (-not $SkipTests) { & (Join-Path $PSScriptRoot 'test.ps1') -Configuration $Configuration }

    # The user authorizes stopping the app when it occupies the fixed dist directory.
    $desktopRecoveryPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PaneSpace/desktop-recovery.json'
    $desktopWatcherId = 0
    if (Test-Path -LiteralPath $desktopRecoveryPath -PathType Leaf) {
        try { $desktopWatcherId = (Get-Content -LiteralPath $desktopRecoveryPath -Raw | ConvertFrom-Json).WatcherProcessId }
        catch { $desktopWatcherId = 0 }
    }
    Get-Process -Name CamCanvas,PaneSpace -ErrorAction SilentlyContinue |
        Where-Object { $_.Id -ne $desktopWatcherId } | Stop-Process -Force -ErrorAction Stop
    if ($desktopWatcherId -gt 0) {
        $desktopWatcher = Get-Process -Id $desktopWatcherId -ErrorAction SilentlyContinue
        if ($desktopWatcher -and $desktopWatcher.ProcessName -eq 'PaneSpace' -and -not $desktopWatcher.WaitForExit(10000)) {
            throw 'Desktop icon recovery is still running. Wait for it to finish before publishing.'
        }
    }
    & dotnet publish (Join-Path $projectRoot 'src/Desktop/PaneSpace.csproj') -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=false -o $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publish failed (exit $LASTEXITCODE)." }
    foreach ($legacyFile in @('CamCanvas.exe', 'CamCanvas.dll', 'CamCanvas.pdb', 'CamCanvas.deps.json',
        'CamCanvas.runtimeconfig.json', 'CamCanvas.Core.dll', 'CamCanvas.Core.pdb')) {
        $legacyPath = Join-Path $publishDirectory $legacyFile
        if (Test-Path -LiteralPath $legacyPath -PathType Leaf) { Remove-Item -LiteralPath $legacyPath -Force }
    }
    Write-Host "Published: $(Join-Path $publishDirectory 'PaneSpace.exe')"
}
finally {
    Pop-Location
}
