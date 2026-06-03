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

function Read-JsonFile {
    param([string]$Path)
    return Get-Content -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json
}

function Read-TextFile {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

$manifestPath = Join-Path $Root "data\locker_16029_v43_internal_sheetmetal_delivery.json"
$manifestExists = Test-Path -LiteralPath $manifestPath -PathType Leaf
Add-Check -Name "v43_delivery_manifest_exists" -Ok $manifestExists -Actual $manifestPath -Expected "manifest exists"

if ($manifestExists) {
    $manifest = Read-JsonFile $manifestPath
    $requestId = [string]$manifest.current_request_id
    $primaryAssembly = [string]$manifest.delivery.primary_assembly
    $zipPath = [string]$manifest.delivery.zip_path
    $screenshotFolder = [string]$manifest.delivery.screenshot_folder
    $handoffNote = [string]$manifest.delivery.handoff_note
    $summaryPath = [string]$manifest.evidence.summary
    $goldGatePath = [string]$manifest.evidence.gold_structure_gate
    $feedbackPath = [string]$manifest.evidence.structure_feedback
    $restorePath = [string]$manifest.evidence.door_lock_tongue_restore

    Add-Check -Name "current_request_id_is_v18" -Ok ($requestId -eq "v43-int-v18-lockfix") -Actual $requestId -Expected "v43-int-v18-lockfix"
    Add-Check -Name "route_width_740" -Ok ([int]$manifest.route.cabinet_width_mm -eq 740) -Actual $manifest.route.cabinet_width_mm -Expected 740
    Add-Check -Name "route_sequence_l642_r246" -Ok ($manifest.route.row_sequence -eq "L642-R246") -Actual $manifest.route.row_sequence -Expected "L642-R246"
    Add-Check -Name "cad_mainline_sw2020" -Ok ($manifest.route.cad_mainline -eq "SolidWorks 2020") -Actual $manifest.route.cad_mainline -Expected "SolidWorks 2020"
    Add-Check -Name "user_visual_confirmed" -Ok ($manifest.verified.user_visual_confirmed -eq $true) -Actual $manifest.verified.user_visual_confirmed -Expected $true

    foreach ($item in @(
        @{ name = "primary_assembly_exists"; path = $primaryAssembly },
        @{ name = "download_zip_exists"; path = $zipPath },
        @{ name = "handoff_note_exists"; path = $handoffNote },
        @{ name = "summary_exists"; path = $summaryPath },
        @{ name = "gold_gate_exists"; path = $goldGatePath },
        @{ name = "structure_feedback_exists"; path = $feedbackPath },
        @{ name = "lock_tongue_restore_exists"; path = $restorePath }
    )) {
        Add-Check -Name $item.name -Ok (Test-Path -LiteralPath $item.path) -Actual $item.path -Expected "path exists"
    }
    Add-Check -Name "screenshot_folder_recorded" -Ok (-not [string]::IsNullOrWhiteSpace($screenshotFolder)) -Actual $screenshotFolder -Expected "screenshot folder path recorded"

    if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
        $zipSize = (Get-Item -LiteralPath $zipPath).Length
        Add-Check -Name "download_zip_size_nontrivial" -Ok ($zipSize -gt 95MB) -Actual $zipSize -Expected "> 95 MB"
    }

    if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
        $summary = Read-JsonFile $summaryPath
        Add-Check -Name "summary_ready_status" -Ok ($summary.status -eq "solidworks_2020_full_assembly_ready") -Actual $summary.status -Expected "solidworks_2020_full_assembly_ready"
        Add-Check -Name "summary_gold_gate_pass" -Ok ($summary.goldStructureGateStatus -eq "PASS") -Actual $summary.goldStructureGateStatus -Expected "PASS"
        Add-Check -Name "summary_feedback_clean" -Ok ($summary.structureFeedbackStatus -eq "clean") -Actual $summary.structureFeedbackStatus -Expected "clean"
        Add-Check -Name "summary_lock_tongue_count_6" -Ok ([int]$summary.doorLockTongueCount -eq 6) -Actual $summary.doorLockTongueCount -Expected 6
        Add-Check -Name "summary_lock_tongue_restore_added_6" -Ok ([int]$summary.doorLockTongueRestoreAddedCount -eq 6) -Actual $summary.doorLockTongueRestoreAddedCount -Expected 6
        Add-Check -Name "summary_electric_components_zero" -Ok ([int]$summary.electricalOrElectricLockComponentCount -eq 0) -Actual $summary.electricalOrElectricLockComponentCount -Expected 0
        Add-Check -Name "summary_back_seam_centered" -Ok ($summary.backSheetMetalRepairStatus -eq "side_panel_sheetmetal_back_flange_centered") -Actual $summary.backSheetMetalRepairStatus -Expected "side_panel_sheetmetal_back_flange_centered"
    }

    if (Test-Path -LiteralPath $goldGatePath -PathType Leaf) {
        $goldGate = Read-JsonFile $goldGatePath
        $issueCount = if ($null -eq $goldGate.issues) { 0 } else { @($goldGate.issues).Count }
        Add-Check -Name "gold_structure_gate_pass" -Ok ($goldGate.status -eq "PASS") -Actual $goldGate.status -Expected "PASS"
        Add-Check -Name "gold_structure_gate_no_issues" -Ok ($issueCount -eq 0) -Actual $issueCount -Expected 0
    }

    if (Test-Path -LiteralPath $feedbackPath -PathType Leaf) {
        $feedback = Read-JsonFile $feedbackPath
        $issueCount = if ($null -eq $feedback.issues) { 0 } else { @($feedback.issues).Count }
        Add-Check -Name "structure_feedback_clean" -Ok ($feedback.status -eq "clean") -Actual $feedback.status -Expected "clean"
        Add-Check -Name "structure_feedback_no_issues" -Ok ($issueCount -eq 0) -Actual $issueCount -Expected 0
        Add-Check -Name "structure_feedback_lock_tongue_count_6" -Ok ([int]$feedback.derived.doorLockTongueCount -eq 6) -Actual $feedback.derived.doorLockTongueCount -Expected 6
    }

    if (Test-Path -LiteralPath $restorePath -PathType Leaf) {
        $restore = Read-JsonFile $restorePath
        $restoreItems = @($restore.items)
        $savedCount = @($restoreItems | Where-Object { $_.added -eq $true -and $_.saved -eq $true -and [string]::IsNullOrEmpty([string]$_.error) }).Count
        $addedCount = if ($null -ne $restore.addedCount) { [int]$restore.addedCount } else { [int]$restore.added_count }
        Add-Check -Name "lock_tongue_restore_status" -Ok ($restore.status -eq "restored") -Actual $restore.status -Expected "restored"
        Add-Check -Name "lock_tongue_restore_added_6" -Ok ($addedCount -eq 6) -Actual $addedCount -Expected 6
        Add-Check -Name "lock_tongue_restore_saved_6" -Ok ($savedCount -eq 6) -Actual $savedCount -Expected 6
    }

    $requestPath = Join-Path $Root "data\review_generation_requests\$requestId.json"
    $indexPath = Join-Path $Root "data\review_generation_requests\generation_request_index.json"
    Add-Check -Name "request_record_exists" -Ok (Test-Path -LiteralPath $requestPath -PathType Leaf) -Actual $requestPath -Expected "request JSON exists"
    Add-Check -Name "generation_index_exists" -Ok (Test-Path -LiteralPath $indexPath -PathType Leaf) -Actual $indexPath -Expected "generation index exists"
    if (Test-Path -LiteralPath $requestPath -PathType Leaf) {
        $requestRecord = Read-JsonFile $requestPath
        Add-Check -Name "request_download_url_current" -Ok ($requestRecord.downloadUrl -eq $manifest.delivery.download_url) -Actual $requestRecord.downloadUrl -Expected $manifest.delivery.download_url
        Add-Check -Name "request_zip_path_current" -Ok ($requestRecord.zipPath -eq $zipPath) -Actual $requestRecord.zipPath -Expected $zipPath
    }
    if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
        $index = Read-JsonFile $indexPath
        Add-Check -Name "generation_index_current_first" -Ok ($index.requests[0].id -eq $requestId) -Actual $index.requests[0].id -Expected $requestId
    }
}

$apiPath = Join-Path $Root "services\api\app\main.py"
$appPath = Join-Path $Root "apps\web\src\App.tsx"
$studioDataPath = Join-Path $Root "apps\web\src\data\studioData.ts"
$reviewPortalPath = Join-Path $Root "tools\serve_16029_review_downloads.mjs"

$apiText = Read-TextFile $apiPath
$appText = Read-TextFile $appPath
$studioDataText = Read-TextFile $studioDataPath
$reviewPortalText = Read-TextFile $reviewPortalPath

Add-Check -Name "api_catalog_v43_current_zip" -Ok ($apiText.Contains("16029-v43-internal-sheetmetal-lockfix-zip") -and $apiText.Contains("review_generation_v43-int-v18-lockfix_solidworks2020_full_assembly.zip")) -Actual "services/api/app/main.py" -Expected "v43 current zip in API catalog"
Add-Check -Name "api_scope_v43" -Ok $apiText.Contains("16029 740W / L642-R246 / v43") -Actual "services/api/app/main.py" -Expected "v43 current route copy"
Add-Check -Name "frontend_handoff_v43" -Ok $appText.Contains("16029 740W / L642-R246 / v43") -Actual "apps/web/src/App.tsx" -Expected "v43 handoff copy"
Add-Check -Name "frontend_current_v43_asset_highlight" -Ok $appText.Contains("asset.id.includes('v43-internal-sheetmetal')") -Actual "apps/web/src/App.tsx" -Expected "v43 asset highlighted"
Add-Check -Name "frontend_review_filter_v43" -Ok $appText.Contains("item.project === '16029 740W / L642-R246 / v43'") -Actual "apps/web/src/App.tsx" -Expected "review page filters v43 current route"
Add-Check -Name "studio_data_v43_capability" -Ok $studioDataText.Contains("locker_16029_v43_internal_sheetmetal_current") -Actual "apps/web/src/data/studioData.ts" -Expected "v43 current capability"
Add-Check -Name "studio_data_current_metric_v43" -Ok $studioDataText.Contains("value: '740W v43'") -Actual "apps/web/src/data/studioData.ts" -Expected "current metric is v43"
Add-Check -Name "review_portal_current_round_v43" -Ok ($reviewPortalText.Contains("16029-v43-internal-sheetmetal-review-20260603") -and $reviewPortalText.Contains("v43-int-v18-lockfix")) -Actual "tools/serve_16029_review_downloads.mjs" -Expected "v43 review round and current request"
Add-Check -Name "review_portal_hides_old_same_route_requests" -Ok ($reviewPortalText.Contains("readCurrentDeliveryManifest") -and $reviewPortalText.Contains("isVisibleCurrentGenerationRequest")) -Actual "tools/serve_16029_review_downloads.mjs" -Expected "manifest-based request filtering"
Add-Check -Name "review_portal_default_prompt_excludes_electric_hardware" -Ok ($reviewPortalText.Contains("no electrical board") -and $reviewPortalText.Contains("no cabinet-side electric lock body") -and $reviewPortalText.Contains("no cabinet-side electric lock hook")) -Actual "tools/serve_16029_review_downloads.mjs" -Expected "default prompt excludes electric hardware"
Add-Check -Name "solidworks_2020_only" -Ok (($apiText + $appText + $studioDataText + $reviewPortalText).Contains("SolidWorks 2020") -and -not (($apiText + $appText + $studioDataText + $reviewPortalText).Contains("SolidWorks 2025"))) -Actual "UI/API/portal CAD copy" -Expected "SolidWorks 2020 only"

$failed = @($checks | Where-Object { -not $_.ok -and $_.severity -eq "error" })
$gate = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    status = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }
    scope = "16029 740W / L642-R246 / v43 internal sheet-metal handoff scope"
    current_request_id = if ($manifestExists) { [string]$manifest.current_request_id } else { "" }
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
$lines.Add("# 16029 Current Handoff Scope Gate")
$lines.Add("")
$lines.Add("- Status: $($gate.status)")
$lines.Add("- Scope: $($gate.scope)")
$lines.Add("- Current request: $($gate.current_request_id)")
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
if ($failed.Count -gt 0) {
    exit 1
}
