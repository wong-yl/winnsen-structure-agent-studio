param(
  [ValidateSet(10, 12, 14)]
  [int[]]$DoorCounts = @(10, 12, 14)
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-DOOR-ARRAY-MODULE-20260521'
$builder = Join-Path $repo 'workers\solidworks_tools\bin\BuildDoorArrayModule.exe'
$buildBuilder = Join-Path $repo 'workers\solidworks_tools\build_door_array_module.ps1'
$cadRootName = -join ([char[]](0x673A,0x68B0,0x7ED3,0x6784,0x5DE5,0x7A0B,0x5E08,0x667A,0x80FD,0x4F53))
$softwareInstallDirName = -join ([char[]](0x8F6F,0x4EF6,0x5B89,0x88C5,0x5F55))
$exporter = Join-Path (Join-Path 'D:\' $cadRootName) 'scripts\sw_export_step_ascii.js'
$freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
$bboxWorker = Join-Path $repo 'workers\rule_extractions\RULE-16029-WELD-MODULE-PLACEMENT-20260521\freecad_env_worker_entry.py'
$ordinaryDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-ORDINARY-DOOR-MODULE-20260521'
$exactSourceDir = 'C:\sw16029_direct_18door\source'
$door2Of12AssemblyName = (-join ([char[]](0x50A8,0x7269,0x67DC,0x95E8))) + '2' + ([char]0x2571) + '12' + (-join ([char[]](0x88C5,0x914D))) + '.SLDASM'

$variants = @(
  @{
    Doors = 10
    DoorHeight = 359
    OrdinaryDoor = Join-Path $ordinaryDir 'native_16029_ordinary_door_10door_v1_csharp.SLDASM'
  },
  @{
    Doors = 12
    DoorHeight = 298
    OrdinaryDoor = Join-Path $exactSourceDir $door2Of12AssemblyName
  },
  @{
    Doors = 14
    DoorHeight = 254.429
    OrdinaryDoor = Join-Path $ordinaryDir 'native_16029_ordinary_door_14door_v1_csharp.SLDASM'
  }
) | Where-Object { $DoorCounts -contains [int]$_.Doors }

if (-not (Test-Path -LiteralPath $builder)) {
  & $buildBuilder | Out-Host
}

foreach ($required in @($builder, $exporter, $freecad, $bboxWorker)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Required file was not found: $required"
  }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$summary = @()
foreach ($variant in $variants) {
  if (-not (Test-Path -LiteralPath $variant.OrdinaryDoor)) {
    throw "Required ordinary door module was not found: $($variant.OrdinaryDoor)"
  }

  $base = "native_16029_door_array_$($variant.Doors)door_v1_csharp"
  $asm = Join-Path $outDir ($base + '.SLDASM')
  $resultJson = Join-Path $outDir ($base + '_result.json')
  $step = Join-Path $outDir ($base + '.step')
  $bboxJson = Join-Path $outDir ($base + '_step_bbox.json')
  $bboxCsv = Join-Path $outDir ($base + '_step_bbox.csv')

  & $builder $asm $resultJson ([string]$variant.Doors) ([string]$variant.DoorHeight) $variant.OrdinaryDoor 32 | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "SolidWorks door-array generation failed for $($variant.Doors)-door variant"
  }

  & cscript.exe //Nologo $exporter $asm $step | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "SolidWorks STEP export failed for $($variant.Doors)-door variant"
  }

  $env:STEP_PATH = $step
  $env:JSON_OUT = $bboxJson
  $env:CSV_OUT = $bboxCsv
  & $freecad $bboxWorker | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "FreeCAD bbox inspection failed for $($variant.Doors)-door variant"
  }

  $parts = Import-Csv -LiteralPath $bboxCsv | Where-Object { $_.type_id -eq 'App::Part' -and $_.label -like "native_16029_ordinary_door_$($variant.Doors)door_v1_csharp*" }
  $summary += [pscustomobject]@{
    Doors = $variant.Doors
    DoorHeightMm = $variant.DoorHeight
    Assembly = $asm
    Step = $step
    BboxCsv = $bboxCsv
    OrdinaryDoorAppPartCount = @($parts).Count
  }
}

$summaryPath = Join-Path $outDir 'door_array_variant_build_summary.csv'
$summary | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding UTF8
$summary | Format-Table -AutoSize
Write-Output "summary=$summaryPath"
