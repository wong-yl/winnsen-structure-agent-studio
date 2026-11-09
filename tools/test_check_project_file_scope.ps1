$ErrorActionPreference = 'Stop'
$guard = Join-Path $PSScriptRoot 'check_project_file_scope.ps1'
$blocked = @(
    '.env', '.env.production', 'apps/web/.env.local', 'apps/web/.ENV.LOCAL',
    'data/studio.sqlite', 'data/studio.sqlite-wal', 'data/studio.sqlite-shm',
    'data/studio.sqlite-journal', 'data/studio.sqlite3', 'data/studio.db', 'data/studio.db-journal',
    'designs/probe.igs', 'designs/probe.IGES', 'designs/production.dxf',
    'data/review_generation_requests/order-001.json',
    'workers/native_model_requests/parametric_v1/bin-fixed/build.json',
    'output/result.json', 'workers/handoffs/model.json', 'models/test.SLDPRT',
    "models/new`nmodel.SLDASM", 'tools/probe.exe', 'data/review_download_users.json'
)
$allowed = @(
    '.env.example', 'apps/web/.env.example', 'apps/web/package-lock.json',
    'services/api/app/main.py', 'services/api/tests/test_api.py',
    'workers/native_model_requests/parametric_v1/NativeParametricPhysicalAudit.cs',
    'workers/native_model_requests/development/v1/tools/topcover_parts_native_v1/TopCoverPartsNative.exe.config',
    'configs/locker_16029_layout_rule_evidence.status.txt', '.github/dependabot.yml'
)
$result = & $guard -Files ($blocked + $allowed) -Json | ConvertFrom-Json
if ($LASTEXITCODE -ne 1 -or $result.pass -or $result.blocked.Count -ne $blocked.Count) {
    throw 'Scope guard did not reject exactly the forbidden paths.'
}
foreach ($file in $blocked) { if ($file -cnotin $result.blocked) { throw "Forbidden path accepted: $file" } }
$result = & $guard -Files $allowed -Json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or -not $result.pass -or $result.blocked.Count) { throw 'Allowed source/configuration example rejected.' }
Write-Output "Project file scope regression PASS: $($blocked.Count + $allowed.Count) paths"
exit 0
