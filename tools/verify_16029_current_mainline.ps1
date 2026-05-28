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
