@echo off
chcp 65001 >nul
echo ========================================
echo   启动前端开发服务器
echo ========================================
cd /d "%~dp0"
npm run dev
pause
