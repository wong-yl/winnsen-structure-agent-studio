$ErrorActionPreference = 'Stop'
$modelPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\16029_1000W_1917H_550D_10door_freecad_reference.FCStd'
$freecadExe = 'D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCAD.exe'
$statusPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\open_10door_reference_in_freecad.status.txt'

if (-not (Test-Path -LiteralPath $modelPath)) {
  throw "Model file was not found: $modelPath"
}
if (-not (Test-Path -LiteralPath $freecadExe)) {
  throw "FreeCAD.exe was not found: $freecadExe"
}

Start-Process -FilePath $freecadExe -ArgumentList @($modelPath)
$line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | launched | Started FreeCAD with file=$modelPath"
Set-Content -LiteralPath $statusPath -Value $line -Encoding UTF8
