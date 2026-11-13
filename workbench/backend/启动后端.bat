@echo off
chcp 65001 >nul
echo ========================================
echo   启动后端API服务
echo ========================================
cd /d "%~dp0"
call venv\Scripts\activate.bat
python main.py
pause
