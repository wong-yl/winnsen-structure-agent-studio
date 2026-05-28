param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Resolve-Decision {
    param([string]$PathText)

    if ($PathText -like "workers/maintenance/build_16029_engineering_handoff*") {
        return [pscustomobject]@{
            group = "legacy_engineering_handoff"
            decision = "defer"
            owner_decision = "Do not include in first commit."
            reason = "Older same-size/native handoff route; current engineer-facing route is 800W gold-variable LMS/SML."
            next_action = "Review later as a separate legacy-route cleanup or remove from current UI entirely."
        }
    }

    if ($PathText -like "workers/maintenance/build_native_16029_*") {
        return [pscustomobject]@{
            group = "native_variant_builders"
            decision = "defer"
            owner_decision = "Do not include in first commit."
            reason = "Native SolidWorks module variant builders are outside the current gold-variable handoff flow."
            next_action = "Review later only if native SLDPRT/SLDASM regeneration becomes the active route."
        }
    }

    if ($PathText -like "workers/solidworks_tools/Build*Module.cs" -or $PathText -like "workers/solidworks_tools/build_*_module.ps1") {
        return [pscustomobject]@{
            group = "solidworks_module_builders"
            decision = "defer"
            owner_decision = "Do not include in first commit."
            reason = "Reusable SolidWorks module builders; useful but not required for current STEP review package gate."
            next_action = "Audit with generated native parts and builder tests before accepting."
        }
    }

    if ($PathText -like "workers/solidworks_tools/PackAndGoAssembly.cs" -or $PathText -like "workers/solidworks_tools/build_pack_and_go_assembly.ps1") {
        return [pscustomobject]@{
            group = "pack_and_go_tooling"
            decision = "defer"
            owner_decision = "Do not include in first commit."
            reason = "Pack-and-Go native assembly packaging is not part of the current zipped STEP review handoff."
            next_action = "Review when native assembly delivery is re-opened."
        }
    }

    if ($PathText -like "workers/solidworks_tools/sw_make_parametric_part_dimension.js") {
        return [pscustomobject]@{
            group = "parametric_part_dimension_tool"
            decision = "defer"
            owner_decision = "Do not include in first commit."
            reason = "Parametric part-dimension mutation support is separate from current fixed-token LMS/SML generation."
            next_action = "Review later with a focused SolidWorks automation smoke test."
        }
    }

    return [pscustomobject]@{
        group = "unclassified_needs_decision"
        decision = "block"
        owner_decision = "Do not include in first commit."
        reason = "No explicit owner decision has been recorded."
        next_action = "Classify this file before any commit."
    }
}

$scopeScript = Join-Path $Root "tools\check_16029_first_commit_scope.ps1"
$scope = & $scopeScript -Root $Root -Json | ConvertFrom-Json
$rows = New-Object System.Collections.Generic.List[object]

foreach ($item in $scope.needs_decision) {
    $decision = Resolve-Decision -PathText $item.path
    $rows.Add([pscustomobject]@{
        status = $item.status
        path = $item.path
        group = $decision.group
        decision = $decision.decision
        owner_decision = $decision.owner_decision
        reason = $decision.reason
        next_action = $decision.next_action
    })
}

$blocked = @($rows | Where-Object { $_.decision -eq "block" })
$report = [ordered]@{
    generated_at = (Get-Date).ToString("o")
    status = if ($blocked.Count -eq 0) { "PASS" } else { "FAIL" }
    needs_decision_total = $rows.Count
    unclassified = $blocked.Count
    rows = $rows
}

if ($Json) {
    $report | ConvertTo-Json -Depth 5
    exit $(if ($blocked.Count -eq 0) { 0 } else { 1 })
}

"16029 needs-decision audit"
"Status: $($report.status)"
"Needs decision files: $($report.needs_decision_total)"
"Unclassified: $($report.unclassified)"
""
$rows | Sort-Object group, path | Format-Table status, group, decision, path -AutoSize | Out-String -Width 240

if ($blocked.Count -gt 0) {
    "Unclassified files must be classified before commit:"
    $blocked | ForEach-Object { "  - $($_.path)" }
    exit 1
}

exit 0
