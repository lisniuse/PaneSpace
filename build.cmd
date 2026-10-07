@echo off
rem Compatibility entry point: validate and publish to the fixed dist directory.
pwsh -NoProfile -File "%~dp0scripts\publish.ps1" %*
exit /b %errorlevel%
