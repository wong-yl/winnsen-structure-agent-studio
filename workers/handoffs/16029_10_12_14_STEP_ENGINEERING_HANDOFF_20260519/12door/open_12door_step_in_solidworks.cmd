@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1" *> "%~dpn0.log"
if errorlevel 1 start "" notepad.exe "%~dpn0.log"
