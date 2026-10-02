[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$toolDir = $PSScriptRoot
$binDir = Join-Path $toolDir 'bin'
$sourcePath = Join-Path $toolDir 'BuildLockTopology888x14.cs'
$executablePath = Join-Path $binDir 'BuildLockTopology888x14.exe'
$manifestPath = Join-Path $toolDir 'lock_toolchain_manifest.json'
$validatorPath = Join-Path $toolDir 'ValidateLockTopology888x14.mjs'
$compilerPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$interopPath = Join-Path $binDir 'SolidWorks.Interop.sldworks.dll'
$constantsPath = Join-Path $binDir 'SolidWorks.Interop.swconst.dll'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-Sha256Text([string]$Value) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
        return ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

$sourceStampPattern = '(private\s+const\s+string\s+ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})("\s*;)'
$source = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::UTF8)
if ([regex]::Matches($source, $sourceStampPattern).Count -ne 1) {
    throw 'BuildLockTopology888x14.cs must contain exactly one source identity stamp.'
}
$normalizedSource = [regex]::Replace($source, $sourceStampPattern, '${1}__SOURCE_SHA256__${2}')
$sourceNormalizedSha256 = Get-Sha256Text $normalizedSource
$compiledSource = [regex]::Replace($source, $sourceStampPattern, {
    param($match)
    return $match.Groups[1].Value + $sourceNormalizedSha256 + $match.Groups[2].Value
})

$temporarySource = Join-Path $toolDir ".BuildLockTopology888x14.build-$PID.cs"
$temporaryExecutable = Join-Path $binDir ".BuildLockTopology888x14.build-$PID.exe"
try {
    [IO.File]::WriteAllText($temporarySource, $compiledSource, [Text.UTF8Encoding]::new($false))
    & $compilerPath /nologo /platform:x64 /target:exe /optimize+ /warn:4 /warnaserror+ `
        "/out:$temporaryExecutable" "/reference:$interopPath" "/reference:$constantsPath" `
        /reference:System.Management.dll /reference:System.Web.Extensions.dll $temporarySource
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $temporaryExecutable -PathType Leaf)) {
        throw "BuildLockTopology888x14 compile failed with exit code $LASTEXITCODE."
    }
    Move-Item -LiteralPath $temporaryExecutable -Destination $executablePath -Force
}
finally {
    Remove-Item -LiteralPath $temporarySource -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $temporaryExecutable -Force -ErrorAction SilentlyContinue
}

$identity = & $executablePath --identity
if ($LASTEXITCODE -ne 0) { throw 'Built lock topology executable identity check failed.' }
$identityMap = @{}
foreach ($line in $identity) {
    if ($line -match '^([^=]+)=(.+)$') { $identityMap[$matches[1]] = $matches[2] }
}
$executableSha256 = Get-Sha256 $executablePath
if ($identityMap.sourceNormalizedSha256 -ne $sourceNormalizedSha256 -or
    $identityMap.executableSha256 -ne $executableSha256) {
    throw 'Built lock topology executable does not bind its source and executable identities.'
}

$selfTest = & $executablePath --self-test | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $selfTest.selfTest -ne 'PASS' -or $selfTest.solidWorksStarted -ne $false -or
    $selfTest.standardToolchainNormalizerContract -ne $true) {
    throw 'Built lock topology executable self-test failed.'
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$primary = $manifest.tools.native_lock_topology_888x14_v1
$primary.sourceNormalizedSha256 = $sourceNormalizedSha256
$primary.executableSha256 = $executableSha256
$manifest.validator.sha256 = Get-Sha256 $validatorPath
$manifestJson = ($manifest | ConvertTo-Json -Depth 20) + "`n"
$temporaryManifest = "$manifestPath.build-$PID.tmp"
try {
    [IO.File]::WriteAllText($temporaryManifest, $manifestJson, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryManifest -Destination $manifestPath -Force
}
finally {
    Remove-Item -LiteralPath $temporaryManifest -Force -ErrorAction SilentlyContinue
}

[pscustomobject]@{
    status = 'PASS'
    sourceNormalizedSha256 = $sourceNormalizedSha256
    executableSha256 = $executableSha256
    manifestSha256 = Get-Sha256 $manifestPath
    solidWorksStarted = $false
} | ConvertTo-Json
