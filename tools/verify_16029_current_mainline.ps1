param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$SkipWebBuild,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Name,
        [bool]$Ok,
        [int]$ExitCode,
        [string]$Command,
        [string]$Output
    )
    $results.Add([pscustomobject]@{
        name = $Name
        ok = $Ok
        exit_code = $ExitCode
        command = $Command
        output = $Output.Trim()
    })
}

function Invoke-Check {
    param(
        [string]$Name,
        [string]$Command,
        [string]$WorkingDirectory = $Root
    )
    $output = ""
    $exitCode = 0
    try {
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -Command $Command 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
        if ($null -eq $exitCode) {
            $exitCode = 0
        }
    }
    catch {
        $output = $_ | Out-String
        $exitCode = 1
    }
    Add-Result -Name $Name -Ok ($exitCode -eq 0) -ExitCode $exitCode -Command $Command -Output $output
}

function Invoke-CheckInDirectory {
    param(
        [string]$Name,
        [string]$Command,
        [string]$WorkingDirectory
    )
    $escapedDirectory = $WorkingDirectory.Replace("'", "''")
    Invoke-Check -Name $Name -Command "Set-Location -LiteralPath '$escapedDirectory'; $Command"
}

$apiDir = Join-Path $Root "services\api"
$webDir = Join-Path $Root "apps\web"
$scopeGateScript = Join-Path $Root "workers\maintenance\validate_16029_current_handoff_scope.ps1"
$scopeCheckScript = Join-Path $Root "tools\check_16029_first_commit_scope.ps1"
$needsDecisionAuditScript = Join-Path $Root "tools\audit_16029_needs_decision.ps1"
$stagedGuardScript = Join-Path $Root "tools\guard_16029_staged_scope.ps1"
$reviewPortalScript = Join-Path $Root "tools\serve_16029_review_downloads.mjs"

Invoke-CheckInDirectory -Name "api_python_compile" -WorkingDirectory $Root -Command "python -m py_compile services\api\app\main.py services\api\app\config.py"
Invoke-CheckInDirectory -Name "review_portal_node_check" -WorkingDirectory $Root -Command "node --check tools\serve_16029_review_downloads.mjs"
Invoke-CheckInDirectory -Name "review_portal_manual_generation_control_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_review_portal_manual_generation_control.mjs"
Invoke-CheckInDirectory -Name "review_portal_manual_generation_control_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_review_portal_manual_generation_control.mjs"
Invoke-CheckInDirectory -Name "review_generation_queue_node_check" -WorkingDirectory $Root -Command "node --check tools\process_16029_review_generation_queue.mjs"
Invoke-CheckInDirectory -Name "review_queue_parametric_scaffold_contract" -WorkingDirectory $Root -Command "`$source = Get-Content -LiteralPath 'tools\process_16029_review_generation_queue.mjs' -Raw -Encoding UTF8; if (`$source -notmatch 'solidworks_2020_parametric_scaffold_needs_engineering_validation' -or `$source -notmatch 'solidworks2020_parametric_scaffold_needs_engineering_validation' -or `$source -notmatch 'parametricScaffoldNeedsEngineeringValidation') { throw 'review queue does not preserve parametric scaffold validation status' }"
Invoke-CheckInDirectory -Name "template_rule_planner_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_template_rules.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_collector_compile" -WorkingDirectory $Root -Command "python -m py_compile tools\collect_16029_gold_sheetmetal_evidence.py"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_refresh" -WorkingDirectory $Root -Command "python tools\collect_16029_gold_sheetmetal_evidence.py --quiet"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_gold_sheetmetal_evidence.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_gold_sheetmetal_evidence.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_build_node_check" -WorkingDirectory $Root -Command "node --check tools\build_16029_gold_sheetmetal_rules.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_build" -WorkingDirectory $Root -Command "node tools\build_16029_gold_sheetmetal_rules.mjs --quiet"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_gold_sheetmetal_rules.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_gold_sheetmetal_rules.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rule_worker_queue_contract" -WorkingDirectory $Root -Command "`$planner = Get-Content -LiteralPath 'tools\locker_16029_template_rules.mjs' -Raw -Encoding UTF8; `$worker = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; `$single = Get-Content -LiteralPath 'tools\generate_review_solidworks_single_door.ps1' -Raw -Encoding UTF8; `$queue = Get-Content -LiteralPath 'tools\process_16029_review_generation_queue.mjs' -Raw -Encoding UTF8; if (`$planner -notmatch 'sheetMetalRuleEvidence' -or `$planner -notmatch 'bound_to_1000w_gold_dxf' -or `$worker -notmatch 'goldSheetMetalRuleEvidence' -or `$worker -notmatch 'sheetMetalRuleBindingStatus' -or `$worker -notmatch 'sheetMetalDetectedCutFeatureCount' -or `$worker -notmatch 'generatedNativeDoorModuleHoleDatumCount' -or `$worker -notmatch 'generatedNativeDoorModuleSuppressedCutFeatureCount' -or `$single -notmatch 'sheetMetalExpectedFlatWidthMm' -or `$single -notmatch 'sheetMetalHoleDatums' -or `$single -notmatch 'template_cut_features_configured_from_gold_dxf_datums_pending_direct_rebuild' -or `$single -notmatch 'suppress_template_lock_or_label_pilot_cut' -or `$single -notmatch 'sheetMetalSuppressedCutFeatureCount' -or `$queue -notmatch 'goldSheetMetalRuleEvidence' -or `$queue -notmatch 'goldSheetMetalRuleHoleFeatureStatus' -or `$queue -notmatch 'generatedNativeDoorModuleHoleDatumCount' -or `$queue -notmatch 'generatedNativeDoorModuleDetectedCutFeatureCount' -or `$queue -notmatch 'generatedNativeDoorModuleSuppressedCutFeatureCount') { throw '1000W gold DXF sheet-metal rules are not preserved through planner, workers, and queue' }"
Invoke-CheckInDirectory -Name "template_rule_matrix_check" -WorkingDirectory $Root -Command "node tools\verify_16029_template_rule_matrix.mjs"
Invoke-CheckInDirectory -Name "template_seed_gold_reference_boundary_contract" -WorkingDirectory $Root -Command "`$planner = Get-Content -LiteralPath 'tools\locker_16029_template_rules.mjs' -Raw -Encoding UTF8; `$worker = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; `$queue = Get-Content -LiteralPath 'tools\process_16029_review_generation_queue.mjs' -Raw -Encoding UTF8; if (`$planner -notmatch '740W / L642-R246 / v43 template seed' -or `$planner -notmatch '1000W remains the gold/source structural reference' -or `$worker -notmatch 'goldSourceReference' -or `$worker -notmatch '1000W x 1917H x 550D' -or `$queue -notmatch 'Compare against the 1000W gold/source reference') { throw '740W template seed / 1000W gold-source boundary is not preserved' }"
Invoke-CheckInDirectory -Name "gold_source_manifest_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_gold_source_manifest.mjs"
Invoke-CheckInDirectory -Name "gold_source_module_targets_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_gold_module_targets.mjs"
Invoke-CheckInDirectory -Name "electrical_exclusion_contract" -WorkingDirectory $Root -Command "`$manifest = Get-Content -LiteralPath 'tools\locker_16029_gold_source_manifest.mjs' -Raw -Encoding UTF8; `$targets = Get-Content -LiteralPath 'tools\locker_16029_gold_module_targets.mjs' -Raw -Encoding UTF8; `$worker = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; `$gate = Get-Content -LiteralPath 'tools\verify_16029_gold_structure_gate.mjs' -Raw -Encoding UTF8; if (`$manifest -match '电控U型锁钩ZJA-S500' -or `$manifest -match 'lock_control_board_assembly' -or `$targets -match 'cabinet_lock_body_\\$\\{side\\}' -or `$targets -notmatch 'generatedDoors.get' -or `$worker -notmatch 'freeze_verified_v43_door_modules_internal_sheetmetal_only' -or `$worker -notmatch 'freezeVerifiedV43DoorRoute' -or `$worker -notmatch 'RemoveAssemblyComponentsByPattern.exe' -or `$worker -notmatch 'solidworks_2020_electric_lock_hook_cleanup.json' -or `$worker -notmatch 'electric_lock_hook\\|zja_s500\\|ZJA-S500' -or `$worker -notmatch 'electricalOrElectricLockComponentCount' -or `$worker -notmatch 'cabinetSideElectricalAndElectricLockComponentsExcluded' -or `$gate -notmatch 'ZJA-S500') { throw 'generated assemblies must freeze v43 door route, remove electric-lock hook hardware from generated package copies, exclude cabinet-side electrical/lock placements, and report any residual electric-lock components through the gate' }"
Invoke-CheckInDirectory -Name "lock_hole_datum_contract" -WorkingDirectory $Root -Command "`$scaffold = Get-Content -LiteralPath 'tools\generate_16029_parametric_scaffold_freecad.py' -Raw -Encoding UTF8; `$worker = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; `$queue = Get-Content -LiteralPath 'tools\process_16029_review_generation_queue.mjs' -Raw -Encoding UTF8; if (`$scaffold -notmatch 'lock_mounting_hole_datum_left' -or `$worker -notmatch 'lock_mounting_hole_datum_left' -or `$queue -notmatch 'lock-hole datums') { throw 'lock-hole datum binding is not preserved' }"
Invoke-CheckInDirectory -Name "template_structure_feedback_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_structure_feedback.mjs"
Invoke-CheckInDirectory -Name "template_structure_feedback_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_structure_feedback_contract.mjs"
Invoke-CheckInDirectory -Name "template_structure_feedback_quality_gate_contract" -WorkingDirectory $Root -Command "`$source = Get-Content -LiteralPath 'tools\locker_16029_structure_feedback.mjs' -Raw -Encoding UTF8; if (`$source -notmatch 'parametric_scaffold_requires_engineering_validation' -or `$source -notmatch '1000W gold/source reference' -or `$source -notmatch 'const sourceWidthMm = 1000') { throw 'structure feedback does not preserve 1000W gold-source and parametric scaffold quality gates' }"
Invoke-CheckInDirectory -Name "gold_structure_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_gold_structure_gate.mjs"
Invoke-CheckInDirectory -Name "gold_structure_gate_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_gold_structure_gate_contract.mjs"
Invoke-CheckInDirectory -Name "gold_structure_gate_worker_queue_contract" -WorkingDirectory $Root -Command "`$gate = Get-Content -LiteralPath 'tools\verify_16029_gold_structure_gate.mjs' -Raw -Encoding UTF8; `$worker = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; `$queue = Get-Content -LiteralPath 'tools\process_16029_review_generation_queue.mjs' -Raw -Encoding UTF8; if (`$gate -notmatch 'GOLD_SOURCE_BASELINE' -or `$gate -notmatch 'component_count_below_gold_floor' -or `$gate -notmatch 'lock_mounting_hole_datum' -or `$gate -notmatch 'electrical_or_electric_lock_components_present' -or `$worker -notmatch 'goldStructureGateIssueCount' -or `$queue -notmatch 'goldStructureGateIssueCount') { throw '1000W gold-source structure gate is not wired through worker and queue' }"
Invoke-CheckInDirectory -Name "v43_delivery_manifest_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_v43_delivery_manifest.mjs"
Invoke-CheckInDirectory -Name "v43_delivery_manifest_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_v43_delivery_manifest.mjs"
Invoke-CheckInDirectory -Name "door_lock_tongue_restore_contract" -WorkingDirectory $Root -Command "`$worker = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; `$gate = Get-Content -LiteralPath 'tools\verify_16029_gold_structure_gate.mjs' -Raw -Encoding UTF8; `$feedback = Get-Content -LiteralPath 'tools\locker_16029_structure_feedback.mjs' -Raw -Encoding UTF8; if (`$worker -notmatch 'RestoreDoorLockTongues.exe' -or `$worker -notmatch 'doorLockTongueRestore' -or `$worker -notmatch 'doorLockTongueCount' -or `$gate -notmatch 'door_lock_tongue' -or `$gate -notmatch 'doorLockTongueCount' -or `$feedback -notmatch 'missing_door_lock_tongue') { throw 'mechanical door lock tongue restore and gate checks are not wired through worker, gold gate, and structure feedback' }"
Invoke-CheckInDirectory -Name "generated_native_door_chinese_copy_contract" -WorkingDirectory $Root -Command "`$source = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; if (`$source -notmatch 'New-GeneratedNativeDoorModuleRecord' -or `$source -notmatch 'native_named' -or `$source -notmatch 'originalAssembly' -or `$source -notmatch 'namedNativeFileMap' -or `$source -notmatch 'generated native door module Chinese-named assembly') { throw 'full assembly generator does not preserve Chinese-named generated native door module bindings' }"
Invoke-CheckInDirectory -Name "single_door_electric_lock_hook_exclusion_contract" -WorkingDirectory $Root -Command "`$source = Get-Content -LiteralPath 'tools\generate_review_solidworks_single_door.ps1' -Raw -Encoding UTF8; if (`$source -notmatch 'skip-electric-lock-hook') { throw 'single-door generator must omit electric lock hook entities' }"
Invoke-CheckInDirectory -Name "exact_template_parametric_accessory_contract" -WorkingDirectory $Root -Command "`$source = Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8; if (`$source -notmatch 'generate parametric scaffold STEP evidence' -or `$source -notmatch 'lock_mounting_hole_datum_left' -or `$source -notmatch 'lock_mounting_hole_datum_right' -or `$source -notmatch 'leveling_foot' -or `$source -notmatch 'parametricInternalSheetMetalRepairEnabled' -or `$source -notmatch 'partition_stiffener_left_front' -or `$source -notmatch 'shelf_locating_foot_datum' -or `$source -notmatch 'front_frame_locating_notch_datum' -or `$source -notmatch 'restoreVerifiedV43NativeAssemblyBase' -or `$source -notmatch 'Write-RestoredV43TemplatePlacementRows' -or `$source -notmatch 'evidence_only_kept_out_of_restored_v43_visible_candidate' -or `$source -notmatch 'parametricScaffoldVisiblePlacementCount' -or `$source -notmatch 'parametricScaffoldVisiblePlacementCount -gt 0' -or `$source -notmatch 'replaceCabinetTargetsWithParametricScaffold') { throw 'full assembly generator must restore the exact v43 native base and keep parametric scaffold evidence out of the restored v43 visible candidate' }"
Invoke-CheckInDirectory -Name "single_door_generation_script_syntax" -WorkingDirectory $Root -Command "`$null = [scriptblock]::Create((Get-Content -LiteralPath 'tools\generate_review_solidworks_single_door.ps1' -Raw -Encoding UTF8))"
Invoke-CheckInDirectory -Name "full_assembly_generation_script_syntax" -WorkingDirectory $Root -Command "`$null = [scriptblock]::Create((Get-Content -LiteralPath 'tools\generate_review_solidworks_full_assembly.ps1' -Raw -Encoding UTF8))"
Invoke-CheckInDirectory -Name "review_generation_helper_compile" -WorkingDirectory $Root -Command "python -m py_compile tools\generate_review_task_simple_freecad_model.py"
Invoke-CheckInDirectory -Name "parametric_scaffold_freecad_helper_compile" -WorkingDirectory $Root -Command "python -m py_compile tools\generate_16029_parametric_scaffold_freecad.py"
Invoke-CheckInDirectory -Name "solidworks_structure_inspector_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_inspect_assembly_components.ps1'"
Invoke-CheckInDirectory -Name "solidworks_component_renamer_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_rename_assembly_components.ps1'"
Invoke-CheckInDirectory -Name "solidworks_component_remover_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_remove_assembly_components_by_pattern.ps1'"
Invoke-CheckInDirectory -Name "solidworks_door_lock_tongue_restore_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_restore_door_lock_tongues.ps1'"
Invoke-CheckInDirectory -Name "solidworks_placed_components_builder_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_placed_components_module.ps1'"
Invoke-CheckInDirectory -Name "solidworks_part_body_probe_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_probe_part_bodies.ps1'"
Invoke-CheckInDirectory -Name "back_sheetmetal_side_panel_repair_script_syntax" -WorkingDirectory $Root -Command "`$null = [scriptblock]::Create((Get-Content -LiteralPath 'workers\solidworks_tools\repair_16029_back_sheetmetal_side_panels.ps1' -Raw -Encoding UTF8))"
Invoke-CheckInDirectory -Name "solidworks_body_exporter_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_export_selected_part_bodies.ps1'"
Invoke-CheckInDirectory -Name "solidworks_step_importer_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_import_step_save_native.ps1'"
Invoke-CheckInDirectory -Name "first_commit_scope_check" -WorkingDirectory $Root -Command "& '$scopeCheckScript'"
Invoke-CheckInDirectory -Name "needs_decision_audit" -WorkingDirectory $Root -Command "& '$needsDecisionAuditScript'"
Invoke-CheckInDirectory -Name "staged_scope_guard" -WorkingDirectory $Root -Command "& '$stagedGuardScript'"
Invoke-CheckInDirectory -Name "current_handoff_scope_gate" -WorkingDirectory $Root -Command "& '$scopeGateScript'"

if (-not $SkipWebBuild) {
    Invoke-CheckInDirectory -Name "web_build" -WorkingDirectory $webDir -Command "npm run build"
}

$failed = @($results | Where-Object { -not $_.ok })
$report = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    root = $Root
    status = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }
    checks_total = $results.Count
    checks_failed = $failed.Count
    results = $results
}

if ($Json) {
    $report | ConvertTo-Json -Depth 5
}
else {
    "16029 current mainline verification"
    "Status: $($report.status)"
    "Checks: $($report.checks_total)"
    "Failed: $($report.checks_failed)"
    ""
    $results | Select-Object name, ok, exit_code | Format-Table -AutoSize | Out-String -Width 200
    if ($failed.Count -gt 0) {
        ""
        "Failed details:"
        foreach ($item in $failed) {
            "## $($item.name)"
            $item.output
            ""
        }
    }
}

if ($failed.Count -gt 0) {
    exit 1
}

exit 0
