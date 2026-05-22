$ErrorActionPreference = 'Stop'
$dependencyCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_dependency_summary.csv'
$qualityCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_quality_summary.csv'
$packAndGoCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\solidworks_pack_and_go_summary.csv'
$packAndGoIndependenceCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\solidworks_pack_and_go_independence_summary.csv'
$verifiedRulePacketJson = 'D:\Winnsen_Structure_Agent_Studio\data\locker_16029_verified_rule_packet.json'
$verifiedRulePacketCsv = 'D:\Winnsen_Structure_Agent_Studio\data\locker_16029_verified_rule_packet.csv'
$statusPath = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\CHECK_HANDOFF_READY.status.txt'

$lines = @()
$lines += "16029 handoff ready check"
$lines += "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$lines += ""

if (-not (Test-Path -LiteralPath $dependencyCsv)) {
  $lines += "FAIL: dependency summary CSV missing: $dependencyCsv"
  Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
  exit 1
}
if (-not (Test-Path -LiteralPath $qualityCsv)) {
  $lines += "FAIL: quality summary CSV missing: $qualityCsv"
  Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
  exit 1
}

$dependencyRows = @(Import-Csv -LiteralPath $dependencyCsv)
$missingDependencies = @($dependencyRows | Where-Object {
  -not $_.source_path -or
  $_.exists -ne 'yes' -or
  -not (Test-Path -LiteralPath $_.source_path)
})

$qualityRows = @(Import-Csv -LiteralPath $qualityCsv)
$qualityFailures = @($qualityRows | Where-Object {
  $_.native_validation_ok -ne 'yes' -or
  [int]$_.native_failed_check_count -ne 0 -or
  [int]$_.dependency_missing_count -ne 0
})
$packAndGoRows = @()
$packAndGoFailures = @()
if ($packAndGoCsv) {
  if (-not (Test-Path -LiteralPath $packAndGoCsv)) {
    $packAndGoFailures = @([pscustomobject]@{door='all'; ok='missing_csv'; file_count=0; document_count=0})
  } else {
    $packAndGoRows = @(Import-Csv -LiteralPath $packAndGoCsv)
    $packAndGoFailures = @($packAndGoRows | Where-Object {
      $_.ok -ne 'True' -or
      [int]$_.file_count -le 0 -or
      [int]$_.sldasm_count -le 0 -or
      [int]$_.sldprt_count -le 0
    })
  }
}
$independenceRows = @()
$independenceFailures = @()
if ($packAndGoIndependenceCsv) {
  if (-not (Test-Path -LiteralPath $packAndGoIndependenceCsv)) {
    $independenceFailures = @([pscustomobject]@{door='all'; ok='missing_csv'; external_top_reference_count=0; missing_top_reference_path_count=0})
  } else {
    $independenceRows = @(Import-Csv -LiteralPath $packAndGoIndependenceCsv)
    $independenceFailures = @($independenceRows | Where-Object {
      $_.ok -ne 'True' -or
      [int]$_.external_top_reference_count -ne 0 -or
      [int]$_.missing_top_reference_path_count -ne 0 -or
      [int]$_.package_cad_file_count -le 0
    })
  }
}
$verifiedRuleRows = @()
$verifiedRuleFailures = @()
if (-not (Test-Path -LiteralPath $verifiedRulePacketJson)) {
  $verifiedRuleFailures = @([pscustomobject]@{door_count='all'; problem='verified rule packet JSON missing'})
} elseif (-not (Test-Path -LiteralPath $verifiedRulePacketCsv)) {
  $verifiedRuleFailures = @([pscustomobject]@{door_count='all'; problem='verified rule packet CSV missing'})
} else {
  $verifiedRulePacket = Get-Content -LiteralPath $verifiedRulePacketJson -Raw -Encoding UTF8 | ConvertFrom-Json
  if ($verifiedRulePacket.status -ne 'PASS') {
    $verifiedRuleFailures += [pscustomobject]@{door_count='all'; problem="verified rule packet status is $($verifiedRulePacket.status)"}
  }
  $verifiedRuleRows = @(Import-Csv -LiteralPath $verifiedRulePacketCsv)
  if ($verifiedRuleRows.Count -ne 3) {
    $verifiedRuleFailures += [pscustomobject]@{door_count='all'; problem="verified rule row count is $($verifiedRuleRows.Count), expected 3"}
  }
  $verifiedRuleFailures += @($verifiedRuleRows | Where-Object {
    $_.enabled_for_engineering_handoff -ne 'True' -or
    $_.right_column_rotation_ok -ne 'yes' -or
    $_.left_right_y_alignment_ok -ne 'yes' -or
    [double]$_.left_right_door_bbox_y_delta -gt 0.1 -or
    $_.native_skeleton_status -ne 'PASS' -or
    [int]$_.native_failed_errors -ne 0 -or
    $_.handoff_native_validation_ok -ne 'yes' -or
    [int]$_.dependency_missing_count -ne 0 -or
    $_.pack_and_go_ok -ne 'True' -or
    [int]$_.external_top_reference_count -ne 0 -or
    [int]$_.missing_top_reference_path_count -ne 0
  } | ForEach-Object {
    [pscustomobject]@{door_count=$_.door_count; problem='verified rule gate failed'}
  })
}

if ($missingDependencies.Count -gt 0 -or $qualityFailures.Count -gt 0 -or $packAndGoFailures.Count -gt 0 -or $independenceFailures.Count -gt 0 -or $verifiedRuleFailures.Count -gt 0) {
  $lines += "FAIL: handoff is not ready."
  $lines += "Missing dependencies: $($missingDependencies.Count)"
  foreach ($row in $missingDependencies) {
    $lines += ("  {0}door | {1} | {2}" -f $row.door_count, $row.role, $row.source_path)
  }
  $lines += "Quality failures: $($qualityFailures.Count)"
  foreach ($row in $qualityFailures) {
    $lines += ("  {0}door | validation={1} | failed_checks={2} | missing_dependencies={3}" -f $row.door_count, $row.native_validation_ok, $row.native_failed_check_count, $row.dependency_missing_count)
  }
  $lines += "Pack-and-Go failures: $($packAndGoFailures.Count)"
  foreach ($row in $packAndGoFailures) {
    $lines += ("  {0}door | ok={1} | files={2} | documents={3}" -f $row.door, $row.ok, $row.file_count, $row.document_count)
  }
  $lines += "Pack-and-Go independence failures: $($independenceFailures.Count)"
  foreach ($row in $independenceFailures) {
    $lines += ("  {0}door | ok={1} | external_refs={2} | empty_paths={3}" -f $row.door, $row.ok, $row.external_top_reference_count, $row.missing_top_reference_path_count)
  }
  $lines += "Verified rule packet failures: $($verifiedRuleFailures.Count)"
  foreach ($row in $verifiedRuleFailures) {
    $lines += ("  {0}door | {1}" -f $row.door_count, $row.problem)
  }
  Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
  exit 1
}

$lines += "PASS: 10/12/14 native SolidWorks references are ready for engineering review on this workstation."
$lines += "Dependency rows: $($dependencyRows.Count)"
foreach ($row in $qualityRows) {
  $lines += ("  {0}door | checks={1}/{2} | dependencies={3}/{4} | doors={5} | shelves={6} | crossbars={7}" -f $row.door_count, ([int]$row.native_check_count - [int]$row.native_failed_check_count), $row.native_check_count, $row.dependency_existing_count, $row.dependency_row_count, $row.reported_door_count, $row.shelf_count, $row.crossbar_count)
}
if ($packAndGoRows.Count -gt 0) {
  $lines += "Pack-and-Go:"
  foreach ($row in $packAndGoRows) {
    $lines += ("  {0}door | files={1} | assemblies={2} | parts={3} | total_mb={4}" -f $row.door, $row.file_count, $row.sldasm_count, $row.sldprt_count, $row.total_mb)
  }
}
if ($independenceRows.Count -gt 0) {
  $lines += "Pack-and-Go independence:"
  foreach ($row in $independenceRows) {
    $lines += ("  {0}door | top_refs={1} | external_refs={2} | empty_paths={3} | cad_files={4}" -f $row.door, $row.top_reference_count, $row.external_top_reference_count, $row.missing_top_reference_path_count, $row.package_cad_file_count)
  }
}
if ($verifiedRuleRows.Count -gt 0) {
  $lines += "Verified rule packet:"
  foreach ($row in $verifiedRuleRows) {
    $lines += ("  {0}door | rows={1} | door_h={2} | pitch={3} | shelves={4} | crossbars={5} | mirror={6} | bbox_d={7}" -f $row.door_count, $row.rows_per_column, $row.door_height_mm, $row.door_pitch_mm, $row.shelves, $row.front_frame_crossbars, $row.right_column_rotation_ok, $row.left_right_door_bbox_y_delta)
  }
}
Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
exit 0
