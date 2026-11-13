@echo off
chcp 65001 >nul
title 16029订单 - 批量导出钣金展开图

echo.
echo ============================================================
echo   16029订单 - 批量导出钣金展开图
echo ============================================================
echo.
echo 【重要提示】
echo   1. 请先打开 SolidWorks（不要打开任何文档）
echo   2. 本脚本需要 4-6 小时完成 33 个文件
echo   3. 可以随时按 Ctrl+C 中断，再次运行会继续
echo   4. 不要在运行期间操作 SolidWorks
echo.
echo ============================================================
echo.

pause

echo [检查] 安装依赖...
python -m pip install pywin32 -q 2>nul

echo.
echo [启动] 批量导出程序...
echo.

python "D:\Winnsen_Structure_Agent_Studio\scripts\batch_export_flatpattern.py"

echo.
echo.
echo ============================================================
echo   处理完成！
echo ============================================================
echo.
pause
