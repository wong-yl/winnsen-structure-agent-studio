param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$approvedZips = @(
    "workers\handoffs\16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip",
    "workers\handoffs\16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip",
    "workers\handoffs\16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip"
)

$legacyApiEndpoints = @(
    "/api/locker-16029-variant-rule-packet",
    "/api/locker-16029-verified-rule-packet",
    "/api/locker-16029-variant-quality-matrix",
    "/api/locker-16029-engineering-handoff-bundle"
)

$currentSolidWorksShortcut = "C:\Users\Public\Desktop\SOLIDWORKS 2020.lnk"
$currentSolidWorksExe = "D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe"
$solidWorksOpenEvidence = @(
    @{
        variant = "LMS"
        screenshot = "workers\generation_logs\cad_open_screenshots\solidworks2020_lms_step_open.png"
        json = "workers\generation_logs\cad_open_screenshots\solidworks2020_lms_step_open.png.json"
    },
    @{
        variant = "SML"
        screenshot = "workers\generation_logs\cad_open_screenshots\solidworks2020_sml_step_open.png"
        json = "workers\generation_logs\cad_open_screenshots\solidworks2020_sml_step_open.png.json"
    }
)

$blockedTerms = @(
    "1200W",
    "2117H",
    "W537",
    "1000W",
    "10/12/14",
    "rule-review",
    "RULE_REVIEW",
    "DUAL_RULE_REVIEW",
    "lightweight",
    "earlier",
    "SOLIDWORKS 2025",
    "SolidWorks 2025"
)

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

function Get-ObjectBlockAfter {
    param(
        [string]$Text,
        [string]$Needle
    )
    $start = $Text.IndexOf($Needle)
    if ($start -lt 0) {
        return ""
    }
    $end = $Text.IndexOf("`n  },", $start)
    if ($end -lt 0) {
        return $Text.Substring($start)
    }
    return $Text.Substring($start, $end - $start)
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zipFindings = New-Object System.Collections.Generic.List[object]
$nestedZipFindings = New-Object System.Collections.Generic.List[object]
foreach ($relativeZip in $approvedZips) {
    $zipPath = Join-Path $Root $relativeZip
    Add-Check -Name "approved_zip_exists:$relativeZip" -Ok (Test-Path -LiteralPath $zipPath -PathType Leaf) -Actual $zipPath -Expected "file exists"
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
        continue
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ($relativeZip -like "*DUAL_GOLD_VARIABLE*" -and $entry.FullName -like "*.zip") {
                $nestedZipFindings.Add([pscustomobject]@{
                    zip = $relativeZip
                    entry = $entry.FullName
                })
            }
            if ($entry.FullName -notmatch "\.(md|json|csv|txt)$") {
                continue
            }
            $reader = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8, $true)
            try {
                $text = $reader.ReadToEnd()
                foreach ($term in $blockedTerms) {
                    if ($text.Contains($term)) {
                        $zipFindings.Add([pscustomobject]@{
                            zip = $relativeZip
                            entry = $entry.FullName
                            term = $term
                        })
                    }
                }
            }
            finally {
                $reader.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

Add-Check -Name "approved_zip_text_scope_clean" -Ok ($zipFindings.Count -eq 0) -Actual $zipFindings.Count -Expected "0 blocked term hits in current handoff zips"
Add-Check -Name "dual_package_no_nested_zip_entries" -Ok ($nestedZipFindings.Count -eq 0) -Actual $nestedZipFindings.Count -Expected "DUAL package exposes LMS/SML folders directly, no zip inside zip"

$apiPath = Join-Path $Root "services\api\app\main.py"
$apiText = Read-TextFile $apiPath
$catalogStart = $apiText.IndexOf("def review_download_catalog()")
$catalogEnd = $apiText.IndexOf("def review_download_asset", $catalogStart)
$catalogText = if ($catalogStart -ge 0 -and $catalogEnd -gt $catalogStart) { $apiText.Substring($catalogStart, $catalogEnd - $catalogStart) } else { "" }
foreach ($relativeZip in $approvedZips) {
    $fileName = Split-Path $relativeZip -Leaf
    Add-Check -Name "download_catalog_includes:$fileName" -Ok $catalogText.Contains($fileName) -Actual $fileName -Expected "listed in review_download_catalog"
}
foreach ($term in $blockedTerms) {
    Add-Check -Name "download_catalog_excludes:$term" -Ok (-not $catalogText.Contains($term)) -Actual $term -Expected "not in review_download_catalog"
}

$appPath = Join-Path $Root "apps\web\src\App.tsx"
$appText = Read-TextFile $appPath
Add-Check -Name "primary_nav_limited_to_engineer_pages" -Ok $appText.Contains("new Set<PageId>(['overview', 'handoff', 'review'])") -Actual "PRIMARY_NAV_PAGE_IDS" -Expected "overview/handoff/review only"
Add-Check -Name "hidden_hash_pages_redirected" -Ok $appText.Contains("isPageId(value) && PRIMARY_NAV_PAGE_IDS.has(value)") -Actual "initialPageFromHash" -Expected "non-primary hash returns overview"
Add-Check -Name "frontend_review_queue_current_project_only" -Ok $appText.Contains("item.project === '16029 800W gold-variable'") -Actual "visibleReviewItems filter" -Expected "review page filters to current 800W gold-variable project only"
Add-Check -Name "frontend_solidworks_2020_label" -Ok ($appText.Contains("SOLIDWORKS 2020") -and -not $appText.Contains("SOLIDWORKS 2025")) -Actual "App.tsx CAD label" -Expected "SOLIDWORKS 2020 only"

$reviewPortalPath = Join-Path $Root "tools\serve_16029_review_downloads.mjs"
$reviewPortalText = Read-TextFile $reviewPortalPath
Add-Check -Name "review_login_portal_current_round" -Ok (
    $reviewPortalText.Contains("16029-800w-gold-variable-review-20260528") -and
    $reviewPortalText.Contains("16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip") -and
    $reviewPortalText.Contains("16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip") -and
    $reviewPortalText.Contains("16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip") -and
    $reviewPortalText.Contains("SolidWorks 2020")
) -Actual "tools/serve_16029_review_downloads.mjs" -Expected "current 800W gold-variable review round and three packages"
foreach ($term in $blockedTerms) {
    Add-Check -Name "review_login_portal_excludes:$term" -Ok (-not $reviewPortalText.Contains($term)) -Actual $term -Expected "not in review login portal"
}

$configPath = Join-Path $Root "services\api\app\config.py"
$configText = Read-TextFile $configPath
Add-Check -Name "solidworks_2020_shortcut_exists" -Ok (Test-Path -LiteralPath $currentSolidWorksShortcut -PathType Leaf) -Actual $currentSolidWorksShortcut -Expected "SolidWorks 2020 shortcut exists"
Add-Check -Name "solidworks_2020_exe_exists" -Ok (Test-Path -LiteralPath $currentSolidWorksExe -PathType Leaf) -Actual $currentSolidWorksExe -Expected "SolidWorks 2020 executable exists"
Add-Check -Name "api_config_solidworks_2020_default" -Ok ($configText.Contains($currentSolidWorksShortcut) -and $configText.Contains($currentSolidWorksExe) -and -not $configText.Contains("SOLIDWORKS 2025")) -Actual "services/api/app/config.py" -Expected "SolidWorks 2020 shortcut and exe defaults"
foreach ($evidence in $solidWorksOpenEvidence) {
    $screenshotPath = Join-Path $Root $evidence.screenshot
    $jsonEvidencePath = Join-Path $Root $evidence.json
    Add-Check -Name "solidworks_2020_open_screenshot_exists:$($evidence.variant)" -Ok (Test-Path -LiteralPath $screenshotPath -PathType Leaf) -Actual $screenshotPath -Expected "SolidWorks 2020 screenshot file exists"
    Add-Check -Name "solidworks_2020_open_json_exists:$($evidence.variant)" -Ok (Test-Path -LiteralPath $jsonEvidencePath -PathType Leaf) -Actual $jsonEvidencePath -Expected "SolidWorks 2020 open JSON exists"
    if (Test-Path -LiteralPath $jsonEvidencePath -PathType Leaf) {
        $openEvidence = Get-Content -LiteralPath $jsonEvidencePath -Encoding UTF8 | ConvertFrom-Json
        Add-Check -Name "solidworks_2020_opened:$($evidence.variant)" -Ok (
            $openEvidence.requested_mainline -eq "SolidWorks 2020" -and
            $openEvidence.solidworks_revision -like "28.*" -and
            $openEvidence.opened -eq $true -and
            $openEvidence.preview_saved -eq $true
        ) -Actual ($openEvidence | ConvertTo-Json -Compress) -Expected "SolidWorks 2020 revision 28.x opened STEP and saved screenshot"
    }
}

Add-Check -Name "current_scope_api_exists" -Ok $apiText.Contains('/api/locker-16029-current-handoff-scope') -Actual "/api/locker-16029-current-handoff-scope" -Expected "current handoff scope endpoint exists"
foreach ($endpoint in $legacyApiEndpoints) {
    $routeIndex = $apiText.IndexOf($endpoint)
    $nextRouteIndex = if ($routeIndex -ge 0) { $apiText.IndexOf('@app.', $routeIndex + $endpoint.Length) } else { -1 }
    $block = if ($routeIndex -ge 0 -and $nextRouteIndex -gt $routeIndex) {
        $apiText.Substring($routeIndex, $nextRouteIndex - $routeIndex)
    }
    elseif ($routeIndex -ge 0) {
        $apiText.Substring($routeIndex)
    }
    else {
        ""
    }
    Add-Check -Name "legacy_api_marked:$endpoint" -Ok ($block.Contains("mark_locker_16029_legacy_response") -and $apiText.Contains("legacy_evidence_only")) -Actual $endpoint -Expected "returns marked legacy_evidence_only response"
}

$manifestJsonPath = Join-Path $Root "data\locker_16029_project_route_manifest.json"
$manifestMdPath = Join-Path $Root "data\locker_16029_project_route_manifest.md"
Add-Check -Name "route_manifest_json_exists" -Ok (Test-Path -LiteralPath $manifestJsonPath -PathType Leaf) -Actual $manifestJsonPath -Expected "route manifest JSON exists"
Add-Check -Name "route_manifest_md_exists" -Ok (Test-Path -LiteralPath $manifestMdPath -PathType Leaf) -Actual $manifestMdPath -Expected "route manifest markdown exists"
if (Test-Path -LiteralPath $manifestJsonPath -PathType Leaf) {
    $manifest = Get-Content -LiteralPath $manifestJsonPath -Encoding UTF8 | ConvertFrom-Json
    $cadMainline = $manifest.current_route.cad_mainline
    Add-Check -Name "route_manifest_cad_mainline_2020" -Ok (
        $cadMainline.primary_cad -eq "SolidWorks 2020" -and
        $cadMainline.solidworks_shortcut -eq $currentSolidWorksShortcut -and
        $cadMainline.solidworks_exe -eq $currentSolidWorksExe
    ) -Actual ($cadMainline | ConvertTo-Json -Compress) -Expected "SolidWorks 2020 current CAD mainline"
}

$studioDataPath = Join-Path $Root "apps\web\src\data\studioData.ts"
$studioDataText = Read-TextFile $studioDataPath
Add-Check -Name "frontend_data_solidworks_2020_mainline" -Ok ($studioDataText.Contains("SolidWorks 2020") -and -not $studioDataText.Contains("SolidWorks 2025")) -Actual "studioData.ts" -Expected "current UI data references SolidWorks 2020 only"
$currentCapabilityBlock = Get-ObjectBlockAfter -Text $studioDataText -Needle "id: 'locker_16029_gold_variable_current'"
Add-Check -Name "frontend_current_16029_capability_exists" -Ok ($currentCapabilityBlock.Length -gt 0) -Actual "locker_16029_gold_variable_current" -Expected "current 16029 gold-variable capability exists"
Add-Check -Name "frontend_current_16029_capability_sw2020_verified" -Ok (
    $currentCapabilityBlock.Contains("status: 'generatable'") -and
    $currentCapabilityBlock.Contains("SolidWorks 2020") -and
    $currentCapabilityBlock.Contains("generate_16029_800w_gold_variable_model_freecad.py")
) -Actual "current capability block" -Expected "current capability uses SolidWorks 2020 evidence gate and fixed gold-variable generator"
foreach ($legacyCapabilityId in @("locker_16029_regression", "locker_16029_door_panel")) {
    $legacyBlock = Get-ObjectBlockAfter -Text $studioDataText -Needle "id: '$legacyCapabilityId'"
    Add-Check -Name "frontend_legacy_capability_reference_only:$legacyCapabilityId" -Ok ($legacyBlock.Contains("status: 'reference_only'") -and $legacyBlock.Contains("legacy evidence")) -Actual $legacyCapabilityId -Expected "reference_only legacy evidence"
}
$generatable16029Blocks = New-Object System.Collections.Generic.List[string]
$searchFrom = 0
while ($true) {
    $idx = $studioDataText.IndexOf("status: 'generatable'", $searchFrom)
    if ($idx -lt 0) {
        break
    }
    $blockStart = $studioDataText.LastIndexOf("`n  {", $idx)
    if ($blockStart -lt 0) {
        $blockStart = $idx
    }
    $blockEnd = $studioDataText.IndexOf("`n  },", $idx)
    if ($blockEnd -lt 0) {
        $blockEnd = $studioDataText.Length
    }
    $block = $studioDataText.Substring($blockStart, $blockEnd - $blockStart)
    if ($block.Contains("16029")) {
        $generatable16029Blocks.Add($block)
    }
    $searchFrom = $idx + 1
}
$badGeneratable16029 = New-Object System.Collections.Generic.List[object]
foreach ($block in $generatable16029Blocks) {
    foreach ($term in $blockedTerms) {
        if ($block.Contains($term)) {
            $badGeneratable16029.Add([pscustomobject]@{ term = $term; excerpt = $block.Substring(0, [Math]::Min(160, $block.Length)) })
        }
    }
}
Add-Check -Name "frontend_generatable_16029_no_legacy_terms" -Ok ($badGeneratable16029.Count -eq 0) -Actual $badGeneratable16029.Count -Expected "0 blocked terms in generatable 16029 capability blocks"

$obsoletePatterns = @(
    "16029_WIDTH_CANDIDATE*",
    "16029_HEIGHT_CANDIDATE*",
    "16029_800W_*RULE_REVIEW*",
    "16029_10_12_14*"
)
$obsoleteCounts = @{}
foreach ($pattern in $obsoletePatterns) {
    $obsoleteCounts[$pattern] = @(Get-ChildItem -LiteralPath (Join-Path $Root "workers\handoffs") -Filter $pattern -Force -ErrorAction SilentlyContinue).Count
}

$failed = @($checks | Where-Object { -not $_.ok -and $_.severity -eq "error" })
$gate = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    status = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }
    scope = "16029 800W gold-variable engineer-facing handoff scope"
    approved_zips = $approvedZips
    legacy_api_endpoints = $legacyApiEndpoints
    blocked_terms = $blockedTerms
    obsolete_handoff_inventory = $obsoleteCounts
    checks_total = $checks.Count
    checks_failed = $failed.Count
    zip_findings = $zipFindings
    checks = $checks
}

$dataDir = Join-Path $Root "data"
$jsonPath = Join-Path $dataDir "locker_16029_current_handoff_scope_gate.json"
$csvPath = Join-Path $dataDir "locker_16029_current_handoff_scope_gate.csv"
$mdPath = Join-Path $dataDir "locker_16029_current_handoff_scope_gate.md"

$gate | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$checks | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# 16029 Current Handoff Scope Gate")
$lines.Add("")
$lines.Add("- Status: $($gate.status)")
$lines.Add("- Scope: $($gate.scope)")
$lines.Add("- Checks: $($gate.checks_total)")
$lines.Add("- Failed: $($gate.checks_failed)")
$lines.Add("")
$lines.Add("## Approved engineer-facing zips")
$lines.Add("")
foreach ($zip in $approvedZips) {
    $lines.Add(('- `{0}`' -f $zip))
}
$lines.Add("")
$lines.Add("## Historical handoff inventory")
$lines.Add("")
foreach ($pattern in $obsoletePatterns) {
    $lines.Add(('- `{0}`: {1} present as history, not engineer-facing catalog' -f $pattern, $obsoleteCounts[$pattern]))
}
$lines.Add("")
$lines.Add("## Checks")
$lines.Add("")
foreach ($check in $checks) {
    $status = if ($check.ok) { "PASS" } else { "FAIL" }
    $lines.Add(('- {0} `{1}`: actual `{2}` expected `{3}`' -f $status, $check.name, $check.actual, $check.expected))
}
$lines | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Output ($gate | ConvertTo-Json -Depth 5)
if ($failed.Count -gt 0) {
    exit 1
}
