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

function Assert-Dir([string] $Path, [string] $Label) {
  if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    throw "$Label was not found: $Path"
  }
}

function Reset-GeneratedSubdir([string] $Path, [string] $ParentPath, [string] $Label) {
  $fullPath = [IO.Path]::GetFullPath($Path)
  $fullParent = [IO.Path]::GetFullPath($ParentPath).TrimEnd('\')
  if (-not $fullPath.StartsWith($fullParent + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "$Label path is outside the generated output directory: $Path"
  }
  if (Test-Path -LiteralPath $fullPath) {
    Remove-Item -LiteralPath $fullPath -Recurse -Force
  }
  New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
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
    $process = Start-Process -FilePath $FilePath -ArgumentList (Join-ProcessArguments $Arguments) -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
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

function Invoke-StructureInspection(
  [string] $ToolPath,
  [string] $AssemblyPath,
  [string] $JsonPath,
  [string] $Label,
  [string] $WaitLabel
) {
  $lastError = $null
  for ($attempt = 1; $attempt -le 2; $attempt++) {
    if (Test-Path -LiteralPath $JsonPath -PathType Leaf) {
      Remove-Item -LiteralPath $JsonPath -Force -ErrorAction SilentlyContinue
    }
    $attemptLabel = if ($attempt -eq 1) { $Label } else { "$Label retry $attempt" }
    try {
      Invoke-External $ToolPath @($AssemblyPath, $JsonPath) $attemptLabel
      Wait-File $JsonPath $WaitLabel 30
      return
    }
    catch {
      $lastError = $_
      if ($attempt -lt 2) {
        Write-Warning "$Label did not produce a usable structure JSON; retrying once. $($_.Exception.Message)"
        Start-Sleep -Seconds 2
      }
    }
  }
  throw $lastError
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

function Get-ObjectString($Object, [string] $Name, [string] $Fallback = '') {
  if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) {
    return [string] $Object.PSObject.Properties[$Name].Value
  }
  return $Fallback
}

function Get-ObjectNumber($Object, [string] $Name, [double] $Fallback = 0) {
  if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) {
    $value = [double] $Object.PSObject.Properties[$Name].Value
    if (-not [double]::IsNaN($value) -and -not [double]::IsInfinity($value)) {
      return $value
    }
  }
  return $Fallback
}

function Get-ObjectArray($Object, [string] $Name) {
  if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) {
    return @($Object.PSObject.Properties[$Name].Value)
  }
  return @()
}

function Find-ScaffoldPart($Manifest, [string] $Key) {
  foreach ($part in @($Manifest.parts)) {
    if ([string] $part.key -eq $Key) {
      return $part
    }
  }
  return $null
}

function New-PlacementLine([string] $Role, [string] $Path, [double] $TxMm, [double] $TyMm, [double] $TzMm, [string] $Rotation = '1,0,0,0,1,0,0,0,1') {
  $safeRole = $Role.Replace("`t", ' ')
  $safePath = $Path.Replace("`t", ' ')
  return @($safeRole, $safePath, (Invariant $TxMm), (Invariant $TyMm), (Invariant $TzMm), $Rotation) -join "`t"
}

function Write-PlacementRows([string] $Path, [System.Collections.Generic.List[string]] $Rows) {
  $lines = [System.Collections.Generic.List[string]]::new()
  $lines.Add("role`tpath`ttx_mm`tty_mm`ttz_mm`trotation")
  foreach ($row in $Rows) {
    $lines.Add($row)
  }
  [IO.File]::WriteAllLines($Path, $lines, [Text.UTF8Encoding]::new($false))
}

function ConvertTo-SafeFileStem([string] $Text) {
  $safe = [Regex]::Replace($Text, '[\\/:*?"<>|]+', '_').Trim()
  $safe = [Regex]::Replace($safe, '\s+', '_')
  if ([string]::IsNullOrWhiteSpace($safe)) {
    $safe = 'parametric_scaffold_part'
  }
  if ($safe.Length -gt 110) {
    $safe = $safe.Substring(0, 110)
  }
  return $safe
}

function Use-NamedNativePart([string] $SourcePath, [string] $Role, [string] $NamedDir) {
  New-Item -ItemType Directory -Force -Path $NamedDir | Out-Null
  $target = Join-Path $NamedDir ("{0}.SLDPRT" -f (ConvertTo-SafeFileStem $Role))
  Copy-Item -LiteralPath $SourcePath -Destination $target -Force
  return $target
}

function Copy-NamedGeneratedDoorFile([string] $SourcePath, [string] $TargetName, [string] $NamedDir) {
  if ([string]::IsNullOrWhiteSpace($SourcePath) -or -not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
    return $null
  }
  New-Item -ItemType Directory -Force -Path $NamedDir | Out-Null
  $target = Join-Path $NamedDir $TargetName
  Copy-Item -LiteralPath $SourcePath -Destination $target -Force
  return $target
}

function Find-GeneratedDoorOutputFile($DoorSummary, [string] $OutputDir, [string] $Pattern) {
  foreach ($fileName in @($DoorSummary.outputFiles)) {
    if ([string] $fileName -match $Pattern) {
      $candidate = Join-Path $OutputDir ([string] $fileName)
      if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return $candidate
      }
    }
  }
  return ''
}

function New-GeneratedNativeDoorModuleRecord(
  [string] $Side,
  [string] $UnitText,
  [double] $DoorWidthMm,
  [double] $DoorHeightMm,
  [string] $Handedness,
  $DoorSummary,
  [string] $SummaryPath,
  [string] $DoorOutDir
) {
  $originalAssembly = [string] $DoorSummary.primaryAssembly
  Assert-File $originalAssembly 'generated native door module assembly'

  $ratioSeparator = TextFromCodes @(0x2571)
  $ratioLabel = "{0}{1}12" -f $UnitText, $ratioSeparator
  $sideLabel = if ($Handedness -eq 'right' -or $Side.ToUpperInvariant() -eq 'R') { TextFromCodes @(0x53f3) } else { TextFromCodes @(0x5de6) }
  $storageDoor = TextFromCodes @(0x50a8, 0x7269, 0x67dc, 0x95e8)
  $doorPanel = $storageDoor + (TextFromCodes @(0x677f))
  $assemblyText = TextFromCodes @(0x88c5, 0x914d)
  $weldText = TextFromCodes @(0x710a, 0x63a5)
  $stiffenerText = TextFromCodes @(0x67dc, 0x95e8, 0x52a0, 0x5f3a, 0x7b4b)
  $latchPlateText = TextFromCodes @(0x63d2, 0x9500, 0x56fa, 0x5b9a, 0x677f)
  $namedDir = Join-Path $DoorOutDir 'native_named'

  $namedFileMap = New-Object System.Collections.Generic.List[object]
  function Add-NamedGeneratedDoorFile([string] $Kind, [string] $SourcePath, [string] $TargetName) {
    $targetPath = Copy-NamedGeneratedDoorFile $SourcePath $TargetName $namedDir
    if ($null -eq $targetPath) {
      return ''
    }
    $namedFileMap.Add([pscustomobject] ([ordered] @{
      kind = $Kind
      source = $SourcePath
      named = $targetPath
    })) | Out-Null
    return $targetPath
  }

  $namedAssembly = Add-NamedGeneratedDoorFile 'ordinaryAssembly' $originalAssembly ("{0}{1}{2}_{3}.SLDASM" -f $storageDoor, $ratioLabel, $assemblyText, $sideLabel)
  $namedWeldAssembly = Add-NamedGeneratedDoorFile 'weldAssembly' ([string] $DoorSummary.weldAssembly) ("{0}{1}{2}_{3}.SLDASM" -f $storageDoor, $ratioLabel, $weldText, $sideLabel)
  $namedPanelPart = Add-NamedGeneratedDoorFile 'sheetMetalPanelPart' ([string] $DoorSummary.sheetMetalPanelPart) ("{0}{1}.SLDPRT" -f $doorPanel, $ratioLabel)
  $namedStiffenerPart = Add-NamedGeneratedDoorFile 'sheetMetalStiffenerPart' ([string] $DoorSummary.sheetMetalStiffenerPart) ("{0}{1}.SLDPRT" -f $stiffenerText, $ratioLabel)
  $topLatchPart = Find-GeneratedDoorOutputFile $DoorSummary $DoorOutDir '^top_latch_from_bottom_mirror_.*\.SLDPRT$'
  $namedTopLatchPart = Add-NamedGeneratedDoorFile 'topLatchPart' $topLatchPart ("{0}{1}_{2}.SLDPRT" -f $latchPlateText, $ratioLabel, $sideLabel)

  Assert-File $namedAssembly 'generated native door module Chinese-named assembly'
  return [pscustomobject] ([ordered] @{
    side = $Side
    unit = $UnitText
    ratio = "$UnitText/12"
    doorWidthMm = $DoorWidthMm
    doorHeightMm = $DoorHeightMm
    handedness = $Handedness
    assembly = $namedAssembly
    originalAssembly = $originalAssembly
    namedAssembly = $namedAssembly
    weldAssembly = $namedWeldAssembly
    sheetMetalPanelPart = $namedPanelPart
    sheetMetalStiffenerPart = $namedStiffenerPart
    sheetMetalRuleBindingStatus = Get-ObjectString $DoorSummary 'sheetMetalRuleBindingStatus'
    sheetMetalRuleClassId = Get-ObjectString $DoorSummary 'sheetMetalRuleClassId'
    sheetMetalSourceDxf = Get-ObjectString $DoorSummary 'sheetMetalSourceDxf'
    sheetMetalExpectedFlatWidthMm = Get-ObjectNumber $DoorSummary 'sheetMetalExpectedFlatWidthMm' 0
    sheetMetalExpectedFlatHeightMm = Get-ObjectNumber $DoorSummary 'sheetMetalExpectedFlatHeightMm' 0
    sheetMetalHoleDatumCount = Get-ObjectNumber $DoorSummary 'sheetMetalHoleDatumCount' 0
    sheetMetalHoleDatums = @(Get-ObjectArray $DoorSummary 'sheetMetalHoleDatums')
    sheetMetalHoleFeatureStatus = Get-ObjectString $DoorSummary 'sheetMetalHoleFeatureStatus'
    sheetMetalDetectedCutFeatureNames = @(Get-ObjectArray $DoorSummary 'sheetMetalDetectedCutFeatureNames')
    sheetMetalDetectedCutFeatureCount = Get-ObjectNumber $DoorSummary 'sheetMetalDetectedCutFeatureCount' 0
    sheetMetalDetectedCutSketchDimensionCount = Get-ObjectNumber $DoorSummary 'sheetMetalDetectedCutSketchDimensionCount' 0
    sheetMetalSuppressedCutFeatureNames = @(Get-ObjectArray $DoorSummary 'sheetMetalSuppressedCutFeatureNames')
    sheetMetalSuppressedCutFeatureCount = Get-ObjectNumber $DoorSummary 'sheetMetalSuppressedCutFeatureCount' 0
    sheetMetalAppliedFeatureOperations = @(Get-ObjectArray $DoorSummary 'sheetMetalAppliedFeatureOperations')
    topLatchPart = $namedTopLatchPart
    namedNativeDir = $namedDir
    namedNativeFileMap = @($namedFileMap.ToArray())
    summary = $SummaryPath
    roleSuffix = 'generated_native_door_module'
    source = 'solidworks_2020_native_single_door_generator'
  })
}

function Remove-CabinetTargetPlacementRows([string] $PlacementPath, $Targets) {
  $targetRoles = @(
    $Targets |
      Where-Object { (Get-ObjectString $_.binding 'type') -ne 'fixed_accessory_module' } |
      ForEach-Object { [string] $_.role } |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
      Select-Object -Unique
  )
  if ($targetRoles.Count -eq 0) {
    return 0
  }
  $lines = [System.Collections.Generic.List[string]]::new()
  $sourceLines = [IO.File]::ReadAllLines($PlacementPath, [Text.Encoding]::UTF8)
  if ($sourceLines.Count -eq 0) {
    return 0
  }
  $lines.Add($sourceLines[0])
  $removed = 0
  foreach ($line in @($sourceLines | Select-Object -Skip 1)) {
    if ([string]::IsNullOrWhiteSpace($line)) {
      continue
    }
    $role = ($line -split "`t", 2)[0]
    $isCabinetTarget = $false
    foreach ($targetRole in $targetRoles) {
      if ($role.Contains($targetRole)) {
        $isCabinetTarget = $true
        break
      }
    }
    if ($isCabinetTarget) {
      $removed += 1
      continue
    }
    $lines.Add($line)
  }
  [IO.File]::WriteAllLines($PlacementPath, $lines, [Text.UTF8Encoding]::new($false))
  return $removed
}

function Write-RestoredV43TemplatePlacementRows([string] $SourcePlacementPath, [string] $OutputPlacementPath, [string[]] $AdditionalRows) {
  $sourceLines = [IO.File]::ReadAllLines($SourcePlacementPath, [Text.Encoding]::UTF8)
  if ($sourceLines.Count -le 0) {
    throw "Template placement table is empty: $SourcePlacementPath"
  }
  $rows = [System.Collections.Generic.List[string]]::new()
  $rows.Add($sourceLines[0]) | Out-Null
  $excluded = 0
  foreach ($line in @($sourceLines | Select-Object -Skip 1)) {
    if ([string]::IsNullOrWhiteSpace($line)) {
      continue
    }
    $text = $line.ToLowerInvariant()
    $isExcludedElectricalOrCabinetLock = $text.Contains('gold_electronics_module') -or
      $text.Contains('cabinet_lock_body') -or
      $text.Contains('electric_lock_body')
    if ($isExcludedElectricalOrCabinetLock) {
      $excluded += 1
      continue
    }
    $rows.Add($line) | Out-Null
  }
  foreach ($row in @($AdditionalRows)) {
    if (-not [string]::IsNullOrWhiteSpace($row)) {
      $rows.Add($row) | Out-Null
    }
  }
  [IO.File]::WriteAllLines($OutputPlacementPath, $rows, [Text.UTF8Encoding]::new($false))
  return [pscustomobject] @{
    placementCount = [Math]::Max(0, $rows.Count - 1)
    excludedElectricalOrCabinetLockCount = $excluded
  }
}

$root = Split-Path -Parent $PSScriptRoot
$toolDir = Join-Path $root 'workers\solidworks_tools'
$rulePlanner = Join-Path $root 'tools\locker_16029_template_rules.mjs'
$moduleTargetsTool = Join-Path $root 'tools\locker_16029_gold_module_targets.mjs'
$structureFeedbackTool = Join-Path $root 'tools\locker_16029_structure_feedback.mjs'
$goldStructureGateTool = Join-Path $root 'tools\verify_16029_gold_structure_gate.mjs'
$goldSheetMetalRulesJson = Join-Path $root 'data\locker_16029_gold_sheetmetal_rules.json'
$parametricScaffoldTool = Join-Path $root 'tools\generate_16029_parametric_scaffold_freecad.py'
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
Assert-File $goldStructureGateTool '1000W gold-source structure gate'
Assert-File $goldSheetMetalRulesJson '1000W gold-source sheet-metal rules'
Assert-File $parametricScaffoldTool 'parametric scaffold generator'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $folder = "sw2020_full_$(Token $CabinetWidthMm)W_parametric_template"
  $OutputDir = Join-Path (Join-Path $root "workers\generated_models\review_generation_requests\$RequestId") $folder
}
$OutputDir = [IO.Path]::GetFullPath($OutputDir)

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$packDir = Join-Path $OutputDir 'pack_and_go'
$evidenceDir = Join-Path $OutputDir 'evidence'
$captureOutDir = Join-Path $OutputDir 'review_captures'
Reset-GeneratedSubdir $packDir $OutputDir 'Pack-and-Go output'
Reset-GeneratedSubdir $evidenceDir $OutputDir 'evidence output'
Reset-GeneratedSubdir $captureOutDir $OutputDir 'review capture output'

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
$compatibleWithNativeTemplate = [bool] $rulePlan.derived.compatibleWithNativeTemplate
$templateDoorModuleBindingStatus = [string] $rulePlan.derived.doorModuleBinding.status
$missingTemplateDoorModuleUnits = @($rulePlan.derived.doorModuleBinding.missingUnits)
$freezeVerifiedV43DoorRoute = $true
$restoreVerifiedV43NativeAssemblyBase = $freezeVerifiedV43DoorRoute -and $compatibleWithNativeTemplate
$replaceCabinetTargetsWithParametricScaffold = -not $restoreVerifiedV43NativeAssemblyBase
$forceGeneratedDoorModulesWithoutElectricLock = $false
$nativeDoorModuleGenerationPolicy = if ($freezeVerifiedV43DoorRoute) { 'freeze_verified_v43_door_modules_internal_sheetmetal_only' } elseif ($forceGeneratedDoorModulesWithoutElectricLock) { 'force_generated_modules_without_cabinet_electric_lock' } else { 'missing_template_units_only' }
$generatedDoorModulesJson = Join-Path $evidenceDir 'generated_native_door_modules.json'
$generatedNativeDoorModules = New-Object System.Collections.Generic.List[object]
$generatedNativeDoorModuleKeys = @{}
$requiredGeneratedDoorModuleKeys = @{}
if ($forceGeneratedDoorModulesWithoutElectricLock -or $missingTemplateDoorModuleUnits.Count -gt 0) {
  $singleDoorGenerator = Join-Path $root 'tools\generate_review_solidworks_single_door.ps1'
  Assert-File $singleDoorGenerator 'single-door native SolidWorks generator'
  foreach ($column in @($rulePlan.columns)) {
    $side = ([string] $column.side).ToUpperInvariant()
    if ($side -ne 'R') {
      $side = 'L'
    }
    foreach ($row in @($column.rows)) {
      $unitValue = [double] $row.unit
      $unitText = Invariant $unitValue
      $requiresGeneratedNativeDoorModule = $forceGeneratedDoorModulesWithoutElectricLock -or ($missingTemplateDoorModuleUnits -contains $unitText)
      if (-not $requiresGeneratedNativeDoorModule) {
        continue
      }
      $key = "$side|$unitText"
      $requiredGeneratedDoorModuleKeys[$key] = $true
      if ($generatedNativeDoorModuleKeys.ContainsKey($key)) {
        continue
      }
      $handedness = if ($side -eq 'R') { 'right' } else { 'left' }
      $doorOutDir = Join-Path $evidenceDir ("generated_door_modules\{0}_{1}_12" -f $side, (Token $unitValue))
      $doorRequestId = "$RequestId-$side-$(Token $unitValue)"
      $doorWidthForGenerator = [double] $rulePlan.derived.doorWidthMm
      $doorHeightForGenerator = [double] $row.heightMm
      $doorSummaryJson = Join-Path $doorOutDir 'solidworks_2020_native_generation_summary.json'
      if (Test-Path -LiteralPath $doorSummaryJson -PathType Leaf) {
        try {
          $doorSummary = Read-Json $doorSummaryJson
          $existingSheetMetalRuleStatus = Get-ObjectString $doorSummary 'sheetMetalRuleBindingStatus'
          $existingHoleDatumCount = Get-ObjectNumber $doorSummary 'sheetMetalHoleDatumCount' 0
          $existingHoleFeatureStatus = Get-ObjectString $doorSummary 'sheetMetalHoleFeatureStatus'
          if ($null -ne $doorSummary.primaryAssembly -and (Test-Path -LiteralPath ([string] $doorSummary.primaryAssembly) -PathType Leaf) -and $existingSheetMetalRuleStatus -eq 'bound_to_1000w_gold_dxf' -and $existingHoleDatumCount -gt 0 -and $existingHoleFeatureStatus -match 'gold_dxf_datums') {
            Write-Host "[reuse generated native $side $unitText/12 door module] $doorSummaryJson"
            $moduleRecord = New-GeneratedNativeDoorModuleRecord `
              -Side $side `
              -UnitText $unitText `
              -DoorWidthMm ([double] $rulePlan.derived.doorWidthMm) `
              -DoorHeightMm ([double] $row.heightMm) `
              -Handedness $handedness `
              -DoorSummary $doorSummary `
              -SummaryPath $doorSummaryJson `
              -DoorOutDir $doorOutDir
            $generatedNativeDoorModules.Add($moduleRecord) | Out-Null
            $generatedNativeDoorModuleKeys[$key] = $true
            continue
          }
        }
        catch {
          Write-Warning "Existing generated native $side $unitText/12 door module summary could not be reused; regenerating. $($_.Exception.Message)"
        }
      }
      Invoke-External 'powershell.exe' @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $singleDoorGenerator,
        '-RequestId',
        $doorRequestId,
        '-DoorWidthMm',
        (Invariant $doorWidthForGenerator),
        '-DoorHeightMm',
        (Invariant $doorHeightForGenerator),
        '-Handedness',
        $handedness,
        '-SheetMetalRuleJson',
        $goldSheetMetalRulesJson,
        '-DoorUnit',
        $unitText,
        '-OutputDir',
        $doorOutDir
      ) "generate missing native $side $unitText/12 door module"
      Wait-File $doorSummaryJson 'generated native door module summary'
      $doorSummary = Read-Json $doorSummaryJson
      Assert-File ([string] $doorSummary.primaryAssembly) 'generated native door module assembly'
      $moduleRecord = New-GeneratedNativeDoorModuleRecord `
        -Side $side `
        -UnitText $unitText `
        -DoorWidthMm ([double] $rulePlan.derived.doorWidthMm) `
        -DoorHeightMm ([double] $row.heightMm) `
        -Handedness $handedness `
        -DoorSummary $doorSummary `
        -SummaryPath $doorSummaryJson `
        -DoorOutDir $doorOutDir
      $generatedNativeDoorModules.Add($moduleRecord) | Out-Null
      $generatedNativeDoorModuleKeys[$key] = $true
    }
  }
}
$generatedNativeDoorModuleArray = @($generatedNativeDoorModules.ToArray())
$generatedDoorManifest = [ordered] @{
  schema = 'winnsen.locker16029.generated_native_door_modules.v1'
  templateDoorModuleBindingStatus = $templateDoorModuleBindingStatus
  missingTemplateDoorModuleUnits = @($missingTemplateDoorModuleUnits)
  nativeDoorModuleGenerationPolicy = $nativeDoorModuleGenerationPolicy
  generatedModulesRequiredForNoElectricLock = $forceGeneratedDoorModulesWithoutElectricLock
  freezeVerifiedV43DoorRoute = $freezeVerifiedV43DoorRoute
  modules = @($generatedNativeDoorModuleArray)
}
$generatedDoorManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $generatedDoorModulesJson -Encoding UTF8
$unresolvedNativeDoorModuleKeys = @($requiredGeneratedDoorModuleKeys.Keys | Where-Object { -not $generatedNativeDoorModuleKeys.ContainsKey($_) })
$unresolvedNativeDoorModuleUnits = @($unresolvedNativeDoorModuleKeys | ForEach-Object { ($_ -split '\|', 2)[1] } | Sort-Object -Unique)
$generatedNativeDoorModuleCount = $generatedNativeDoorModuleArray.Count
$generatedNativeDoorModuleHoleDatumCount = 0
$generatedNativeDoorModuleDetectedCutFeatureCount = 0
$generatedNativeDoorModuleSuppressedCutFeatureCount = 0
$generatedNativeDoorModuleHoleFeatureStatuses = @(
  $generatedNativeDoorModuleArray |
    ForEach-Object {
      $generatedNativeDoorModuleHoleDatumCount += [int] (Get-ObjectNumber $_ 'sheetMetalHoleDatumCount' 0)
      $generatedNativeDoorModuleDetectedCutFeatureCount += [int] (Get-ObjectNumber $_ 'sheetMetalDetectedCutFeatureCount' 0)
      $generatedNativeDoorModuleSuppressedCutFeatureCount += [int] (Get-ObjectNumber $_ 'sheetMetalSuppressedCutFeatureCount' 0)
      Get-ObjectString $_ 'sheetMetalHoleFeatureStatus'
    } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique
)
$nativeDoorModuleBindingStatus = if ($unresolvedNativeDoorModuleUnits.Count -gt 0) { 'needs_native_door_generation' } elseif ($generatedNativeDoorModuleCount -gt 0) { 'ready_from_generated_modules' } else { 'ready_from_template_modules' }
$nativeDoorModuleNeedsGeneration = $unresolvedNativeDoorModuleUnits.Count -gt 0
$missingNativeDoorModuleUnits = @($unresolvedNativeDoorModuleUnits)
$missingNativeDoorModuleUnitsText = if ($missingNativeDoorModuleUnits.Count -gt 0) { $missingNativeDoorModuleUnits -join ', ' } else { 'none' }

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
  '--generated-door-modules', $generatedDoorModulesJson,
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
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_remove_assembly_components_by_pattern.ps1')) 'compile assembly component remover tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_restore_door_lock_tongues.ps1')) 'compile door lock tongue restore tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_placed_components_module.ps1')) 'compile placed-components assembly tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_probe_part_bodies.ps1')) 'compile part body probe tool'
Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_import_step_save_native.ps1')) 'compile step importer'

$fixedModuleDir = Join-Path $evidenceDir 'ref_body'
New-Item -ItemType Directory -Force -Path $fixedModuleDir | Out-Null
$fixedModuleAssembly = Join-Path $fixedModuleDir 'body_fixed.SLDASM'
$fixedModuleBuildJson = Join-Path $fixedModuleDir 'body_fixed_build.json'
$placedTool = Join-Path $toolDir 'bin\BuildPlacedComponentsModule.exe'
Assert-File $placedTool 'placed-components assembly tool'
$parametricScaffoldDir = Join-Path $evidenceDir 'parametric_scaffold'
$parametricScaffoldManifestJson = Join-Path $parametricScaffoldDir 'parametric_scaffold_manifest.json'
$parametricScaffoldPlacementsTsv = Join-Path $parametricScaffoldDir 'parametric_scaffold_placements.tsv'
$parametricScaffoldStatus = 'not_generated'
$parametricScaffoldNativePartCount = 0
$parametricScaffoldPlacementCount = 0
$parametricScaffoldVisiblePlacementCount = 0
$parametricScaffoldReplacedPlacementCount = 0
$parametricScaffoldPlacementMode = if ($restoreVerifiedV43NativeAssemblyBase) { 'evidence_only_kept_out_of_restored_v43_visible_candidate' } else { 'replace_cabinet_targets_for_non_template_structure_revision' }
$parametricInternalSheetMetalRepairEnabled = $true
$placementRows = [System.Collections.Generic.List[string]]::new()
$restoredV43TemplatePlacementsTsv = Join-Path $evidenceDir 'v43_restored_internal_sheetmetal_candidate_placements.tsv'
$restoredV43TemplatePlacementCount = 0
$restoredV43TemplateExcludedPlacementCount = 0
$restoredV43NativeAssemblyBaseUsed = $false

if ($true) {
  $softwareInstallDirName = TextFromCodes @(0x8f6f, 0x4ef6, 0x5b89, 0x88c5, 0x5f55)
  $freecad = Join-Path (Join-Path 'D:\' $softwareInstallDirName) 'freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe'
  Assert-File $freecad 'FreeCADCmd for internal parameter scaffold'
  New-Item -ItemType Directory -Force -Path $parametricScaffoldDir | Out-Null

  $env:WINNSEN_16029_PARAMETRIC_SCAFFOLD_PLAN = $planJson
  $env:WINNSEN_16029_PARAMETRIC_SCAFFOLD_OUT_DIR = $parametricScaffoldDir
  $env:WINNSEN_16029_PARAMETRIC_SCAFFOLD_MANIFEST = $parametricScaffoldManifestJson
  try {
    $scaffoldToolForPython = $parametricScaffoldTool.Replace('\', '\\').Replace("'", "\\'")
    $scaffoldCode = "import runpy; runpy.run_path(r'$scaffoldToolForPython', run_name='__main__')"
    Invoke-External $freecad @('-c', $scaffoldCode) 'generate parametric scaffold STEP evidence'
  }
  finally {
    Remove-Item Env:\WINNSEN_16029_PARAMETRIC_SCAFFOLD_PLAN, Env:\WINNSEN_16029_PARAMETRIC_SCAFFOLD_OUT_DIR, Env:\WINNSEN_16029_PARAMETRIC_SCAFFOLD_MANIFEST -ErrorAction SilentlyContinue
  }
  Wait-File $parametricScaffoldManifestJson 'parametric scaffold manifest json'
  $parametricScaffoldManifest = Read-Json $parametricScaffoldManifestJson
  $importer = Join-Path $toolDir 'bin\ImportStepSaveNative.exe'
  Assert-File $importer 'step importer'

  $nativeByKey = @{}
  foreach ($part in @($parametricScaffoldManifest.parts)) {
    $stepPath = Get-ObjectString $part 'sourceStepPath'
    $nativePreferredPath = Get-ObjectString $part 'nativePreferredPath'
    $roundtripStepPath = Get-ObjectString $part 'roundtripStepPath'
    $importJson = Get-ObjectString $part 'importResultJson'
    Assert-File $stepPath "parametric scaffold STEP $($part.key)"
    Invoke-External $importer @($stepPath, $nativePreferredPath, $roundtripStepPath, $importJson) "save parametric scaffold $($part.key) as SolidWorks 2020 native"
    Wait-File $importJson "parametric scaffold import result $($part.key)"
    $importResult = Read-Json $importJson
    $nativePath = Get-ObjectString $importResult 'nativePath'
    Assert-File $nativePath "parametric scaffold native $($part.key)"
    $nativeByKey[[string] $part.key] = $nativePath
  }
  $parametricScaffoldNativePartCount = $nativeByKey.Count
  $parametricSuffix = TextFromCodes @(0x53c2, 0x6570, 0x5316)
  $namedNativeDir = Join-Path $parametricScaffoldDir 'native_named'

  $columnCenterBySide = @{}
  foreach ($column in @($rulePlan.columns)) {
    $side = ([string] $column.side).ToUpperInvariant()
    $rows = @($column.rows)
    if ($rows.Count -gt 0) {
      $columnCenterBySide[$side] = [double] $rows[0].centerXmm
    }
  }

  if ($parametricInternalSheetMetalRepairEnabled -and ($replaceCabinetTargetsWithParametricScaffold -or $restoreVerifiedV43NativeAssemblyBase)) {
    foreach ($target in @($moduleTargets.targets)) {
      $bindingType = Get-ObjectString $target.binding 'type'
      $partKey = [string] $target.key
      $part = Find-ScaffoldPart $parametricScaffoldManifest $partKey
      if ($bindingType -eq 'row_boundary_shelf') {
        $side = (Get-ObjectString $target.binding 'side').ToUpperInvariant()
        $partKey = if ($side -eq 'R') { 'cabinet_right_shelf_weldment' } else { 'cabinet_left_shelf_weldment' }
        $part = Find-ScaffoldPart $parametricScaffoldManifest $partKey
        if ($null -eq $part -or -not $nativeByKey.ContainsKey($partKey)) {
          continue
        }
        $targetRole = [string] $target.role
        $boundaryY = Get-ObjectNumber $target.binding 'boundaryYmm' (Get-ObjectNumber $target.placement 'targetTyMm' 0)
        $tx = if ($columnCenterBySide.ContainsKey($side)) { [double] $columnCenterBySide[$side] } else { 0.0 }
        $role = "{0}_{1}_Y{2}" -f $targetRole, $parametricSuffix, (Token $boundaryY)
        $nativePathForRole = Use-NamedNativePart $nativeByKey[$partKey] $role $namedNativeDir
        $placementRows.Add((New-PlacementLine $role $nativePathForRole $tx $boundaryY (-$CabinetDepthMm / 2.0))) | Out-Null
        continue
      }
      if ($null -eq $part -or -not $nativeByKey.ContainsKey($partKey)) {
        continue
      }
      $targetRole = [string] $target.role
      $placement = $part.defaultPlacement
      $role = "{0}_{1}" -f $targetRole, $parametricSuffix
      $nativePathForRole = Use-NamedNativePart $nativeByKey[$partKey] $role $namedNativeDir
      $placementRows.Add((New-PlacementLine $role $nativePathForRole (Get-ObjectNumber $placement 'txMm' 0) (Get-ObjectNumber $placement 'tyMm' 0) (Get-ObjectNumber $placement 'tzMm' 0))) | Out-Null
    }
  }

  foreach ($column in @($rulePlan.columns)) {
    $side = ([string] $column.side).ToUpperInvariant()
    $partKey = if ($side -eq 'R') { 'lock_mounting_hole_datum_right' } else { 'lock_mounting_hole_datum_left' }
    $part = Find-ScaffoldPart $parametricScaffoldManifest $partKey
    if ($null -eq $part -or -not $nativeByKey.ContainsKey($partKey)) {
      continue
    }
    $nativePathForRole = Use-NamedNativePart $nativeByKey[$partKey] ([string] $part.role) $namedNativeDir
    foreach ($row in @($column.rows)) {
      $rowIndex = [int] $row.index
      $rowY = [double] $row.centerYmm
      $rowX = if ($side -eq 'R') { [Math]::Abs([double] $rulePlan.derived.lockBodyAbsXmm) } else { -[Math]::Abs([double] $rulePlan.derived.lockBodyAbsXmm) }
      $rowZ = [double] $rulePlan.derived.lockBodyZmm
      $role = "{0}_{1}_row{2}" -f ([string] $part.role), $side, ([string] $rowIndex).PadLeft(2, '0')
      $placementRows.Add((New-PlacementLine $role $nativePathForRole $rowX $rowY $rowZ)) | Out-Null
    }
  }

  foreach ($partKey in @(
    'partition_stiffener_left_front',
    'partition_stiffener_left_rear',
    'partition_stiffener_right_front',
    'partition_stiffener_right_rear'
  )) {
    $part = Find-ScaffoldPart $parametricScaffoldManifest $partKey
    if ($null -eq $part -or -not $nativeByKey.ContainsKey($partKey)) {
      continue
    }
    $placement = $part.defaultPlacement
    $role = [string] $part.role
    $nativePathForRole = Use-NamedNativePart $nativeByKey[$partKey] $role $namedNativeDir
    $placementRows.Add((New-PlacementLine $role $nativePathForRole (Get-ObjectNumber $placement 'txMm' 0) (Get-ObjectNumber $placement 'tyMm' 0) (Get-ObjectNumber $placement 'tzMm' 0))) | Out-Null
  }

  $shelfLocatorPart = Find-ScaffoldPart $parametricScaffoldManifest 'shelf_locating_foot_datum'
  $frontNotchPart = Find-ScaffoldPart $parametricScaffoldManifest 'front_frame_locating_notch_datum'
  foreach ($column in @($rulePlan.columns)) {
    $side = ([string] $column.side).ToUpperInvariant()
    if ($side -ne 'R') {
      $side = 'L'
    }
    $rows = @($column.rows)
    $columnX = if ($columnCenterBySide.ContainsKey($side)) { [double] $columnCenterBySide[$side] } else { 0.0 }
    foreach ($row in @($rows | Select-Object -First ([Math]::Max(0, $rows.Count - 1)))) {
      $boundaryY = [double] $row.topYmm
      if ($null -ne $shelfLocatorPart -and $nativeByKey.ContainsKey('shelf_locating_foot_datum')) {
        $role = "{0}_{1}_after_row{2}" -f ([string] $shelfLocatorPart.role), $side, ([string] ([int] $row.index)).PadLeft(2, '0')
        $nativePathForRole = Use-NamedNativePart $nativeByKey['shelf_locating_foot_datum'] ([string] $shelfLocatorPart.role) $namedNativeDir
        $placementRows.Add((New-PlacementLine $role $nativePathForRole $columnX $boundaryY -30.0)) | Out-Null
      }
      if ($null -ne $frontNotchPart -and $nativeByKey.ContainsKey('front_frame_locating_notch_datum')) {
        $role = "{0}_{1}_after_row{2}" -f ([string] $frontNotchPart.role), $side, ([string] ([int] $row.index)).PadLeft(2, '0')
        $nativePathForRole = Use-NamedNativePart $nativeByKey['front_frame_locating_notch_datum'] ([string] $frontNotchPart.role) $namedNativeDir
        $placementRows.Add((New-PlacementLine $role $nativePathForRole $columnX $boundaryY -12.0)) | Out-Null
      }
    }
  }

  $footPart = Find-ScaffoldPart $parametricScaffoldManifest 'leveling_foot'
  if ($null -ne $footPart -and $nativeByKey.ContainsKey('leveling_foot')) {
    $footRole = [string] $footPart.role
    $footNativePath = Use-NamedNativePart $nativeByKey['leveling_foot'] $footRole $namedNativeDir
    $footPlacements = @(
      @('LF', (-$CabinetWidthMm / 2.0 + 55.0), -17.0, -55.0),
      @('RF', ($CabinetWidthMm / 2.0 - 55.0), -17.0, -55.0),
      @('LB', (-$CabinetWidthMm / 2.0 + 55.0), -17.0, (-$CabinetDepthMm + 55.0)),
      @('RB', ($CabinetWidthMm / 2.0 - 55.0), -17.0, (-$CabinetDepthMm + 55.0))
    )
    foreach ($foot in $footPlacements) {
      $placementRows.Add((New-PlacementLine ("{0}_{1}" -f $footRole, $foot[0]) $footNativePath ([double] $foot[1]) ([double] $foot[2]) ([double] $foot[3]))) | Out-Null
    }
  }

  if ($parametricInternalSheetMetalRepairEnabled -and $replaceCabinetTargetsWithParametricScaffold) {
    $parametricScaffoldReplacedPlacementCount = Remove-CabinetTargetPlacementRows $moduleTargetsFullCandidatePlacementsTsv @($moduleTargets.targets)
  }
  Write-PlacementRows $parametricScaffoldPlacementsTsv $placementRows
  if ($replaceCabinetTargetsWithParametricScaffold) {
    [IO.File]::AppendAllText($moduleTargetsFullCandidatePlacementsTsv, (($placementRows.ToArray() -join [Environment]::NewLine) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    $parametricScaffoldVisiblePlacementCount = $placementRows.Count
  }
  $parametricScaffoldPlacementCount = $placementRows.Count
  $parametricScaffoldStatus = 'generated_parametric_scaffold_needs_engineering_validation'
}

if ($restoreVerifiedV43NativeAssemblyBase) {
  $restorePlacementResult = Write-RestoredV43TemplatePlacementRows -SourcePlacementPath $sourcePlacements -OutputPlacementPath $restoredV43TemplatePlacementsTsv -AdditionalRows @()
  $restoredV43TemplatePlacementCount = [int] $restorePlacementResult.placementCount
  $restoredV43TemplateExcludedPlacementCount = [int] $restorePlacementResult.excludedElectricalOrCabinetLockCount
  $restoredV43NativeAssemblyBaseUsed = $true
}

Invoke-External $placedTool @($moduleTargetsFixedPlacementsTsv, $fixedModuleAssembly, $fixedModuleBuildJson) 'build fixed cabinet source-reference module assembly'
Wait-File $fixedModuleBuildJson 'fixed cabinet source-reference module build json'
$fixedModuleBuild = Read-Json $fixedModuleBuildJson
if (-not $fixedModuleBuild.saved) {
  throw "Fixed cabinet source-reference module assembly was not saved: $fixedModuleBuildJson"
}
$fixedModuleStructureJson = Join-Path $fixedModuleDir 'body_fixed_structure.json'
$inspectTool = Join-Path $toolDir 'bin\InspectAssemblyComponents.exe'
Assert-File $inspectTool 'assembly structure inspection tool'
Invoke-StructureInspection $inspectTool $fixedModuleAssembly $fixedModuleStructureJson 'inspect fixed cabinet source-reference module assembly' 'fixed cabinet source-reference module structure json'
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
  Invoke-StructureInspection $inspectTool $shelfCandidateAssembly $shelfCandidateStructureJson 'inspect shelf binding source-reference candidate assembly' 'shelf binding candidate structure json'
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
  Invoke-StructureInspection $inspectTool $cabinetCandidateAssembly $cabinetCandidateStructureJson 'inspect cabinet body source-reference candidate assembly' 'cabinet body candidate structure json'
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
$centeredBackSeamEnabled = $false
$centeredBackSeamStatus = 'not_applicable'
$centeredBackSeamDir = Join-Path $OutputDir 'bs'
$centeredBackSeamNativePartsDir = Join-Path $centeredBackSeamDir 'p'
$centeredBackSeamAssembly = Join-Path $centeredBackSeamDir 'back_center.SLDASM'
$centeredBackSeamBuildJson = Join-Path $centeredBackSeamDir 'back_center.json'
$centeredBackSeamPanelCount = 0
$centeredBackSeamCenterXMm = $null
$centeredBackSeamGapMm = $null
$backSheetMetalRepairEnabled = $false
$backSheetMetalRepairStatus = 'not_applicable'
$backSheetMetalRepairDir = Join-Path $OutputDir 'br'
$backSheetMetalRepairPackDir = Join-Path $backSheetMetalRepairDir 'p'
$backSheetMetalRepairPackJson = Join-Path $backSheetMetalRepairDir 'pg.json'
$backSheetMetalRepairAssembly = Join-Path $backSheetMetalRepairPackDir 'candidate_16029_740W_L642_R246_v43_internal_sheetmetal_flat_full.SLDASM'
$backSheetMetalRepairBuildJson = Join-Path $backSheetMetalRepairDir 'trim.json'
$backSheetMetalRepairRepairedPartCount = 0
$doorLockTongueRestoreEnabled = $false
$doorLockTongueRestoreStatus = 'not_applicable'
$doorLockTongueRestoreJson = Join-Path $evidenceDir 'solidworks_2020_door_lock_tongue_restore.json'
$doorLockTongueRestorePart = ''
$doorLockTongueRestoreAddedCount = 0
$doorLockTongueRestoreSkippedExistingCount = 0
$doorLockTongueRestoreFailedCount = 0
$doorLockTongueSourcePart = Join-Path $root 'workers\generated_models\SW-NATIVE-16029-740W-1917H-550D-L642-R246-ORDINARY-20260528\sw2020_gold_compat_parts\electric_lock_hook_zja_s500_SW2020_from_ascii_step.SLDPRT'
$internalSheetMetalRepairSeedAssembly = Join-Path $root 'workers\generated_models\review_generation_requests\v43-int-v9-internal-sheetmetal-role-named-full\pack_and_go_after_hook_cleanup_no_electric_lock\candidate_16029_740W_L642_R246_v43_internal_sheetmetal_flat_full.SLDASM'
$internalSheetMetalRepairSeedUsed = $false
$internalSheetMetalRepairSeedStatus = 'not_applicable'
$fullCandidatePlacementSourceTsv = if ($restoreVerifiedV43NativeAssemblyBase) { $restoredV43TemplatePlacementsTsv } else { $moduleTargetsFullCandidatePlacementsTsv }
if ($restoreVerifiedV43NativeAssemblyBase -or $cabinetCandidateTargetCount -gt 0) {
  New-Item -ItemType Directory -Force -Path $fullCandidateDir | Out-Null
  $fullCandidateBuild = $null
  for ($buildAttempt = 1; $buildAttempt -le 2; $buildAttempt++) {
    $buildLabel = if ($buildAttempt -eq 1) { 'build full assembly candidate' } else { 'build full assembly candidate retry' }
    Invoke-External $placedTool @($fullCandidatePlacementSourceTsv, $fullCandidateAssembly, $fullCandidateBuildJson) $buildLabel
    Wait-File $fullCandidateBuildJson 'full assembly candidate build json' 600
    $fullCandidateBuild = Read-Json $fullCandidateBuildJson
    if ($fullCandidateBuild.saved) {
      break
    }
    if ($buildAttempt -lt 2) {
      Write-Warning "Full assembly candidate was not saved; retrying once after SolidWorks settles. $($fullCandidateBuild.error)"
      Start-Sleep -Seconds 2
    }
  }
  if (-not $fullCandidateBuild.saved) {
    throw "Full assembly source-reference candidate was not saved: $fullCandidateBuildJson"
  }
  Invoke-StructureInspection $inspectTool $fullCandidateAssembly $fullCandidateStructureJson 'inspect full assembly source-reference candidate' 'full assembly candidate structure json'
  $fullCandidateStructure = Read-Json $fullCandidateStructureJson
  if (-not $fullCandidateStructure.opened -or $fullCandidateStructure.component_count -le 0) {
    throw "Full assembly candidate structure inspection did not produce usable component data: $fullCandidateStructureJson"
  }
  $fullCandidateTopLevelCount = @($fullCandidateStructure.components | Where-Object { $_.depth -eq 0 }).Count
  $fullCandidateComponentCount = $fullCandidateStructure.component_count
  $fullCandidateStatus = if ($restoreVerifiedV43NativeAssemblyBase) { 'restored_v43_native_template_with_internal_scaffold_evidence_held_separate_needs_validation' } else { 'needs_engineering_validation' }
  $packSourceAssembly = $fullCandidateAssembly
}

$centeredBackSeamCanUseV43Geometry = $freezeVerifiedV43DoorRoute -and $compatibleWithNativeTemplate -and ([Math]::Abs([double] $CabinetWidthMm - 740.0) -lt 0.001)
if ($centeredBackSeamCanUseV43Geometry -and (Test-Path -LiteralPath $internalSheetMetalRepairSeedAssembly -PathType Leaf)) {
  $internalSheetMetalRepairSeedUsed = $true
  $internalSheetMetalRepairSeedStatus = 'using_v9_role_named_internal_sheetmetal_seed'
  $packSourceAssembly = $internalSheetMetalRepairSeedAssembly
} elseif ($centeredBackSeamCanUseV43Geometry) {
  $internalSheetMetalRepairSeedStatus = 'missing_v9_role_named_internal_sheetmetal_seed_fell_back_to_restored_v43_candidate'
}
if ($centeredBackSeamCanUseV43Geometry) {
  $centeredBackSeamEnabled = $false
  $backSheetMetalRepairEnabled = $true
  Reset-GeneratedSubdir $backSheetMetalRepairDir $OutputDir 'back sheet-metal side-panel repair output'
  $packToolForRepair = Join-Path $toolDir 'bin\PackAndGoAssembly.exe'
  Assert-File $packToolForRepair 'pack-and-go assembly tool for back sheet-metal repair seed copy'
  Invoke-External $packToolForRepair @($packSourceAssembly, $backSheetMetalRepairPackDir, $backSheetMetalRepairPackJson) 'copy v9 seed before side-panel sheet-metal repair'
  Wait-File $backSheetMetalRepairPackJson 'back sheet-metal repair seed pack-and-go json' 600
  $backSheetMetalRepairSeedPack = Read-Json $backSheetMetalRepairPackJson
  if (-not $backSheetMetalRepairSeedPack.opened -or -not $backSheetMetalRepairSeedPack.pack_and_go_created) {
    throw "Back sheet-metal repair seed Pack-and-Go failed: $backSheetMetalRepairPackJson"
  }
  $packSourceAssemblyLeaf = Split-Path -Leaf $packSourceAssembly
  $packedSeedAssemblies = @(Get-ChildItem -LiteralPath $backSheetMetalRepairPackDir -File -Filter '*.SLDASM' | Where-Object { $_.Name -eq $packSourceAssemblyLeaf })
  if ($packedSeedAssemblies.Count -eq 0) {
    $packedSeedAssemblies = @(Get-ChildItem -LiteralPath $backSheetMetalRepairPackDir -File -Filter 'candidate_16029_740W_L642_R246*_flat_full.SLDASM')
  }
  if ($packedSeedAssemblies.Count -ne 1) {
    throw "Expected exactly one root back sheet-metal repair seed assembly in $backSheetMetalRepairPackDir, found $($packedSeedAssemblies.Count)"
  }
  $backSheetMetalRepairAssembly = $packedSeedAssemblies[0].FullName
  Assert-File $backSheetMetalRepairAssembly 'back sheet-metal repair seed assembly'
  $backSheetMetalRepairTool = Join-Path $toolDir 'repair_16029_back_sheetmetal_side_panels.ps1'
  Assert-File $backSheetMetalRepairTool 'side-panel back sheet-metal repair script'
  Invoke-External 'powershell.exe' @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $backSheetMetalRepairTool,
    '-PackDir',
    $backSheetMetalRepairPackDir,
    '-AssemblyPath',
    $backSheetMetalRepairAssembly,
    '-OutJson',
    $backSheetMetalRepairBuildJson
  ) 'trim side-panel sheet-metal back flanges to centered seam datum'
  Wait-File $backSheetMetalRepairBuildJson 'side-panel back sheet-metal repair json' 600
  $backSheetMetalRepairBuild = Read-Json $backSheetMetalRepairBuildJson
  if (-not $backSheetMetalRepairBuild.success) {
    throw "Side-panel back sheet-metal repair failed: $backSheetMetalRepairBuildJson"
  }
  $backSheetMetalRepairStatus = Get-ObjectString $backSheetMetalRepairBuild 'centeredBackSheetMetalStatus'
  $backSheetMetalRepairRepairedPartCount = [int] (Get-ObjectNumber $backSheetMetalRepairBuild 'repairedPartCount' 0)
  $centeredBackSeamStatus = $backSheetMetalRepairStatus
  $centeredBackSeamPanelCount = 0
  $centeredBackSeamCenterXMm = [double] (Get-ObjectNumber $backSheetMetalRepairBuild.right.afterBox 'xmin_mm' 0.5)
  $centeredBackSeamGapMm = $null
  $packSourceAssembly = $backSheetMetalRepairAssembly
  $doorLockTongueRestoreEnabled = $true
  $doorLockTongueRestoreTool = Join-Path $toolDir 'bin\RestoreDoorLockTongues.exe'
  Assert-File $doorLockTongueRestoreTool 'door lock tongue restore tool'
  Assert-File $doorLockTongueSourcePart 'frozen v43 mechanical door lock tongue source part'
  $doorLockTongueRestoreDoorWidthMm = [double] (Get-ObjectNumber $rulePlan.derived 'doorWidthMm' 307.0)
  if ([double]::IsNaN($doorLockTongueRestoreDoorWidthMm) -or [double]::IsInfinity($doorLockTongueRestoreDoorWidthMm) -or $doorLockTongueRestoreDoorWidthMm -le 0) {
    $doorLockTongueRestoreDoorWidthMm = 307.0
  }
  Invoke-External $doorLockTongueRestoreTool @(
    $backSheetMetalRepairPackDir,
    $doorLockTongueSourcePart,
    $doorLockTongueRestoreJson,
    (Invariant $doorLockTongueRestoreDoorWidthMm)
  ) 'restore mechanical door lock tongue geometry from frozen v43 route'
  Wait-File $doorLockTongueRestoreJson 'door lock tongue restore json' 600
  $doorLockTongueRestore = Read-Json $doorLockTongueRestoreJson
  if (-not $doorLockTongueRestore.success) {
    throw "Door lock tongue restore failed: $doorLockTongueRestoreJson"
  }
  $doorLockTongueRestoreStatus = Get-ObjectString $doorLockTongueRestore 'status'
  $doorLockTongueRestorePart = Get-ObjectString $doorLockTongueRestore 'lock_tongue_part'
  $doorLockTongueRestoreAddedCount = [int] (Get-ObjectNumber $doorLockTongueRestore 'added_count' 0)
  $doorLockTongueRestoreSkippedExistingCount = [int] (Get-ObjectNumber $doorLockTongueRestore 'skipped_existing_count' 0)
  $doorLockTongueRestoreFailedCount = [int] (Get-ObjectNumber $doorLockTongueRestore 'failed_count' 0)
} elseif ($freezeVerifiedV43DoorRoute) {
  $centeredBackSeamStatus = 'skipped_non_v43_740w_geometry'
  $backSheetMetalRepairStatus = 'skipped_non_v43_740w_geometry'
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

$electricLockHookCleanupJson = Join-Path $evidenceDir 'solidworks_2020_electric_lock_hook_cleanup.json'
$removeComponentTool = Join-Path $toolDir 'bin\RemoveAssemblyComponentsByPattern.exe'
$electricLockHookCleanup = New-Object System.Collections.Generic.List[object]
$electricLockHookCleanupRemovedCount = 0
$electricLockHookCleanupRemainingCount = 0
$electricLockHookCleanupDeletedOrphanFileCount = 0
$electricLockHookCleanupPattern = 'electric_lock_hook|zja_s500|ZJA-S500'
if ($freezeVerifiedV43DoorRoute) {
  Assert-File $removeComponentTool 'assembly component remover tool'
  $packAssemblies = @(
    Get-ChildItem -LiteralPath $packDir -Filter '*.SLDASM' -File |
      Where-Object { -not ([IO.Path]::GetFullPath($_.FullName)).Equals([IO.Path]::GetFullPath($primaryAssembly), [System.StringComparison]::OrdinalIgnoreCase) } |
      Sort-Object FullName
  )
  foreach ($assemblyFile in $packAssemblies) {
    $cleanupItemJson = Join-Path $evidenceDir ("electric_lock_hook_cleanup_{0}.json" -f ([IO.Path]::GetFileNameWithoutExtension($assemblyFile.Name)))
    Invoke-External $removeComponentTool @($assemblyFile.FullName, $electricLockHookCleanupPattern, $cleanupItemJson) "remove electric-lock hook hardware from generated package copy $($assemblyFile.Name)"
    Wait-File $cleanupItemJson 'electric-lock hook cleanup result json'
    $cleanupItem = Read-Json $cleanupItemJson
    $electricLockHookCleanup.Add($cleanupItem) | Out-Null
    $electricLockHookCleanupRemovedCount += Get-JsonInt $cleanupItem 'removed_count' 0
    $electricLockHookCleanupRemainingCount += Get-JsonInt $cleanupItem 'remaining_count' 0
  }
  $orphanFiles = @(Get-ChildItem -LiteralPath $packDir -File | Where-Object { $_.Name -match $electricLockHookCleanupPattern })
  foreach ($orphanFile in $orphanFiles) {
    Remove-Item -LiteralPath $orphanFile.FullName -Force
    $electricLockHookCleanupDeletedOrphanFileCount += 1
  }
}
$electricLockHookCleanupSummary = [ordered] @{
  schema = 'winnsen.locker16029.electric_lock_hook_cleanup.v1'
  enabled = $freezeVerifiedV43DoorRoute
  pattern = $electricLockHookCleanupPattern
  assemblyCount = @($electricLockHookCleanup.ToArray()).Count
  removedCount = $electricLockHookCleanupRemovedCount
  remainingCount = $electricLockHookCleanupRemainingCount
  deletedOrphanFileCount = $electricLockHookCleanupDeletedOrphanFileCount
  results = @($electricLockHookCleanup.ToArray())
}
$electricLockHookCleanupSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $electricLockHookCleanupJson -Encoding UTF8

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
Invoke-StructureInspection $inspectTool $primaryAssembly $structureRecordJson 'inspect Pack-and-Go SolidWorks 2020 assembly structure' 'SolidWorks 2020 structure record json'
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

$goldStructureGateJson = Join-Path $evidenceDir 'gold_source_structure_gate.json'
Invoke-External $NodeExe @(
  $goldStructureGateTool,
  '--candidate', $structureRecordJson,
  '--expected-door-count', ([string] $DoorCount),
  '--out', $goldStructureGateJson
) 'analyze Pack-and-Go assembly against 1000W gold-source structure gate' @(0, 1)
Wait-File $goldStructureGateJson '1000W gold-source structure gate json'
$goldStructureGate = Read-Json $goldStructureGateJson
$goldStructureGateStatus = [string] $goldStructureGate.status
$goldStructureGateIssueCount = Get-JsonInt $goldStructureGate.summary 'issueCount' 0
$goldStructureGateP0Count = Get-JsonInt $goldStructureGate.summary 'p0Count' 0
$goldStructureGateP1Count = Get-JsonInt $goldStructureGate.summary 'p1Count' 0
$goldStructureGateWarningCount = Get-JsonInt $goldStructureGate.summary 'warningCount' 0
$electricalOrElectricLockComponentCount = Get-JsonInt $goldStructureGate.summary 'electricalOrElectricLockComponentCount' 0
$doorLockTongueCount = Get-JsonInt $goldStructureGate.summary 'doorLockTongueCount' 0

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
$structureNeedsRevision = $structureFeedbackIssueCount -gt 0 -or $goldStructureGateIssueCount -gt 0
$parametricScaffoldNeedsEngineeringValidation = $parametricScaffoldStatus -eq 'generated_parametric_scaffold_needs_engineering_validation' -and $parametricScaffoldVisiblePlacementCount -gt 0
$componentNamingStatus = if ($structureFeedbackP2Count -gt 0) { 'still_flagged_by_structure_feedback' } else { 'clean_after_solidworks_reopen_inspection' }
$handoffReadinessStatus = if ($structureNeedsRevision) { 'needs_structure_revision' } elseif ($nativeDoorModuleNeedsGeneration) { 'needs_native_door_generation' } elseif ($parametricScaffoldNeedsEngineeringValidation) { 'needs_parametric_scaffold_engineering_validation' } else { 'sw2020_review_ready' }

Copy-Item -LiteralPath $sourceResult -Destination (Join-Path $evidenceDir 'template_build_result.json') -Force
Copy-Item -LiteralPath $sourceComponents -Destination (Join-Path $evidenceDir 'template_components.json') -Force
Copy-Item -LiteralPath $sourcePlacements -Destination (Join-Path $evidenceDir 'template_placements.tsv') -Force
Get-ChildItem -LiteralPath $captureDir -File |
  Where-Object { -not $_.Name.StartsWith('~$') } |
  Copy-Item -Destination $captureOutDir -Force

$componentNameLine = if ($ComponentRenameTimeoutSeconds -le 0) {
  "- Engineer-visible component naming: Pack-and-Go/reopen post-inspection status: $componentNamingStatus; optional SolidWorks Name2 assignment skipped."
} else {
  "- Engineer-visible component name normalization: attempted $componentRenameAttemptCount, assigned $componentRenameAssignedCount, read-back verified $componentRenameVerifiedCount; post-inspection status: $componentNamingStatus."
}
$fullCandidateRouteLine = if ($restoreVerifiedV43NativeAssemblyBase) {
  "- Full assembly candidate: $fullCandidateStatus, SolidWorks top-level candidate count: $fullCandidateTopLevelCount, component count: $fullCandidateComponentCount. The verified v43 shell and door placement route is restored as the visual/modeling base; top-level electronics and cabinet electric-lock body placements are filtered. FreeCAD scaffold rows are held in evidence only and are not appended to the restored v43 visible candidate."
} else {
  "- Full assembly candidate: $fullCandidateStatus, SolidWorks top-level candidate count: $fullCandidateTopLevelCount, component count: $fullCandidateComponentCount. Non-template structure revision uses cabinet weldment module candidates before Pack-and-Go."
}

$readme = @(
  '# SolidWorks 2020 Full Assembly Review Package',
  '',
  'This package is generated from the review queue by using the current verified 740W L642/R246 full-assembly template seed. The 1000W x 1917H x 550D model remains the gold/source structural quality reference.',
  '',
  '## Boundary',
  '',
  '- CAD mainline: SolidWorks 2020.',
  '- This package is a review/download package, not a production drawing release.',
  "- Requested route: $(Invariant $CabinetWidthMm)W x $(Invariant $CabinetHeightMm)H x $(Invariant $CabinetDepthMm)D, $Columns columns, $DoorCount doors, row sequence $RowSequence.",
  "- Template rule mode: $($rulePlan.route.mode).",
  "- Native-template exact match: $($rulePlan.derived.compatibleWithNativeTemplate).",
  "- Native door module binding: $nativeDoorModuleBindingStatus, template binding: $templateDoorModuleBindingStatus, generated native door modules: $generatedNativeDoorModuleCount, unresolved ratio units: $missingNativeDoorModuleUnitsText.",
  "- Generated native door sheet-metal evidence: hole datums=$generatedNativeDoorModuleHoleDatumCount, active template cut features=$generatedNativeDoorModuleDetectedCutFeatureCount, suppressed template cut features=$generatedNativeDoorModuleSuppressedCutFeatureCount, status=$($generatedNativeDoorModuleHoleFeatureStatuses -join ', ').",
  "- Native door module generation policy: $nativeDoorModuleGenerationPolicy. The verified v43 door route is frozen for this internal sheet-metal repair pass; door sheet metal, door size, door sequence, and door mirroring are not regenerated.",
  "- 1000W gold-source sheet-metal rules: $($rulePlan.derived.sheetMetalRuleEvidence.status), flat width extra=$($rulePlan.derived.sheetMetalRuleEvidence.doorFlatWidthExtraMm), flat height extra=$($rulePlan.derived.sheetMetalRuleEvidence.doorFlatHeightExtraMm), hole status=$($rulePlan.derived.sheetMetalRuleEvidence.commonHoleStatus), SW cut-feature status=$($rulePlan.derived.sheetMetalRuleEvidence.solidWorksHoleFeatureStatus).",
  "- Rule boundary: $($rulePlan.boundary)",
  "- Gold-source module target count: $($moduleTargets.derived.targetCount), fixed cabinet modules: $($moduleTargets.derived.fixedCabinetModuleCount), fixed non-electrical accessories: $($moduleTargets.derived.fixedAccessoryModuleCount), shelves from row boundaries: $($moduleTargets.derived.shelfModuleCount), build-ready fixed modules: $($moduleTargets.derived.buildReadyTargetCount), source-reference fixed module top-level count: $fixedModuleTopLevelCount.",
  "- Shelf binding candidate: $shelfCandidateStatus, requested candidate shelves: $shelfCandidateTargetCount, SolidWorks top-level candidate count: $shelfCandidateTopLevelCount. This is evidence only, not engineer-ready geometry.",
  "- Cabinet body candidate: $cabinetCandidateStatus, requested cabinet candidate modules: $cabinetCandidateTargetCount, SolidWorks top-level candidate count: $cabinetCandidateTopLevelCount. This combines fixed cabinet weldments and row-boundary shelf candidates for validation.",
  "- Cabinet body candidate bbox check: $cabinetCandidateBboxStatus, shelf bbox count: $cabinetCandidateShelfBboxCount, fixed body bbox count: $cabinetCandidateFixedBodyBboxCount.",
  "- Parametric internal sheet-metal repair: $parametricScaffoldStatus, enabled=$parametricInternalSheetMetalRepairEnabled, mode=$parametricScaffoldPlacementMode, native parts: $parametricScaffoldNativePartCount, evidence placements: $parametricScaffoldPlacementCount, visible candidate placements: $parametricScaffoldVisiblePlacementCount, replaced source-reference cabinet placements: $parametricScaffoldReplacedPlacementCount. FreeCAD is internal parameter evidence only; in restored v43 mode these scaffold parts are not packed into the engineer-visible full candidate. Cabinet-side electrical boards and electric-lock bodies are excluded from cabinet placements; final residual electrical/electric-lock component count from the gate is $electricalOrElectricLockComponentCount. Row-specific lock-hole datums, shelf locating feet/notches, inner vertical partition stiffeners, and leveling-foot interfaces preserve the required hole/interface positions as evidence.",
  "- Restored v43 native base: used=$restoredV43NativeAssemblyBaseUsed, restored placement rows=$restoredV43TemplatePlacementCount, filtered top-level electrical/cabinet-lock rows=$restoredV43TemplateExcludedPlacementCount.",
  $fullCandidateRouteLine,
  "- Internal sheet-metal repair seed: $internalSheetMetalRepairSeedStatus, used=$internalSheetMetalRepairSeedUsed. This keeps the current v43 door route while preserving the repaired internal cabinet sheet-metal package.",
  "- Rear back seam repair: $backSheetMetalRepairStatus, enabled=$backSheetMetalRepairEnabled, repaired side-panel sheet-metal parts=$backSheetMetalRepairRepairedPartCount, center datum X=$centeredBackSeamCenterXMm mm. This trims the copied cabinet side-panel sheet metal; door sheet metal is not modified and no rear overlay panels are added.",
  "- Mechanical door lock tongue restore: $doorLockTongueRestoreStatus, enabled=$doorLockTongueRestoreEnabled, added=$doorLockTongueRestoreAddedCount, skipped existing=$doorLockTongueRestoreSkippedExistingCount, failed=$doorLockTongueRestoreFailedCount. This restores the frozen v43 door-route lock tongue geometry without adding cabinet-side electric lock bodies, lock-control boards, or electric-lock hook named components.",
  "- Pack-and-Go Chinese save-name normalization: mapped $packAndGoChineseSaveNameMapCount document save names, SetDocumentSaveToNames=$packAndGoSetDocumentSaveToNames.",
  "- Electric-lock hook package cleanup: removed=$electricLockHookCleanupRemovedCount, remaining=$electricLockHookCleanupRemainingCount, deleted orphan files=$electricLockHookCleanupDeletedOrphanFileCount. Door sheet-metal geometry is not regenerated or edited.",
  $componentNameLine,
  "- Engineer structure feedback: $($structureFeedback.status), P0=$structureFeedbackP0Count, P1=$structureFeedbackP1Count, P2=$structureFeedbackP2Count.",
  "- 1000W gold-source structure gate: $goldStructureGateStatus, issues: $goldStructureGateIssueCount, P0=$goldStructureGateP0Count, P1=$goldStructureGateP1Count, warnings=$goldStructureGateWarningCount.",
  "- Handoff readiness: $handoffReadinessStatus.",
  '- The historical direct per-part assembly generation route remains disabled because of transform reliability issues.',
  '',
  '## Files',
  '',
  '- `pack_and_go/`: flattened SolidWorks Pack-and-Go output containing `.SLDASM` and `.SLDPRT` files.',
  '- `br/`: short-path copied seed assembly plus side-panel sheet-metal back-flange trim evidence used before final Pack-and-Go.',
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
$summaryStatus = if ($structureNeedsRevision) { 'solidworks_2020_full_assembly_needs_structure_revision' } elseif ($nativeDoorModuleNeedsGeneration) { 'solidworks_2020_template_rule_needs_native_door_generation' } elseif ($parametricScaffoldNeedsEngineeringValidation) { 'solidworks_2020_parametric_scaffold_needs_engineering_validation' } elseif ($compatibleWithNativeTemplate) { 'solidworks_2020_full_assembly_ready' } else { 'solidworks_2020_template_rule_package_ready' }
$summaryResultKind = if ($structureNeedsRevision -or $parametricScaffoldNeedsEngineeringValidation) { 'solidworks2020_structure_revision_evidence_package' } elseif ($compatibleWithNativeTemplate) { 'solidworks2020_full_assembly_model' } else { 'solidworks2020_template_rule_full_assembly_package' }
$summary = [ordered] @{
  status = $summaryStatus
  resultKind = $summaryResultKind
  handoffReadinessStatus = $handoffReadinessStatus
  requestId = $RequestId
  cadMainline = 'SolidWorks 2020'
  goldSourceReference = '1000W x 1917H x 550D'
  templateSeed = '740W / L642-R246 / v43'
  electricalAndElectricLockComponentsExcluded = ($electricalOrElectricLockComponentCount -eq 0)
  cabinetSideElectricalAndElectricLockComponentsExcluded = $true
  lockInterfaceCarriedBy = 'lock_mounting_hole_datum'
  doorLockTongueCount = $doorLockTongueCount
  goldSheetMetalRules = $goldSheetMetalRulesJson
  goldSheetMetalRuleEvidence = $rulePlan.derived.sheetMetalRuleEvidence
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
  templateDoorModuleBindingStatus = $templateDoorModuleBindingStatus
  nativeDoorModuleBindingStatus = $nativeDoorModuleBindingStatus
  nativeDoorModuleGenerationPolicy = $nativeDoorModuleGenerationPolicy
  freezeVerifiedV43DoorRoute = $freezeVerifiedV43DoorRoute
  restoreVerifiedV43NativeAssemblyBase = $restoreVerifiedV43NativeAssemblyBase
  generatedModulesRequiredForNoElectricLock = $forceGeneratedDoorModulesWithoutElectricLock
  generatedNativeDoorModules = $generatedDoorModulesJson
  generatedNativeDoorModuleCount = $generatedNativeDoorModuleCount
  generatedNativeDoorModuleHoleDatumCount = $generatedNativeDoorModuleHoleDatumCount
  generatedNativeDoorModuleHoleFeatureStatuses = @($generatedNativeDoorModuleHoleFeatureStatuses)
  generatedNativeDoorModuleDetectedCutFeatureCount = $generatedNativeDoorModuleDetectedCutFeatureCount
  generatedNativeDoorModuleSuppressedCutFeatureCount = $generatedNativeDoorModuleSuppressedCutFeatureCount
  missingNativeDoorModuleUnits = @($missingNativeDoorModuleUnits)
  nativeDoorModuleNeedsGeneration = $nativeDoorModuleNeedsGeneration
  goldSourceModuleTargets = $moduleTargetsJson
  goldSourceModuleTargetsTsv = $moduleTargetsTsv
  goldSourceModuleRebuildPlanTsv = $moduleTargetsBuildPlanTsv
  goldSourceFixedCabinetModulePlacementsTsv = $moduleTargetsFixedPlacementsTsv
  goldSourceShelfCandidatePlacementsTsv = $moduleTargetsShelfCandidatePlacementsTsv
  goldSourceCabinetCandidatePlacementsTsv = $moduleTargetsCabinetCandidatePlacementsTsv
  goldSourceFullAssemblyCandidatePlacementsTsv = $moduleTargetsFullCandidatePlacementsTsv
  fullAssemblyCandidatePlacementSourceTsv = $fullCandidatePlacementSourceTsv
  goldSourceModuleTargetCount = $moduleTargets.derived.targetCount
  goldSourceFixedCabinetModuleTargetCount = $moduleTargets.derived.fixedCabinetModuleCount
  goldSourceFixedAccessoryModuleTargetCount = $moduleTargets.derived.fixedAccessoryModuleCount
  goldSourceShelfModuleTargetCount = $moduleTargets.derived.shelfModuleCount
  goldSourceShelfCandidateTargetCount = $shelfCandidateTargetCount
  goldSourceCabinetCandidateTargetCount = $cabinetCandidateTargetCount
  goldSourceModuleBuildReadyTargetCount = $moduleTargets.derived.buildReadyTargetCount
  goldSourceModuleNeedsBindingTargetCount = $moduleTargets.derived.needsBindingTargetCount
  goldSourceModuleTargetsSourceStructureBound = $moduleTargets.derived.sourceStructureBound
  parametricScaffoldStatus = $parametricScaffoldStatus
  parametricScaffoldNeedsEngineeringValidation = $parametricScaffoldNeedsEngineeringValidation
  parametricInternalSheetMetalRepairEnabled = $parametricInternalSheetMetalRepairEnabled
  parametricScaffoldPlacementMode = $parametricScaffoldPlacementMode
  parametricScaffoldManifest = $parametricScaffoldManifestJson
  parametricScaffoldPlacementsTsv = $parametricScaffoldPlacementsTsv
  parametricScaffoldNativePartCount = $parametricScaffoldNativePartCount
  parametricScaffoldPlacementCount = $parametricScaffoldPlacementCount
  parametricScaffoldVisiblePlacementCount = $parametricScaffoldVisiblePlacementCount
  parametricScaffoldReplacedPlacementCount = $parametricScaffoldReplacedPlacementCount
  restoredV43NativeAssemblyBaseUsed = $restoredV43NativeAssemblyBaseUsed
  restoredV43TemplatePlacementsTsv = $restoredV43TemplatePlacementsTsv
  restoredV43TemplatePlacementCount = $restoredV43TemplatePlacementCount
  restoredV43TemplateExcludedPlacementCount = $restoredV43TemplateExcludedPlacementCount
  internalSheetMetalRepairSeedAssembly = $internalSheetMetalRepairSeedAssembly
  internalSheetMetalRepairSeedUsed = $internalSheetMetalRepairSeedUsed
  internalSheetMetalRepairSeedStatus = $internalSheetMetalRepairSeedStatus
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
  centeredBackSeamEnabled = $centeredBackSeamEnabled
  centeredBackSeamStatus = $centeredBackSeamStatus
  centeredBackSeamAssembly = $centeredBackSeamAssembly
  centeredBackSeamBuild = $centeredBackSeamBuildJson
  centeredBackSeamPanelCount = $centeredBackSeamPanelCount
  centeredBackSeamCenterXMm = $centeredBackSeamCenterXMm
  centeredBackSeamGapMm = $centeredBackSeamGapMm
  backSheetMetalRepairEnabled = $backSheetMetalRepairEnabled
  backSheetMetalRepairStatus = $backSheetMetalRepairStatus
  backSheetMetalRepairAssembly = $backSheetMetalRepairAssembly
  backSheetMetalRepairSeedPack = $backSheetMetalRepairPackJson
  backSheetMetalRepairBuild = $backSheetMetalRepairBuildJson
  backSheetMetalRepairRepairedPartCount = $backSheetMetalRepairRepairedPartCount
  doorLockTongueRestoreEnabled = $doorLockTongueRestoreEnabled
  doorLockTongueRestoreStatus = $doorLockTongueRestoreStatus
  doorLockTongueRestoreJson = $doorLockTongueRestoreJson
  doorLockTongueRestorePart = $doorLockTongueRestorePart
  doorLockTongueRestoreAddedCount = $doorLockTongueRestoreAddedCount
  doorLockTongueRestoreSkippedExistingCount = $doorLockTongueRestoreSkippedExistingCount
  doorLockTongueRestoreFailedCount = $doorLockTongueRestoreFailedCount
  doorLockTongueSourcePart = $doorLockTongueSourcePart
  sourceAssembly = $sourceAssembly
  packSourceAssembly = $packSourceAssembly
  outputDir = $OutputDir
  packAndGoDir = $packDir
  primaryAssembly = $primaryAssembly
  packAndGoResult = $packJson
  packAndGoChineseSaveNameMapCount = $packAndGoChineseSaveNameMapCount
  packAndGoGotDocumentSaveToNames = $packAndGoGotDocumentSaveToNames
  packAndGoSetDocumentSaveToNames = $packAndGoSetDocumentSaveToNames
  electricLockHookCleanup = $electricLockHookCleanupJson
  electricLockHookCleanupRemovedCount = $electricLockHookCleanupRemovedCount
  electricLockHookCleanupRemainingCount = $electricLockHookCleanupRemainingCount
  electricLockHookCleanupDeletedOrphanFileCount = $electricLockHookCleanupDeletedOrphanFileCount
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
  goldStructureGate = $goldStructureGateJson
  goldStructureGateStatus = $goldStructureGateStatus
  goldStructureGateIssueCount = $goldStructureGateIssueCount
  goldStructureGateP0Count = $goldStructureGateP0Count
  goldStructureGateP1Count = $goldStructureGateP1Count
  goldStructureGateWarningCount = $goldStructureGateWarningCount
  electricalOrElectricLockComponentCount = $electricalOrElectricLockComponentCount
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
