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
    Get-Process -Name CamCanvas,PaneSpace -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction Stop
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
