$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-ORDINARY-DOOR-MODULE-20260521'
$builder = Join-Path $repo 'workers\solidworks_tools\bin\BuildOrdinaryDoorModule.exe'
$buildBuilder = Join-Path $repo 'workers\solidworks_tools\build_ordinary_door_module.ps1'
$exporter = 'D:\机械结构工程师智能体\scripts\sw_export_step_ascii.js'
$freecad = 'D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
$bboxWorker = Join-Path $repo 'workers\rule_extractions\RULE-16029-WELD-MODULE-PLACEMENT-20260521\freecad_env_worker_entry.py'

$weldDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-DOOR-WELD-MODULE-20260521'
$bushing = 'C:\sw16029_standard_ascii\plastic_bushing.SLDPRT'
$hingePin = 'C:\sw16029_standard_ascii\door_hinge_pin.SLDPRT'
$circlip = 'C:\sw16029_standard_ascii\开口挡圈 5.SLDPRT'
$lockHook = 'C:\sw16029_standard_ascii\electric_lock_hook_zja_s500.SLDPRT'

$variants = @(
  @{
    Doors = 10
    DoorHeight = 359
    WeldAssembly = Join-Path $weldDir 'native_16029_door_weld_5part_10door_v6_csharp.SLDASM'
  },
  @{
    Doors = 12
    DoorHeight = 298
    WeldAssembly = Join-Path $weldDir 'native_16029_door_weld_5part_12door_v6_csharp.SLDASM'
  },
  @{
    Doors = 14
    DoorHeight = 254.429
    WeldAssembly = Join-Path $weldDir 'native_16029_door_weld_5part_14door_v6_csharp.SLDASM'
  }
)

if (-not (Test-Path -LiteralPath $builder)) {
  & $buildBuilder | Out-Host
}

foreach ($required in @($builder, $exporter, $freecad, $bboxWorker, $bushing, $hingePin, $circlip, $lockHook)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Required file was not found: $required"
  }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$summary = @()
foreach ($variant in $variants) {
  if (-not (Test-Path -LiteralPath $variant.WeldAssembly)) {
    throw "Required weld module was not found: $($variant.WeldAssembly)"
  }

  $base = "native_16029_ordinary_door_$($variant.Doors)door_v1_csharp"
  $asm = Join-Path $outDir ($base + '.SLDASM')
  $resultJson = Join-Path $outDir ($base + '_result.json')
  $step = Join-Path $outDir ($base + '.step')
  $bboxJson = Join-Path $outDir ($base + '_step_bbox.json')
  $bboxCsv = Join-Path $outDir ($base + '_step_bbox.csv')

  & $builder $asm $resultJson ([string]$variant.DoorHeight) $variant.WeldAssembly $bushing $hingePin $circlip $lockHook | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "SolidWorks ordinary door generation failed for $($variant.Doors)-door variant"
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

  $parts = Import-Csv -LiteralPath $bboxCsv | Where-Object { $_.type_id -eq 'Part::Feature' }
  $summary += [pscustomobject]@{
    Doors = $variant.Doors
    DoorHeightMm = $variant.DoorHeight
    Assembly = $asm
    Step = $step
    BboxCsv = $bboxCsv
    PartFeatureCount = @($parts).Count
  }
}

$summaryPath = Join-Path $outDir 'ordinary_door_variant_build_summary.csv'
$summary | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding UTF8
$summary | Format-Table -AutoSize
Write-Output "summary=$summaryPath"
