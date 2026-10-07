#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $projectRoot
try {
    if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration }
    & dotnet run --project (Join-Path $projectRoot 'tests/Core/PaneSpace.Core.Tests.csproj') -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Core regression tests failed (exit $LASTEXITCODE)." }
    & dotnet run --project (Join-Path $projectRoot 'tests/Desktop/PaneSpace.Desktop.Tests.csproj') -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Native desktop checks failed (exit $LASTEXITCODE)." }
}
finally {
    Pop-Location
}
