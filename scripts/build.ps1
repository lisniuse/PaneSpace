#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $projectRoot
try {
    & dotnet build (Join-Path $projectRoot 'PaneSpace.slnx') -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
}
finally {
    Pop-Location
}
