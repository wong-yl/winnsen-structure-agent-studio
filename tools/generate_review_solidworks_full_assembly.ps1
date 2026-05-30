param(
  [Parameter(Mandatory = $true)]
  [string] $RequestId,

  [double] $CabinetWidthMm = 740,
  [double] $CabinetHeightMm = 1917,
  [double] $CabinetDepthMm = 550,

  [string] $OutputDir = ''
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

function Wait-File([string] $Path, [string] $Label, [int] $TimeoutSeconds = 10) {
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

function Read-Json([string] $Path) {
  return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$supportedWidth = 740.0
$supportedHeight = 1917.0
$supportedDepth = 550.0
if ([Math]::Abs($CabinetWidthMm - $supportedWidth) -gt 0.5 -or
    [Math]::Abs($CabinetHeightMm - $supportedHeight) -gt 0.5 -or
    [Math]::Abs($CabinetDepthMm - $supportedDepth) -gt 0.5) {
  throw "Template-backed full assembly worker currently supports 740W x 1917H x 550D only; requested $(Invariant $CabinetWidthMm)W x $(Invariant $CabinetHeightMm)H x $(Invariant $CabinetDepthMm)D."
}

$root = Split-Path -Parent $PSScriptRoot
$toolDir = Join-Path $root 'workers\solidworks_tools'
$templateRoot = Join-Path $root 'workers\generated_models\SW-NATIVE-16029-740W-1917H-550D-L642-R246-ORDINARY-20260528'
$sourceAssembly = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue.SLDASM'
$sourceResult = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue_result.json'
$sourceComponents = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue_components.json'
$sourcePlacements = Join-Path $templateRoot 'v43_full\candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue_placements.tsv'
$captureDir = Join-Path $templateRoot 'v43_full\review_captures_latest'

Assert-File $sourceAssembly 'template full assembly'
Assert-File $sourceResult 'template build result'
Assert-File $sourceComponents 'template component inspection'
Assert-File $sourcePlacements 'template placement table'
Assert-Dir $captureDir 'template review captures'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $folder = "sw2020_full_$(Token $CabinetWidthMm)W_L642_R246"
  $OutputDir = Join-Path (Join-Path $root "workers\generated_models\review_generation_requests\$RequestId") $folder
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$packDir = Join-Path $OutputDir 'pack_and_go'
$evidenceDir = Join-Path $OutputDir 'evidence'
$captureOutDir = Join-Path $OutputDir 'review_captures'
New-Item -ItemType Directory -Force -Path $packDir | Out-Null
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
New-Item -ItemType Directory -Force -Path $captureOutDir | Out-Null

Invoke-External 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $toolDir 'build_pack_and_go_assembly.ps1')) 'compile pack-and-go assembly tool'

$packJson = Join-Path $evidenceDir 'solidworks_2020_pack_and_go_result.json'
$packTool = Join-Path $toolDir 'bin\PackAndGoAssembly.exe'
Assert-File $packTool 'pack-and-go assembly tool'
Invoke-External $packTool @($sourceAssembly, $packDir, $packJson) 'pack SolidWorks 2020 full assembly'
Wait-File $packJson 'pack-and-go result json'
$packResult = Read-Json $packJson
if (-not $packResult.opened -or -not $packResult.pack_and_go_created -or -not $packResult.inventory -or $packResult.inventory.file_count -le 0) {
  throw "Pack-and-Go did not produce a usable package: $packJson"
}

$primaryAssembly = Join-Path $packDir (Split-Path -Leaf $sourceAssembly)
if (-not (Test-Path -LiteralPath $primaryAssembly -PathType Leaf)) {
  Copy-Item -LiteralPath $sourceAssembly -Destination $primaryAssembly -Force
}

Copy-Item -LiteralPath $sourceResult -Destination (Join-Path $evidenceDir 'template_build_result.json') -Force
Copy-Item -LiteralPath $sourceComponents -Destination (Join-Path $evidenceDir 'template_components.json') -Force
Copy-Item -LiteralPath $sourcePlacements -Destination (Join-Path $evidenceDir 'template_placements.tsv') -Force
Get-ChildItem -LiteralPath $captureDir -File |
  Where-Object { -not $_.Name.StartsWith('~$') } |
  Copy-Item -Destination $captureOutDir -Force

$readme = @(
  '# SolidWorks 2020 Full Assembly Review Package',
  '',
  'This package is generated from the review queue by using the current verified 740W L642/R246 full-assembly template.',
  '',
  '## Boundary',
  '',
  '- CAD mainline: SolidWorks 2020.',
  '- This package is a review/download package, not a production drawing release.',
  '- Template route: 740W x 1917H x 550D, left column 6/4/2, right column 2/4/6, ordinary storage doors.',
  '- The historical direct per-part assembly generation route remains disabled because of transform reliability issues.',
  '',
  '## Files',
  '',
  '- `pack_and_go/`: flattened SolidWorks Pack-and-Go output containing `.SLDASM` and `.SLDPRT` files.',
  '- `evidence/`: build result, component inspection, placement table, and Pack-and-Go result JSON.',
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
$summary = [ordered] @{
  status = 'solidworks_2020_full_assembly_ready'
  resultKind = 'solidworks2020_full_assembly_model'
  requestId = $RequestId
  cadMainline = 'SolidWorks 2020'
  cabinetWidthMm = $CabinetWidthMm
  cabinetHeightMm = $CabinetHeightMm
  cabinetDepthMm = $CabinetDepthMm
  template = '740W_L642_R246_v43_hidden_lock_body_restored_tongue'
  sourceAssembly = $sourceAssembly
  outputDir = $OutputDir
  packAndGoDir = $packDir
  primaryAssembly = $primaryAssembly
  packAndGoResult = $packJson
  sldasmCount = @($packFiles | Where-Object { $_.Extension.ToLowerInvariant() -eq '.sldasm' }).Count
  sldprtCount = @($packFiles | Where-Object { $_.Extension.ToLowerInvariant() -eq '.sldprt' }).Count
  totalMb = [Math]::Round((($packFiles | Measure-Object Length -Sum).Sum / 1048576.0), 3)
  modelGeneratedAt = (Get-Date).ToUniversalTime().ToString('o')
  boundary = 'Template-backed SolidWorks 2020 full assembly package for review; arbitrary full-assembly parameter generation is not enabled by this worker.'
  outputFiles = @($outputFiles + 'solidworks_2020_full_assembly_generation_summary.json' | Select-Object -Unique)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryJson -Encoding UTF8
Write-Output $summaryJson
