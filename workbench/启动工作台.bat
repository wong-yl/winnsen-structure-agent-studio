@echo off
chcp 65001 >nul
echo ========================================
echo   Winnsen订单工作台 - 快速启动
echo ========================================
echo.

echo [1/3] 检查Python环境...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ✗ 未找到Python，请先安装Python 3.8+
    pause
    exit /b 1
)
echo ✓ Python已安装

echo.
echo [2/3] 检查Node.js环境...
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ✗ 未找到Node.js，请先安装Node.js 18+
    pause
    exit /b 1
)
echo ✓ Node.js已安装

echo.
echo [3/3] 启动服务...
echo.
echo ----------------------------------------
echo 后端API将运行在: http://localhost:8000
echo 前端界面将运行在: http://localhost:3000
echo ----------------------------------------
echo.

set BACKEND_DIR=%~dp0backend
set FRONTEND_DIR=%~dp0frontend

REM 安装后端依赖（如果需要）
if not exist "%BACKEND_DIR%\venv" (
    echo [后端] 首次运行，创建虚拟环境...
    cd /d "%BACKEND_DIR%"
    python -m venv venv
    call venv\Scripts\activate.bat
    pip install -r requirements.txt
) else (
    cd /d "%BACKEND_DIR%"
    call venv\Scripts\activate.bat
)

REM 启动后端（新窗口）
start "Winnsen后端API" cmd /k "cd /d %BACKEND_DIR% && venv\Scripts\activate.bat && python main.py"

REM 等待后端启动
timeout /t 3 /nobreak >nul

REM 安装前端依赖（如果需要）
if not exist "%FRONTEND_DIR%\node_modules" (
    echo [前端] 首次运行，安装依赖...
    cd /d "%FRONTEND_DIR%"
    call npm install
)

REM 启动前端（新窗口）
cd /d "%FRONTEND_DIR%"
start "Winnsen前端界面" cmd /k "npm run dev"

echo.
echo ========================================
echo   ✓ 服务启动成功！
echo ========================================
echo.
echo 打开浏览器访问: http://localhost:3000
echo API文档地址: http://localhost:8000/docs
echo.
echo 按任意键关闭此窗口（服务将继续运行）
pause >nul
