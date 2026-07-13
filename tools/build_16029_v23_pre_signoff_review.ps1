param(
  [string]$RequestId = 'v43-int-v23-all-sources-isolated'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$CandidateRoot = Join-Path $RepoRoot "workers\generated_models\review_generation_requests\$RequestId\sw2020_full_740W_parametric_template"
$SummaryPath = Join-Path $CandidateRoot 'solidworks_2020_full_assembly_generation_summary.json'
$ReviewRoot = Join-Path $CandidateRoot 'evidence\pre_signoff_review'
$WorkingBase = Join-Path $RepoRoot 'workers\tmp_16029_pre_signoff'
$WorkingRoot = Join-Path $WorkingBase $RequestId
$LogRoot = Join-Path $RepoRoot 'workers\generation_logs'
$ReviewZip = Join-Path $LogRoot "review_generation_${RequestId}_pre_signoff_review.zip"
$ProbeExe = Join-Path $RepoRoot 'workers\solidworks_tools\bin\ProbePartBodies.exe'
$InspectExe = Join-Path $RepoRoot 'workers\solidworks_tools\bin\InspectAssemblyComponents.exe'
$CaptureScript = Join-Path $RepoRoot 'workers\solidworks_tools\sw_capture_named_views.js'

function Assert-PathUnderRoot {
  param([string]$Path, [string]$AllowedRoot)
  $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
  $fullRoot = [System.IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\')
  if (-not $fullPath.StartsWith($fullRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "unsafe path outside allowed root: $fullPath"
  }
}

function Reset-SafeDirectory {
  param([string]$Path, [string]$AllowedRoot)
  Assert-PathUnderRoot -Path $Path -AllowedRoot $AllowedRoot
  if (Test-Path -LiteralPath $Path) {
    Remove-Item -LiteralPath $Path -Recurse -Force
  }
  New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Get-FileRecord {
  param([string]$Path, [string]$Variant, [string]$Role)
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "required source file missing: $Path"
  }
  $item = Get-Item -LiteralPath $Path
  [pscustomobject]@{
    variant = $Variant
    role = $Role
    path = $item.FullName
    file_name = $item.Name
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $item.FullName).Hash
    length = [long]$item.Length
    last_write_utc = $item.LastWriteTimeUtc.ToString('o')
  }
}

function ConvertFrom-JsonString {
  param([string]$Value)
  ('"' + $Value + '"') | ConvertFrom-Json
}

function Invoke-CheckedProcess {
  param([string]$FilePath, [string[]]$Arguments, [string]$Label)
  & $FilePath @Arguments | Out-Host
  if ($LASTEXITCODE -ne 0) {
    throw "$Label failed with exit code $LASTEXITCODE"
  }
}

function Wait-SolidWorksExit {
  param([int]$ProcessId, [string]$Label)
  if ($ProcessId -le 0) { return }
  try {
    Wait-Process -Id $ProcessId -Timeout 30 -ErrorAction Stop
  } catch {
    if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
      throw "$Label left SolidWorks process $ProcessId running"
    }
  }
}

function Get-PartMetrics {
  param($Probe)
  $bodies = @($Probe.bodies)
  if ($bodies.Count -eq 0) { return $null }
  $xmin = ($bodies | Measure-Object -Property x_min_mm -Minimum).Minimum
  $ymin = ($bodies | Measure-Object -Property y_min_mm -Minimum).Minimum
  $zmin = ($bodies | Measure-Object -Property z_min_mm -Minimum).Minimum
  $xmax = ($bodies | Measure-Object -Property x_max_mm -Maximum).Maximum
  $ymax = ($bodies | Measure-Object -Property y_max_mm -Maximum).Maximum
  $zmax = ($bodies | Measure-Object -Property z_max_mm -Maximum).Maximum
  [pscustomobject]@{
    body_count = [int]$Probe.body_count
    x_len_mm = [math]::Round([double]$xmax - [double]$xmin, 3)
    y_len_mm = [math]::Round([double]$ymax - [double]$ymin, 3)
    z_len_mm = [math]::Round([double]$zmax - [double]$zmin, 3)
    volume_mm3 = [math]::Round([double](($bodies | Measure-Object -Property volume_mm3 -Sum).Sum), 3)
    surface_area_mm2 = [math]::Round([double](($bodies | Measure-Object -Property surface_area_mm2 -Sum).Sum), 3)
  }
}

function Get-AssemblyMetrics {
  param($Inspection)
  $boxes = @($Inspection.components | Where-Object { $null -ne $_.box })
  if ($boxes.Count -eq 0) { return $null }
  $xmin = ($boxes | ForEach-Object { $_.box.xmin_mm } | Measure-Object -Minimum).Minimum
  $ymin = ($boxes | ForEach-Object { $_.box.ymin_mm } | Measure-Object -Minimum).Minimum
  $zmin = ($boxes | ForEach-Object { $_.box.zmin_mm } | Measure-Object -Minimum).Minimum
  $xmax = ($boxes | ForEach-Object { $_.box.xmax_mm } | Measure-Object -Maximum).Maximum
  $ymax = ($boxes | ForEach-Object { $_.box.ymax_mm } | Measure-Object -Maximum).Maximum
  $zmax = ($boxes | ForEach-Object { $_.box.zmax_mm } | Measure-Object -Maximum).Maximum
  [pscustomobject]@{
    component_count = [int]$Inspection.component_count
    boxed_component_count = $boxes.Count
    x_len_mm = [math]::Round([double]$xmax - [double]$xmin, 3)
    y_len_mm = [math]::Round([double]$ymax - [double]$ymin, 3)
    z_len_mm = [math]::Round([double]$zmax - [double]$zmin, 3)
  }
}

function Test-Close {
  param([double]$A, [double]$B, [double]$AbsoluteTolerance, [double]$RelativeTolerance = 0.0)
  $limit = [math]::Max($AbsoluteTolerance, [math]::Abs($A) * $RelativeTolerance)
  [math]::Abs($A - $B) -le $limit
}

function Compare-PartMetrics {
  param($Reference, $Candidate)
  if ($null -eq $Reference -or $null -eq $Candidate) {
    return [pscustomobject]@{ status = 'UNAVAILABLE'; checks = $null }
  }
  $checks = [ordered]@{
    body_count = $Reference.body_count -eq $Candidate.body_count
    x_len_mm = Test-Close $Reference.x_len_mm $Candidate.x_len_mm 0.05
    y_len_mm = Test-Close $Reference.y_len_mm $Candidate.y_len_mm 0.05
    z_len_mm = Test-Close $Reference.z_len_mm $Candidate.z_len_mm 0.05
    volume_mm3 = Test-Close $Reference.volume_mm3 $Candidate.volume_mm3 10.0 0.00001
    surface_area_mm2 = Test-Close $Reference.surface_area_mm2 $Candidate.surface_area_mm2 10.0 0.00001
  }
  $matched = -not ($checks.Values -contains $false)
  [pscustomobject]@{
    status = if ($matched) { 'GEOMETRY_METRICS_MATCH' } else { 'GEOMETRY_METRICS_DIFFER' }
    checks = [pscustomobject]$checks
  }
}

function Compare-AssemblyMetrics {
  param($Reference, $Candidate)
  if ($null -eq $Reference -or $null -eq $Candidate) {
    return [pscustomobject]@{ status = 'UNAVAILABLE'; checks = $null }
  }
  $checks = [ordered]@{
    component_count = $Reference.component_count -eq $Candidate.component_count
    x_len_mm = Test-Close $Reference.x_len_mm $Candidate.x_len_mm 0.05
    y_len_mm = Test-Close $Reference.y_len_mm $Candidate.y_len_mm 0.05
    z_len_mm = Test-Close $Reference.z_len_mm $Candidate.z_len_mm 0.05
  }
  $matched = -not ($checks.Values -contains $false)
  [pscustomobject]@{
    status = if ($matched) { 'LIMITED_ASSEMBLY_METRICS_MATCH' } else { 'LIMITED_ASSEMBLY_METRICS_DIFFER' }
    checks = [pscustomobject]$checks
  }
}

foreach ($required in @($SummaryPath, $ProbeExe, $InspectExe, $CaptureScript)) {
  if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
    throw "required input missing: $required"
  }
}
if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) {
  throw 'SolidWorks is already running; close it before building the isolated review evidence.'
}

$summary = Get-Content -Raw -LiteralPath $SummaryPath | ConvertFrom-Json
if ($summary.status -ne 'solidworks_2020_controlled_candidate_pass' -or $summary.releaseEligible -ne $false) {
  throw "unexpected candidate status: status=$($summary.status), releaseEligible=$($summary.releaseEligible)"
}
if ($summary.v43ExactStructureGateStatus -ne 'PASS' -or $summary.controlledSourceImmutabilityStatus -ne 'PASS') {
  throw 'candidate exact-structure or controlled-source integrity gate is not PASS'
}
if ([int]$summary.controlledSourceChangedCount -ne 0) {
  throw "controlled source changed count is $($summary.controlledSourceChangedCount), expected 0"
}
$PrimaryAssembly = [string]$summary.primaryAssembly
if (-not (Test-Path -LiteralPath $PrimaryAssembly -PathType Leaf)) {
  throw "primary assembly missing: $PrimaryAssembly"
}

Reset-SafeDirectory -Path $ReviewRoot -AllowedRoot $CandidateRoot
Reset-SafeDirectory -Path $WorkingRoot -AllowedRoot $WorkingBase
New-Item -ItemType Directory -Path $LogRoot -Force | Out-Null

$sourceVariants = @(
  [pscustomobject]@{
    id = 'current_gold_source'
    base = Join-Path $RepoRoot (ConvertFrom-JsonString 'workers/analysis/desktop_reference/16029_\u91d1\u6807\u51c6\u539f\u59cb\u7d20\u6750_U\u76d8_20260526/1.\u5de5\u7a0b\u56fe')
    files = [ordered]@{
      vertical_left = ConvertFrom-JsonString '\u95e8\u6846 \u7ad6\u9694\u677fL.sldprt'
      vertical_right = ConvertFrom-JsonString '\u95e8\u6846 \u7ad6\u9694\u677fR.SLDPRT'
      maintenance_door = ConvertFrom-JsonString '\u5e94\u6025\u7ef4\u62a4\u95e8.SLDPRT'
      maintenance_lock_hole = ConvertFrom-JsonString '\u7ef4\u62a4\u95e8\u9501\u5b54.SLDPRT'
      maintenance_assembly = ConvertFrom-JsonString '\u5e94\u6025\u7ef4\u62a4\u95e8\u710a\u63a5.SLDASM'
    }
  },
  [pscustomobject]@{
    id = 'mirror_template_source'
    base = Join-Path $RepoRoot (ConvertFrom-JsonString 'workers/analysis/desktop_reference/\u53c2\u6570\u5316\u6a21\u677f\u7d20\u6750_U\u76d8\u539f\u59cb_20260526/16029 \u5bc4\u5b58\u67dc(\u6807\u51c6\u7ec4\u5408\u5f0f 1917\u00d71000\u00d7550)/1.\u5de5\u7a0b\u56fe')
    files = [ordered]@{
      vertical_left = ConvertFrom-JsonString '\u95e8\u6846 \u7ad6\u9694\u677fL.sldprt'
      vertical_right = ConvertFrom-JsonString '\u95e8\u6846 \u7ad6\u9694\u677fR.SLDPRT'
      maintenance_door = ConvertFrom-JsonString '\u5e94\u6025\u7ef4\u62a4\u95e8.SLDPRT'
      maintenance_lock_hole = ConvertFrom-JsonString '\u7ef4\u62a4\u95e8\u9501\u5b54.SLDPRT'
      maintenance_assembly = ConvertFrom-JsonString '\u5e94\u6025\u7ef4\u62a4\u95e8\u710a\u63a5.SLDASM'
    }
  },
  [pscustomobject]@{
    id = 'backup_20190423'
    base = Join-Path $RepoRoot (ConvertFrom-JsonString 'workers/analysis/desktop_reference/16029_\u91d1\u6807\u51c6\u539f\u59cb\u7d20\u6750_U\u76d8_20260526/10.\u5907\u4efd/1.\u5de5\u7a0b\u56fe20190423')
    files = [ordered]@{
      vertical_left = ConvertFrom-JsonString '\u95e8\u6846 \u7ad6\u9694\u677fL.sldprt'
      vertical_right = ConvertFrom-JsonString '\u95e8\u6846 \u7ad6\u9694\u677fR(\u955c\u5411).sldprt'
      maintenance_door = ConvertFrom-JsonString '\u5e94\u6025\u7ef4\u62a4\u95e8.SLDPRT'
      maintenance_lock_hole = ConvertFrom-JsonString '\u7ef4\u62a4\u95e8\u9501\u5b54.SLDPRT'
      maintenance_assembly = ConvertFrom-JsonString '\u5e94\u6025\u7ef4\u62a4\u95e8\u710a\u63a5.SLDASM'
    }
  }
)
$v20Snapshot = [pscustomobject]@{
  id = 'v20_pre_v21_snapshot'
  role = 'maintenance_door'
  path = Join-Path $RepoRoot (ConvertFrom-JsonString 'workers/generated_models/review_generation_requests/v43-int-v20-centerstrip/sw2020_full_740W_parametric_template/pack_and_go/\u9501\u63a7\u7ef4\u62a4\u6761_\u6e90\u94a3\u91d1.SLDPRT')
}

$beforeRecords = [System.Collections.Generic.List[object]]::new()
$afterRecords = [System.Collections.Generic.List[object]]::new()
$copyRecords = [System.Collections.Generic.List[object]]::new()
$partProbeRows = [System.Collections.Generic.List[object]]::new()
$assemblyRows = [System.Collections.Generic.List[object]]::new()
$probeLookup = @{}
$assemblyLookup = @{}

foreach ($variant in $sourceVariants) {
  $copyDir = Join-Path $WorkingRoot $variant.id
  New-Item -ItemType Directory -Path $copyDir -Force | Out-Null
  Get-ChildItem -LiteralPath $variant.base -File | Where-Object {
    $_.Extension -ieq '.SLDPRT' -or $_.Extension -ieq '.SLDASM'
  } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $copyDir $_.Name) -Force
  }

  foreach ($entry in $variant.files.GetEnumerator()) {
    $sourcePath = Join-Path $variant.base $entry.Value
    $copyPath = Join-Path $copyDir $entry.Value
    $beforeRecords.Add((Get-FileRecord -Path $sourcePath -Variant $variant.id -Role $entry.Key))
    if (-not (Test-Path -LiteralPath $copyPath -PathType Leaf)) {
      throw "isolated copy missing: $copyPath"
    }
    $copyRecords.Add([pscustomobject]@{
      variant = $variant.id
      role = $entry.Key
      source_path = $sourcePath
      isolated_copy_path = $copyPath
    })
    if ($entry.Key -eq 'maintenance_assembly') { continue }
    $probeDir = Join-Path $ReviewRoot "probes\$($variant.id)"
    New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    $probeJson = Join-Path $probeDir "$($entry.Key).json"
    Invoke-CheckedProcess -FilePath $ProbeExe -Arguments @($copyPath, $probeJson, '--exit-session') -Label "part probe $($variant.id)/$($entry.Key)"
    $probe = Get-Content -Raw -LiteralPath $probeJson | ConvertFrom-Json
    if (-not $probe.opened -or -not $probe.read_only_requested -or $probe.body_count -le 0 -or $probe.error) {
      throw "invalid part probe result: $probeJson"
    }
    Wait-SolidWorksExit -ProcessId ([int]$probe.solidworks_process_id) -Label "part probe $($variant.id)/$($entry.Key)"
    $metrics = Get-PartMetrics -Probe $probe
    $probeLookup["$($variant.id)|$($entry.Key)"] = $metrics
    $partProbeRows.Add([pscustomobject]@{
      variant = $variant.id
      role = $entry.Key
      probe = $probeJson
      metrics = $metrics
    })
  }

  $asmRole = 'maintenance_assembly'
  $asmCopy = Join-Path $copyDir $variant.files[$asmRole]
  $asmDir = Join-Path $ReviewRoot "assemblies\$($variant.id)"
  New-Item -ItemType Directory -Path $asmDir -Force | Out-Null
  $asmJson = Join-Path $asmDir 'maintenance_assembly.json'
  Invoke-CheckedProcess -FilePath $InspectExe -Arguments @($asmCopy, $asmJson, '--exit-session') -Label "assembly inspection $($variant.id)"
  $inspection = Get-Content -Raw -LiteralPath $asmJson | ConvertFrom-Json
  if (-not $inspection.opened -or $inspection.component_count -le 0 -or $inspection.error) {
    throw "invalid assembly inspection result: $asmJson"
  }
  Wait-SolidWorksExit -ProcessId ([int]$inspection.solidworks_process_id) -Label "assembly inspection $($variant.id)"
  $asmMetrics = Get-AssemblyMetrics -Inspection $inspection
  $assemblyLookup[$variant.id] = $asmMetrics
  $assemblyRows.Add([pscustomobject]@{
    variant = $variant.id
    role = $asmRole
    inspection = $asmJson
    metrics = $asmMetrics
  })
}

$v20Record = Get-FileRecord -Path $v20Snapshot.path -Variant $v20Snapshot.id -Role $v20Snapshot.role
$beforeRecords.Add($v20Record)
$v20CopyDir = Join-Path $WorkingRoot $v20Snapshot.id
New-Item -ItemType Directory -Path $v20CopyDir -Force | Out-Null
$v20Copy = Join-Path $v20CopyDir ([System.IO.Path]::GetFileName($v20Snapshot.path))
Copy-Item -LiteralPath $v20Snapshot.path -Destination $v20Copy -Force
$v20ProbeDir = Join-Path $ReviewRoot "probes\$($v20Snapshot.id)"
New-Item -ItemType Directory -Path $v20ProbeDir -Force | Out-Null
$v20ProbeJson = Join-Path $v20ProbeDir 'maintenance_door.json'
Invoke-CheckedProcess -FilePath $ProbeExe -Arguments @($v20Copy, $v20ProbeJson, '--exit-session') -Label 'part probe v20 snapshot'
$v20Probe = Get-Content -Raw -LiteralPath $v20ProbeJson | ConvertFrom-Json
if (-not $v20Probe.opened -or -not $v20Probe.read_only_requested -or $v20Probe.body_count -le 0 -or $v20Probe.error) {
  throw "invalid v20 snapshot probe: $v20ProbeJson"
}
Wait-SolidWorksExit -ProcessId ([int]$v20Probe.solidworks_process_id) -Label 'part probe v20 snapshot'
$v20Metrics = Get-PartMetrics -Probe $v20Probe
$probeLookup["$($v20Snapshot.id)|$($v20Snapshot.role)"] = $v20Metrics
$partProbeRows.Add([pscustomobject]@{
  variant = $v20Snapshot.id
  role = $v20Snapshot.role
  probe = $v20ProbeJson
  metrics = $v20Metrics
})

$viewDir = Join-Path $ReviewRoot 'internal_views'
$captureJson = Join-Path $ReviewRoot 'internal_view_capture.json'
$hideNames = 'names:' + ((@(
  '\u50a8\u7269\u67dc\u95e8\u88c5\u914d_L6-1',
  '\u50a8\u7269\u67dc\u95e8\u88c5\u914d_L4-1',
  '\u50a8\u7269\u67dc\u95e8\u88c5\u914d_L2-1',
  '\u50a8\u7269\u67dc\u95e8\u88c5\u914d_R2-1',
  '\u50a8\u7269\u67dc\u95e8\u88c5\u914d_R4-1',
  '\u50a8\u7269\u67dc\u95e8\u88c5\u914d_R6-1'
) | ForEach-Object { ConvertFrom-JsonString $_ }) -join ';')
Invoke-CheckedProcess -FilePath 'cscript.exe' -Arguments @('//nologo', $CaptureScript, $PrimaryAssembly, $viewDir, $captureJson, 'close', $hideNames) -Label 'internal view capture'
$capture = Get-Content -Raw -LiteralPath $captureJson | ConvertFrom-Json
$savedViewCount = @($capture.views | Where-Object { $_.saved }).Count
if (-not $capture.opened -or -not $capture.read_only_requested -or $capture.hide_components.hidden_count -ne 6 -or $savedViewCount -ne 6 -or -not $capture.exited) {
  throw "internal view capture did not meet the 6-door/6-view/read-only contract: $captureJson"
}
Wait-SolidWorksExit -ProcessId ([int]$capture.solidworks_process_id) -Label 'internal view capture'

foreach ($variant in $sourceVariants) {
  foreach ($entry in $variant.files.GetEnumerator()) {
    $afterRecords.Add((Get-FileRecord -Path (Join-Path $variant.base $entry.Value) -Variant $variant.id -Role $entry.Key))
  }
}
$afterRecords.Add((Get-FileRecord -Path $v20Snapshot.path -Variant $v20Snapshot.id -Role $v20Snapshot.role))

$sourceChanges = [System.Collections.Generic.List[object]]::new()
foreach ($before in $beforeRecords) {
  $after = $afterRecords | Where-Object { $_.variant -eq $before.variant -and $_.role -eq $before.role } | Select-Object -First 1
  $changed = $null -eq $after -or $before.sha256 -ne $after.sha256 -or $before.length -ne $after.length -or $before.last_write_utc -ne $after.last_write_utc
  if ($changed) {
    $sourceChanges.Add([pscustomobject]@{ variant = $before.variant; role = $before.role; before = $before; after = $after })
  }
}
if ($sourceChanges.Count -ne 0) {
  throw "source provenance run changed $($sourceChanges.Count) source files"
}

$partComparisons = [System.Collections.Generic.List[object]]::new()
foreach ($role in @('vertical_left', 'vertical_right', 'maintenance_door', 'maintenance_lock_hole')) {
  $reference = $probeLookup["current_gold_source|$role"]
  foreach ($variantId in @('mirror_template_source', 'backup_20190423')) {
    $candidate = $probeLookup["$variantId|$role"]
    $partComparisons.Add([pscustomobject]@{
      role = $role
      reference_variant = 'current_gold_source'
      candidate_variant = $variantId
      comparison = Compare-PartMetrics -Reference $reference -Candidate $candidate
      reference_metrics = $reference
      candidate_metrics = $candidate
    })
  }
}
$partComparisons.Add([pscustomobject]@{
  role = 'maintenance_door'
  reference_variant = 'current_gold_source'
  candidate_variant = $v20Snapshot.id
  comparison = Compare-PartMetrics -Reference $probeLookup['current_gold_source|maintenance_door'] -Candidate $probeLookup["$($v20Snapshot.id)|maintenance_door"]
  reference_metrics = $probeLookup['current_gold_source|maintenance_door']
  candidate_metrics = $probeLookup["$($v20Snapshot.id)|maintenance_door"]
})

$assemblyComparisons = [System.Collections.Generic.List[object]]::new()
foreach ($variantId in @('mirror_template_source', 'backup_20190423')) {
  $assemblyComparisons.Add([pscustomobject]@{
    role = 'maintenance_assembly'
    reference_variant = 'current_gold_source'
    candidate_variant = $variantId
    comparison = Compare-AssemblyMetrics -Reference $assemblyLookup['current_gold_source'] -Candidate $assemblyLookup[$variantId]
    reference_metrics = $assemblyLookup['current_gold_source']
    candidate_metrics = $assemblyLookup[$variantId]
    evidence_boundary = 'component count and aggregate component boxes only; not mate, interference, tolerance, or manufacturing equivalence proof'
  })
}

$sourceManifest = [ordered]@{
  schema = 'winnsen.locker16029.pre_signoff_source_copy_manifest.v1'
  generated_at = (Get-Date).ToString('o')
  request_id = $RequestId
  copied_sources_used_for_read_only_or_copy_only_inspection = $copyRecords
  source_records_before = $beforeRecords
  source_records_after = $afterRecords
  source_changed_count = $sourceChanges.Count
  source_changes = $sourceChanges
  isolated_working_root = $WorkingRoot
  isolated_working_root_excluded_from_review_zip = $true
}
$sourceManifestPath = Join-Path $ReviewRoot 'source_copy_manifest.json'
$sourceManifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $sourceManifestPath -Encoding UTF8

$provenanceReport = [ordered]@{
  schema = 'winnsen.locker16029.source_provenance_report.v1'
  generated_at = (Get-Date).ToString('o')
  request_id = $RequestId
  status = 'EVIDENCE_COLLECTED_HISTORICAL_PROVENANCE_PARTIAL'
  release_eligible = $false
  automatic_restore_allowed = $false
  byte_identical_pre_v21_source_proven_for_all_touched_files = $false
  history = @(
    'v21 touched five files in the current gold/source directory; the run is retained as technical evidence only.',
    'v22 touched two additional vertical-part files in the mirror template directory; its source guard was incomplete.',
    'v23 localized all placement dependencies: controlledSourceFileCount=302, controlledSourceChangedCount=0, module placements=39, restored-v43 placements=7.'
  )
  v23_candidate_gate = [ordered]@{
    status = $summary.status
    release_eligible = [bool]$summary.releaseEligible
    exact_structure_gate = $summary.v43ExactStructureGateStatus
    controlled_source_integrity = $summary.controlledSourceImmutabilityStatus
    controlled_source_file_count = [int]$summary.controlledSourceFileCount
    controlled_source_changed_count = [int]$summary.controlledSourceChangedCount
    localized_module_target_placement_count = [int]$summary.localizedModuleTargetPlacementCount
    localized_restored_v43_placement_count = [int]$summary.localizedRestoredV43PlacementCount
    external_reference_count = [int]$summary.structureRecordExternalReferenceCount
  }
  source_inspection_guard = [ordered]@{
    source_changed_count = $sourceChanges.Count
    part_open_read_only_requested = $true
    assembly_inspection_used_isolated_copies = $true
    review_capture_read_only_requested = [bool]$capture.read_only_requested
  }
  part_probe_results = $partProbeRows
  part_comparisons = $partComparisons
  assembly_inspection_results = $assemblyRows
  assembly_comparisons = $assemblyComparisons
  interpretation = @(
    'GEOMETRY_METRICS_MATCH means body count, aggregate envelope, volume, and surface area matched within the stated tolerances.',
    'A metrics match does not prove byte identity, feature-tree identity, material identity, drawing revision identity, or that a file is the exact pre-v21 source.',
    'Assembly comparison is intentionally limited to component count and aggregate component boxes.',
    'No source file is automatically restored or overwritten by this evidence run.'
  )
}
$provenancePath = Join-Path $ReviewRoot 'source_provenance_report.json'
$provenanceReport | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $provenancePath -Encoding UTF8

$partTable = @($partComparisons | ForEach-Object {
  "| $($_.role) | $($_.candidate_variant) | $($_.comparison.status) |"
}) -join "`n"
$assemblyTable = @($assemblyComparisons | ForEach-Object {
  "| $($_.role) | $($_.candidate_variant) | $($_.comparison.status) |"
}) -join "`n"
$provenanceMarkdown = @"
# 16029 v23 historical source comparison

Conclusion: v23 closes controlled-source isolation for candidate generation. Historical byte-level provenance for the files touched by v21/v22 is still incomplete, so no automatic overwrite or restored-original claim is allowed.

## v23 automatic gates

- Candidate status: $($summary.status)
- Exact structure gate: $($summary.v43ExactStructureGateStatus)
- Controlled source integrity: $($summary.controlledSourceImmutabilityStatus)
- Controlled files: $($summary.controlledSourceFileCount); changed: $($summary.controlledSourceChangedCount)
- Localized placements: $($summary.localizedModuleTargetPlacementCount + $summary.localizedRestoredV43PlacementCount) (39 + 7)
- External references: $($summary.structureRecordExternalReferenceCount)
- Release eligible: false

## Zero-write guard

This comparison used isolated copies. Source SHA256, length, and last-write checks changed: $($sourceChanges.Count).

## Part geometry metrics

| Role | Compared variant | Result |
|---|---|---|
$partTable

GEOMETRY_METRICS_MATCH only means solid-body count, aggregate envelope, volume, and surface area matched within tolerance. It does not prove byte, feature-tree, material, drawing revision, or historical-source identity.

## Limited assembly metrics

| Role | Compared variant | Result |
|---|---|---|
$assemblyTable

Assembly comparison covers component count and component boxes only. It does not replace mate, interference, clearance, tolerance, or manufacturing signoff.

## Disposition boundary

- Do not automatically overwrite current_gold_source or mirror_template_source.
- Retain v21 as technical evidence after touching five current files; retain v22 as technical evidence after touching two mirror files.
- v23 is the current controlled candidate. Engineering and prototype signoff remain required before any release decision.
"@
$provenanceMarkdown | Set-Content -LiteralPath (Join-Path $ReviewRoot 'source_provenance_report.md') -Encoding UTF8

$checklist = @"
# 16029 v23 engineering pre-signoff checklist

Current classification: controlled_candidate_pass. This is not release PASS and is not production-ready.

## Automatically verified

- [x] Kept the 740W / L642-R246 / v43 seed route.
- [x] Exact v43 visible-structure contract PASS with zero failed checks.
- [x] Controlled-source integrity PASS for 302 files with changed=0.
- [x] Localized 39 module placements and 7 restored-v43 placements to candidate copies.
- [x] Primary assembly has zero external references.
- [x] Six doors, six lock tongues, six lock-hole datums, and one center maintenance sheet-metal part; electrical components excluded.
- [x] Captured six internal views read-only after hiding exactly six door components.
- [x] Historical comparisons used isolated copies; source changed count is zero.

## Structure engineer must confirm

- [ ] Confirm the lock tongue, hook, lock hole, and locating hole use the intended common datum; inspect engagement depth and offset on every door.
- [ ] Confirm real fit between shelf locating feet and front-frame locating notches, including direction, clearance, and assembly access.
- [ ] Confirm inner vertical partition reinforcement plate position, count, weld access, and load path against the 1000W gold/source intent.
- [ ] Inspect external surfaces for unintended through holes and disposition every hole against formal drawings.
- [ ] Check all six doors for opening, sag, gaps, collision, and stops.
- [ ] Confirm sheet-metal thickness, bend radius/K factor, welds, coating, and tolerance stack.
- [ ] Decide disposition for the five current files touched by v21 and the two mirror files touched by v22. Geometry similarity is not revision identity.

## Prototype and load validation

- [ ] Build and inspect a physical prototype; record assembly issues, lock life, and repeated open/close results.
- [ ] Verify shelf/cabinet target loads, tip resistance, and anchoring method.
- [ ] Feed prototype deviations into the formal SolidWorks 2020 model and drawings, then rerun all gates.

## Release boundary

- [ ] Start a separate release gate only after structure engineering, drawings/tolerances, supplier process, and prototype signoff are complete.
- [ ] Do not issue CrownCAD, DXF, BOM, or supplier production packages from this candidate.
"@
$checklist | Set-Content -LiteralPath (Join-Path $ReviewRoot 'engineering_signoff_checklist.md') -Encoding UTF8

$reviewFiles = Get-ChildItem -LiteralPath $ReviewRoot -Recurse -File | ForEach-Object {
  [pscustomobject]@{
    relative_path = $_.FullName.Substring($ReviewRoot.Length + 1)
    length = [long]$_.Length
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
  }
}
$reviewManifest = [ordered]@{
  schema = 'winnsen.locker16029.pre_signoff_review_manifest.v1'
  generated_at = (Get-Date).ToString('o')
  request_id = $RequestId
  classification = 'controlled_candidate_pre_signoff_review'
  release_eligible = $false
  candidate_root = $CandidateRoot
  primary_assembly = $PrimaryAssembly
  internal_view_count = $savedViewCount
  hidden_door_component_count = [int]$capture.hide_components.hidden_count
  source_changed_count = $sourceChanges.Count
  files = $reviewFiles
  excluded = @('isolated native source copies', 'supplier DXF', 'BOM', 'release approval')
}
$reviewManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ReviewRoot 'pre_signoff_review_manifest.json') -Encoding UTF8

if (Test-Path -LiteralPath $ReviewZip) {
  Assert-PathUnderRoot -Path $ReviewZip -AllowedRoot $LogRoot
  Remove-Item -LiteralPath $ReviewZip -Force
}
Compress-Archive -Path (Join-Path $ReviewRoot '*') -DestinationPath $ReviewZip -CompressionLevel Optimal

Assert-PathUnderRoot -Path $WorkingRoot -AllowedRoot $WorkingBase
Remove-Item -LiteralPath $WorkingRoot -Recurse -Force

[pscustomobject]@{
  request_id = $RequestId
  review_root = $ReviewRoot
  review_zip = $ReviewZip
  review_zip_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $ReviewZip).Hash
  source_changed_count = $sourceChanges.Count
  part_comparison_count = $partComparisons.Count
  assembly_comparison_count = $assemblyComparisons.Count
  internal_view_count = $savedViewCount
  hidden_door_component_count = [int]$capture.hide_components.hidden_count
  release_eligible = $false
} | ConvertTo-Json -Depth 6
