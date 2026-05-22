$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$handoffRoot = Join-Path $repoRoot 'workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519'
$inspectScript = Join-Path $repoRoot 'workers\solidworks_tools\sw_open_assembly_reference_inspect.js'
$previewScript = Join-Path $repoRoot 'workers\solidworks_tools\sw_open_zoom_save_preview.js'
$closeActiveScript = Join-Path $repoRoot 'workers\solidworks_tools\sw_close_active_doc_if_path.js'
$exitIfEmptyScript = Join-Path $repoRoot 'workers\solidworks_tools\sw_exit_if_no_active_doc.js'

$variants = @(
  [pscustomobject]@{
    door = 10
    packageDir = Join-Path $handoffRoot '10door\solidworks_pack_and_go'
    topAssembly = Join-Path $handoffRoot '10door\solidworks_pack_and_go\16029_1000W_1917H_550D_10door_enriched_v2.SLDASM'
  },
  [pscustomobject]@{
    door = 12
    packageDir = Join-Path $handoffRoot '12door\solidworks_pack_and_go'
    topAssembly = Join-Path $handoffRoot '12door\solidworks_pack_and_go\16029_1000W_1917H_550D_12door_enriched_v2.SLDASM'
  },
  [pscustomobject]@{
    door = 14
    packageDir = Join-Path $handoffRoot '14door\solidworks_pack_and_go'
    topAssembly = Join-Path $handoffRoot '14door\solidworks_pack_and_go\16029_1000W_1917H_550D_14door_enriched_v2.SLDASM'
  }
)

function Close-SolidWorksDocs {
  for ($i = 0; $i -lt 30; $i++) {
    $out = & cscript.exe //Nologo $closeActiveScript
    if ($out -match 'no_active_doc') { break }
    Start-Sleep -Milliseconds 500
  }
}

function Test-InPackagePath([string] $path, [string] $packageDir) {
  if ([string]::IsNullOrWhiteSpace($path)) { return $false }
  $fullPath = [System.IO.Path]::GetFullPath($path).TrimEnd('\')
  $fullRoot = [System.IO.Path]::GetFullPath($packageDir).TrimEnd('\')
  return $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)
}

$summary = @()
foreach ($variant in $variants) {
  if (-not (Test-Path -LiteralPath $variant.topAssembly)) {
    $summary += [pscustomobject]@{
      door = $variant.door
      ok = $false
      opened = $false
      preview_saved = $false
      top_reference_count = 0
      external_top_reference_count = 0
      missing_top_reference_path_count = 1
      package_cad_file_count = 0
      package_sldasm_count = 0
      package_sldprt_count = 0
      package_dir = $variant.packageDir
      top_assembly = $variant.topAssembly
      inspect_json = ''
      preview = ''
      error = 'top assembly missing'
    }
    continue
  }

  $inspectJson = Join-Path $handoffRoot ("solidworks_pack_and_go_{0}door_reference_audit.json" -f $variant.door)
  $previewPath = Join-Path $handoffRoot ("solidworks_pack_and_go_{0}door_open_check.png" -f $variant.door)

  & cscript.exe //Nologo $inspectScript $variant.topAssembly $inspectJson
  $inspectExit = $LASTEXITCODE
  $inspect = Get-Content -LiteralPath $inspectJson -Raw -Encoding UTF8 | ConvertFrom-Json
  Close-SolidWorksDocs

  & cscript.exe //Nologo $previewScript $variant.topAssembly $previewPath
  $previewExit = $LASTEXITCODE
  $previewJson = "$previewPath.json"
  $preview = Get-Content -LiteralPath $previewJson -Raw -Encoding UTF8 | ConvertFrom-Json
  Close-SolidWorksDocs

  $components = @($inspect.components)
  $references = @($inspect.references)
  $referencePaths = @($references | ForEach-Object { [string]$_.path } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  $externalPaths = @($referencePaths | Where-Object { -not (Test-InPackagePath $_ $variant.packageDir) })
  $missingPaths = @($references | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.path) })
  $cadFiles = @(Get-ChildItem -LiteralPath $variant.packageDir -File | Where-Object { $_.Extension -match '^\.(SLDASM|SLDPRT)$' })
  $sldasmFiles = @($cadFiles | Where-Object { $_.Extension -ieq '.SLDASM' })
  $sldprtFiles = @($cadFiles | Where-Object { $_.Extension -ieq '.SLDPRT' })
  $previewExists = Test-Path -LiteralPath $previewPath
  $previewSize = if ($previewExists) { (Get-Item -LiteralPath $previewPath).Length } else { 0 }

  $summary += [pscustomobject]@{
    door = $variant.door
    ok = ($inspectExit -eq 0 -and $previewExit -eq 0 -and $inspect.opened -eq $true -and $preview.opened -eq $true -and $preview.preview_saved -eq $true -and $references.Count -gt 0 -and $externalPaths.Count -eq 0 -and $missingPaths.Count -eq 0 -and $cadFiles.Count -gt 0 -and $previewSize -gt 10240)
    opened = $inspect.opened
    preview_saved = $preview.preview_saved
    top_reference_count = $references.Count
    external_top_reference_count = $externalPaths.Count
    missing_top_reference_path_count = $missingPaths.Count
    package_cad_file_count = $cadFiles.Count
    package_sldasm_count = $sldasmFiles.Count
    package_sldprt_count = $sldprtFiles.Count
    package_dir = $variant.packageDir
    top_assembly = $variant.topAssembly
    inspect_json = $inspectJson
    preview = $previewPath
    error = if ($externalPaths.Count -gt 0) { ($externalPaths | Select-Object -First 3) -join '; ' } else { '' }
  }
}

& cscript.exe //Nologo $exitIfEmptyScript | Out-Null

$summaryPath = Join-Path $handoffRoot 'solidworks_pack_and_go_independence_summary.csv'
$summary | Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding UTF8

$overall = if (($summary | Where-Object { $_.ok -ne $true }).Count -eq 0) { 'PASS' } else { 'FAIL' }
$reportPath = Join-Path $handoffRoot 'SOLIDWORKS_PACK_AND_GO_INDEPENDENCE_20260522.md'
$lines = @(
  '# SolidWorks Pack-and-Go Independence Verification 2026-05-22',
  '',
  'Scope: 16029 1000W x 1917H x 550D Pack-and-Go folders for 10/12/14 door engineering references.',
  '',
  'Method: open each top assembly inside its `solidworks_pack_and_go` folder, inspect component reference paths, save a preview PNG, then close the document before opening the next model.',
  '',
  "Result: $overall",
  '',
  '| Door count | OK | Opened | Preview saved | Top refs | External refs | Empty paths | CAD files | SLDASM | SLDPRT | Package folder |',
  '| ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |'
)
foreach ($row in $summary) {
  $lines += '| ' + (@(
      $row.door,
      $row.ok,
      $row.opened,
      $row.preview_saved,
      $row.top_reference_count,
      $row.external_top_reference_count,
      $row.missing_top_reference_path_count,
      $row.package_cad_file_count,
      $row.package_sldasm_count,
      $row.package_sldprt_count,
      $row.package_dir
    ) -join ' | ') + ' |'
}
$lines += @(
  '',
  'Evidence:',
  '',
  '- Summary CSV: `solidworks_pack_and_go_independence_summary.csv`',
  '- Per-door reference audit JSON: `solidworks_pack_and_go_10door_reference_audit.json`, `solidworks_pack_and_go_12door_reference_audit.json`, `solidworks_pack_and_go_14door_reference_audit.json`',
  '- Per-door preview PNG: `solidworks_pack_and_go_10door_open_check.png`, `solidworks_pack_and_go_12door_open_check.png`, `solidworks_pack_and_go_14door_open_check.png`',
  '',
  'Boundary:',
  '',
  '- This verifies that visible top-level component references resolve inside each Pack-and-Go folder on this workstation.',
  '- Recursive SolidWorks document packaging is covered by the Pack-and-Go result JSON and `solidworks_pack_and_go_summary.csv`.',
  '- It does not make these engineering references into released production drawings, BOMs, DXF files, or sheet-metal flat patterns.'
)
Set-Content -LiteralPath $reportPath -Value $lines -Encoding UTF8

$summary | Format-Table -AutoSize
