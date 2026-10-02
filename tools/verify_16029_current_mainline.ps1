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
        if ($null -eq $exitCode) { $exitCode = 0 }
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

Invoke-CheckInDirectory -Name "api_python_compile" -WorkingDirectory $Root -Command "python -m py_compile services\api\app\main.py services\api\app\config.py"
Invoke-CheckInDirectory -Name "review_portal_node_check" -WorkingDirectory $Root -Command "node --check tools\serve_16029_review_downloads.mjs"
Invoke-CheckInDirectory -Name "native_generator_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_native_generator.mjs"
Invoke-CheckInDirectory -Name "native_generator_cli_node_check" -WorkingDirectory $Root -Command "node --check tools\process_16029_native_generation_request.mjs"
Invoke-CheckInDirectory -Name "native_generator_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_generator.mjs"
Invoke-CheckInDirectory -Name "native_generator_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_generator.mjs"
Invoke-CheckInDirectory -Name "native_task_store_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_task_store.mjs"
Invoke-CheckInDirectory -Name "native_task_store_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_task_store.mjs"
Invoke-CheckInDirectory -Name "native_task_store_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_task_store.mjs"
Invoke-CheckInDirectory -Name "native_recipe_registry_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_native_recipe_registry.mjs"
Invoke-CheckInDirectory -Name "native_recipe_registry_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_recipe_registry.mjs"
Invoke-CheckInDirectory -Name "native_recipe_registry_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_recipe_registry.mjs"
Invoke-CheckInDirectory -Name "native_worker_node_check" -WorkingDirectory $Root -Command "node --check tools\run_16029_native_worker.mjs"
Invoke-CheckInDirectory -Name "native_worker_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_worker.mjs"
Invoke-CheckInDirectory -Name "native_worker_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_worker.mjs"
Invoke-CheckInDirectory -Name "native_execution_authorization_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_execution_authorization.mjs"
Invoke-CheckInDirectory -Name "native_execution_authorization_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_execution_authorization.mjs"
Invoke-CheckInDirectory -Name "native_execution_authorization_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_execution_authorization.mjs"
Invoke-CheckInDirectory -Name "native_assembly_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_assembly_contract.mjs"
Invoke-CheckInDirectory -Name "native_assembly_contract_test_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_assembly_contract.mjs"
Invoke-CheckInDirectory -Name "native_assembly_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_assembly_contract.mjs"
Invoke-CheckInDirectory -Name "native_stage_contract_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_stage_contract.mjs"
Invoke-CheckInDirectory -Name "native_stage_contract_test_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_stage_contract.mjs"
Invoke-CheckInDirectory -Name "native_stage_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_stage_contract.mjs"
Invoke-CheckInDirectory -Name "native_toolchain_manifest_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_toolchain_manifests.mjs"
Invoke-CheckInDirectory -Name "native_toolchain_manifest_test_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_toolchain_manifests.mjs"
Invoke-CheckInDirectory -Name "native_toolchain_manifest_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_toolchain_manifests.mjs"
Invoke-CheckInDirectory -Name "native_stage_executor_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_stage_executor.mjs"
Invoke-CheckInDirectory -Name "native_stage_executor_test_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_stage_executor.mjs"
Invoke-CheckInDirectory -Name "native_stage_executor_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_stage_executor.mjs"
Invoke-CheckInDirectory -Name "native_publication_node_check" -WorkingDirectory $Root -Command "node --check tools\lib\locker_16029_native_publication.mjs"
Invoke-CheckInDirectory -Name "native_publication_test_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_native_publication.mjs"
Invoke-CheckInDirectory -Name "native_publication_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_publication.mjs"
Invoke-CheckInDirectory -Name "parametric_input_boundaries" -WorkingDirectory $Root -Command "node tools\verify_16029_parametric_input_boundaries.mjs"
Invoke-CheckInDirectory -Name "parametric_source_adapters" -WorkingDirectory $Root -Command "node workers\native_model_requests\parametric_v1\verify_adapters.mjs"
Invoke-CheckInDirectory -Name "native_portal_session_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_native_portal_session.mjs"
Invoke-CheckInDirectory -Name "api_task_state_tests" -WorkingDirectory $apiDir -Command "python -m unittest discover -s tests -v"
Invoke-CheckInDirectory -Name "review_portal_manual_generation_control_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_review_portal_manual_generation_control.mjs"
Invoke-CheckInDirectory -Name "review_portal_manual_generation_control_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_review_portal_manual_generation_control.mjs"
Invoke-CheckInDirectory -Name "review_portal_feedback_upload_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_review_portal_feedback_upload.mjs"
Invoke-CheckInDirectory -Name "review_portal_feedback_upload_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_review_portal_feedback_upload.mjs"
Invoke-CheckInDirectory -Name "review_portal_native_storage_boundary" -WorkingDirectory $Root -Command "`$portal = Get-Content -LiteralPath 'tools\serve_16029_review_downloads.mjs' -Raw -Encoding UTF8; if (`$portal -notmatch 'createNativeTaskStore' -or `$portal -notmatch 'generationTaskSnapshot' -or `$portal -notmatch 'listSnapshot' -or `$portal -notmatch 'normalize16029NativeModelRequest' -or `$portal -notmatch 'locker_16029_native_generator.mjs' -or `$portal -match 'process_16029_review_generation_queue' -or `$portal -match 'generate_review_solidworks_full_assembly') { throw 'portal must use the unified native assistance-model task store and must not reference the removed generator chain' }"
Invoke-CheckInDirectory -Name "legacy_generator_files_removed" -WorkingDirectory $Root -Command "`$paths = @('tools\generate_review_solidworks_full_assembly.ps1','tools\generate_review_solidworks_single_door.ps1','tools\generate_16029_parametric_scaffold_freecad.py','tools\generate_review_task_simple_freecad_model.py','tools\scale_16029_source_step_freecad.py','tools\locker_16029_template_rules.mjs','tools\locker_16029_controlled_generation_policy.mjs','tools\process_16029_review_generation_queue.mjs','tools\locker_16029_generation_cache.mjs','tools\verify_16029_generation_cache.mjs','tools\verify_16029_template_rule_matrix.mjs'); `$remaining = @(`$paths | Where-Object { Test-Path -LiteralPath `$_ }); if (`$remaining.Count -ne 0) { throw ('legacy generator files remain: ' + (`$remaining -join ', ')) }"

Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_collector_compile" -WorkingDirectory $Root -Command "python -m py_compile tools\collect_16029_gold_sheetmetal_evidence.py"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_refresh" -WorkingDirectory $Root -Command "python tools\collect_16029_gold_sheetmetal_evidence.py --quiet"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_gold_sheetmetal_evidence.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_evidence_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_gold_sheetmetal_evidence.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_build_node_check" -WorkingDirectory $Root -Command "node --check tools\build_16029_gold_sheetmetal_rules.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_build" -WorkingDirectory $Root -Command "node tools\build_16029_gold_sheetmetal_rules.mjs --quiet"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_gold_sheetmetal_rules.mjs"
Invoke-CheckInDirectory -Name "gold_sheetmetal_rules_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_gold_sheetmetal_rules.mjs"
Invoke-CheckInDirectory -Name "gold_source_manifest_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_gold_source_manifest.mjs"
Invoke-CheckInDirectory -Name "gold_source_module_targets_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_gold_module_targets.mjs"
Invoke-CheckInDirectory -Name "template_structure_feedback_node_check" -WorkingDirectory $Root -Command "node --check tools\locker_16029_structure_feedback.mjs"
Invoke-CheckInDirectory -Name "template_structure_feedback_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_structure_feedback_contract.mjs"
Invoke-CheckInDirectory -Name "v43_exact_structure_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_v43_exact_structure_contract.mjs"
Invoke-CheckInDirectory -Name "v43_exact_structure_gate_contract_test" -WorkingDirectory $Root -Command "node tools\verify_16029_v43_exact_structure_contract.test.mjs"
Invoke-CheckInDirectory -Name "gold_structure_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_gold_structure_gate.mjs"
Invoke-CheckInDirectory -Name "gold_structure_gate_contract" -WorkingDirectory $Root -Command "node tools\verify_16029_gold_structure_gate_contract.mjs"
Invoke-CheckInDirectory -Name "v43_delivery_manifest_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_v43_delivery_manifest.mjs"
Invoke-CheckInDirectory -Name "v43_delivery_manifest_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_v43_delivery_manifest.mjs"
Invoke-CheckInDirectory -Name "v37_engineering_assistance_delivery_manifest_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_v37_delivery_manifest.mjs"
Invoke-CheckInDirectory -Name "v37_engineering_assistance_delivery_manifest_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_v37_delivery_manifest.mjs"

Invoke-CheckInDirectory -Name "solidworks_placed_source_readonly_contract" -WorkingDirectory $Root -Command "`$builder = Get-Content -LiteralPath 'workers\solidworks_tools\BuildPlacedComponentsModule.cs' -Raw -Encoding UTF8; if (`$builder -notmatch 'spec.ReadOnly = true' -or `$builder -notmatch 'swOpenDocOptions_ReadOnly' -or `$builder -notmatch 'read_only_requested' -or `$builder -match 'spec.ReadOnly = false') { throw 'placed component sources must be opened read-only and report the request in build evidence' }"
Invoke-CheckInDirectory -Name "solidworks_review_capture_node_check" -WorkingDirectory $Root -Command "node --check workers\solidworks_tools\sw_capture_named_views.js"
Invoke-CheckInDirectory -Name "v23_pre_signoff_review_script_syntax" -WorkingDirectory $Root -Command "`$null = [scriptblock]::Create((Get-Content -LiteralPath 'tools\build_16029_v23_pre_signoff_review.ps1' -Raw -Encoding UTF8))"
Invoke-CheckInDirectory -Name "v23_engineering_signoff_gate_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_v23_engineering_signoff.mjs"
Invoke-CheckInDirectory -Name "v23_engineering_signoff_test_node_check" -WorkingDirectory $Root -Command "node --check tools\verify_16029_v23_engineering_signoff.test.mjs"
Invoke-CheckInDirectory -Name "v23_engineering_signoff_contract_test" -WorkingDirectory $Root -Command "node tools\verify_16029_v23_engineering_signoff.test.mjs"
Invoke-CheckInDirectory -Name "v23_engineering_signoff_gate" -WorkingDirectory $Root -Command "node tools\verify_16029_v23_engineering_signoff.mjs"

Invoke-CheckInDirectory -Name "solidworks_structure_inspector_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_inspect_assembly_components.ps1'"
Invoke-CheckInDirectory -Name "solidworks_component_renamer_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_rename_assembly_components.ps1'"
Invoke-CheckInDirectory -Name "solidworks_component_remover_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_remove_assembly_components_by_pattern.ps1'"
Invoke-CheckInDirectory -Name "solidworks_door_lock_tongue_restore_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_restore_door_lock_tongues.ps1'"
Invoke-CheckInDirectory -Name "solidworks_v43_lock_hole_datum_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_add_16029_v43_lock_hole_datums.ps1'"
Invoke-CheckInDirectory -Name "solidworks_v43_shelf_band_normalizer_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_normalize_16029_v43_shelf_bands.ps1'"
Invoke-CheckInDirectory -Name "solidworks_center_maintenance_sheetmetal_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_add_16029_center_maintenance_sheetmetal.ps1'"
Invoke-CheckInDirectory -Name "solidworks_verified_v43_door_panel_feature_repair_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_repair_verified_v43_door_panel_feature.ps1'"
Invoke-CheckInDirectory -Name "solidworks_placed_components_builder_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_placed_components_module.ps1'"
Invoke-CheckInDirectory -Name "solidworks_part_body_probe_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_probe_part_bodies.ps1'"
Invoke-CheckInDirectory -Name "solidworks_part_body_probe_contract" -WorkingDirectory $Root -Command "`$source = Get-Content -LiteralPath 'workers\solidworks_tools\ProbePartBodies.cs' -Raw -Encoding UTF8; if (`$source -notmatch 'swOpenDocOptions_ReadOnly' -or `$source -notmatch 'read_only_requested' -or `$source -notmatch 'GetMassProperties' -or `$source -notmatch 'volume_mm3' -or `$source -notmatch 'surface_area_mm2' -or `$source -notmatch '--exit-session') { throw 'part geometry probes must request read-only opens, report volume and surface area, and support isolated-session exit' }"
Invoke-CheckInDirectory -Name "back_sheetmetal_side_panel_repair_script_syntax" -WorkingDirectory $Root -Command "`$null = [scriptblock]::Create((Get-Content -LiteralPath 'workers\solidworks_tools\repair_16029_back_sheetmetal_side_panels.ps1' -Raw -Encoding UTF8))"
Invoke-CheckInDirectory -Name "solidworks_body_exporter_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_export_selected_part_bodies.ps1'"
Invoke-CheckInDirectory -Name "solidworks_step_importer_compile" -WorkingDirectory $Root -Command "& 'workers\solidworks_tools\build_import_step_save_native.ps1'"
Invoke-CheckInDirectory -Name "first_commit_scope_check" -WorkingDirectory $Root -Command "& '$scopeCheckScript'"
Invoke-CheckInDirectory -Name "needs_decision_audit" -WorkingDirectory $Root -Command "& '$needsDecisionAuditScript'"
Invoke-CheckInDirectory -Name "staged_scope_guard" -WorkingDirectory $Root -Command "& '$stagedGuardScript'"
Invoke-CheckInDirectory -Name "current_handoff_scope_gate" -WorkingDirectory $Root -Command "& '$scopeGateScript'"

if (-not $SkipWebBuild) {
    Invoke-CheckInDirectory -Name "web_build" -WorkingDirectory $webDir -Command "npm run build"
    Invoke-CheckInDirectory -Name "web_lint" -WorkingDirectory $webDir -Command "npm run lint"
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

if ($failed.Count -gt 0) { exit 1 }
exit 0
