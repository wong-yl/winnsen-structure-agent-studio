@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1"
set EXITCODE=%ERRORLEVEL%
start "" notepad.exe "%~dpn0.status.txt"
exit /b %EXITCODE%
