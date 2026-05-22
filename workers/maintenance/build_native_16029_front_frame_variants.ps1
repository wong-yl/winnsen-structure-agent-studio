param(
  [ValidateSet(10, 12, 14)]
  [int[]]$DoorCounts = @(10, 12, 14)
)

$ErrorActionPreference = 'Stop'

$repo = 'D:\Winnsen_Structure_Agent_Studio'
$outDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-FRONT-FRAME-MODULE-20260521'
$toolBuild = Join-Path $repo 'workers\solidworks_tools\build_placed_components_module.ps1'
$builder = Join-Path $repo 'workers\solidworks_tools\bin\BuildPlacedComponentsModule.exe'
$cadRootName = -join ([char[]](0x673A,0x68B0,0x7ED3,0x6784,0x5DE5,0x7A0B,0x5E08,0x667A,0x80FD,0x4F53))
$softwareInstallDirName = -join ([char[]](0x8F6F,0x4EF6,0x5B89,0x88C5,0x5F55))
$exporter = Join-Path (Join-Path 'D:\' $cadRootName) 'scripts\sw_export_step_ascii.js'
$freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
$freecadEntry = Join-Path $repo 'workers\rule_extractions\RULE-16029-WELD-MODULE-PLACEMENT-20260521\freecad_env_worker_entry.py'

$source = 'C:\sw16029_standard_ascii'
$framePrefix = -join ([char[]](0x95E8,0x6846,0x20))
$verticalName = -join ([char[]](0x7AD6,0x9694,0x677F))
$crossName = -join ([char[]](0x6A2A,0x9694,0x677F))
$parts = @{
  top = Join-Path $source ($framePrefix + ([char]0x4E0A) + '.sldprt')
  bottom = Join-Path $source ($framePrefix + ([char]0x4E0B) + '.sldprt')
  left = Join-Path $source ($framePrefix + ([char]0x5DE6) + '.sldprt')
  right = Join-Path $source ($framePrefix + ([char]0x53F3) + '.sldprt')
  verticalL = Join-Path $source ($framePrefix + $verticalName + 'L.sldprt')
  verticalR = Join-Path $source ($framePrefix + $verticalName + 'R.SLDPRT')
  crossL = Join-Path $source ($framePrefix + $crossName + '.sldprt')
  crossR = Join-Path $source ($framePrefix + $crossName + 'R.SLDPRT')
}

$variants = @(
  @{ doors = 10; height = 359.0 },
  @{ doors = 12; height = 298.0 },
  @{ doors = 14; height = 254.429 }
) | Where-Object { $DoorCounts -contains [int]$_.doors }

function Format-Mm([double]$value) {
  return $value.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
}

function Add-Placement([System.Collections.Generic.List[string]]$lines, [string]$role, [string]$path, [double]$tx, [double]$ty, [double]$tz) {
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing source part: $path"
  }
  $lines.Add(($role, $path, (Format-Mm $tx), (Format-Mm $ty), (Format-Mm $tz)) -join "`t")
}

function Write-FrontFramePlacements([int]$doors, [double]$doorHeight, [string]$path) {
  $rowsPerColumn = [int]($doors / 2)
  $pitch = $doorHeight + 7.0
  $bottomPanelYMin = 32.0
  $standardCrossCenterY = 1700.0

  $lines = [System.Collections.Generic.List[string]]::new()
  $lines.Add("role`tpath`ttx_mm`tty_mm`ttz_mm")
  Add-Placement $lines 'frame_top' $parts.top 0 0 0
  Add-Placement $lines 'frame_bottom' $parts.bottom 0 0 0
  Add-Placement $lines 'frame_left' $parts.left 0 0 0
  Add-Placement $lines 'frame_right' $parts.right 0 0 0
  Add-Placement $lines 'frame_vertical_L' $parts.verticalL 0 0 0
  Add-Placement $lines 'frame_vertical_R' $parts.verticalR 0 0 0

  $levelIndex = 0
  for ($row = $rowsPerColumn - 1; $row -ge 1; $row--) {
    $targetCenterY = $bottomPanelYMin + $doorHeight + (($pitch - $doorHeight) / 2.0) + (($row - 1) * $pitch)
    $ty = $targetCenterY - $standardCrossCenterY
    Add-Placement $lines ("frame_cross_L_{0:00}" -f $levelIndex) $parts.crossL 0 $ty 0
    Add-Placement $lines ("frame_cross_R_{0:00}" -f $levelIndex) $parts.crossR 0 $ty 0
    $levelIndex++
  }

  [IO.File]::WriteAllLines($path, $lines, [Text.UTF8Encoding]::new($false))
}

function Invoke-FreeCadBbox([string]$stepPath, [string]$jsonOut, [string]$csvOut) {
  $env:STEP_PATH = $stepPath
  $env:JSON_OUT = $jsonOut
  $env:CSV_OUT = $csvOut
  & $freecad $freecadEntry | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "FreeCAD bbox failed for $stepPath"
  }
  Remove-Item Env:\STEP_PATH,Env:\JSON_OUT,Env:\CSV_OUT -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
& $toolBuild | Out-Host
if (-not (Test-Path -LiteralPath $builder)) {
  throw "Builder not found: $builder"
}

$summary = @()
foreach ($variant in $variants) {
  $doors = [int]$variant.doors
  $height = [double]$variant.height
  $rows = [int]($doors / 2)
  $expectedCrossBars = ($rows - 1) * 2

  $stem = "native_16029_front_frame_${doors}door_v1"
  $placements = Join-Path $outDir ($stem + '_placements.tsv')
  $asm = Join-Path $outDir ($stem + '.SLDASM')
  $resultJson = Join-Path $outDir ($stem + '_result.json')
  $step = Join-Path $outDir ($stem + '.step')
  $bboxJson = Join-Path $outDir ($stem + '_step_bbox.json')
  $bboxCsv = Join-Path $outDir ($stem + '_step_bbox.csv')

  Write-FrontFramePlacements $doors $height $placements

  & $builder $placements $asm $resultJson | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "BuildPlacedComponentsModule failed for ${doors}door"
  }

  & cscript.exe //Nologo $exporter $asm $step | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "STEP export failed for ${doors}door"
  }

  Invoke-FreeCadBbox $step $bboxJson $bboxCsv

  $bboxRows = Import-Csv -LiteralPath $bboxCsv | Where-Object {
    ([double]$_.volume -gt 0) -and
    ([math]::Abs([double]$_.x_min) -lt 1e20) -and
    ([math]::Abs([double]$_.y_min) -lt 1e20) -and
    ([math]::Abs([double]$_.z_min) -lt 1e20)
  }
  $crossRows = @($bboxRows | Where-Object {
    ($_.type_id -eq 'Part::Feature') -and
    ([math]::Abs(([double]$_.y_len) - 15.0) -lt 0.02) -and
    ([math]::Abs(([double]$_.z_min) - (-19.7)) -lt 0.2) -and
    ([double]$_.z_max -lt 0.2) -and
    ([double]$_.x_len -gt 400.0) -and
    ([double]$_.x_len -lt 470.0)
  })
  $summary += [pscustomobject]@{
    doors = $doors
    rows_per_column = $rows
    door_height_mm = $height
    pitch_mm = $height + 7.0
    expected_crossbar_parts = $expectedCrossBars
    actual_crossbar_parts = $crossRows.Count
    result = $(if ($crossRows.Count -eq $expectedCrossBars) { 'PASS' } else { 'CHECK' })
    assembly = $asm
    step = $step
  }
}

$summaryPath = Join-Path $outDir 'front_frame_variant_validation_summary.csv'
$summary | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding UTF8
$summary | Format-Table -AutoSize

& cscript.exe //Nologo (Join-Path $repo 'workers\solidworks_tools\sw_exit_if_no_active_doc.js') | Out-Host
