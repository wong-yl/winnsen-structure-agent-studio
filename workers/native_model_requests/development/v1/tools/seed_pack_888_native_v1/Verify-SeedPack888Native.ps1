[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$toolDir = $PSScriptRoot
$buildPath = Join-Path $toolDir 'Build-SeedPack888Native.ps1'
$readmePath = Join-Path $toolDir 'README.md'
$sourcePath = Join-Path $toolDir 'SeedPack888Native.cs'
$executablePath = Join-Path $toolDir 'SeedPack888Native.exe'
$nodeVerifierPath = Join-Path $toolDir 'verify_static_v2.mjs'
$manifestPath = Join-Path $toolDir 'toolchain_manifest.json'
$outputPath = Join-Path $toolDir 'static_verification.json'
$sha256SumsPath = Join-Path $toolDir 'SHA256SUMS.txt'

foreach ($requiredPath in @($buildPath, $readmePath, $sourcePath, $executablePath,
        $nodeVerifierPath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required verification input is missing: $requiredPath"
    }
}

function Get-Processes {
    return @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
        Sort-Object ProcessName, Id | ForEach-Object {
            [ordered]@{
                name = $_.ProcessName
                id = $_.Id
                startUtcTicks = $_.StartTime.ToUniversalTime().Ticks
            }
        })
}

$before = @(Get-Processes)
if ($before.Count -ne 0) {
    throw 'Static seed verification requires a zero CAD process baseline.'
}

$nodeOutput = @(& node $nodeVerifierPath 2>&1 | ForEach-Object { $_.ToString() })
$nodeExit = $LASTEXITCODE
if ($nodeExit -ne 0) {
    throw "Node static verifier failed ($nodeExit): $($nodeOutput -join [Environment]::NewLine)"
}
$nodeReport = ($nodeOutput -join [Environment]::NewLine) | ConvertFrom-Json
if ($nodeReport.status -ne 'STATIC_PASS_RUNTIME_NOT_RUN') {
    throw 'Node verifier did not return STATIC_PASS_RUNTIME_NOT_RUN.'
}

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executablePath
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$process = [Diagnostics.Process]::Start($startInfo)
$noArgsStandardOutput = $process.StandardOutput.ReadToEnd()
$noArgsStandardError = $process.StandardError.ReadToEnd()
$process.WaitForExit()
$noArgsExit = $process.ExitCode
$process.Dispose()
if ($noArgsExit -ne 2 -or $noArgsStandardError -notmatch '^Usage:') {
    throw "No-argument fail-closed check failed ($noArgsExit)."
}

$after = @(Get-Processes)
if ($after.Count -ne 0) {
    throw 'Static verification started a CAD process.'
}

$report = [ordered]@{
    schema = 'winnsen.16029.native_seed_pack_static_verification.v2'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    status = 'STATIC_PASS_RUNTIME_NOT_RUN'
    runtimeCadStarted = $false
    checksFailed = 0
    checksTotal = [int]$nodeReport.checksTotal + 3
    sourceSha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    executableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
    manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    nodeVerifierSha256 = (Get-FileHash -LiteralPath $nodeVerifierPath -Algorithm SHA256).Hash
    noArgsExitCode = $noArgsExit
    processBaseline = $before
    processAfter = $after
}
[IO.File]::WriteAllText($outputPath, (($report | ConvertTo-Json -Depth 8) + "`n"),
    [Text.UTF8Encoding]::new($false))

$checksumInputs = [ordered]@{
    'Build-SeedPack888Native.ps1' = $buildPath
    'README.md' = $readmePath
    'SeedPack888Native.cs' = $sourcePath
    'SeedPack888Native.exe' = $executablePath
    'static_verification.json' = $outputPath
    'toolchain_manifest.json' = $manifestPath
    'verify_static_v2.mjs' = $nodeVerifierPath
    'Verify-SeedPack888Native.ps1' = $PSCommandPath
}
$checksumLines = @($checksumInputs.GetEnumerator() | ForEach-Object {
    "$((Get-FileHash -LiteralPath $_.Value -Algorithm SHA256).Hash.ToUpperInvariant())  $($_.Key)"
})
[IO.File]::WriteAllText($sha256SumsPath, ($checksumLines -join "`n") + "`n",
    [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 8
