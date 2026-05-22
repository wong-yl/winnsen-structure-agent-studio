$ErrorActionPreference = 'Stop'
$assemblyPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\16029_1000W_1917H_550D_10door_enriched_v2.SLDASM'
$solidWorksExe = 'D:\软件安装录\soildworks\SOLIDWORKS\SLDWORKS.exe'
$solidWorksShortcut = 'C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk'
$solidWorksOpenScript = 'D:\机械结构工程师智能体\scripts\sw_open_and_activate.js'
$dependencyManifestPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\native_dependency_manifest.csv'
$missingDependencyPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\open_10door_native_enriched_solidworks.missing_dependencies.txt'
$statusPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\open_10door_native_enriched_solidworks.status.txt'
$stdoutPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\open_10door_native_enriched_solidworks.solidworks_open.stdout.txt'
$stderrPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\open_10door_native_enriched_solidworks.solidworks_open.stderr.txt'

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

if (-not (Test-Path -LiteralPath $assemblyPath)) {
  throw "Native SolidWorks assembly was not found: $assemblyPath"
}

if ($dependencyManifestPath -and (Test-Path -LiteralPath $dependencyManifestPath)) {
  $manifestRows = Import-Csv -LiteralPath $dependencyManifestPath
  $missingRows = @($manifestRows | Where-Object {
    -not $_.source_path -or
    $_.exists -ne 'yes' -or
    -not (Test-Path -LiteralPath $_.source_path)
  })
  if ($missingRows.Count -gt 0) {
    $lines = @(
      "Missing SolidWorks native dependency references before opening:",
      "Assembly: $assemblyPath",
      "Manifest: $dependencyManifestPath",
      ""
    )
    foreach ($row in $missingRows) {
      $lines += ("0 | 1" -f $row.role, $row.source_path)
    }
    Set-Content -LiteralPath $missingDependencyPath -Value $lines -Encoding UTF8
    Write-OpenStatus "missing_dependency" "Missing $($missingRows.Count) native dependency reference(s). See: $missingDependencyPath"
    Start-Process -FilePath "notepad.exe" -ArgumentList @($missingDependencyPath)
    Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
    exit 1
  }
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
  $proc = Start-Process -FilePath "cscript.exe" -ArgumentList @("//Nologo", $solidWorksOpenScript, $assemblyPath) -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
  $deadline = (Get-Date).AddSeconds(240)
  while ((Get-Date) -lt $deadline -and -not $proc.HasExited) {
    Start-Sleep -Seconds 5
    $proc.Refresh()
  }
  if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-OpenStatus "manual_open_required" "SolidWorks API open timed out; selected native assembly in Explorer: $assemblyPath"
    Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
    exit 0
  }
  $output = ""
  if (Test-Path -LiteralPath $stdoutPath) {
    $output = Get-Content -LiteralPath $stdoutPath -Raw -Encoding Default
  }
  if ($output -match "active=") {
    Write-OpenStatus "opened" "SolidWorks API reported active document for native assembly: $assemblyPath"
    exit 0
  }
  Write-OpenStatus "manual_open_required" "SolidWorks API did not confirm open; selected native assembly in Explorer: $assemblyPath"
  Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
  exit 0
}

Start-Process -FilePath "explorer.exe" -ArgumentList "/select,`"$assemblyPath`""
Write-OpenStatus "manual_open_required" "Selected native assembly in Explorer: $assemblyPath"
