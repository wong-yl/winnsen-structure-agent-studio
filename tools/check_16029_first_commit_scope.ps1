param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Normalize-PathText {
    param([string]$PathText)
    return $PathText.Replace("\", "/")
}

function Test-AnyPattern {
    param(
        [string]$PathText,
        [string[]]$Patterns
    )
    foreach ($pattern in $Patterns) {
        if ($PathText -like $pattern) {
            return $true
        }
    }
    return $false
}

$includeExact = @(
    ".gitignore",
    "README.md",
    "docs/16029_current_project_process.md",
    "docs/16029_memory_gold_sheetmetal_no_electric_lock_20260602.md",
    "docs/16029_memory_v43_internal_sheetmetal_repair_plan_20260602.md",
    "docs/16029_git_cleanup_boundary.md",
    "docs/project_code_management_policy.md",
    "docs/platform_ui_interaction_enhancement_20260530.md",
    "docs/first_commit_candidate_16029_20260528.md",
    "docs/16029_vibe_coding_video_runbook.md",
    "data/locker_16029_project_route_manifest.json",
    "data/locker_16029_project_route_manifest.md",
    "data/locker_16029_gold_sheetmetal_evidence.json",
    "data/locker_16029_gold_sheetmetal_evidence.md",
    "data/locker_16029_gold_sheetmetal_evidence.csv",
    "data/locker_16029_gold_sheetmetal_rules.json",
    "data/locker_16029_gold_sheetmetal_rules.md",
    "data/locker_16029_v43_internal_sheetmetal_delivery.json",
    "apps/web/index.html",
    "apps/web/src/App.tsx",
    "apps/web/src/App.css",
    "apps/web/src/data/studioData.ts",
    "services/api/README.md",
    "services/api/app/config.py",
    "services/api/app/main.py",
    "tools/serve_16029_review_downloads.mjs",
    "tools/process_16029_review_generation_queue.mjs",
    "tools/locker_16029_controlled_generation_policy.mjs",
    "tools/locker_16029_template_rules.mjs",
    "tools/locker_16029_gold_source_manifest.mjs",
    "tools/locker_16029_gold_module_targets.mjs",
    "tools/locker_16029_structure_feedback.mjs",
    "tools/verify_16029_structure_feedback_contract.mjs",
    "tools/verify_16029_gold_structure_gate.mjs",
    "tools/verify_16029_gold_structure_gate_contract.mjs",
    "tools/collect_16029_gold_sheetmetal_evidence.py",
    "tools/build_16029_gold_sheetmetal_rules.mjs",
    "tools/verify_16029_gold_sheetmetal_evidence.mjs",
    "tools/verify_16029_gold_sheetmetal_rules.mjs",
    "tools/verify_16029_v43_delivery_manifest.mjs",
    "tools/verify_16029_review_portal_manual_generation_control.mjs",
    "tools/verify_16029_template_rule_matrix.mjs",
    "tools/generate_review_solidworks_single_door.ps1",
    "tools/generate_review_solidworks_full_assembly.ps1",
    "tools/generate_16029_parametric_scaffold_freecad.py",
    "tools/scale_16029_source_step_freecad.py",
    "tools/trim_16029_side_panel_step_freecad.py",
    "tools/generate_review_task_simple_freecad_model.py",
    "tools/start_16029_review_portal.mjs",
    "tools/run_16029_review_portal_watchdog.ps1",
    "tools/render_16029_project_flow_pdf.mjs",
    "tools/check_16029_first_commit_scope.ps1",
    "tools/audit_16029_needs_decision.ps1",
    "tools/verify_16029_current_mainline.ps1",
    "tools/stage_16029_first_commit.ps1",
    "tools/guard_16029_staged_scope.ps1",
    "workers/maintenance/generate_16029_800w_gold_variable_model_freecad.py",
    "workers/maintenance/finalize_16029_800w_gold_variable_handoff.py",
    "workers/maintenance/validate_16029_current_handoff_scope.ps1",
    "workers/maintenance/build_16029_dimension_contract.py",
    "workers/maintenance/validate_16029_dimension_contract.py",
    "workers/maintenance/16029_v43_internal_sheetmetal_delivery_handoff_20260603.md",
    "workers/maintenance/build_16029_variable_door_stack_contract.py",
    "workers/maintenance/build_16029_800w_lms_contract.py",
    "workers/solidworks_tools/StepOpenProbe.cs",
    "workers/solidworks_tools/build_step_open_probe.ps1",
    "workers/solidworks_tools/InspectAssemblyComponents.cs",
    "workers/solidworks_tools/build_inspect_assembly_components.ps1",
    "workers/solidworks_tools/PackAndGoAssembly.cs",
    "workers/solidworks_tools/RenameAssemblyComponents.cs",
    "workers/solidworks_tools/build_rename_assembly_components.ps1",
    "workers/solidworks_tools/RemoveAssemblyComponentsByPattern.cs",
    "workers/solidworks_tools/build_remove_assembly_components_by_pattern.ps1",
    "workers/solidworks_tools/RestoreDoorLockTongues.cs",
    "workers/solidworks_tools/build_restore_door_lock_tongues.ps1",
    "workers/solidworks_tools/RepairVerifiedV43DoorPanelFeature.cs",
    "workers/solidworks_tools/build_repair_verified_v43_door_panel_feature.ps1",
    "workers/solidworks_tools/ImportStepSaveNative.cs",
    "workers/solidworks_tools/build_import_step_save_native.ps1",
    "workers/solidworks_tools/sw_clone_master_model_height_probe.js",
    "workers/solidworks_tools/sw_capture_named_views.js",
    "workers/solidworks_tools/BuildOrdinaryDoorModule.cs",
    "workers/solidworks_tools/BuildPlacedComponentsModule.cs",
    "workers/solidworks_tools/BuildCenteredBackSeamAssembly.cs",
    "workers/solidworks_tools/build_centered_back_seam_assembly.ps1",
    "workers/solidworks_tools/repair_16029_back_sheetmetal_side_panels.ps1",
    "workers/maintenance/validate_16029_dimension_contract.py"
)

$excludePatterns = @(
    "workers/handoffs/*",
    "workers/generated_models/*",
    "workers/generation_logs/*",
    "workers/analysis/*",
    "workers/tmp_*",
    "data/*_gate*.*",
    "data/*_validation*.*",
    "data/*_summary*.*",
    "data/*_contract*.*",
    "data/*_blocker*.*",
    "data/review_download_users.json",
    "data/review_download_invite_code.txt",
    "data/review_feedback/*",
    "workers/solidworks_tools/bin/*",
    "*.step",
    "*.STEP",
    "*.stp",
    "*.STP",
    "*.FCStd",
    "*.fcstd",
    "*.SLDPRT",
    "*.sldprt",
    "*.SLDASM",
    "*.sldasm"
)

$needsDecisionPatterns = @(
    "workers/solidworks_tools/BuildDoorArrayModule.cs",
    "workers/solidworks_tools/BuildDoorWeldModule.cs",
    "workers/solidworks_tools/PackAndGoAssembly.cs",
    "workers/solidworks_tools/sw_make_parametric_part_dimension.js",
    "workers/solidworks_tools/build_door_array_module.ps1",
    "workers/solidworks_tools/build_door_weld_module.ps1",
    "workers/solidworks_tools/build_ordinary_door_module.ps1",
    "workers/solidworks_tools/build_pack_and_go_assembly.ps1",
    "workers/solidworks_tools/build_placed_components_module.ps1",
    "workers/maintenance/build_16029_engineering_handoff.py",
    "workers/maintenance/build_16029_engineering_handoff_bundle.py",
    "workers/maintenance/build_native_16029_*"
)

$rawStatus = & git -C $Root status --short
$items = New-Object System.Collections.Generic.List[object]

foreach ($line in $rawStatus) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -lt 4) {
        continue
    }
    $status = $line.Substring(0, 2).Trim()
    $path = Normalize-PathText $line.Substring(3)
    $category = "uncategorized"
    $reason = "not matched by first-commit policy"

    if ($includeExact -contains $path) {
        $category = "include"
        $reason = "first clean commit candidate"
    }
    elseif (Test-AnyPattern -PathText $path -Patterns $excludePatterns) {
        $category = "exclude"
        $reason = "generated, local-only, binary, or handoff evidence"
    }
    elseif (Test-AnyPattern -PathText $path -Patterns $needsDecisionPatterns) {
        $category = "needs_decision"
        $reason = "source change outside the current 16029 v43 internal sheet-metal cleanup"
    }

    $items.Add([pscustomobject]@{
        status = $status
        path = $path
        category = $category
        reason = $reason
    })
}

$groups = $items | Group-Object category | ForEach-Object {
    [pscustomobject]@{
        category = $_.Name
        count = $_.Count
    }
}

$report = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    root = $Root
    total = $items.Count
    summary = $groups
    include = @($items | Where-Object category -eq "include" | Select-Object status, path, reason)
    exclude = @($items | Where-Object category -eq "exclude" | Select-Object status, path, reason)
    needs_decision = @($items | Where-Object category -eq "needs_decision" | Select-Object status, path, reason)
    uncategorized = @($items | Where-Object category -eq "uncategorized" | Select-Object status, path, reason)
}

if ($Json) {
    $report | ConvertTo-Json -Depth 6
    exit 0
}

"16029 first-commit scope check"
"Total: $($report.total)"
""
"Summary:"
$groups | Sort-Object category | Format-Table -AutoSize | Out-String -Width 200
"Include:"
$report.include | Format-Table status, path -AutoSize | Out-String -Width 240
"Needs decision:"
$report.needs_decision | Format-Table status, path -AutoSize | Out-String -Width 240
"Excluded from first commit:"
$report.exclude | Format-Table status, path -AutoSize | Out-String -Width 240
if ($report.uncategorized.Count -gt 0) {
    "Uncategorized:"
    $report.uncategorized | Format-Table status, path -AutoSize | Out-String -Width 240
    exit 2
}

exit 0
