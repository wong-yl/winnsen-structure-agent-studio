param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Normalize-PathText {
    param([string]$PathText)
    return $PathText.Replace("\", "/")
}

$scopeScript = Join-Path $Root "tools\check_16029_first_commit_scope.ps1"
$scope = & $scopeScript -Root $Root -Json | ConvertFrom-Json
$includeSet = @{}
foreach ($item in $scope.include) {
    $includeSet[$item.path] = $true
}

$staged = @(& git -C $Root diff --cached --name-only | ForEach-Object { Normalize-PathText $_ })
$bad = New-Object System.Collections.Generic.List[string]

foreach ($path in $staged) {
    if (-not $includeSet.ContainsKey($path)) {
        $bad.Add($path)
    }
}

if ($bad.Count -gt 0) {
    Write-Output "Blocked: staged files outside the 16029 first-commit include scope:"
    $bad | ForEach-Object { Write-Output "  - $_" }
    Write-Output ""
    Write-Output "Unstage them before committing, or update docs and tools/check_16029_first_commit_scope.ps1 with an explicit decision."
    exit 1
}

Write-Output "Staged scope guard PASS."
Write-Output "Staged files: $($staged.Count)"
exit 0
