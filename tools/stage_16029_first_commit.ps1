param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$scopeScript = Join-Path $Root "tools\check_16029_first_commit_scope.ps1"
$scope = & $scopeScript -Root $Root -Json | ConvertFrom-Json

if ($scope.uncategorized.Count -gt 0) {
    Write-Error "Refusing to stage: first-commit scope has uncategorized files. Run tools\check_16029_first_commit_scope.ps1."
}

$includePaths = @($scope.include | ForEach-Object { $_.path })

if ($includePaths.Count -eq 0) {
    Write-Output "No include files found for first commit."
    exit 0
}

Write-Output "16029 first-commit stage plan"
Write-Output "Mode: $(if ($Apply) { 'APPLY' } else { 'DRY-RUN' })"
Write-Output "Include count: $($includePaths.Count)"
Write-Output ""
$includePaths | ForEach-Object { Write-Output "  + $_" }
Write-Output ""
Write-Output "Excluded and needs_decision files will not be staged by this script."

if (-not $Apply) {
    Write-Output ""
    Write-Output "Dry run only. Re-run with -Apply only when staging is explicitly approved."
    exit 0
}

foreach ($path in $includePaths) {
    & git -C $Root add -- $path
    if ($LASTEXITCODE -ne 0) {
        throw "git add failed for $path"
    }
}

Write-Output ""
Write-Output "Staged first-commit include files."
& git -C $Root diff --cached --name-only
