[CmdletBinding()]
param(
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$toolDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $toolDir '..\..\..\..\..\..')).Path
$sourcePath = Join-Path $toolDir 'SeedPack888Native.cs'
$executablePath = Join-Path $toolDir 'SeedPack888Native.exe'
$readmePath = Join-Path $toolDir 'README.md'
$verifierPath = Join-Path $toolDir 'Verify-SeedPack888Native.ps1'
$nodeVerifierPath = Join-Path $toolDir 'verify_static_v2.mjs'
$manifestPath = Join-Path $toolDir 'toolchain_manifest.json'
$staticVerificationPath = Join-Path $toolDir 'static_verification.json'
$sha256SumsPath = Join-Path $toolDir 'SHA256SUMS.txt'
$interopRoot = [IO.Path]::GetFullPath((Join-Path $toolDir '..\bin'))
$sldworksInterop = Join-Path $interopRoot 'SolidWorks.Interop.sldworks.dll'
$swconstInterop = Join-Path $interopRoot 'SolidWorks.Interop.swconst.dll'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

foreach ($requiredPath in @($sourcePath, $readmePath, $verifierPath, $nodeVerifierPath,
        $sldworksInterop, $swconstInterop, $compiler)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required build input is missing: $requiredPath"
    }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

$sourceStampPattern = '(private\s+const\s+string\s+ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})("\s*;)'

function Get-NormalizedSourceSha256([string]$Path) {
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    if ([regex]::Matches($text, $sourceStampPattern).Count -ne 1) {
        throw "Source must contain exactly one recognized ExpectedSourceSha256 stamp: $Path"
    }
    $text = [regex]::Replace($text, $sourceStampPattern, '${1}__SOURCE_SHA256__${2}')
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($text)
        return ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

$before = @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
    Sort-Object ProcessName, Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" })

$sourceHash = Get-NormalizedSourceSha256 $sourcePath
$source = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::UTF8)
if ([regex]::Matches($source, $sourceStampPattern).Count -ne 1) {
    throw 'Seed source stamp cannot be embedded because the declaration is missing or ambiguous.'
}
$source = [regex]::Replace($source, $sourceStampPattern, {
    param($match)
    return $match.Groups[1].Value + $sourceHash + $match.Groups[2].Value
})
[IO.File]::WriteAllText($sourcePath, $source, [Text.UTF8Encoding]::new($false))
if ((Get-NormalizedSourceSha256 $sourcePath) -ne $sourceHash) {
    throw 'Normalized source SHA drifted while embedding the source identity.'
}
$embeddedStampPattern = '(private\s+const\s+string\s+ExpectedSourceSha256\s*=\s*")([A-F0-9]{64})("\s*;)'
$embeddedStamp = [regex]::Matches($source, $embeddedStampPattern)
if ($embeddedStamp.Count -ne 1 -or $embeddedStamp[0].Groups[2].Value -ne $sourceHash) {
    throw 'Embedded ExpectedSourceSha256 does not equal the normalized source SHA-256.'
}

$arguments = @(
    '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/warnaserror+',
    '/codepage:65001', "/out:$executablePath", '/reference:System.Core.dll',
    '/reference:System.Management.dll', '/reference:System.Web.Extensions.dll',
    "/reference:$sldworksInterop", "/reference:$swconstInterop", $sourcePath
)
$compilerOutput = @(& $compiler @arguments 2>&1 | ForEach-Object { $_.ToString() })
$compilerExitCode = $LASTEXITCODE
if ($compilerExitCode -ne 0 -or -not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "C# compile failed ($compilerExitCode): $($compilerOutput -join [Environment]::NewLine)"
}

$afterCompile = @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
    Sort-Object ProcessName, Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" })
if (@(Compare-Object -ReferenceObject $before -DifferenceObject $afterCompile).Count -ne 0) {
    throw 'Static compilation changed the CAD process set.'
}

$repoForward = $repoRoot.Replace('\', '/').TrimEnd('/') + '/'
function Get-RepoRelative([string]$Path) {
    $full = (Resolve-Path -LiteralPath $Path).Path.Replace('\', '/')
    if (-not $full.StartsWith($repoForward, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Artifact escaped repository: $full"
    }
    return $full.Substring($repoForward.Length)
}

$manifest = [ordered]@{
    schema = 'winnsen.16029.native_toolchain_manifest.v1'
    generatedBy = Get-RepoRelative $verifierPath
    tool = [ordered]@{
        id = 'native_seed_pack_888x14_v1'
        sourcePath = Get-RepoRelative $sourcePath
        sourceNormalizedSha256 = $sourceHash
        executablePath = Get-RepoRelative $executablePath
        executableSha256 = Get-Sha256 $executablePath
        verifierPath = Get-RepoRelative $verifierPath
        verifierSha256 = Get-Sha256 $verifierPath
    }
}
$manifestJson = ($manifest | ConvertTo-Json -Depth 8) + "`n"
[IO.File]::WriteAllText($manifestPath, $manifestJson, [Text.UTF8Encoding]::new($false))

if (-not $SkipVerify) {
    & $verifierPath
    if ($LASTEXITCODE -ne 0) { throw 'Seed static verification failed.' }

    if (-not (Test-Path -LiteralPath $staticVerificationPath -PathType Leaf)) {
        throw "Static verification artifact is missing: $staticVerificationPath"
    }
    $checksumInputs = [ordered]@{
        'Build-SeedPack888Native.ps1' = $PSCommandPath
        'README.md' = $readmePath
        'SeedPack888Native.cs' = $sourcePath
        'SeedPack888Native.exe' = $executablePath
        'static_verification.json' = $staticVerificationPath
        'toolchain_manifest.json' = $manifestPath
        'verify_static_v2.mjs' = $nodeVerifierPath
        'Verify-SeedPack888Native.ps1' = $verifierPath
    }
    $checksumLines = @($checksumInputs.GetEnumerator() | ForEach-Object {
        "$(Get-Sha256 $_.Value)  $($_.Key)"
    })
    [IO.File]::WriteAllText($sha256SumsPath,
        ($checksumLines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    foreach ($entry in $checksumInputs.GetEnumerator()) {
        $expected = @($checksumLines | Where-Object { $_ -like "*  $($entry.Key)" })
        if ($expected.Count -ne 1 -or
            -not $expected[0].StartsWith((Get-Sha256 $entry.Value) + '  ',
                [StringComparison]::Ordinal)) {
            throw "SHA256SUMS verification failed: $($entry.Key)"
        }
    }
}

[ordered]@{
    status = 'PASS'
    compiler = $compiler
    compilerExitCode = $compilerExitCode
    compilerOutput = $compilerOutput
    sourceNormalizedSha256 = $sourceHash
    executableSha256 = Get-Sha256 $executablePath
    toolchainManifestSha256 = Get-Sha256 $manifestPath
    sha256SumsUpdated = -not $SkipVerify
    sha256SumsSha256 = if ($SkipVerify) { '' } else { Get-Sha256 $sha256SumsPath }
    cadProcessesBefore = $before
    cadProcessesAfterCompile = $afterCompile
} | ConvertTo-Json -Depth 6
