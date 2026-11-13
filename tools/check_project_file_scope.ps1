param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string[]]$Files,
    [string]$Base,
    [string]$Head,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
if (-not $PSBoundParameters.ContainsKey('Files')) {
    $arguments = @('-C', $Root, 'diff', '--name-only', '--diff-filter=ACMR', '-z')
    if ($Base -or $Head) {
        if ($Base -notmatch '^[a-fA-F0-9]{40,64}$' -or $Head -notmatch '^[a-fA-F0-9]{40,64}$') {
            throw 'Base and Head must be full commit hashes.'
        }
        $arguments += @($Base, $Head)
    } else {
        $arguments += '--cached'
    }
    $raw = & git @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect changed file paths.' }
    $Files = @(($raw -join "`n").Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
}

$patterns = @(
    '\.(zip|png|jpg|jpeg|pdf|docx|exe|dll|pdb|FCStd|FCBak|step|stp|igs|iges|dxf|SLDPRT|SLDASM|SLDDRW)$',
    '(^|/)\.env($|\.)',
    '\.(sqlite3?|db)(-(wal|shm|journal))?$',
    '^workers/(handoffs|generated_models|analysis)/',
    '^output/',
    '^data/(review_download_users\.json|review_download_invite_code\.txt|review_portal\.local\.json)$',
    '^data/(review_feedback|review_generation_requests|native_model_requests|review_portal_runtime|parametric_attempts|parametric_portal_acceptance)/',
    '^data/parametric_.*\.mjs$',
    '^workers/native_model_requests/.*/(bin[^/]*|build[^/]*|runtime_data|evidence)/',
    '^docs/(report_assets|qa_assets)/'
)
$blocked = @(
    foreach ($file in $Files) {
        $path = $file.Replace('\', '/')
        foreach ($pattern in $patterns) {
            if ($path -match $pattern) {
                if ($pattern -eq '(^|/)\.env($|\.)' -and $path -cmatch '(^|/)\.env\.example$') { continue }
                $file
                break
            }
        }
    }
)
if ($Json) {
    @{ pass = ($blocked.Count -eq 0); checked = $Files.Count; blocked = @($blocked) } | ConvertTo-Json -Compress
} elseif ($blocked.Count) {
    Write-Output 'Blocked local configuration, database, generated or binary files:'
    $blocked | ForEach-Object { Write-Output "  - $_" }
} else {
    Write-Output "Project file scope PASS: $($Files.Count) files"
}
if ($blocked.Count) { exit 1 }
exit 0
