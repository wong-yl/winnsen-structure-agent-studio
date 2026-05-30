param(
  [Parameter(Mandatory = $true)]
  [string] $RequestId,

  [double] $DoorWidthMm = 300,
  [double] $DoorHeightMm = 1917,

  [ValidateSet('left', 'right')]
  [string] $Handedness = 'left',

  [string] $OutputDir = '',

  [switch] $OpenInSolidWorks
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invariant([double] $Value) {
  return $Value.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
}

function Token([double] $Value) {
  return (Invariant $Value).Replace('.', 'p')
}

function TextFromCodes([int[]] $Codes) {
  return -join ($Codes | ForEach-Object { [char] $_ })
}

function Assert-File([string] $Path, [string] $Label) {
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "$Label was not found: $Path"
  }
}

function Resolve-PartByPrefix([string] $Root, [string] $Prefix, [string] $Suffix) {
  $found = Get-ChildItem -LiteralPath $Root -File |
    Where-Object { $_.Name.StartsWith($Prefix) -and $_.Name.EndsWith($Suffix) } |
    Select-Object -First 1
  if (-not $found) {
    throw "part was not found under $Root with prefix '$Prefix' and suffix '$Suffix'"
  }
  return $found.FullName
}

function Invoke-External([string] $FilePath, [string[]] $Arguments, [string] $Label, [int[]] $AllowedExitCodes = @(0)) {
  Write-Host "[$Label] $FilePath $($Arguments -join ' ')"
  Set-Variable -Name LASTEXITCODE -Scope Global -Value 0
  $output = & $FilePath @Arguments 2>&1
  $commandSucceeded = $?
  $exitCode = [int] (Get-Variable -Name LASTEXITCODE -Scope Global -ValueOnly -ErrorAction SilentlyContinue)
  if (-not $commandSucceeded -and $exitCode -eq 0) {
    $exitCode = 1
  }
  if ($output) {
    $output | ForEach-Object { Write-Host $_ }
  }
  if ($AllowedExitCodes -notcontains $exitCode) {
    throw "$Label failed with exit code $exitCode"
  }
}

function Read-Json([string] $Path) {
  return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-CloneSaved([string] $JsonPath, [string] $PartPath, [string] $StepPath, [string] $Label) {
  Assert-File $JsonPath "$Label result json"
  $result = Read-Json $JsonPath
  if (-not $result.savedPart -or -not (Test-Path -LiteralPath $PartPath -PathType Leaf)) {
    throw "$Label did not save a SolidWorks part: $PartPath"
  }
  if (-not $result.savedStep -or -not (Test-Path -LiteralPath $StepPath -PathType Leaf)) {
    throw "$Label did not export a STEP evidence file: $StepPath"
  }
}

$root = Split-Path -Parent $PSScriptRoot
$toolDir = Join-Path $root 'workers\solidworks_tools'
$sourceRoot = 'C:\sw16029_standard_ascii'
$compatDir = Join-Path $root 'workers\generated_models\SW-NATIVE-16029-740W-1917H-550D-L642-R246-ORDINARY-20260528\sw2020_gold_compat_parts'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $folder = "solidworks_2020_sheetmetal_door_$(Token $DoorWidthMm)W_$(Token $DoorHeightMm)H"
  $OutputDir = Join-Path (Join-Path $root "workers\generated_models\review_generation_requests\$RequestId") $folder
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$panelTemplate = Join-Path $sourceRoot 'door_panel_2_12.SLDPRT'
$stiffenerTemplate = Join-Path $sourceRoot 'rib_2_12.SLDPRT'
$plasticBushing = Join-Path $sourceRoot ("{0}.SLDPRT" -f (TextFromCodes @(0x5851,0x6599,0x8f74,0x5957,0x28,0x4e91,0x7ec5,0x6a21,0x5177,0x29)))
$hingePin = Join-Path $sourceRoot ("{0}.SLDPRT" -f (TextFromCodes @(0x95e8,0x8f74,0x9500)))

$latchPrefix = TextFromCodes @(0x63d2,0x9500,0x56fa,0x5b9a,0x677f)
$hookPadPrefix = ('U' + (TextFromCodes @(0x578b,0x9501,0x94a9,0x57ab,0x677f)))
$circlipPrefix = TextFromCodes @(0x5f00,0x53e3,0x6321,0x5708,0x35)

$latchPlate = Resolve-PartByPrefix $compatDir $latchPrefix '_SW2020_from_gold_step.SLDPRT'
$latchPlateStep = Resolve-PartByPrefix $compatDir $latchPrefix '_SW2020_from_gold_step_roundtrip.step'
$hookPad = Resolve-PartByPrefix $compatDir $hookPadPrefix '_SW2020_from_gold_step.SLDPRT'
$circlip = Resolve-PartByPrefix $compatDir $circlipPrefix '_SW2020_from_gold_assembly_step.SLDPRT'
$lockHook = Join-Path $compatDir 'electric_lock_hook_zja_s500_SW2020_from_ascii_step.SLDPRT'

$softwareInstallDirName = TextFromCodes @(0x8f6f,0x4ef6,0x5b89,0x88c5,0x5f55)
$freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
$cscript = Join-Path $env:SystemRoot 'System32\cscript.exe'
if (-not (Test-Path -LiteralPath $cscript -PathType Leaf)) {
  $cscript = 'cscript.exe'
}

foreach ($required in @(
  $panelTemplate,
  $stiffenerTemplate,
  $plasticBushing,
  $hingePin,
  $latchPlate,
  $latchPlateStep,
  $hookPad,
  $circlip,
  $lockHook,
  $freecad
)) {
  Assert-File $required 'required source file'
}

Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_door_weld_module.ps1')) 'compile door weld module'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_ordinary_door_module.ps1')) 'compile ordinary door module'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_import_step_save_native.ps1')) 'compile step importer'

$widthToken = Token $DoorWidthMm
$heightToken = Token $DoorHeightMm
$stiffenerLengthMm = [Math]::Round($DoorHeightMm - 10.5, 3)
$stiffenerToken = Token $stiffenerLengthMm
$sketch1 = (TextFromCodes @(0x8349,0x56fe)) + '1'

$panelPart = Join-Path $OutputDir "review_single_door_panel_W${widthToken}_H${heightToken}_sheetmetal.SLDPRT"
$panelStep = Join-Path $OutputDir "review_single_door_panel_W${widthToken}_H${heightToken}_sheetmetal.step"
$panelJson = Join-Path $OutputDir "review_single_door_panel_W${widthToken}_H${heightToken}_sheetmetal_result.json"
$stiffenerPart = Join-Path $OutputDir "review_single_door_stiffener_L${stiffenerToken}_sheetmetal.SLDPRT"
$stiffenerStep = Join-Path $OutputDir "review_single_door_stiffener_L${stiffenerToken}_sheetmetal.step"
$stiffenerJson = Join-Path $OutputDir "review_single_door_stiffener_L${stiffenerToken}_sheetmetal_result.json"
$topLatchStep = Join-Path $OutputDir "top_latch_from_bottom_mirror_W${widthToken}_H${heightToken}_${Handedness}.step"
$topLatchReport = Join-Path $OutputDir "top_latch_from_bottom_mirror_W${widthToken}_H${heightToken}_${Handedness}_report.json"
$topLatchPart = Join-Path $OutputDir "top_latch_from_bottom_mirror_W${widthToken}_H${heightToken}_${Handedness}.SLDPRT"
$topLatchRoundtripStep = Join-Path $OutputDir "top_latch_from_bottom_mirror_W${widthToken}_H${heightToken}_${Handedness}_roundtrip.step"
$topLatchImportJson = Join-Path $OutputDir "top_latch_from_bottom_mirror_W${widthToken}_H${heightToken}_${Handedness}_import.json"
$weldAsm = Join-Path $OutputDir "review_single_door_weld_W${widthToken}_H${heightToken}_${Handedness}.SLDASM"
$weldJson = Join-Path $OutputDir "review_single_door_weld_W${widthToken}_H${heightToken}_${Handedness}_result.json"
$weldStep = Join-Path $OutputDir "review_single_door_weld_W${widthToken}_H${heightToken}_${Handedness}.step"
$weldExportJson = Join-Path $OutputDir "review_single_door_weld_W${widthToken}_H${heightToken}_${Handedness}_export.json"
$ordinaryAsm = Join-Path $OutputDir "review_single_ordinary_door_W${widthToken}_H${heightToken}_${Handedness}.SLDASM"
$ordinaryJson = Join-Path $OutputDir "review_single_ordinary_door_W${widthToken}_H${heightToken}_${Handedness}_result.json"
$ordinaryStep = Join-Path $OutputDir "review_single_ordinary_door_W${widthToken}_H${heightToken}_${Handedness}.step"
$ordinaryExportJson = Join-Path $OutputDir "review_single_ordinary_door_W${widthToken}_H${heightToken}_${Handedness}_export.json"
$summaryJson = Join-Path $OutputDir 'solidworks_2020_native_generation_summary.json'

$cloneScript = Join-Path $toolDir 'sw_clone_master_model_height_probe.js'
Invoke-External $cscript @('//Nologo', $cloneScript, $panelTemplate, $panelPart, $panelStep, $panelJson, $sketch1, 'D1', (Invariant $DoorHeightMm), $sketch1, 'D2', (Invariant $DoorWidthMm)) 'generate sheet-metal door panel' @(0, 4)
Assert-CloneSaved $panelJson $panelPart $panelStep 'sheet-metal door panel'

Invoke-External $cscript @('//Nologo', $cloneScript, $stiffenerTemplate, $stiffenerPart, $stiffenerStep, $stiffenerJson, $sketch1, 'D2', (Invariant $stiffenerLengthMm)) 'generate sheet-metal stiffener' @(0, 4)
Assert-CloneSaved $stiffenerJson $stiffenerPart $stiffenerStep 'sheet-metal stiffener'

$side = if ($Handedness -eq 'right') { -1.0 } else { 1.0 }
$latchTxMm = -(($DoorWidthMm / 2.0) - 10.0) * $side
$latchBottomTyMm = -($DoorHeightMm / 2.0) + 28.8
$env:SOURCE_STEP = $latchPlateStep
$env:OUTPUT_STEP = $topLatchStep
$env:REPORT_JSON = $topLatchReport
$env:LATCH_TX_MM = Invariant $latchTxMm
$env:LATCH_BOTTOM_TY_MM = Invariant $latchBottomTyMm
$env:LATCH_TZ_MM = '-0.8'
$mirrorScript = Join-Path $toolDir 'mirror_bottom_latch_to_top_freecad.py'
$mirrorCode = "import runpy; runpy.run_path(r'$mirrorScript', run_name='__main__')"
Invoke-External $freecad @('-c', $mirrorCode) 'mirror top latch with internal FreeCAD evidence'
Assert-File $topLatchStep 'mirrored top latch STEP'
Assert-File $topLatchReport 'mirrored top latch report'

$importer = Join-Path $toolDir 'bin\ImportStepSaveNative.exe'
Invoke-External $importer @($topLatchStep, $topLatchPart, $topLatchRoundtripStep, $topLatchImportJson) 'save mirrored top latch as SolidWorks native'
Assert-File $topLatchPart 'mirrored top latch SolidWorks part'

$weldBuilder = Join-Path $toolDir 'bin\BuildDoorWeldModule.exe'
Invoke-External $weldBuilder @($weldAsm, $weldJson, (Invariant $DoorHeightMm), $panelPart, $stiffenerPart, $latchPlate, $hookPad, $Handedness, (Invariant $DoorWidthMm), "top-latch=$topLatchPart") 'build door weld assembly'
Assert-File $weldAsm 'door weld assembly'

$ordinaryBuilder = Join-Path $toolDir 'bin\BuildOrdinaryDoorModule.exe'
Invoke-External $ordinaryBuilder @($ordinaryAsm, $ordinaryJson, (Invariant $DoorHeightMm), $weldAsm, $plasticBushing, $hingePin, $circlip, $lockHook, $Handedness, (Invariant $DoorWidthMm)) 'build ordinary door assembly'
Assert-File $ordinaryAsm 'ordinary door assembly'

$exportScript = Join-Path $toolDir 'sw_export_model_step.js'
Invoke-External $cscript @('//Nologo', $exportScript, $weldAsm, $weldStep, $weldExportJson) 'export door weld assembly STEP evidence'
Invoke-External $cscript @('//Nologo', $exportScript, $ordinaryAsm, $ordinaryStep, $ordinaryExportJson) 'export ordinary door assembly STEP evidence'

$opened = $false
if ($OpenInSolidWorks) {
  Invoke-External $cscript @('//Nologo', $exportScript, $ordinaryAsm, $ordinaryStep, $ordinaryExportJson, 'keepopen') 'open ordinary door assembly in SolidWorks 2020'
  $opened = $true
}

$outputFiles = Get-ChildItem -LiteralPath $OutputDir -File |
  Where-Object { -not $_.Name.StartsWith('~$') } |
  Sort-Object Name |
  ForEach-Object { $_.Name }
$summaryFileName = Split-Path -Leaf $summaryJson
if ($outputFiles -notcontains $summaryFileName) {
  $outputFiles = @($outputFiles) + $summaryFileName
}

$summary = [ordered] @{
  status = 'solidworks_2020_native_ready'
  resultKind = 'solidworks2020_sheetmetal_model'
  requestId = $RequestId
  cadMainline = 'SolidWorks 2020'
  doorWidthMm = $DoorWidthMm
  doorHeightMm = $DoorHeightMm
  handedness = $Handedness
  outputDir = $OutputDir
  sheetMetalPanelPart = $panelPart
  sheetMetalPanelStep = $panelStep
  sheetMetalStiffenerPart = $stiffenerPart
  sheetMetalStiffenerStep = $stiffenerStep
  weldAssembly = $weldAsm
  weldStep = $weldStep
  primaryAssembly = $ordinaryAsm
  ordinaryStep = $ordinaryStep
  openedInSolidWorks = $opened
  modelGeneratedAt = (Get-Date).ToUniversalTime().ToString('o')
  boundary = 'SolidWorks 2020 native sheet-metal model; FreeCAD was used only for internal mirrored latch evidence.'
  outputFiles = @($outputFiles)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryJson -Encoding UTF8
Write-Output $summaryJson
