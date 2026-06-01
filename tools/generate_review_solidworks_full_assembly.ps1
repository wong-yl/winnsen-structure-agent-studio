param(
  [Parameter(Mandatory = $true)]
  [string] $RequestId,

  [double] $CabinetWidthMm = 740,
  [double] $CabinetHeightMm = 1917,
  [double] $CabinetDepthMm = 550,

  [int] $Columns = 2,
  [int] $DoorCount = 6,
  [double] $DoorWidthMm = 0,
  [double] $DoorHeightMm = 0,
  [string] $RowSequence = 'L642-R246',
  [string] $Prompt = '',

  [string] $OutputDir = '',
  [int] $ComponentRenameTimeoutSeconds = 0
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invariant([double] $Value) {
  return $Value.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
}

function Token([double] $Value) {
  return (Invariant $Value).Replace('.', 'p')
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

function Assert-Dir([string] $Path, [string] $Label) {
  if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    throw "$Label was not found: $Path"
  }
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

function Invoke-ExternalWithTimeout(
  [string] $FilePath,
  [string[]] $Arguments,
  [string] $Label,
  [int] $TimeoutSeconds,
  [int[]] $AllowedExitCodes = @(0)
) {
  Write-Host "[$Label] $FilePath $($Arguments -join ' ')"
  $stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) ("locker_16029_stdout_{0}.log" -f ([guid]::NewGuid().ToString('N')))
  $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ("locker_16029_stderr_{0}.log" -f ([guid]::NewGuid().ToString('N')))
  $process = $null
  try {
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $finished = $process.WaitForExit([Math]::Max(1, $TimeoutSeconds) * 1000)
    if (-not $finished) {
      try {
        $process.Kill()
      }
      catch {
      }
      throw "$Label timed out after $TimeoutSeconds seconds"
    }
    if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) {
      Get-Content -LiteralPath $stdoutPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    }
    if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
      Get-Content -LiteralPath $stderrPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    }
    if ($AllowedExitCodes -notcontains [int] $process.ExitCode) {
      throw "$Label failed with exit code $($process.ExitCode)"
    }
  }
  finally {
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
  }
}

function Read-Json([string] $Path) {
  return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-JsonInt($Object, [string] $Name, [int] $Fallback = 0) {
  if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) {
    return [int] $Object.PSObject.Properties[$Name].Value
  }
  return $Fallback
}

function Get-BoxNumber($Component, [string] $Name) {
  if ($null -eq $Component -or $null -eq $Component.box -or $null -eq $Component.box.PSObject.Properties[$Name]) {
    return $null
  }
  return [double] $Component.box.PSObject.Properties[$Name].Value
}

function New-EmptyEnvelope() {
  return [ordered] @{
    xminMm = $null
    yminMm = $null
    zminMm = $null
    xmaxMm = $null
    ymaxMm = $null
    zmaxMm = $null
  }
}

function Add-ComponentToEnvelope($Envelope, $Component) {
  $pairs = @(
    @('xminMm', 'xmin_mm', $true),
    @('yminMm', 'ymin_mm', $true),
    @('zminMm', 'zmin_mm', $true),
    @('xmaxMm', 'xmax_mm', $false),
    @('ymaxMm', 'ymax_mm', $false),
    @('zmaxMm', 'zmax_mm', $false)
  )
  foreach ($pair in $pairs) {
    $target = [string] $pair[0]
    $source = [string] $pair[1]
    $isMin = [bool] $pair[2]
    $value = Get-BoxNumber $Component $source
    if ($null -eq $value) {
      continue
    }
    if ($null -eq $Envelope[$target]) {
      $Envelope[$target] = [Math]::Round($value, 3)
    } elseif ($isMin -and $value -lt [double] $Envelope[$target]) {
      $Envelope[$target] = [Math]::Round($value, 3)
    } elseif ((-not $isMin) -and $value -gt [double] $Envelope[$target]) {
      $Envelope[$target] = [Math]::Round($value, 3)
    }
  }
}

function Test-ComponentInsideEnvelope($Component, $Envelope, [double] $ToleranceMm = 2.0) {
  foreach ($name in @('xminMm', 'yminMm', 'zminMm', 'xmaxMm', 'ymaxMm', 'zmaxMm')) {
    if ($null -eq $Envelope[$name]) {
      return $false
    }
  }
  $xmin = Get-BoxNumber $Component 'xmin_mm'
  $ymin = Get-BoxNumber $Component 'ymin_mm'
  $zmin = Get-BoxNumber $Component 'zmin_mm'
  $xmax = Get-BoxNumber $Component 'xmax_mm'
  $ymax = Get-BoxNumber $Component 'ymax_mm'
  $zmax = Get-BoxNumber $Component 'zmax_mm'
  if ($null -eq $xmin -or $null -eq $ymin -or $null -eq $zmin -or $null -eq $xmax -or $null -eq $ymax -or $null -eq $zmax) {
    return $false
  }
  return $xmin -ge ([double] $Envelope['xminMm'] - $ToleranceMm) -and
    $ymin -ge ([double] $Envelope['yminMm'] - $ToleranceMm) -and
    $zmin -ge ([double] $Envelope['zminMm'] - $ToleranceMm) -and
    $xmax -le ([double] $Envelope['xmaxMm'] + $ToleranceMm) -and
    $ymax -le ([double] $Envelope['ymaxMm'] + $ToleranceMm) -and
    $zmax -le ([double] $Envelope['zmaxMm'] + $ToleranceMm)
}

$root = Split-Path -Parent $PSScriptRoot
$toolDir = Join-Path $root 'workers\solidworks_tools'
$rulePlanner = Join-Path $root 'tools\locker_16029_template_rules.mjs'
$moduleTargetsTool = Join-Path $root 'tools\locker_16029_gold_module_targets.mjs'
$structureFeedbackTool = Join-Path $root 'tools\locker_16029_structure_feedback.mjs'
$templateRoot = Join-Path $root 'workers\generated_models\SW-NATIVE-16029-740W-1917H-550D-L642-R246-ORDINARY-20260528'
$sourceAssembly = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue.SLDASM'
$sourceResult = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue_result.json'
$sourceComponents = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue_components.json'
$sourcePlacements = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue_placements.tsv'
$captureDir = Join-Path $templateRoot 'v43_full\review_captures_latest'
$goldStructureTrace = Join-Path $root 'workers\generation_logs\gold_source_trace_16029\original_16029_total_assembly_structure.json'
$NodeExe = $env:STUDIO_NODE_EXE
if ([string]::IsNullOrWhiteSpace($NodeExe) -or -not (Test-Path -LiteralPath $NodeExe -PathType Leaf)) {
  $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
  $NodeExe = if ($nodeCommand) { $nodeCommand.Source } else { 'node' }
}

Assert-File $sourceAssembly 'template full assembly'
Assert-File $sourceResult 'template build result'
Assert-File $sourceComponents 'template component inspection'
Assert-File $sourcePlacements 'template placement table'
Assert-Dir $captureDir 'template review captures'
Assert-File $rulePlanner 'template rule planner'
Assert-File $moduleTargetsTool 'gold-source module target planner'
Assert-File $structureFeedbackTool 'template structure feedback analyzer'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $folder = "sw2020_full_$(Token $CabinetWidthMm)W_parametric_template"
  $OutputDir = Join-Path (Join-Path $root "workers\generated_models\review_generation_requests\$RequestId") $folder
}
$OutputDir = [IO.Path]::GetFullPath($OutputDir)

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$packDir = Join-Path $OutputDir 'pack_and_go'
$evidenceDir = Join-Path $OutputDir 'evidence'
$captureOutDir = Join-Path $OutputDir 'review_captures'
New-Item -ItemType Directory -Force -Path $packDir | Out-Null
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
New-Item -ItemType Directory -Force -Path $captureOutDir | Out-Null

$planJson = Join-Path $evidenceDir 'template_rule_plan.json'
$planArgs = @(
  $rulePlanner,
  '--out', $planJson,
  '--cabinet-width', (Invariant $CabinetWidthMm),
  '--cabinet-height', (Invariant $CabinetHeightMm),
  '--cabinet-depth', (Invariant $CabinetDepthMm),
  '--columns', ([string] $Columns),
  '--door-count', ([string] $DoorCount),
  '--row-sequence', $RowSequence,
  '--prompt', $Prompt
)
if ($DoorWidthMm -gt 0) {
  $planArgs += @('--door-width', (Invariant $DoorWidthMm))
}
if ($DoorHeightMm -gt 0) {
  $planArgs += @('--door-height', (Invariant $DoorHeightMm))
}
Invoke-External $NodeExe $planArgs 'plan template full assembly rule'
Wait-File $planJson 'template rule plan json'
$rulePlan = Read-Json $planJson

$moduleTargetsJson = Join-Path $evidenceDir 'gold_source_module_targets.json'
$moduleTargetsTsv = Join-Path $evidenceDir 'gold_source_module_targets.tsv'
$moduleTargetsBuildPlanTsv = Join-Path $evidenceDir 'gold_source_module_rebuild_plan.tsv'
$moduleTargetsFixedPlacementsTsv = Join-Path $evidenceDir 'gold_source_fixed_cabinet_module_placements.tsv'
$moduleTargetsShelfCandidatePlacementsTsv = Join-Path $evidenceDir 'gold_source_shelf_binding_candidate_placements.tsv'
$moduleTargetsCabinetCandidatePlacementsTsv = Join-Path $evidenceDir 'gold_source_cabinet_body_candidate_placements.tsv'
$moduleTargetsFullCandidatePlacementsTsv = Join-Path $evidenceDir 'gold_source_full_assembly_candidate_placements.tsv'
$moduleTargetArgs = @(
  $moduleTargetsTool,
  '--plan', $planJson,
  '--out', $moduleTargetsJson,
  '--tsv', $moduleTargetsTsv,
  '--build-plan', $moduleTargetsBuildPlanTsv,
  '--fixed-placements', $moduleTargetsFixedPlacementsTsv,
  '--shelf-candidate-placements', $moduleTargetsShelfCandidatePlacementsTsv,
  '--cabinet-candidate-placements', $moduleTargetsCabinetCandidatePlacementsTsv,
  '--template-placements', $sourcePlacements,
  '--full-candidate-placements', $moduleTargetsFullCandidatePlacementsTsv
)
if (Test-Path -LiteralPath $goldStructureTrace -PathType Leaf) {
  $moduleTargetArgs += @('--gold-structure', $goldStructureTrace)
}
Invoke-External $NodeExe $moduleTargetArgs 'plan gold-source cabinet module targets'
Wait-File $moduleTargetsJson 'gold-source module targets json'
Wait-File $moduleTargetsTsv 'gold-source module targets tsv'
Wait-File $moduleTargetsBuildPlanTsv 'gold-source module rebuild plan tsv'
Wait-File $moduleTargetsFixedPlacementsTsv 'gold-source fixed cabinet module placements tsv'
Wait-File $moduleTargetsShelfCandidatePlacementsTsv 'gold-source shelf binding candidate placements tsv'
Wait-File $moduleTargetsCabinetCandidatePlacementsTsv 'gold-source cabinet body candidate placements tsv'
Wait-File $moduleTargetsFullCandidatePlacementsTsv 'gold-source full assembly candidate placements tsv'
$moduleTargets = Read-Json $moduleTargetsJson

Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_pack_and_go_assembly.ps1')) 'compile pack-and-go assembly tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_inspect_assembly_components.ps1')) 'compile assembly structure inspection tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_rename_assembly_components.ps1')) 'compile assembly component rename tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_placed_components_module.ps1')) 'compile placed-components assembly tool'

$fixedModuleDir = Join-Path $evidenceDir 'ref_body'
New-Item -ItemType Directory -Force -Path $fixedModuleDir | Out-Null
$fixedModuleAssembly = Join-Path $fixedModuleDir 'body_fixed.SLDASM'
$fixedModuleBuildJson = Join-Path $fixedModuleDir 'body_fixed_build.json'
$placedTool = Join-Path $toolDir 'bin\BuildPlacedComponentsModule.exe'
Assert-File $placedTool 'placed-components assembly tool'
Invoke-External $placedTool @($moduleTargetsFixedPlacementsTsv, $fixedModuleAssembly, $fixedModuleBuildJson) 'build fixed cabinet source-reference module assembly'
Wait-File $fixedModuleBuildJson 'fixed cabinet source-reference module build json'
$fixedModuleBuild = Read-Json $fixedModuleBuildJson
if (-not $fixedModuleBuild.saved) {
  throw "Fixed cabinet source-reference module assembly was not saved: $fixedModuleBuildJson"
}
$fixedModuleStructureJson = Join-Path $fixedModuleDir 'body_fixed_structure.json'
$inspectTool = Join-Path $toolDir 'bin\InspectAssemblyComponents.exe'
Assert-File $inspectTool 'assembly structure inspection tool'
Invoke-External $inspectTool @($fixedModuleAssembly, $fixedModuleStructureJson) 'inspect fixed cabinet source-reference module assembly'
Wait-File $fixedModuleStructureJson 'fixed cabinet source-reference module structure json'
$fixedModuleStructure = Read-Json $fixedModuleStructureJson
if (-not $fixedModuleStructure.opened -or $fixedModuleStructure.component_count -le 0) {
  throw "Fixed cabinet source-reference structure inspection did not produce usable component data: $fixedModuleStructureJson"
}
$fixedModuleTopLevelCount = @($fixedModuleStructure.components | Where-Object { $_.depth -eq 0 }).Count

$shelfCandidateDir = Join-Path $evidenceDir 'ref_shelf'
$shelfCandidateAssembly = Join-Path $shelfCandidateDir 'shelf_candidate.SLDASM'
$shelfCandidateBuildJson = Join-Path $shelfCandidateDir 'shelf_candidate_build.json'
$shelfCandidateStructureJson = Join-Path $shelfCandidateDir 'shelf_candidate_structure.json'
$shelfCandidateBuild = $null
$shelfCandidateStructure = $null
$shelfCandidateTopLevelCount = 0
$shelfCandidateComponentCount = 0
$shelfCandidateStatus = 'not_generated_no_candidate_rows'
$shelfCandidateTargetCount = Get-JsonInt $moduleTargets.derived 'shelfCandidateTargetCount' 0
if ($shelfCandidateTargetCount -gt 0) {
  New-Item -ItemType Directory -Force -Path $shelfCandidateDir | Out-Null
  Invoke-External $placedTool @($moduleTargetsShelfCandidatePlacementsTsv, $shelfCandidateAssembly, $shelfCandidateBuildJson) 'build shelf binding source-reference candidate assembly'
  Wait-File $shelfCandidateBuildJson 'shelf binding candidate build json'
  $shelfCandidateBuild = Read-Json $shelfCandidateBuildJson
  if (-not $shelfCandidateBuild.saved) {
    throw "Shelf binding candidate assembly was not saved: $shelfCandidateBuildJson"
  }
  Invoke-External $inspectTool @($shelfCandidateAssembly, $shelfCandidateStructureJson) 'inspect shelf binding source-reference candidate assembly'
  Wait-File $shelfCandidateStructureJson 'shelf binding candidate structure json'
  $shelfCandidateStructure = Read-Json $shelfCandidateStructureJson
  if (-not $shelfCandidateStructure.opened -or $shelfCandidateStructure.component_count -le 0) {
    throw "Shelf binding candidate structure inspection did not produce usable component data: $shelfCandidateStructureJson"
  }
  $shelfCandidateTopLevelCount = @($shelfCandidateStructure.components | Where-Object { $_.depth -eq 0 }).Count
  $shelfCandidateComponentCount = $shelfCandidateStructure.component_count
  $shelfCandidateStatus = 'needs_engineering_validation'
}

$cabinetCandidateDir = Join-Path $evidenceDir 'ref_cabinet'
$cabinetCandidateAssembly = Join-Path $cabinetCandidateDir 'cabinet_candidate.SLDASM'
$cabinetCandidateBuildJson = Join-Path $cabinetCandidateDir 'cabinet_candidate_build.json'
$cabinetCandidateStructureJson = Join-Path $cabinetCandidateDir 'cabinet_candidate_structure.json'
$cabinetCandidateBuild = $null
$cabinetCandidateStructure = $null
$cabinetCandidateTopLevelCount = 0
$cabinetCandidateComponentCount = 0
$cabinetCandidateStatus = 'not_generated_no_candidate_rows'
$cabinetCandidateBboxStatus = 'not_available'
$cabinetCandidateShelfInsideBodyEnvelope = $false
$cabinetCandidateShelfBboxCount = 0
$cabinetCandidateFixedBodyBboxCount = 0
$cabinetCandidateBodyEnvelope = New-EmptyEnvelope
$cabinetCandidateTargetCount = Get-JsonInt $moduleTargets.derived 'cabinetCandidateTargetCount' 0
if ($cabinetCandidateTargetCount -gt 0) {
  New-Item -ItemType Directory -Force -Path $cabinetCandidateDir | Out-Null
  Invoke-External $placedTool @($moduleTargetsCabinetCandidatePlacementsTsv, $cabinetCandidateAssembly, $cabinetCandidateBuildJson) 'build cabinet body source-reference candidate assembly'
  Wait-File $cabinetCandidateBuildJson 'cabinet body candidate build json'
  $cabinetCandidateBuild = Read-Json $cabinetCandidateBuildJson
  if (-not $cabinetCandidateBuild.saved) {
    throw "Cabinet body candidate assembly was not saved: $cabinetCandidateBuildJson"
  }
  Invoke-External $inspectTool @($cabinetCandidateAssembly, $cabinetCandidateStructureJson) 'inspect cabinet body source-reference candidate assembly'
  Wait-File $cabinetCandidateStructureJson 'cabinet body candidate structure json'
  $cabinetCandidateStructure = Read-Json $cabinetCandidateStructureJson
  if (-not $cabinetCandidateStructure.opened -or $cabinetCandidateStructure.component_count -le 0) {
    throw "Cabinet body candidate structure inspection did not produce usable component data: $cabinetCandidateStructureJson"
  }
  $cabinetCandidateTopLevelCount = @($cabinetCandidateStructure.components | Where-Object { $_.depth -eq 0 }).Count
  $cabinetCandidateComponentCount = $cabinetCandidateStructure.component_count
  $cabinetCandidateStatus = 'needs_engineering_validation'
  $cabinetCandidateTopLevelComponents = @($cabinetCandidateStructure.components | Where-Object { $_.depth -eq 0 -and -not $_.is_hidden -and -not $_.is_suppressed })
  $shelfNameMarker = -join ([char[]](0x6A2A, 0x5C42, 0x677F))
  $cabinetCandidateFixedBodyComponents = @($cabinetCandidateTopLevelComponents | Where-Object { -not ([string] $_.name).Contains($shelfNameMarker) -and $null -ne $_.box })
  $cabinetCandidateShelfComponents = @($cabinetCandidateTopLevelComponents | Where-Object { ([string] $_.name).Contains($shelfNameMarker) -and $null -ne $_.box })
  $cabinetCandidateFixedBodyBboxCount = $cabinetCandidateFixedBodyComponents.Count
  $cabinetCandidateShelfBboxCount = $cabinetCandidateShelfComponents.Count
  foreach ($component in $cabinetCandidateFixedBodyComponents) {
    Add-ComponentToEnvelope $cabinetCandidateBodyEnvelope $component
  }
  $cabinetCandidateShelfInsideBodyEnvelope = $cabinetCandidateShelfBboxCount -gt 0
  foreach ($component in $cabinetCandidateShelfComponents) {
    if (-not (Test-ComponentInsideEnvelope $component $cabinetCandidateBodyEnvelope 2.0)) {
      $cabinetCandidateShelfInsideBodyEnvelope = $false
      break
    }
  }
  if ($cabinetCandidateFixedBodyBboxCount -eq 0 -or $cabinetCandidateShelfBboxCount -eq 0) {
    $cabinetCandidateBboxStatus = 'missing_component_bbox'
  } elseif ($cabinetCandidateShelfInsideBodyEnvelope) {
    $cabinetCandidateBboxStatus = 'bbox_validated_inside_fixed_cabinet_envelope'
  } else {
    $cabinetCandidateBboxStatus = 'shelf_bbox_outside_fixed_cabinet_envelope'
  }
}

$fullCandidateDir = Join-Path $evidenceDir 'ref_full'
$fullCandidateAssembly = Join-Path $fullCandidateDir 'full_candidate.SLDASM'
$fullCandidateBuildJson = Join-Path $fullCandidateDir 'full_candidate_build.json'
$fullCandidateStructureJson = Join-Path $fullCandidateDir 'full_candidate_structure.json'
$fullCandidateBuild = $null
$fullCandidateStructure = $null
$fullCandidateTopLevelCount = 0
$fullCandidateComponentCount = 0
$fullCandidateStatus = 'not_generated_no_candidate_rows'
$packSourceAssembly = $sourceAssembly
if ($cabinetCandidateTargetCount -gt 0) {
  New-Item -ItemType Directory -Force -Path $fullCandidateDir | Out-Null
  Invoke-External $placedTool @($moduleTargetsFullCandidatePlacementsTsv, $fullCandidateAssembly, $fullCandidateBuildJson) 'build full assembly source-reference candidate with cabinet weldment modules'
  Wait-File $fullCandidateBuildJson 'full assembly candidate build json'
  $fullCandidateBuild = Read-Json $fullCandidateBuildJson
  if (-not $fullCandidateBuild.saved) {
    throw "Full assembly source-reference candidate was not saved: $fullCandidateBuildJson"
  }
  Invoke-External $inspectTool @($fullCandidateAssembly, $fullCandidateStructureJson) 'inspect full assembly source-reference candidate'
  Wait-File $fullCandidateStructureJson 'full assembly candidate structure json'
  $fullCandidateStructure = Read-Json $fullCandidateStructureJson
  if (-not $fullCandidateStructure.opened -or $fullCandidateStructure.component_count -le 0) {
    throw "Full assembly candidate structure inspection did not produce usable component data: $fullCandidateStructureJson"
  }
  $fullCandidateTopLevelCount = @($fullCandidateStructure.components | Where-Object { $_.depth -eq 0 }).Count
  $fullCandidateComponentCount = $fullCandidateStructure.component_count
  $fullCandidateStatus = 'needs_engineering_validation'
  $packSourceAssembly = $fullCandidateAssembly
}

$packJson = Join-Path $evidenceDir 'solidworks_2020_pack_and_go_result.json'
$packTool = Join-Path $toolDir 'bin\PackAndGoAssembly.exe'
Assert-File $packTool 'pack-and-go assembly tool'
Invoke-External $packTool @($packSourceAssembly, $packDir, $packJson) 'pack SolidWorks 2020 full assembly'
Wait-File $packJson 'pack-and-go result json'
$packResult = Read-Json $packJson
if (-not $packResult.opened -or -not $packResult.pack_and_go_created -or -not $packResult.inventory -or $packResult.inventory.file_count -le 0) {
  throw "Pack-and-Go did not produce a usable package: $packJson"
}
$packAndGoChineseSaveNameMapCount = Get-JsonInt $packResult 'chinese_save_name_map_count' 0
$packAndGoSetDocumentSaveToNames = if ($null -ne $packResult.PSObject.Properties['set_document_save_to_names']) { [bool] $packResult.set_document_save_to_names } else { $false }
$packAndGoGotDocumentSaveToNames = if ($null -ne $packResult.PSObject.Properties['got_document_save_to_names']) { [bool] $packResult.got_document_save_to_names } else { $false }

$primaryAssembly = Join-Path $packDir (Split-Path -Leaf $packSourceAssembly)
if (-not (Test-Path -LiteralPath $primaryAssembly -PathType Leaf)) {
  Copy-Item -LiteralPath $packSourceAssembly -Destination $primaryAssembly -Force
}

$componentRenameJson = Join-Path $evidenceDir 'solidworks_2020_component_rename.json'
$renameTool = Join-Path $toolDir 'bin\RenameAssemblyComponents.exe'
$componentRenameRunStatus = 'skipped'
$componentRenameError = ''
$componentRename = $null

if ($ComponentRenameTimeoutSeconds -le 0) {
  $componentRenameError = 'component rename skipped; SolidWorks Name2 normalization remains advisory'
  $componentRename = [pscustomobject]@{
    status = $componentRenameRunStatus
    error = $componentRenameError
    attempted_count = 0
    assigned_count = 0
    verified_count = 0
    renamed_count = 0
    renames = @()
  }
  $componentRename | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $componentRenameJson -Encoding UTF8
}
else {
  Assert-File $renameTool 'assembly component rename tool'
  $componentRenameRunStatus = 'completed'
  try {
    Invoke-ExternalWithTimeout $renameTool @($primaryAssembly, $componentRenameJson) 'normalize engineer-visible SolidWorks component names' $ComponentRenameTimeoutSeconds
    Wait-File $componentRenameJson 'SolidWorks 2020 component rename json'
    $componentRename = Read-Json $componentRenameJson
  }
  catch {
    $componentRenameRunStatus = 'skipped_or_timed_out'
    $componentRenameError = $_.Exception.Message
    Write-Warning "Engineer-visible component name normalization did not complete: $componentRenameError"
    $componentRename = [pscustomobject]@{
      status = $componentRenameRunStatus
      error = $componentRenameError
      attempted_count = 0
      assigned_count = 0
      verified_count = 0
      renamed_count = 0
      renames = @()
    }
    $componentRename | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $componentRenameJson -Encoding UTF8
  }
}

$structureRecordJson = Join-Path $evidenceDir 'solidworks_2020_structure_record.json'
Assert-File $inspectTool 'assembly structure inspection tool'
Invoke-External $inspectTool @($primaryAssembly, $structureRecordJson) 'inspect Pack-and-Go SolidWorks 2020 assembly structure'
Wait-File $structureRecordJson 'SolidWorks 2020 structure record json'
$structureRecord = Read-Json $structureRecordJson
if (-not $structureRecord.opened -or $structureRecord.component_count -le 0) {
  throw "SolidWorks structure inspection did not produce usable component data: $structureRecordJson"
}
$structureRecordExternalReferenceCount = @($structureRecord.components | Where-Object {
  $_.path -and (-not $_.path.StartsWith($packDir, [System.StringComparison]::OrdinalIgnoreCase))
}).Count
if ($structureRecordExternalReferenceCount -gt 0) {
  throw "Pack-and-Go structure record contains $structureRecordExternalReferenceCount external component references outside: $packDir"
}
$structureRecordTopLevelCount = @($structureRecord.components | Where-Object { $_.depth -eq 0 }).Count
$structureRecordMaxDepth = ($structureRecord.components.depth | Measure-Object -Maximum).Maximum

$structureFeedbackJson = Join-Path $evidenceDir 'structure_feedback.json'
Invoke-External $NodeExe @(
  $structureFeedbackTool,
  '--plan', $planJson,
  '--structure', $structureRecordJson,
  '--module-targets', $moduleTargetsJson,
  '--out', $structureFeedbackJson
) 'analyze SolidWorks 2020 structure against gold-source feedback'
Wait-File $structureFeedbackJson 'SolidWorks 2020 structure feedback json'
$structureFeedback = Read-Json $structureFeedbackJson
$componentRenameFallbackCount = @($componentRename.renames).Count
$componentRenameAttemptCount = Get-JsonInt $componentRename 'attempted_count' $componentRenameFallbackCount
$componentRenameAssignedCount = Get-JsonInt $componentRename 'assigned_count' (Get-JsonInt $componentRename 'renamed_count' 0)
$componentRenameVerifiedCount = Get-JsonInt $componentRename 'verified_count' (Get-JsonInt $componentRename 'renamed_count' 0)
$structureFeedbackIssueCount = Get-JsonInt $structureFeedback.derived 'issueCount' 0
$structureFeedbackP0Count = Get-JsonInt $structureFeedback.derived 'p0Count' 0
$structureFeedbackP1Count = Get-JsonInt $structureFeedback.derived 'p1Count' 0
$structureFeedbackP2Count = Get-JsonInt $structureFeedback.derived 'p2Count' 0
$structureNeedsRevision = $structureFeedbackIssueCount -gt 0
$componentNamingStatus = if ($structureFeedbackP2Count -gt 0) { 'still_flagged_by_structure_feedback' } else { 'clean_after_solidworks_reopen_inspection' }
$handoffReadinessStatus = if ($structureNeedsRevision) { 'needs_structure_revision' } else { 'engineer_ready' }

Copy-Item -LiteralPath $sourceResult -Destination (Join-Path $evidenceDir 'template_build_result.json') -Force
Copy-Item -LiteralPath $sourceComponents -Destination (Join-Path $evidenceDir 'template_components.json') -Force
Copy-Item -LiteralPath $sourcePlacements -Destination (Join-Path $evidenceDir 'template_placements.tsv') -Force
Get-ChildItem -LiteralPath $captureDir -File |
  Where-Object { -not $_.Name.StartsWith('~$') } |
  Copy-Item -Destination $captureOutDir -Force

$readme = @(
  '# SolidWorks 2020 Full Assembly Review Package',
  '',
  'This package is generated from the review queue by using the current verified 740W L642/R246 full-assembly template rule.',
  '',
  '## Boundary',
  '',
  '- CAD mainline: SolidWorks 2020.',
  '- This package is a review/download package, not a production drawing release.',
  "- Requested route: $(Invariant $CabinetWidthMm)W x $(Invariant $CabinetHeightMm)H x $(Invariant $CabinetDepthMm)D, $Columns columns, $DoorCount doors, row sequence $RowSequence.",
  "- Template rule mode: $($rulePlan.route.mode).",
  "- Native-template exact match: $($rulePlan.derived.compatibleWithNativeTemplate).",
  "- Rule boundary: $($rulePlan.boundary)",
  "- Gold-source cabinet module target count: $($moduleTargets.derived.targetCount), shelves from row boundaries: $($moduleTargets.derived.shelfModuleCount), build-ready fixed modules: $($moduleTargets.derived.buildReadyTargetCount), source-reference fixed module top-level count: $fixedModuleTopLevelCount.",
  "- Shelf binding candidate: $shelfCandidateStatus, requested candidate shelves: $shelfCandidateTargetCount, SolidWorks top-level candidate count: $shelfCandidateTopLevelCount. This is evidence only, not engineer-ready geometry.",
  "- Cabinet body candidate: $cabinetCandidateStatus, requested cabinet candidate modules: $cabinetCandidateTargetCount, SolidWorks top-level candidate count: $cabinetCandidateTopLevelCount. This combines fixed cabinet weldments and row-boundary shelf candidates for validation.",
  "- Cabinet body candidate bbox check: $cabinetCandidateBboxStatus, shelf bbox count: $cabinetCandidateShelfBboxCount, fixed body bbox count: $cabinetCandidateFixedBodyBboxCount.",
  "- Full assembly candidate: $fullCandidateStatus, SolidWorks top-level candidate count: $fullCandidateTopLevelCount, component count: $fullCandidateComponentCount. The old shell-frame-shelf black-box placement is replaced by cabinet weldment modules before Pack-and-Go.",
  "- Pack-and-Go Chinese save-name normalization: mapped $packAndGoChineseSaveNameMapCount document save names, SetDocumentSaveToNames=$packAndGoSetDocumentSaveToNames.",
  "- Engineer-visible component name normalization: attempted $componentRenameAttemptCount, assigned $componentRenameAssignedCount, read-back verified $componentRenameVerifiedCount; post-inspection status: $componentNamingStatus.",
  "- Engineer structure feedback: $($structureFeedback.status), P0=$structureFeedbackP0Count, P1=$structureFeedbackP1Count, P2=$structureFeedbackP2Count.",
  "- Handoff readiness: $handoffReadinessStatus.",
  '- The historical direct per-part assembly generation route remains disabled because of transform reliability issues.',
  '',
  '## Files',
  '',
  '- `pack_and_go/`: flattened SolidWorks Pack-and-Go output containing `.SLDASM` and `.SLDPRT` files.',
  '- `evidence/`: template rule plan, gold-source module targets/rebuild plan, cabinet-body/full-assembly source-reference evidence, build result, component inspection, placement table, Pack-and-Go result JSON, and SolidWorks 2020 structure record JSON.',
  '- `review_captures/`: reference screenshots from the current verified assembly.',
  '- `solidworks_2020_full_assembly_generation_summary.json`: normalized queue summary.',
  ''
)
$readmePath = Join-Path $OutputDir 'README.md'
$readme | Set-Content -LiteralPath $readmePath -Encoding UTF8

$packFiles = Get-ChildItem -LiteralPath $packDir -File |
  Where-Object { -not $_.Name.StartsWith('~$') -and @('.sldasm', '.sldprt') -contains $_.Extension.ToLowerInvariant() } |
  Sort-Object FullName

$outputFiles = Get-ChildItem -LiteralPath $OutputDir -Recurse -File |
  Where-Object { -not $_.Name.StartsWith('~$') } |
  Sort-Object FullName |
  ForEach-Object { $_.FullName.Substring($OutputDir.Length).TrimStart('\') }

$summaryJson = Join-Path $OutputDir 'solidworks_2020_full_assembly_generation_summary.json'
$compatibleWithNativeTemplate = [bool] $rulePlan.derived.compatibleWithNativeTemplate
$summaryStatus = if ($structureNeedsRevision) { 'solidworks_2020_full_assembly_needs_structure_revision' } elseif ($compatibleWithNativeTemplate) { 'solidworks_2020_full_assembly_ready' } else { 'solidworks_2020_template_rule_package_ready' }
$summaryResultKind = if ($structureNeedsRevision) { 'solidworks2020_structure_revision_evidence_package' } elseif ($compatibleWithNativeTemplate) { 'solidworks2020_full_assembly_model' } else { 'solidworks2020_template_rule_full_assembly_package' }
$summary = [ordered] @{
  status = $summaryStatus
  resultKind = $summaryResultKind
  handoffReadinessStatus = $handoffReadinessStatus
  requestId = $RequestId
  cadMainline = 'SolidWorks 2020'
  cabinetWidthMm = $CabinetWidthMm
  cabinetHeightMm = $CabinetHeightMm
  cabinetDepthMm = $CabinetDepthMm
  columns = $Columns
  doorCount = $DoorCount
  doorWidthMm = $rulePlan.derived.doorWidthMm
  rowSequence = $RowSequence
  template = '740W_L642_R246_v43_hidden_lock_body_restored_tongue'
  templateRulePlan = $planJson
  templateRuleMode = $rulePlan.route.mode
  compatibleWithNativeTemplate = $compatibleWithNativeTemplate
  goldSourceModuleTargets = $moduleTargetsJson
  goldSourceModuleTargetsTsv = $moduleTargetsTsv
  goldSourceModuleRebuildPlanTsv = $moduleTargetsBuildPlanTsv
  goldSourceFixedCabinetModulePlacementsTsv = $moduleTargetsFixedPlacementsTsv
  goldSourceShelfCandidatePlacementsTsv = $moduleTargetsShelfCandidatePlacementsTsv
  goldSourceCabinetCandidatePlacementsTsv = $moduleTargetsCabinetCandidatePlacementsTsv
  goldSourceFullAssemblyCandidatePlacementsTsv = $moduleTargetsFullCandidatePlacementsTsv
  goldSourceModuleTargetCount = $moduleTargets.derived.targetCount
  goldSourceShelfModuleTargetCount = $moduleTargets.derived.shelfModuleCount
  goldSourceShelfCandidateTargetCount = $shelfCandidateTargetCount
  goldSourceCabinetCandidateTargetCount = $cabinetCandidateTargetCount
  goldSourceModuleBuildReadyTargetCount = $moduleTargets.derived.buildReadyTargetCount
  goldSourceModuleNeedsBindingTargetCount = $moduleTargets.derived.needsBindingTargetCount
  goldSourceModuleTargetsSourceStructureBound = $moduleTargets.derived.sourceStructureBound
  fixedCabinetSourceReferenceAssembly = $fixedModuleAssembly
  fixedCabinetSourceReferenceBuild = $fixedModuleBuildJson
  fixedCabinetSourceReferenceStructure = $fixedModuleStructureJson
  fixedCabinetSourceReferenceComponentCount = $fixedModuleStructure.component_count
  fixedCabinetSourceReferenceTopLevelCount = $fixedModuleTopLevelCount
  shelfBindingCandidateStatus = $shelfCandidateStatus
  shelfBindingCandidateAssembly = $shelfCandidateAssembly
  shelfBindingCandidateBuild = $shelfCandidateBuildJson
  shelfBindingCandidateStructure = $shelfCandidateStructureJson
  shelfBindingCandidateComponentCount = $shelfCandidateComponentCount
  shelfBindingCandidateTopLevelCount = $shelfCandidateTopLevelCount
  cabinetBodyCandidateStatus = $cabinetCandidateStatus
  cabinetBodyCandidateAssembly = $cabinetCandidateAssembly
  cabinetBodyCandidateBuild = $cabinetCandidateBuildJson
  cabinetBodyCandidateStructure = $cabinetCandidateStructureJson
  cabinetBodyCandidateComponentCount = $cabinetCandidateComponentCount
  cabinetBodyCandidateTopLevelCount = $cabinetCandidateTopLevelCount
  cabinetBodyCandidateBboxStatus = $cabinetCandidateBboxStatus
  cabinetBodyCandidateShelfInsideBodyEnvelope = $cabinetCandidateShelfInsideBodyEnvelope
  cabinetBodyCandidateShelfBboxCount = $cabinetCandidateShelfBboxCount
  cabinetBodyCandidateFixedBodyBboxCount = $cabinetCandidateFixedBodyBboxCount
  cabinetBodyCandidateBodyEnvelope = $cabinetCandidateBodyEnvelope
  fullAssemblyCandidateStatus = $fullCandidateStatus
  fullAssemblyCandidateAssembly = $fullCandidateAssembly
  fullAssemblyCandidateBuild = $fullCandidateBuildJson
  fullAssemblyCandidateStructure = $fullCandidateStructureJson
  fullAssemblyCandidateComponentCount = $fullCandidateComponentCount
  fullAssemblyCandidateTopLevelCount = $fullCandidateTopLevelCount
  sourceAssembly = $sourceAssembly
  packSourceAssembly = $packSourceAssembly
  outputDir = $OutputDir
  packAndGoDir = $packDir
  primaryAssembly = $primaryAssembly
  packAndGoResult = $packJson
  packAndGoChineseSaveNameMapCount = $packAndGoChineseSaveNameMapCount
  packAndGoGotDocumentSaveToNames = $packAndGoGotDocumentSaveToNames
  packAndGoSetDocumentSaveToNames = $packAndGoSetDocumentSaveToNames
  structureRecord = $structureRecordJson
  componentRename = $componentRenameJson
  componentRenameCount = $componentRenameVerifiedCount
  componentRenameAttemptCount = $componentRenameAttemptCount
  componentRenameAssignedCount = $componentRenameAssignedCount
  componentRenameVerifiedCount = $componentRenameVerifiedCount
  componentRenameRunStatus = $componentRenameRunStatus
  componentRenameError = $componentRenameError
  componentRenameTimeoutSeconds = $ComponentRenameTimeoutSeconds
  componentNamingStatus = $componentNamingStatus
  structureRecordEnumerationMode = $structureRecord.enumeration_mode
  structureRecordComponentCount = $structureRecord.component_count
  structureRecordTopLevelCount = $structureRecordTopLevelCount
  structureRecordMaxDepth = $structureRecordMaxDepth
  structureRecordExternalReferenceCount = $structureRecordExternalReferenceCount
  structureFeedback = $structureFeedbackJson
  structureFeedbackStatus = $structureFeedback.status
  structureFeedbackIssueCount = $structureFeedbackIssueCount
  structureFeedbackP0Count = $structureFeedbackP0Count
  structureFeedbackP1Count = $structureFeedbackP1Count
  structureFeedbackP2Count = $structureFeedbackP2Count
  sldasmCount = @($packFiles | Where-Object { $_.Extension.ToLowerInvariant() -eq '.sldasm' }).Count
  sldprtCount = @($packFiles | Where-Object { $_.Extension.ToLowerInvariant() -eq '.sldprt' }).Count
  totalMb = [Math]::Round((($packFiles | Measure-Object Length -Sum).Sum / 1048576.0), 3)
  modelGeneratedAt = (Get-Date).ToUniversalTime().ToString('o')
  boundary = $rulePlan.boundary
  outputFiles = @($outputFiles + 'solidworks_2020_full_assembly_generation_summary.json' | Select-Object -Unique)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryJson -Encoding UTF8
Write-Output $summaryJson
