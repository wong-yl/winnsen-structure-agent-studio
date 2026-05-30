param(
  [Parameter(Mandatory=$true)][string]$StepPath,
  [Parameter(Mandatory=$true)][string]$CsvPath,
  [Parameter(Mandatory=$true)][string]$JsonPath,
  [Parameter(Mandatory=$true)][string]$MdPath,
  [Parameter(Mandatory=$true)][string]$FreeCadPath
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$freecad = $FreeCadPath
$worker = Join-Path $repo 'workers\maintenance\inspect_step_assembly_bboxes_freecad.py'

if (-not (Test-Path -LiteralPath $freecad)) {
  throw "FreeCADCmd.exe was not found: $freecad"
}
if (-not (Test-Path -LiteralPath $worker)) {
  throw "STEP bbox worker was not found: $worker"
}
if (-not (Test-Path -LiteralPath $StepPath)) {
  throw "STEP file was not found: $StepPath"
}

$env:STEP_BBOX_INPUT = $StepPath
$env:STEP_BBOX_CSV = $CsvPath
$env:STEP_BBOX_JSON = $JsonPath
$env:STEP_BBOX_MD = $MdPath

& $freecad $worker
if ($LASTEXITCODE -ne 0) {
  throw "FreeCAD bbox inspection failed with exit code $LASTEXITCODE"
}
