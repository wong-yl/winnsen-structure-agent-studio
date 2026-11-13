# PowerShell脚本：运行SolidWorks宏
# 作者：Claude
# 日期：2026-11-02

$ErrorActionPreference = "Stop"
$macroPath = "D:\Winnsen_Structure_Agent_Studio\scripts\BatchExportFlatPattern.swb"
$logFile = "D:\Winnsen_Structure_Agent_Studio\scripts\macro_run_log.txt"

# 开始记录
"========== 宏执行测试 ==========" | Out-File -FilePath $logFile -Encoding UTF8
"开始时间: $(Get-Date)" | Out-File -FilePath $logFile -Append -Encoding UTF8
"" | Out-File -FilePath $logFile -Append -Encoding UTF8

try {
    # 连接到SolidWorks
    Write-Host "正在连接SolidWorks..."
    "正在连接SolidWorks..." | Out-File -FilePath $logFile -Append -Encoding UTF8

    $swApp = [System.Runtime.InteropServices.Marshal]::GetActiveObject("SldWorks.Application")

    if ($null -eq $swApp) {
        throw "无法连接到SolidWorks实例"
    }

    Write-Host "✓ 成功连接到SolidWorks"
    "✓ 成功连接到SolidWorks" | Out-File -FilePath $logFile -Append -Encoding UTF8
    "SolidWorks版本: $($swApp.RevisionNumber())" | Out-File -FilePath $logFile -Append -Encoding UTF8
    "" | Out-File -FilePath $logFile -Append -Encoding UTF8

    # 检查宏文件
    if (-not (Test-Path $macroPath)) {
        throw "宏文件不存在: $macroPath"
    }

    Write-Host "✓ 宏文件存在: $macroPath"
    "✓ 宏文件存在: $macroPath" | Out-File -FilePath $logFile -Append -Encoding UTF8
    "" | Out-File -FilePath $logFile -Append -Encoding UTF8

    # 运行宏
    Write-Host "正在执行宏..."
    Write-Host "注意：宏可能会弹出对话框，请查看SolidWorks窗口"
    "正在执行宏..." | Out-File -FilePath $logFile -Append -Encoding UTF8
    "注意：宏可能会弹出对话框，请查看SolidWorks窗口" | Out-File -FilePath $logFile -Append -Encoding UTF8
    "" | Out-File -FilePath $logFile -Append -Encoding UTF8

    # RunMacro2(MacroPath, ModuleName, ProcedureName, Options, Errors)
    # Options: 1 = swRunMacroUnloadAfterRun
    $errors = 0
    $result = $swApp.RunMacro2($macroPath, "main", "", 1, [ref]$errors)

    if ($result) {
        Write-Host "✓ 宏执行成功"
        "✓ 宏执行成功" | Out-File -FilePath $logFile -Append -Encoding UTF8
    } else {
        Write-Host "⚠ 宏返回失败状态，错误代码: $errors"
        "⚠ 宏返回失败状态，错误代码: $errors" | Out-File -FilePath $logFile -Append -Encoding UTF8
    }

} catch {
    Write-Host "错误: $_"
    "错误: $_" | Out-File -FilePath $logFile -Append -Encoding UTF8
    exit 1
}

"" | Out-File -FilePath $logFile -Append -Encoding UTF8
"结束时间: $(Get-Date)" | Out-File -FilePath $logFile -Append -Encoding UTF8
"========== 完成 ==========" | Out-File -FilePath $logFile -Append -Encoding UTF8

Write-Host ""
Write-Host "宏执行完成，日志保存到: $logFile"
