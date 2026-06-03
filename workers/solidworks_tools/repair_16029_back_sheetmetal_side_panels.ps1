param(
  [Parameter(Mandatory = $true)][string]$PackDir,
  [Parameter(Mandatory = $true)][string]$AssemblyPath,
  [Parameter(Mandatory = $true)][string]$OutJson
)

$ErrorActionPreference = 'Stop'

function Read-Json([string]$Path) {
  Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Assert-File([string]$Path, [string]$Label) {
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "$Label missing: $Path"
  }
}

function Invoke-Checked([string]$Exe, [string[]]$ArgumentList, [string]$Label) {
  & $Exe @ArgumentList | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "$Label failed with exit code $LASTEXITCODE"
  }
}

function Find-SidePart([string]$SideLabel) {
  $pattern = if ($SideLabel -eq 'left') { '*左侧板焊接_body*.SLDPRT' } else { '*右侧板焊接_body*.SLDPRT' }
  $parts = @(Get-ChildItem -LiteralPath $PackDir -File -Filter '*.SLDPRT' | Where-Object { $_.Name -like $pattern })
  if ($parts.Count -eq 0) {
    $bodyIdPattern = if ($SideLabel -eq 'left') { '*body084.SLDPRT' } else { '*body031.SLDPRT' }
    $parts = @(Get-ChildItem -LiteralPath $PackDir -File -Filter '*.SLDPRT' | Where-Object { $_.Name -like $bodyIdPattern })
  }
  if ($parts.Count -ne 1) {
    throw "Expected exactly one $SideLabel side panel part in $PackDir, found $($parts.Count)"
  }
  return $parts[0].FullName
}

function Repair-Side([string]$SideLabel, [string]$PartPath, [double]$KeepBoundaryMm, [string]$WorkDir) {
  $exportScript = Join-Path $PSScriptRoot 'sw_export_model_step.js'
  $importTool = Join-Path $PSScriptRoot 'bin\ImportStepSaveNative.exe'
  $probeTool = Join-Path $PSScriptRoot 'bin\ProbePartBodies.exe'
  $trimScript = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'tools\trim_16029_side_panel_step_freecad.py'
  Assert-File $exportScript 'SolidWorks STEP exporter'
  Assert-File $importTool 'SolidWorks STEP native importer'
  Assert-File $probeTool 'SolidWorks part body probe'
  Assert-File $trimScript 'FreeCAD STEP trim script'

  $sideDir = Join-Path $WorkDir $SideLabel
  New-Item -ItemType Directory -Force -Path $sideDir | Out-Null
  $beforeProbe = Join-Path $sideDir 'before_probe.json'
  $sourceStep = Join-Path $sideDir 'source.step'
  $exportJson = Join-Path $sideDir 'export_step.json'
  $trimmedStep = Join-Path $sideDir 'trimmed.step'
  $trimJson = Join-Path $sideDir 'freecad_trim.json'
  $roundtripStep = Join-Path $sideDir 'roundtrip.step'
  $importJson = Join-Path $sideDir 'import_native.json'
  $afterProbe = Join-Path $sideDir 'after_probe.json'

  Invoke-Checked -Exe $probeTool -ArgumentList @($PartPath, $beforeProbe) -Label "$SideLabel side before probe"
  Invoke-Checked -Exe 'cscript.exe' -ArgumentList @('//Nologo', $exportScript, $PartPath, $sourceStep, $exportJson) -Label "$SideLabel side STEP export"
  $exportResult = Read-Json $exportJson
  if (-not $exportResult.step_saved -or -not (Test-Path -LiteralPath $sourceStep -PathType Leaf)) {
    throw "$SideLabel side STEP export did not produce source.step"
  }

  $softwareInstallDirName = -join ([char[]](0x8f6f, 0x4ef6, 0x5b89, 0x88c5, 0x5f55))
  $freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
  Assert-File $freecad 'FreeCADCmd'
  $env:SOURCE_STEP = $sourceStep
  $env:OUTPUT_STEP = $trimmedStep
  $env:SIDE = $SideLabel
  $env:KEEP_BOUNDARY_MM = [string]$KeepBoundaryMm
  $env:REPORT_JSON = $trimJson
  try {
    $code = "import runpy; runpy.run_path(r'$($trimScript.Replace('\', '\\').Replace("'", "\'"))', run_name='__main__')"
    Invoke-Checked -Exe $freecad -ArgumentList @('-c', $code) -Label "$SideLabel side STEP trim"
  }
  finally {
    Remove-Item Env:\SOURCE_STEP, Env:\OUTPUT_STEP, Env:\SIDE, Env:\KEEP_BOUNDARY_MM, Env:\REPORT_JSON -ErrorAction SilentlyContinue
  }
  Assert-File $trimmedStep "$SideLabel trimmed STEP"

  $backupPath = "$PartPath.pretrim.SLDPRT"
  Copy-Item -LiteralPath $PartPath -Destination $backupPath -Force
  Remove-Item -LiteralPath $PartPath -Force
  Invoke-Checked -Exe $importTool -ArgumentList @($trimmedStep, $PartPath, $roundtripStep, $importJson) -Label "$SideLabel side STEP import to native"
  Assert-File $PartPath "$SideLabel repaired native side panel"
  Invoke-Checked -Exe $probeTool -ArgumentList @($PartPath, $afterProbe) -Label "$SideLabel side after probe"

  $before = Read-Json $beforeProbe
  $after = Read-Json $afterProbe
  return [ordered]@{
    side = $SideLabel
    partPath = $PartPath
    backupPath = $backupPath
    keepBoundaryMm = $KeepBoundaryMm
    sourceStep = $sourceStep
    trimmedStep = $trimmedStep
    roundtripStep = $roundtripStep
    beforeProbe = $beforeProbe
    afterProbe = $afterProbe
    beforeBox = @($before.bodies)[0]
    afterBox = @($after.bodies)[0]
  }
}

Assert-File $AssemblyPath 'Back sheet-metal repair assembly'
if (-not (Test-Path -LiteralPath $PackDir -PathType Container)) {
  throw "PackDir missing: $PackDir"
}

$workDir = Split-Path -Parent $OutJson
New-Item -ItemType Directory -Force -Path $workDir | Out-Null
$leftPart = Find-SidePart 'left'
$rightPart = Find-SidePart 'right'
$left = Repair-Side 'left' $leftPart 14.7 $workDir
$right = Repair-Side 'right' $rightPart 0.5 $workDir

$leftAfter = $left.afterBox
$rightAfter = $right.afterBox
$leftOk = ([Math]::Abs([double]$leftAfter.x_min_mm + 370.0) -le 2.0) -and ([double]$leftAfter.x_max_mm -ge 0.0) -and ([double]$leftAfter.x_max_mm -le 20.0)
$rightOk = ([Math]::Abs([double]$rightAfter.x_max_mm - 370.0) -le 2.0) -and ([Math]::Abs([double]$rightAfter.x_min_mm - 0.5) -le 2.0)
$status = if ($leftOk -and $rightOk) { 'side_panel_sheetmetal_back_flange_centered' } else { 'side_panel_sheetmetal_back_flange_not_centered' }

$result = [ordered]@{
  assemblyPath = $AssemblyPath
  packDir = $PackDir
  left = $left
  right = $right
  repairedPartCount = 2
  centeredBackSheetMetalStatus = $status
  success = ($status -eq 'side_panel_sheetmetal_back_flange_centered')
  error = if ($status -eq 'side_panel_sheetmetal_back_flange_centered') { '' } else { 'side panel bbox validation failed after STEP trim/import' }
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutJson -Encoding UTF8
if (-not $result.success) {
  throw $result.error
}
Write-Output $OutJson
