$ErrorActionPreference = 'Stop'
$stepPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\16029_1000W_1917H_550D_14door_solidworks_import.stp'
$solidWorksExe = 'D:\软件安装录\soildworks\SOLIDWORKS\SLDWORKS.exe'
$solidWorksShortcut = 'C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk'
$solidWorksOpenScript = 'D:\机械结构工程师智能体\scripts\sw_open_and_activate.js'
$statusPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\open_14door_step_in_solidworks.status.txt'
$stdoutPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\open_14door_step_in_solidworks.solidworks_open.stdout.txt'
$stderrPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\open_14door_step_in_solidworks.solidworks_open.stderr.txt'

function Write-OpenStatus([string] $status, [string] $message) {
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $status | $message"
  Set-Content -LiteralPath $statusPath -Value $line -Encoding UTF8
}

function Wait-SolidWorksMainWindow([int] $timeoutSeconds) {
  $deadline = (Get-Date).AddSeconds($timeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    $proc = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle } | Select-Object -First 1
    if ($null -ne $proc) {
      return $true
    }
    Start-Sleep -Seconds 3
  }
  return $false
}

if (-not (Test-Path -LiteralPath $stepPath)) {
  throw "STEP file was not found: $stepPath"
}

$runningSolidWorks = Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle } | Select-Object -First 1
if ($null -eq $runningSolidWorks) {
  if (Test-Path -LiteralPath $solidWorksExe) {
    Start-Process -FilePath $solidWorksExe -WorkingDirectory (Split-Path -LiteralPath $solidWorksExe)
    [void] (Wait-SolidWorksMainWindow 120)
  } elseif (Test-Path -LiteralPath $solidWorksShortcut) {
    Start-Process -FilePath $solidWorksShortcut
    [void] (Wait-SolidWorksMainWindow 120)
  }
}

if (Test-Path -LiteralPath $solidWorksOpenScript) {
  Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
  $proc = Start-Process -FilePath "cscript.exe" -ArgumentList @("//Nologo", $solidWorksOpenScript, $stepPath) -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
  $deadline = (Get-Date).AddSeconds(300)
  while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
    Start-Sleep -Seconds 5
    $proc.Refresh()
  }
  if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-OpenStatus "manual_open_required" "SolidWorks API open timed out; selected STEP in Explorer: $stepPath"
    Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
    exit 0
  }
  $output = ""
  if (Test-Path -LiteralPath $stdoutPath) {
    $output = Get-Content -LiteralPath $stdoutPath -Raw -Encoding Default
  }
  if ($proc.ExitCode -eq 0 -and $output -match "active=") {
    Write-OpenStatus "opened" "SolidWorks API reported active document for STEP: $stepPath"
    exit 0
  }
  Write-OpenStatus "manual_open_required" "SolidWorks API did not confirm open; selected STEP in Explorer: $stepPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
  exit 0
}

if (Test-Path -LiteralPath $solidWorksExe) {
  Start-Process -FilePath $solidWorksExe -WorkingDirectory (Split-Path -LiteralPath $solidWorksExe)
  Write-OpenStatus "manual_open_required" "Started SolidWorks main window only; select STEP manually: $stepPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
  exit 0
}

if (Test-Path -LiteralPath $solidWorksShortcut) {
  Start-Process -FilePath $solidWorksShortcut
  Write-OpenStatus "manual_open_required" "Started SolidWorks shortcut only; select STEP manually: $stepPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
  exit 0
}

Write-OpenStatus "manual_open_required" "SolidWorks executable not found; selected STEP in Explorer: $stepPath"
Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$stepPath`""
