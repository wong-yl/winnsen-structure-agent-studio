$ErrorActionPreference = 'Stop'
$dependencyCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_dependency_summary.csv'
$qualityCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_quality_summary.csv'
$packAndGoCsv = 'D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\solidworks_pack_and_go_summary.csv'
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

if ($missingDependencies.Count -gt 0 -or $qualityFailures.Count -gt 0 -or $packAndGoFailures.Count -gt 0) {
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
Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
exit 0
