param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Name,
        [bool]$Ok,
        [object]$Actual,
        [object]$Expected,
        [string]$Severity = "error"
    )
    $checks.Add([pscustomobject]@{
        name = $Name
        ok = $Ok
        actual = $Actual
        expected = $Expected
        severity = $Severity
    })
}

function Read-TextFile {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Add-PinnedAssetCheck {
    param(
        [string]$Name,
        [string]$Path,
        [long]$ExpectedSize,
        [string]$ExpectedSha256
    )
    $exists = (-not [string]::IsNullOrWhiteSpace($Path)) -and (Test-Path -LiteralPath $Path -PathType Leaf)
    Add-Check -Name "${Name}_exists" -Ok $exists -Actual $Path -Expected "file exists"
    if (-not $exists) { return }
    $item = Get-Item -LiteralPath $Path
    $sha = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    Add-Check -Name "${Name}_size" -Ok ($item.Length -eq $ExpectedSize) -Actual $item.Length -Expected $ExpectedSize
    Add-Check -Name "${Name}_sha256" -Ok ($sha -eq $ExpectedSha256) -Actual $sha -Expected $ExpectedSha256
}

function Find-PinnedAssetPath {
    param(
        [string]$Directory,
        [string]$Prefix,
        [string]$DateToken
    )
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) { return $null }
    $matches = @(Get-ChildItem -LiteralPath $Directory -File -Filter "${Prefix}*${DateToken}.zip")
    if ($matches.Count -ne 1) { return $null }
    return $matches[0].FullName
}

$nativeModulePath = Join-Path $Root "tools\locker_16029_native_generator.mjs"
$nativeCliPath = Join-Path $Root "tools\process_16029_native_generation_request.mjs"
$nativeVerifierPath = Join-Path $Root "tools\verify_16029_native_generator.mjs"
$portalPath = Join-Path $Root "tools\serve_16029_review_downloads.mjs"
$apiPath = Join-Path $Root "services\api\app\main.py"
$appPath = Join-Path $Root "apps\web\src\App.tsx"
$studioDataPath = Join-Path $Root "apps\web\src\data\studioData.ts"

foreach ($item in @(
    @{ name = "native_generator_module"; path = $nativeModulePath },
    @{ name = "native_generator_cli"; path = $nativeCliPath },
    @{ name = "native_generator_verifier"; path = $nativeVerifierPath },
    @{ name = "review_portal"; path = $portalPath }
)) {
    Add-Check -Name "$($item.name)_exists" -Ok (Test-Path -LiteralPath $item.path -PathType Leaf) -Actual $item.path -Expected "file exists"
}

$legacyPaths = @(
    "tools\generate_review_solidworks_full_assembly.ps1",
    "tools\generate_review_solidworks_single_door.ps1",
    "tools\generate_16029_parametric_scaffold_freecad.py",
    "tools\generate_review_task_simple_freecad_model.py",
    "tools\scale_16029_source_step_freecad.py",
    "tools\locker_16029_template_rules.mjs",
    "tools\locker_16029_controlled_generation_policy.mjs",
    "tools\process_16029_review_generation_queue.mjs",
    "tools\locker_16029_generation_cache.mjs",
    "tools\verify_16029_generation_cache.mjs",
    "tools\verify_16029_template_rule_matrix.mjs"
)
$remainingLegacy = @($legacyPaths | Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) })
Add-Check -Name "legacy_generator_chain_removed" -Ok ($remainingLegacy.Count -eq 0) -Actual ($remainingLegacy -join ", ") -Expected "no legacy generator files"

if (Test-Path -LiteralPath $nativeModulePath -PathType Leaf) {
    $nativeText = Read-TextFile $nativeModulePath
    Add-Check -Name "native_generator_identity" -Ok ($nativeText.Contains("16029-solidworks-native-generator-v1") -and $nativeText.Contains("verified_native_seeds_only")) -Actual $nativeModulePath -Expected "SolidWorks native verified-seed authority"
    Add-Check -Name "native_generator_seed_family" -Ok ($nativeText.Contains("v35-740w-six-door-l642-r246") -and $nativeText.Contains("v36-740w-four-door-l66-r66") -and $nativeText.Contains("v37-760w-six-door-l642-r246")) -Actual $nativeModulePath -Expected "V35, V36, V37 seeds"
    Add-Check -Name "native_generator_pending_v38" -Ok $nativeText.Contains("v38-760w-four-door-l66-r66") -Actual $nativeModulePath -Expected "V38 remains a named pending recipe"
}

if (Test-Path -LiteralPath $portalPath -PathType Leaf) {
    $portalText = Read-TextFile $portalPath
    Add-Check -Name "portal_native_request_store" -Ok ($portalText.Contains("createNativeTaskStore") -and $portalText.Contains("generationTaskSnapshot") -and $portalText.Contains("listSnapshot") -and $portalText.Contains("normalize16029NativeModelRequest")) -Actual $portalPath -Expected "native request resolver and unified task-file source store"
    Add-Check -Name "portal_current_seed_assets" -Ok ($portalText.Contains("16029-v43-v37-760w-six-door-engineering-assistance-zip") -and $portalText.Contains("16029-v43-v36-four-door-engineering-assistance-zip") -and $portalText.Contains("16029-v43-v35-one-door-one-lock-hole-rereview-zip")) -Actual $portalPath -Expected "V35, V36, V37 assets"
    Add-Check -Name "portal_no_legacy_runtime_binding" -Ok (-not $portalText.Contains("process_16029_review_generation_queue") -and -not $portalText.Contains("generate_review_solidworks_full_assembly") -and -not $portalText.Contains("readCurrentDeliveryManifest")) -Actual $portalPath -Expected "no old queue, generator, or delivery-manifest request filter"
}

$apiText = Read-TextFile $apiPath
$appText = Read-TextFile $appPath
$studioDataText = Read-TextFile $studioDataPath
Add-Check -Name "api_legacy_16029_task_route_retired" -Ok ($apiText.Contains('def ensure_generation_route_enabled') -and $apiText.Contains('capability_id.startswith("locker_16029_")') -and $apiText.Contains("status_code=410") -and $apiText.Contains('ensure_generation_route_enabled(payload.capability_id)') -and $apiText.Contains('ensure_generation_route_enabled(task.capability_id)')) -Actual $apiPath -Expected "all legacy 16029 SQLite task creation and execution routes are blocked"
Add-Check -Name "frontend_native_assistance_entry" -Ok ($appText.Contains("DEFAULT_MODEL_CAPABILITY_ID = 'locker_16029_native_assistance'") -and $appText.Contains("activeCapability.id.startsWith('locker_16029_')") -and $appText.Contains("ENGINEER_REVIEW_PORTAL_URL")) -Actual $appPath -Expected "all 16029 generation entries route to the native portal"
Add-Check -Name "studio_data_native_assistance_entry" -Ok ($studioDataText.Contains("locker_16029_native_assistance") -and $studioDataText.Contains("process_16029_native_generation_request.mjs")) -Actual $studioDataPath -Expected "new native model capability"

$requestRoot = Join-Path $Root "workers\generated_models\review_generation_requests"
$v35Asset = Find-PinnedAssetPath -Directory (Join-Path $requestRoot "v43-int-v35-one-door-one-lock-hole-fix-r1") -Prefix "16029_v35_" -DateToken "20260809"
$v36Asset = Find-PinnedAssetPath -Directory (Join-Path $requestRoot "v43-int-v36-four-door-l66-r66-r2") -Prefix "16029_v36_740" -DateToken "20260810"
$v37Asset = Find-PinnedAssetPath -Directory (Join-Path $requestRoot "v43-int-v37-760w-six-door-l642-r246-r1") -Prefix "16029_v37_760" -DateToken "20260811"
Add-PinnedAssetCheck -Name "v35_native_seed_zip" -Path $v35Asset -ExpectedSize 24741785 -ExpectedSha256 "440596F0C9E321D0FF04EC978EEBAB31DDFA6D542E337BDA00C5A3B9701963FA"
Add-PinnedAssetCheck -Name "v36_native_seed_zip" -Path $v36Asset -ExpectedSize 25096688 -ExpectedSha256 "DEF241BB9B0D530EB73130C5A3FCBDC22F206B6A59C65EBC5F6CE16B985C3AA4"
Add-PinnedAssetCheck -Name "v37_native_seed_zip" -Path $v37Asset -ExpectedSize 24535798 -ExpectedSha256 "6B9F62E4F697B96909131D81EAD0FA341E059530204DB76480148460883CD1C0"

$failed = @($checks | Where-Object { -not $_.ok -and $_.severity -eq "error" })
$gate = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    status = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }
    scope = "16029 SolidWorks native structure-engineering assistance model scope"
    current_generator_id = "16029-solidworks-native-generator-v1"
    checks_total = $checks.Count
    checks_failed = $failed.Count
    checks = $checks
}

$dataDir = Join-Path $Root "data"
$jsonPath = Join-Path $dataDir "locker_16029_current_handoff_scope_gate.json"
$csvPath = Join-Path $dataDir "locker_16029_current_handoff_scope_gate.csv"
$mdPath = Join-Path $dataDir "locker_16029_current_handoff_scope_gate.md"
$gate | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$checks | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# 16029 Native Structure-Assistance Scope Gate")
$lines.Add("")
$lines.Add("- Status: $($gate.status)")
$lines.Add("- Scope: $($gate.scope)")
$lines.Add("- Generator: $($gate.current_generator_id)")
$lines.Add("- Checks: $($gate.checks_total)")
$lines.Add("- Failed: $($gate.checks_failed)")
$lines.Add("")
$lines.Add("## Checks")
$lines.Add("")
foreach ($check in $checks) {
    $status = if ($check.ok) { "PASS" } else { "FAIL" }
    $lines.Add(('- {0} `{1}`: actual `{2}` expected `{3}`' -f $status, $check.name, $check.actual, $check.expected))
}
$lines | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Output ($gate | ConvertTo-Json -Depth 5)
if ($failed.Count -gt 0) { exit 1 }
