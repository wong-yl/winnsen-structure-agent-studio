param(
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$toolDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $toolDir '..\..\..\..\..\..')).Path
$sourcePath = Join-Path $toolDir 'FinalPack888x14Native.cs'
$executablePath = Join-Path $toolDir 'FinalPack888x14Native.exe'
$verifierPath = Join-Path $toolDir 'verify_static.mjs'
$snapshotPath = Join-Path $toolDir 'assembly_contract.snapshot.json'
$manifestPath = Join-Path $toolDir 'toolchain_manifest.json'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-NormalizedSourceSha256([string]$Path) {
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $text = [regex]::Replace($text,
        'private const string ExpectedSourceSha256\s*=\s*"(?:__SOURCE_SHA256__|[0-9A-F]{64})";',
        { param($match) [regex]::Replace($match.Value,
            '(?:__SOURCE_SHA256__|[0-9A-F]{64})', '__SOURCE_SHA256__') })
    $text = [regex]::Replace($text,
        'private const string ExpectedContractSnapshotSha256\s*=\s*"(?:__CONTRACT_SHA256__|[0-9A-F]{64})";',
        { param($match) [regex]::Replace($match.Value,
            '(?:__CONTRACT_SHA256__|[0-9A-F]{64})', '__CONTRACT_SHA256__') })
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($text)
        return ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '')
    }
    finally { $algorithm.Dispose() }
}

if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) {
    throw "C# compiler missing: $csc"
}

& node (Join-Path $toolDir 'emit_contract_snapshot.mjs') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'assembly contract snapshot emission failed' }

$contractSha = Get-Sha256 $snapshotPath
$sourceSha = Get-NormalizedSourceSha256 $sourcePath
$source = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::UTF8)
$source = [regex]::Replace($source,
    'private const string ExpectedSourceSha256 = "[A-Z0-9_]+";',
    "private const string ExpectedSourceSha256 = `"$sourceSha`";")
$source = [regex]::Replace($source,
    'private const string ExpectedContractSnapshotSha256 = "[A-Z0-9_]+";',
    "private const string ExpectedContractSnapshotSha256 = `"$contractSha`";")
[IO.File]::WriteAllText($sourcePath, $source, [Text.UTF8Encoding]::new($false))

if ((Get-NormalizedSourceSha256 $sourcePath) -ne $sourceSha) {
    throw 'normalized source SHA drifted while embedding identity constants'
}

$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Management.dll',
    'System.Web.Extensions.dll',
    (Join-Path $toolDir 'SolidWorks.Interop.sldworks.dll'),
    (Join-Path $toolDir 'SolidWorks.Interop.swconst.dll')
)
foreach ($reference in $references) {
    if ($reference -like '*.dll' -and [IO.Path]::IsPathRooted($reference) -and
        -not (Test-Path -LiteralPath $reference -PathType Leaf)) {
        throw "compiler reference missing: $reference"
    }
}

$arguments = @('/nologo', '/target:exe', '/platform:x64', '/optimize+', '/warnaserror+',
    "/out:$executablePath")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sourcePath
& $csc @arguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw 'FinalPack888x14Native compilation failed'
}

$repoForward = $repoRoot.Replace('\', '/').TrimEnd('/') + '/'
function Repo-Relative([string]$Path) {
    $full = (Resolve-Path -LiteralPath $Path).Path.Replace('\', '/')
    if (-not $full.StartsWith($repoForward, [StringComparison]::OrdinalIgnoreCase)) {
        throw "artifact escaped repository: $full"
    }
    return $full.Substring($repoForward.Length)
}

$manifest = [ordered]@{
    schema = 'winnsen.16029.native_toolchain_manifest.v1'
    generatedBy = 'Build-FinalPack888x14Native.ps1'
    tool = [ordered]@{
        id = 'native_final_pack_888x14_v1'
        sourcePath = Repo-Relative $sourcePath
        sourceNormalizedSha256 = $sourceSha
        executablePath = Repo-Relative $executablePath
        executableSha256 = Get-Sha256 $executablePath
        verifierPath = Repo-Relative $verifierPath
        verifierSha256 = Get-Sha256 $verifierPath
    }
}
$json = ($manifest | ConvertTo-Json -Depth 8) + "`n"
[IO.File]::WriteAllText($manifestPath, $json, [Text.UTF8Encoding]::new($false))

if (-not $SkipVerify) {
    & node $verifierPath
    if ($LASTEXITCODE -ne 0) { throw 'static verification failed' }
}

[ordered]@{
    status = 'PASS'
    cadStarted = $false
    sourceNormalizedSha256 = $sourceSha
    executableSha256 = Get-Sha256 $executablePath
    contractSnapshotSha256 = $contractSha
    toolchainManifestSha256 = Get-Sha256 $manifestPath
} | ConvertTo-Json
