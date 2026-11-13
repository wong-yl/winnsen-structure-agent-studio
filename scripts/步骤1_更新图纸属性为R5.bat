@echo off
chcp 65001 >nul
title 步骤1 - 批量更新图纸属性为R5格式

echo.
echo ============================================================
echo   步骤1 - 批量更新图纸属性为R5格式
echo ============================================================
echo.
echo 【R5格式配置】
echo   签字栏：设计/绘图/核对/审核/批准 = Eli
echo   日期：2026-11-02
echo   版本：R5 / Eli 签审
echo   备注：按生产工艺出图 / 免样门验证
echo.
echo 【重要提示】
echo   1. 请先打开 SolidWorks（不要打开任何文档）
echo   2. 本脚本会修改 33 个工程图文件
echo   3. 原文件会自动备份
echo   4. 预计耗时：1-2 小时
echo.
echo ============================================================
echo.

pause

echo.
echo [启动] 批量更新程序...
echo.

python "D:\Winnsen_Structure_Agent_Studio\scripts\batch_update_drawing_properties.py"

echo.
echo.
echo ============================================================
echo   步骤1 完成！
echo.
echo   下一步：运行"步骤2_启动展开图批量导出.bat"
echo ============================================================
echo.
pause
