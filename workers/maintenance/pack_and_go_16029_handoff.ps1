$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$handoffRoot = Join-Path $repoRoot 'workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519'
$toolBuildScript = Join-Path $repoRoot 'workers\solidworks_tools\build_pack_and_go_assembly.ps1'
$toolExe = Join-Path $repoRoot 'workers\solidworks_tools\bin\PackAndGoAssembly.exe'
$closeActiveScript = Join-Path $repoRoot 'workers\solidworks_tools\sw_close_active_doc_if_path.js'
$exitIfEmptyScript = Join-Path $repoRoot 'workers\solidworks_tools\sw_exit_if_no_active_doc.js'

if (-not (Test-Path -LiteralPath $toolExe)) {
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $toolBuildScript | Out-Null
}
if (-not (Test-Path -LiteralPath $toolExe)) {
  throw "PackAndGoAssembly.exe was not built: $toolExe"
}

$variants = @(
  [pscustomobject]@{
    door = 10
    assembly = Join-Path $handoffRoot '10door\solidworks_native\16029_1000W_1917H_550D_10door_enriched_v2.SLDASM'
    outDir = Join-Path $handoffRoot '10door\solidworks_pack_and_go'
  },
  [pscustomobject]@{
    door = 12
    assembly = Join-Path $handoffRoot '12door\solidworks_native\16029_1000W_1917H_550D_12door_enriched_v2.SLDASM'
    outDir = Join-Path $handoffRoot '12door\solidworks_pack_and_go'
  },
  [pscustomobject]@{
    door = 14
    assembly = Join-Path $handoffRoot '14door\solidworks_native\16029_1000W_1917H_550D_14door_enriched_v2.SLDASM'
    outDir = Join-Path $handoffRoot '14door\solidworks_pack_and_go'
  }
)

function Close-SolidWorksDocs {
  for ($i = 0; $i -lt 30; $i++) {
    $out = & cscript.exe //Nologo $closeActiveScript
    if ($out -match 'no_active_doc') { break }
    Start-Sleep -Milliseconds 500
  }
}

$summary = @()
foreach ($variant in $variants) {
  New-Item -ItemType Directory -Force -Path $variant.outDir | Out-Null
  $jsonPath = Join-Path $variant.outDir 'pack_and_go_result.json'
  & $toolExe $variant.assembly $variant.outDir $jsonPath
  $exitCode = $LASTEXITCODE
  $result = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $inventory = $result.inventory
  $summary += [pscustomobject]@{
    door = $variant.door
    ok = ($exitCode -eq 0 -and $result.opened -eq $true -and $result.pack_and_go_created -eq $true -and $inventory.file_count -gt 0)
    exit_code = $exitCode
    document_count = $result.document_count
    file_count = $inventory.file_count
    sldasm_count = $inventory.sldasm_count
    sldprt_count = $inventory.sldprt_count
    total_mb = $inventory.total_mb
    out_dir = $variant.outDir
    result_json = $jsonPath
    top_assembly = Join-Path $variant.outDir (Split-Path -Leaf $variant.assembly)
    open_warnings = $result.open_warnings
    error = $result.error
  }
  Close-SolidWorksDocs
}

& cscript.exe //Nologo $exitIfEmptyScript | Out-Null

$summaryPath = Join-Path $handoffRoot 'solidworks_pack_and_go_summary.csv'
$summary | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding UTF8

$reportPath = Join-Path $handoffRoot 'SOLIDWORKS_PACK_AND_GO_20260522.md'
$overall = if (($summary | Where-Object { $_.ok -ne $true }).Count -eq 0) { 'PASS' } else { 'FAIL' }
$lines = @(
  '# SolidWorks Pack-and-Go Handoff 2026-05-22',
  '',
  'Scope: 16029 1000W x 1917H x 550D native enriched SolidWorks engineering references.',
  '',
  "Result: $overall",
  '',
  '| Door count | OK | Document count | Files | SLDASM | SLDPRT | Total MB | Package folder |',
  '| ---: | --- | ---: | ---: | ---: | ---: | ---: | --- |'
)
foreach ($row in $summary) {
  $lines += '| ' + (@(
      $row.door,
      $row.ok,
      $row.document_count,
      $row.file_count,
      $row.sldasm_count,
      $row.sldprt_count,
      $row.total_mb,
      $row.out_dir
    ) -join ' | ') + ' |'
}
$lines += @(
  '',
  'Use:',
  '',
  '- Prefer the top assembly inside each `solidworks_pack_and_go` folder when moving the handoff to another Windows workstation.',
  '- The normal native handoff SLDASM remains useful on this workstation; Pack-and-Go is for safer transfer.',
  '- This is still an engineering reference package, not a released drawing/BOM/DXF package.',
  '',
  'Evidence:',
  '',
  '- Summary CSV: `solidworks_pack_and_go_summary.csv`',
  '- Per-door result JSON files are stored inside each `solidworks_pack_and_go` folder.'
)
Set-Content -LiteralPath $reportPath -Value $lines -Encoding UTF8

$summary | Format-Table -AutoSize
