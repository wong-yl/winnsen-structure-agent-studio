param(
  [ValidateSet(10, 12, 14)]
  [int]$DoorCount = 12,
  [switch]$UseMatrix
)

$ErrorActionPreference = 'Stop'

$repo = 'D:\Winnsen_Structure_Agent_Studio'
$skeletonDir = Join-Path $repo 'workers\generated_models\SW-NATIVE-16029-CABINET-SKELETON-SERIES-20260521'
$outDir = Join-Path $repo ("workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-{0}DOOR-20260521" -f $DoorCount)
$candidateJson = Join-Path $repo 'data\solidworks_16029_fixed_module_candidate_map.json'
$toolBuild = Join-Path $repo 'workers\solidworks_tools\build_placed_components_module.ps1'
$builder = Join-Path $repo 'workers\solidworks_tools\bin\BuildPlacedComponentsModule.exe'
$cadRootName = -join ([char[]](0x673A,0x68B0,0x7ED3,0x6784,0x5DE5,0x7A0B,0x5E08,0x667A,0x80FD,0x4F53))
$softwareInstallDirName = -join ([char[]](0x8F6F,0x4EF6,0x5B89,0x88C5,0x5F55))
$exporter = Join-Path (Join-Path 'D:\' $cadRootName) 'scripts\sw_export_step_ascii.js'
$freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
$freecadEntry = Join-Path $repo 'workers\rule_extractions\RULE-16029-WELD-MODULE-PLACEMENT-20260521\freecad_env_worker_entry.py'

function Format-Mm([double]$value) {
  return $value.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
}

function Add-Placement([System.Collections.Generic.List[string]]$lines, [string]$role, [string]$path, [double]$tx, [double]$ty, [double]$tz, [double[]]$rotation = $null) {
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing source component: $path"
  }
  $cells = [System.Collections.Generic.List[string]]::new()
  $cells.Add($role)
  $cells.Add($path)
  $cells.Add((Format-Mm $tx))
  $cells.Add((Format-Mm $ty))
  $cells.Add((Format-Mm $tz))
  if ($UseMatrix) {
    if ($null -eq $rotation) {
      $rotation = @(1, 0, 0, 0, 1, 0, 0, 0, 1)
    }
    foreach ($value in $rotation) {
      $cells.Add((Format-Mm $value))
    }
  }
  $lines.Add($cells -join "`t")
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

if (-not (Test-Path -LiteralPath $candidateJson)) {
  & python (Join-Path $repo 'workers\maintenance\build_16029_fixed_module_candidate_map.py') | Out-Host
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
& $toolBuild | Out-Host
if (-not (Test-Path -LiteralPath $builder)) {
  throw "Builder not found: $builder"
}

$stem = $(if ($UseMatrix) { "native_16029_${DoorCount}door_cabinet_enriched_v2" } else { "native_16029_${DoorCount}door_cabinet_enriched_v1" })
$skeleton = Join-Path $skeletonDir ("native_16029_{0}door_cabinet_skeleton_v2.SLDASM" -f $DoorCount)
$placements = Join-Path $outDir ($stem + '_placements.tsv')
$asm = Join-Path $outDir ($stem + '.SLDASM')
$resultJson = Join-Path $outDir ($stem + '_result.json')
$step = Join-Path $outDir ($stem + '.step')
$bboxJson = Join-Path $outDir ($stem + '_step_bbox.json')
$bboxCsv = Join-Path $outDir ($stem + '_step_bbox.csv')

if (-not (Test-Path -LiteralPath $skeleton)) {
  throw "Skeleton assembly not found. Run build_native_16029_cabinet_skeleton_variants.ps1 first: $skeleton"
}

$candidateMap = Get-Content -LiteralPath $candidateJson -Encoding UTF8 | ConvertFrom-Json
$safeCandidates = @(
  if ($UseMatrix) {
    $candidateMap.candidates | Where-Object {
      $_.recommended_for_matrix_enriched_model -eq $true -and
      $_.axis_aligned_matrix_ready -eq $true -and
      $_.can_place_native -eq $true
    }
  } else {
    $candidateMap.candidates | Where-Object {
      $_.recommended_for_first_enriched_model -eq $true -and
      $_.placement_strategy -eq 'identity_transform_ready' -and
      $_.can_place_native -eq $true
    }
  }
)
if ($safeCandidates.Count -lt 1) {
  throw "No safe enriched candidates found in $candidateJson"
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add($(if ($UseMatrix) { "role`tpath`ttx_mm`tty_mm`ttz_mm`tr11`tr12`tr13`tr21`tr22`tr23`tr31`tr32`tr33" } else { "role`tpath`ttx_mm`tty_mm`ttz_mm" }))
Add-Placement $lines ("cabinet_skeleton_{0}door_v2" -f $DoorCount) $skeleton 0 0 0
foreach ($candidate in $safeCandidates) {
  if ($UseMatrix) {
    $transform = $candidate.axis_aligned_transform.translation_mm
    $rotation = @($candidate.axis_aligned_transform.matrix | ForEach-Object { [double]$_ })
    Add-Placement $lines $candidate.role $candidate.component_path ([double]$transform.tx_mm) ([double]$transform.ty_mm) ([double]$transform.tz_mm) $rotation
  } else {
    $transform = $candidate.placement_transform_mm
    Add-Placement $lines $candidate.role $candidate.component_path ([double]$transform.tx_mm) ([double]$transform.ty_mm) ([double]$transform.tz_mm)
  }
}
[IO.File]::WriteAllLines($placements, $lines, [Text.UTF8Encoding]::new($false))

& $builder $placements $asm $resultJson | Out-Host
if ($LASTEXITCODE -ne 0) {
  throw "BuildPlacedComponentsModule failed for enriched $DoorCount-door model"
}

& cscript.exe //Nologo $exporter $asm $step | Out-Host
if ($LASTEXITCODE -ne 0) {
  throw "STEP export failed for enriched $DoorCount-door model"
}

Invoke-FreeCadBbox $step $bboxJson $bboxCsv

$env:STUDIO_16029_ENRICHED_DOOR_COUNT = [string]$DoorCount
$env:STUDIO_16029_ENRICHED_DIR = $outDir
$env:STUDIO_16029_ENRICHED_STEM = $stem
$env:STUDIO_16029_ENRICHED_MODE = $(if ($UseMatrix) { 'matrix' } else { 'identity' })
& python (Join-Path $repo 'workers\maintenance\validate_native_16029_cabinet_enriched_12door.py') | Out-Host
$validationExitCode = $LASTEXITCODE
Remove-Item Env:\STUDIO_16029_ENRICHED_DOOR_COUNT,Env:\STUDIO_16029_ENRICHED_DIR,Env:\STUDIO_16029_ENRICHED_STEM,Env:\STUDIO_16029_ENRICHED_MODE -ErrorAction SilentlyContinue
if ($validationExitCode -ne 0) {
  throw "Enriched $DoorCount-door validation failed"
}

& cscript.exe //Nologo (Join-Path $repo 'workers\solidworks_tools\sw_exit_if_no_active_doc.js') | Out-Host

Write-Output "assembly=$asm"
Write-Output "step=$step"
Write-Output "placements=$placements"
