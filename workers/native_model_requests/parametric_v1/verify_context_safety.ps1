[CmdletBinding()]
param(
    [string]$BuildTag = 'csharp-safety',
    [string]$EvidenceDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($BuildTag -notmatch '^[a-zA-Z0-9_-]+$') { throw 'invalid native build tag' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$executable = Join-Path $PSScriptRoot ("bin-$BuildTag\NativeParametricAssembly.exe")
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'compile the native executor first' }
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $repoRoot 'output\16029-context-safety'
}
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
$fixtureRoot = Join-Path $evidenceRoot ("fixture-$PID-" + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
$attempt = Join-Path $fixtureRoot 'parametric_attempts\fixture'
New-Item -ItemType Directory -Path $attempt -Force | Out-Null
$checks = New-Object 'System.Collections.Generic.List[object]'
$beforeProcesses = @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
    Sort-Object ProcessName, Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" })
$assembly = [Reflection.Assembly]::LoadFrom($executable)
$context = $assembly.GetType('Winnsen.StructureAgent.Generation.ParametricContext', $true)
$checkPath = $context.GetMethod('CheckPath', [Reflection.BindingFlags]'Static,NonPublic')

function Invoke-PathCheck([string]$Path, [string]$Root) {
    $arguments = [object[]]::new(2)
    $arguments[0] = $Path
    $arguments[1] = $Root
    $checkPath.Invoke($null, $arguments)
}

function Expect-Rejection([string]$Name, [scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action }
    catch {
        if ($_.Exception.ToString() -notmatch [regex]::Escape($Message)) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "$Name was accepted" }
    $checks.Add([ordered]@{ name = $Name; passed = $true; rejection = $Message })
}

function Run-Executor([string[]]$Arguments, [string]$ExpectedError) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $executable
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Arguments = ($Arguments | ForEach-Object {
        if ($_ -match '["\r\n]') { throw 'invalid fixture command argument' }
        '"' + $_ + '"'
    }) -join ' '
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 1 -or $stderr -notmatch [regex]::Escape($ExpectedError)) {
            throw "fixture executor result mismatch: $($process.ExitCode) $stderr $stdout"
        }
        return [ordered]@{ arguments = $Arguments; exitCode = $process.ExitCode; rejection = $ExpectedError }
    }
    finally { $process.Dispose() }
}

function Write-Json([string]$Path, [object]$Value) {
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
}

$regular = Join-Path $attempt 'regular.txt'
$outside = Join-Path $fixtureRoot 'outside-attempt.txt'
[IO.File]::WriteAllText($regular, 'regular fixture', [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($outside, 'protected fixture', [Text.UTF8Encoding]::new($false))
$outsideHash = (Get-FileHash -LiteralPath $outside -Algorithm SHA256).Hash
$alias = Join-Path $attempt 'alias.txt'
New-Item -ItemType HardLink -Path $alias -Target $outside | Out-Null
Invoke-PathCheck $regular $attempt
Invoke-PathCheck (Join-Path $attempt 'future-file.txt') $attempt
$checks.Add([ordered]@{ name = 'regular_and_future_paths_allowed'; passed = $true })
Expect-Rejection 'external_path_rejected' { Invoke-PathCheck $outside $attempt } 'PARAMETRIC_PATH_ESCAPE'
Expect-Rejection 'external_hardlink_rejected' { Invoke-PathCheck $alias $attempt } 'PARAMETRIC_HARDLINK_PATH'

$missingPlan = Join-Path $attempt 'missing-plan.json'
$checks.Add((Run-Executor @($missingPlan, 'unknown-phase') 'UNKNOWN_PARAMETRIC_PHASE'))
foreach ($phase in @('door-NaN', 'door-Infinity', 'door-0', 'door-10', 'door--1', 'door-300/escape')) {
    $checks.Add((Run-Executor @($missingPlan, $phase) 'PARAMETRIC_DOOR_HEIGHT_INVALID'))
}
$checks.Add((Run-Executor @($missingPlan, 'clone') 'PARAMETRIC_PLAN_HASH_MISMATCH'))
$checks.Add((Run-Executor @() 'Usage: NativeParametricAssembly'))

$planPath = Join-Path $attempt 'parametric_execution.json'
$taskPath = Join-Path $fixtureRoot 'fixture-task.json'
$authPath = Join-Path $attempt 'authorization.json'
$expiresAt = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
Write-Json $taskPath @{ id = 'fixture-task'; nativeBuild = @{ state = 'building'; lease = @{ id = 'fixture-lease'; workerId = 'fixture-worker'; expiresAt = $expiresAt } } }
Write-Json $planPath @{
    schema = 'winnsen.locker16029.parametric_execution.v1'; taskId = 'fixture-task'; taskPath = $taskPath;
    workerId = 'fixture-worker'; sourceRightPartitionSha256 = ('A' * 64);
    contract = @{ geometry = @{ cabinet = @{ widthMm = 888; heightMm = 1917; depthMm = 550 } }; rowsByColumn = @{ L = @(@{ doorHeightMm = 300 }); R = @(@{ doorHeightMm = 300 }) } }
}
$planHash = (Get-FileHash -LiteralPath $planPath -Algorithm SHA256).Hash
$executableHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
$authorization = @{
    phase = 'door-301'; planSha256 = $planHash; executableSha256 = $executableHash; taskId = 'fixture-task';
    workerId = 'fixture-worker'; leaseId = 'fixture-lease'; rightPartitionSha256 = ('A' * 64); expiresAt = $expiresAt
}
Write-Json $authPath $authorization
$environmentNames = @('WINNSEN_PARAMETRIC_PLAN_SHA256', 'WINNSEN_PARAMETRIC_AUTH_PATH', 'WINNSEN_PARAMETRIC_AUTH_SHA256')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    [Environment]::SetEnvironmentVariable('WINNSEN_PARAMETRIC_PLAN_SHA256', $planHash, 'Process')
    [Environment]::SetEnvironmentVariable('WINNSEN_PARAMETRIC_AUTH_PATH', $authPath, 'Process')
    [Environment]::SetEnvironmentVariable('WINNSEN_PARAMETRIC_AUTH_SHA256', (Get-FileHash -LiteralPath $authPath -Algorithm SHA256).Hash, 'Process')
    $checks.Add((Run-Executor @($planPath, 'door-301') 'PARAMETRIC_DOOR_HEIGHT_NOT_IN_CONTRACT'))
    [Environment]::SetEnvironmentVariable('WINNSEN_PARAMETRIC_AUTH_SHA256', ('0' * 64), 'Process')
    $checks.Add((Run-Executor @($planPath, 'door-301') 'PARAMETRIC_AUTH_HASH_MISMATCH'))
    foreach ($key in @('taskId', 'workerId')) {
        $prior = $authorization[$key]
        $authorization[$key] = 'other-identity'
        Write-Json $authPath $authorization
        [Environment]::SetEnvironmentVariable('WINNSEN_PARAMETRIC_AUTH_SHA256', (Get-FileHash -LiteralPath $authPath -Algorithm SHA256).Hash, 'Process')
        $checks.Add((Run-Executor @($planPath, 'door-301') 'PARAMETRIC_AUTH_BINDING_INVALID'))
        $authorization[$key] = $prior
    }
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
}
if ((Get-FileHash -LiteralPath $outside -Algorithm SHA256).Hash -ne $outsideHash) { throw 'external fixture was modified' }
$afterProcesses = @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
    Sort-Object ProcessName, Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" })
if (($beforeProcesses -join ',') -ne ($afterProcesses -join ',')) { throw 'pure safety verification changed the CAD process set' }
$report = [ordered]@{
    passed = $true; executableSha256 = $executableHash; buildTag = $BuildTag; fixtureRoot = $fixtureRoot;
    cadProcessesBefore = $beforeProcesses; cadProcessesAfter = $afterProcesses; externalFixtureUnchanged = $true; checks = @($checks.ToArray())
}
Write-Json (Join-Path $evidenceRoot 'context-safety.result.json') $report
$report | ConvertTo-Json -Depth 12
