$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-DOOR-WELD-MODULE-20260521'
$builder = Join-Path $repo 'workers\solidworks_tools\bin\BuildDoorWeldModule.exe'
$buildBuilder = Join-Path $repo 'workers\solidworks_tools\build_door_weld_module.ps1'
$exporter = Join-Path $repo 'workers\solidworks_tools\sw_export_step_ascii.js'
$softwareInstallDirName = -join ([char[]](0x8F6F,0x4EF6,0x5B89,0x88C5,0x5F55))
$freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
$bboxWorker = Join-Path $repo 'workers\rule_extractions\RULE-16029-WELD-MODULE-PLACEMENT-20260521\freecad_env_worker_entry.py'

$panelDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-DOOR-PANEL-SERIES-20260521'
$stiffenerDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-DOOR-STIFFENER-SERIES-20260521'
$standardRoot = 'C:\sw16029_standard_ascii'
$latchPlateName = (-join ([char[]](0x63D2,0x9500,0x56FA,0x5B9A,0x677F))) + '.SLDPRT'
$hookPadName = 'U' + (-join ([char[]](0x578B,0x9501,0x94A9,0x57AB,0x677F))) + '.SLDPRT'
$latchPlate = Join-Path $standardRoot $latchPlateName
$hookPad = Join-Path $standardRoot $hookPadName

$variants = @(
  @{
    Doors = 10
    DoorHeight = 359
    DoorWidth = 437
    Panel = Join-Path $panelDir 'native_16029_door_panel_10door_H359.SLDPRT'
    Stiffener = Join-Path $stiffenerDir 'native_16029_door_stiffener_10door_L348p5.SLDPRT'
  },
  @{
    Doors = 12
    DoorHeight = 298
    DoorWidth = 437
    Panel = Join-Path $panelDir 'native_16029_door_panel_12door_H298.SLDPRT'
    Stiffener = Join-Path $stiffenerDir 'native_16029_door_stiffener_12door_L287p5.SLDPRT'
  },
  @{
    Doors = 14
    DoorHeight = 254.429
    DoorWidth = 437
    Panel = Join-Path $panelDir 'native_16029_door_panel_14door_H254p429.SLDPRT'
    Stiffener = Join-Path $stiffenerDir 'native_16029_door_stiffener_14door_L243p929.SLDPRT'
  }
)

if (-not (Test-Path -LiteralPath $builder)) {
  & $buildBuilder | Out-Host
}

foreach ($required in @($builder, $exporter, $freecad, $bboxWorker, $latchPlate, $hookPad)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Required file was not found: $required"
  }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$summary = @()
foreach ($variant in $variants) {
  foreach ($part in @($variant.Panel, $variant.Stiffener)) {
    if (-not (Test-Path -LiteralPath $part)) {
      throw "Required generated part was not found: $part"
    }
  }

  foreach ($hand in @('left', 'right')) {
    $suffix = $(if ($hand -eq 'left') { '' } else { '_right' })
    $base = "native_16029_door_weld_5part_$($variant.Doors)door_v6_csharp$suffix"
    $asm = Join-Path $outDir ($base + '.SLDASM')
    $resultJson = Join-Path $outDir ($base + '_result.json')
    $step = Join-Path $outDir ($base + '.step')
    $bboxJson = Join-Path $outDir ($base + '_step_bbox.json')
    $bboxCsv = Join-Path $outDir ($base + '_step_bbox.csv')

    & $builder $asm $resultJson ([string]$variant.DoorHeight) $variant.Panel $variant.Stiffener $latchPlate $hookPad $hand ([string]$variant.DoorWidth) | Out-Host
    if ($LASTEXITCODE -ne 0) {
      throw "SolidWorks assembly generation failed for $($variant.Doors)-door $hand variant"
    }

    & cscript.exe //Nologo $exporter $asm $step | Out-Host
    if ($LASTEXITCODE -ne 0) {
      throw "SolidWorks STEP export failed for $($variant.Doors)-door $hand variant"
    }

    $env:STEP_PATH = $step
    $env:JSON_OUT = $bboxJson
    $env:CSV_OUT = $bboxCsv
    & $freecad $bboxWorker | Out-Host
    if ($LASTEXITCODE -ne 0) {
      throw "FreeCAD bbox inspection failed for $($variant.Doors)-door $hand variant"
    }

    $parts = Import-Csv -LiteralPath $bboxCsv | Where-Object { $_.type_id -eq 'Part::Feature' }
    $summary += [pscustomobject]@{
      Doors = $variant.Doors
      Handedness = $hand
      DoorHeightMm = $variant.DoorHeight
      DoorWidthMm = $variant.DoorWidth
      Assembly = $asm
      Step = $step
      BboxCsv = $bboxCsv
      PartFeatureCount = @($parts).Count
    }
  }
}

$summaryPath = Join-Path $outDir 'door_weld_5part_variant_build_summary.csv'
$summary | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding UTF8
$summary | Format-Table -AutoSize
Write-Output "summary=$summaryPath"
