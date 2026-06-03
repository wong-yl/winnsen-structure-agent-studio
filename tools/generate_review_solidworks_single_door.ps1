param(
  [Parameter(Mandatory = $true)]
  [string] $RequestId,

  [double] $DoorWidthMm = 300,
  [double] $DoorHeightMm = 1917,

  [ValidateSet('left', 'right')]
  [string] $Handedness = 'left',

  [string] $OutputDir = '',

  [string] $SheetMetalRuleJson = '',

  [string] $DoorUnit = '',

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

function Wait-File([string] $Path, [string] $Label, [int] $TimeoutSeconds = 180) {
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
      return
    }
    Start-Sleep -Milliseconds 200
  }
  Assert-File $Path $Label
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

function Quote-ProcessArgument([string] $Value) {
  if ($null -eq $Value) {
    return '""'
  }
  $text = [string] $Value
  if ($text.Length -eq 0) {
    return '""'
  }
  if ($text -notmatch '[\s"]') {
    return $text
  }
  return '"' + $text.Replace('"', '\"') + '"'
}

function Join-ProcessArguments([string[]] $Arguments) {
  return ($Arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join ' '
}

function Invoke-External([string] $FilePath, [string[]] $Arguments, [string] $Label, [int[]] $AllowedExitCodes = @(0)) {
  Write-Host "[$Label] $FilePath $($Arguments -join ' ')"
  $stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) ("locker_16029_stdout_{0}.log" -f ([guid]::NewGuid().ToString('N')))
  $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ("locker_16029_stderr_{0}.log" -f ([guid]::NewGuid().ToString('N')))
  Set-Variable -Name LASTEXITCODE -Scope Global -Value 0
  try {
    $process = Start-Process -FilePath $FilePath -ArgumentList (Join-ProcessArguments $Arguments) -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $process.WaitForExit()
    $exitCode = [int] $process.ExitCode
    Set-Variable -Name LASTEXITCODE -Scope Global -Value $exitCode
    if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) {
      Get-Content -LiteralPath $stdoutPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    }
    if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
      Get-Content -LiteralPath $stderrPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    }
    if ($AllowedExitCodes -notcontains $exitCode) {
      throw "$Label failed with exit code $exitCode"
    }
  }
  finally {
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
  }
}

function Read-Json([string] $Path) {
  return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-ObjectString($Object, [string] $Name, [string] $Fallback = '') {
  if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) {
    return [string] $Object.PSObject.Properties[$Name].Value
  }
  return $Fallback
}

function Get-ObjectNumber($Object, [string] $Name, [double] $Fallback = 0) {
  if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) {
    try {
      $value = [double] $Object.PSObject.Properties[$Name].Value
      if (-not [double]::IsNaN($value) -and -not [double]::IsInfinity($value)) {
        return $value
      }
    }
    catch {
    }
  }
  return $Fallback
}

function New-SheetMetalHoleDatumRecord(
  $Hole,
  [double] $GeneratedFlatWidthMm,
  [double] $GeneratedFlatHeightMm,
  [string] $Handedness
) {
  $distanceToLeftMm = Get-ObjectNumber $Hole 'distanceToLeftMm'
  $distanceToRightMm = Get-ObjectNumber $Hole 'distanceToRightMm'
  $distanceToBottomMm = Get-ObjectNumber $Hole 'distanceToBottomMm'
  $distanceToTopMm = Get-ObjectNumber $Hole 'distanceToTopMm'
  $sourceCxFromCenterMm = Get-ObjectNumber $Hole 'cxFromCenterMm'
  $sourceCyFromCenterMm = Get-ObjectNumber $Hole 'cyFromCenterMm'
  $generatedFlatCxFromCenterMm = [Math]::Round(-($GeneratedFlatWidthMm / 2.0) + $distanceToLeftMm, 3)
  if ($Handedness -eq 'right') {
    $generatedFlatCxFromCenterMm = -$generatedFlatCxFromCenterMm
  }
  $generatedFlatCyFromCenterMm = [Math]::Round(-($GeneratedFlatHeightMm / 2.0) + $distanceToBottomMm, 3)
  return [pscustomobject] ([ordered] @{
    role = Get-ObjectString $Hole 'role'
    diameterMm = [Math]::Round((Get-ObjectNumber $Hole 'diameterMm'), 3)
    radiusMm = [Math]::Round((Get-ObjectNumber $Hole 'radiusMm'), 3)
    sourceCxFromCenterMm = [Math]::Round($sourceCxFromCenterMm, 3)
    sourceCyFromCenterMm = [Math]::Round($sourceCyFromCenterMm, 3)
    generatedFlatCxFromCenterMm = $generatedFlatCxFromCenterMm
    generatedFlatCyFromCenterMm = $generatedFlatCyFromCenterMm
    distanceToLeftMm = [Math]::Round($distanceToLeftMm, 3)
    distanceToRightMm = [Math]::Round($distanceToRightMm, 3)
    distanceToBottomMm = [Math]::Round($distanceToBottomMm, 3)
    distanceToTopMm = [Math]::Round($distanceToTopMm, 3)
    generatedFlatWidthMm = [Math]::Round($GeneratedFlatWidthMm, 3)
    generatedFlatHeightMm = [Math]::Round($GeneratedFlatHeightMm, 3)
    handednessMirror = if ($Handedness -eq 'right') { 'mirrored_from_gold_left_reference' } else { 'gold_left_reference' }
    featureImplementationStatus = 'datum_ready_for_solidworks_cut_feature'
  })
}

function Get-DetectedCutFeatures($PanelResult) {
  $cutFeatureToken = TextFromCodes @(0x5207, 0x9664)
  $featureSuppression = @{}
  foreach ($featureRow in @($PanelResult.features)) {
    $name = Get-ObjectString $featureRow 'name'
    if ([string]::IsNullOrWhiteSpace($name)) {
      continue
    }
    $isSuppressed = $false
    if ($null -ne $featureRow.PSObject.Properties['is_suppressed']) {
      $isSuppressed = [bool] $featureRow.PSObject.Properties['is_suppressed'].Value
    }
    $featureSuppression[$name] = $isSuppressed
  }
  $dimensionRows = @($PanelResult.dimensions)
  $cutRows = @(
    $dimensionRows |
      Where-Object {
        $feature = [string] $_.feature
        $featureType = [string] $_.feature_type
        $isCut = $feature.Contains($cutFeatureToken) -or $feature -match 'Cut' -or $featureType -eq 'Cut'
        $isCut -and (-not ($featureSuppression.ContainsKey($feature) -and [bool] $featureSuppression[$feature]))
      }
  )
  $featureNames = @(
    $cutRows |
      ForEach-Object { [string] $_.feature } |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
      Sort-Object -Unique
  )
  $suppressedFeatureNames = @(
    $featureSuppression.Keys |
      Where-Object { [bool] $featureSuppression[$_] -and ($_.Contains($cutFeatureToken) -or $_ -match 'Cut') } |
      Sort-Object -Unique
  )
  return [pscustomobject] ([ordered] @{
    featureNames = @($featureNames)
    featureCount = @($featureNames).Count
    suppressedFeatureNames = @($suppressedFeatureNames)
    suppressedFeatureCount = @($suppressedFeatureNames).Count
    sketchDimensionCount = @($cutRows).Count
  })
}

function Assert-CloneSaved([string] $JsonPath, [string] $PartPath, [string] $StepPath, [string] $Label) {
  Wait-File $JsonPath "$Label result json"
  Wait-File $PartPath "$Label SolidWorks part"
  Wait-File $StepPath "$Label STEP evidence file"
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
if ([string]::IsNullOrWhiteSpace($SheetMetalRuleJson)) {
  $SheetMetalRuleJson = Join-Path $root 'data\locker_16029_gold_sheetmetal_rules.json'
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $folder = "solidworks_2020_sheetmetal_door_$(Token $DoorWidthMm)W_$(Token $DoorHeightMm)H"
  $OutputDir = Join-Path (Join-Path $root "workers\generated_models\review_generation_requests\$RequestId") $folder
}

$OutputDir = [IO.Path]::GetFullPath($OutputDir)
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
  $freecad,
  $SheetMetalRuleJson
)) {
  Assert-File $required 'required source file'
}

$sheetMetalRules = Read-Json $SheetMetalRuleJson
$sheetMetalRuleBindingStatus = 'not_bound'
$sheetMetalRuleClassId = ''
$sheetMetalSourceDxf = ''
$sheetMetalExpectedFlatWidthMm = $null
$sheetMetalExpectedFlatHeightMm = $null
$sheetMetalHoleDatumCount = 0
$sheetMetalHoleDatums = @()
$sheetMetalHoleFeatureStatus = 'datum_ready_not_cut_feature'
$sheetMetalDetectedCutFeatureNames = @()
$sheetMetalDetectedCutFeatureCount = 0
$sheetMetalDetectedCutSketchDimensionCount = 0
$sheetMetalSuppressedCutFeatureNames = @()
$sheetMetalSuppressedCutFeatureCount = 0
$sheetMetalAppliedFeatureOperations = @()
$panelCloneExtraArgs = @()
if (-not [string]::IsNullOrWhiteSpace($DoorUnit)) {
  $sheetMetalRuleClassId = "$(Invariant ([double] $DoorUnit))/12"
  $classProperty = $sheetMetalRules.doorClasses.PSObject.Properties[$sheetMetalRuleClassId]
  $class = if ($null -ne $classProperty) { $classProperty.Value } else { $null }
  if ($null -ne $class) {
    $sheetMetalRuleBindingStatus = 'bound_to_1000w_gold_dxf'
    $sheetMetalSourceDxf = [string] $class.sourceDxf
    $flatWidthExtraMm = [double] $class.flatWidthExtraMm
    $flatHeightExtraMm = [double] $class.flatHeightExtraMm
    $sheetMetalExpectedFlatWidthMm = [Math]::Round($DoorWidthMm + $flatWidthExtraMm, 3)
    $sheetMetalExpectedFlatHeightMm = [Math]::Round($DoorHeightMm + $flatHeightExtraMm, 3)
    $holeDatums = New-Object System.Collections.Generic.List[object]
    foreach ($hole in @($class.holes)) {
      $holeDatums.Add((New-SheetMetalHoleDatumRecord $hole ([double] $sheetMetalExpectedFlatWidthMm) ([double] $sheetMetalExpectedFlatHeightMm) $Handedness)) | Out-Null
    }
    $sheetMetalHoleDatums = @($holeDatums.ToArray())
    $sheetMetalHoleDatumCount = $sheetMetalHoleDatums.Count
    $lockPilotHoleCount = @($class.holes | Where-Object { (Get-ObjectString $_ 'role') -eq 'lock_or_label_pilot_hole' }).Count
    $lockPilotCutFeature = TextFromCodes @(0x5207, 0x9664, 0x2d, 0x62c9, 0x4f38, 0x36)
    if ($lockPilotHoleCount -eq 0) {
      $panelCloneExtraArgs += @('--suppress-feature', $lockPilotCutFeature)
      $sheetMetalAppliedFeatureOperations += [pscustomobject] ([ordered] @{
        operation = 'suppress_template_lock_or_label_pilot_cut'
        feature = $lockPilotCutFeature
        reason = 'gold_dxf_class_has_no_lock_or_label_pilot_hole'
        sourceClassId = $sheetMetalRuleClassId
      })
    }
    else {
      $sheetMetalAppliedFeatureOperations += [pscustomobject] ([ordered] @{
        operation = 'keep_template_lock_or_label_pilot_cut'
        feature = $lockPilotCutFeature
        reason = 'gold_dxf_class_has_lock_or_label_pilot_hole'
        sourceClassId = $sheetMetalRuleClassId
      })
    }
  }
  else {
    $sheetMetalRuleBindingStatus = 'missing_gold_dxf_class'
  }
}
else {
  $sheetMetalRuleBindingStatus = 'door_unit_not_provided'
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
$panelCloneArgs = @('//Nologo', $cloneScript, $panelTemplate, $panelPart, $panelStep, $panelJson, $sketch1, 'D1', (Invariant $DoorHeightMm), $sketch1, 'D2', (Invariant $DoorWidthMm)) + $panelCloneExtraArgs
Invoke-External $cscript $panelCloneArgs 'generate sheet-metal door panel' @(0, 4)
Assert-CloneSaved $panelJson $panelPart $panelStep 'sheet-metal door panel'
$panelResult = Read-Json $panelJson
$detectedCutFeatures = Get-DetectedCutFeatures $panelResult
$sheetMetalDetectedCutFeatureNames = @($detectedCutFeatures.featureNames)
$sheetMetalDetectedCutFeatureCount = [int] $detectedCutFeatures.featureCount
$sheetMetalDetectedCutSketchDimensionCount = [int] $detectedCutFeatures.sketchDimensionCount
$sheetMetalSuppressedCutFeatureNames = @($detectedCutFeatures.suppressedFeatureNames)
$sheetMetalSuppressedCutFeatureCount = [int] $detectedCutFeatures.suppressedFeatureCount
if ($sheetMetalRuleBindingStatus -eq 'bound_to_1000w_gold_dxf' -and $sheetMetalHoleDatumCount -gt 0) {
  $manualSuppressFailures = @($panelResult.manuallySuppressedFeatures | Where-Object { -not [bool] $_.edit_suppress_ok })
  if ($manualSuppressFailures.Count -gt 0) {
    $sheetMetalHoleFeatureStatus = 'template_cut_feature_configuration_failed_gold_dxf_datums_ready'
  }
  elseif ($sheetMetalDetectedCutFeatureCount -gt 0) {
    $sheetMetalHoleFeatureStatus = 'template_cut_features_configured_from_gold_dxf_datums_pending_direct_rebuild'
  }
  else {
    $sheetMetalHoleFeatureStatus = 'gold_dxf_datums_ready_no_template_cut_features_detected'
  }
}

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
Wait-File $topLatchStep 'mirrored top latch STEP'
Wait-File $topLatchReport 'mirrored top latch report'

$importer = Join-Path $toolDir 'bin\ImportStepSaveNative.exe'
Invoke-External $importer @($topLatchStep, $topLatchPart, $topLatchRoundtripStep, $topLatchImportJson) 'save mirrored top latch as SolidWorks native'
Wait-File $topLatchPart 'mirrored top latch SolidWorks part'
Wait-File $topLatchRoundtripStep 'mirrored top latch roundtrip STEP'
Wait-File $topLatchImportJson 'mirrored top latch import json'

$weldBuilder = Join-Path $toolDir 'bin\BuildDoorWeldModule.exe'
Invoke-External $weldBuilder @($weldAsm, $weldJson, (Invariant $DoorHeightMm), $panelPart, $stiffenerPart, $latchPlate, $hookPad, $Handedness, (Invariant $DoorWidthMm), "top-latch=$topLatchPart") 'build door weld assembly'
Wait-File $weldAsm 'door weld assembly'
Wait-File $weldJson 'door weld assembly result json'

$ordinaryBuilder = Join-Path $toolDir 'bin\BuildOrdinaryDoorModule.exe'
Invoke-External $ordinaryBuilder @($ordinaryAsm, $ordinaryJson, (Invariant $DoorHeightMm), $weldAsm, $plasticBushing, $hingePin, $circlip, $lockHook, $Handedness, (Invariant $DoorWidthMm), 'skip-electric-lock-hook') 'build ordinary door assembly'
Wait-File $ordinaryAsm 'ordinary door assembly'
Wait-File $ordinaryJson 'ordinary door assembly result json'

$exportScript = Join-Path $toolDir 'sw_export_model_step.js'
Invoke-External $cscript @('//Nologo', $exportScript, $weldAsm, $weldStep, $weldExportJson) 'export door weld assembly STEP evidence'
Invoke-External $cscript @('//Nologo', $exportScript, $ordinaryAsm, $ordinaryStep, $ordinaryExportJson) 'export ordinary door assembly STEP evidence'
Wait-File $weldStep 'door weld assembly STEP evidence'
Wait-File $weldExportJson 'door weld assembly export json'
Wait-File $ordinaryStep 'ordinary door assembly STEP evidence'
Wait-File $ordinaryExportJson 'ordinary door assembly export json'

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
  doorUnit = $DoorUnit
  sheetMetalRule = $SheetMetalRuleJson
  sheetMetalRuleBindingStatus = $sheetMetalRuleBindingStatus
  sheetMetalRuleClassId = $sheetMetalRuleClassId
  sheetMetalSourceDxf = $sheetMetalSourceDxf
  sheetMetalExpectedFlatWidthMm = $sheetMetalExpectedFlatWidthMm
  sheetMetalExpectedFlatHeightMm = $sheetMetalExpectedFlatHeightMm
  sheetMetalHoleDatumCount = $sheetMetalHoleDatumCount
  sheetMetalHoleDatums = @($sheetMetalHoleDatums)
  sheetMetalHoleFeatureStatus = $sheetMetalHoleFeatureStatus
  sheetMetalAppliedFeatureOperations = @($sheetMetalAppliedFeatureOperations)
  sheetMetalDetectedCutFeatureNames = @($sheetMetalDetectedCutFeatureNames)
  sheetMetalDetectedCutFeatureCount = $sheetMetalDetectedCutFeatureCount
  sheetMetalDetectedCutSketchDimensionCount = $sheetMetalDetectedCutSketchDimensionCount
  sheetMetalSuppressedCutFeatureNames = @($sheetMetalSuppressedCutFeatureNames)
  sheetMetalSuppressedCutFeatureCount = $sheetMetalSuppressedCutFeatureCount
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
  boundary = 'SolidWorks 2020 native sheet-metal model; FreeCAD was used only for internal mirrored latch evidence. Gold DXF hole data is carried as datum evidence until direct SolidWorks cut-feature generation is implemented.'
  outputFiles = @($outputFiles)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryJson -Encoding UTF8
Write-Output $summaryJson
