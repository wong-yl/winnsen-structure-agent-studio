[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'NativeDoorModule888x14.exe')
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..\..')).Path
$sourcePath = Join-Path $PSScriptRoot 'NativeDoorModule888x14.cs'
$verifierPath = Join-Path $PSScriptRoot 'Verify-NativeDoorModule888x14.ps1'
$manifestPath = Join-Path $PSScriptRoot 'toolchain_manifest.json'
$interopRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin'))
$sldworksInterop = Join-Path $interopRoot 'SolidWorks.Interop.sldworks.dll'
$swconstInterop = Join-Path $interopRoot 'SolidWorks.Interop.swconst.dll'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputFullPath = [IO.Path]::GetFullPath($OutputPath)

function Get-NormalizedSourceSha256 {
    param([string]$Path)
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).
        Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

foreach ($requiredPath in @($sourcePath, $sldworksInterop, $swconstInterop, $compiler)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required build input is missing: $requiredPath"
    }
}

$cadBefore = @(
    Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
        Sort-Object ProcessName, Id |
        ForEach-Object { "$($_.ProcessName):$($_.Id)" }
)

$arguments = @(
    '/nologo',
    '/target:exe',
    '/platform:x64',
    '/optimize+',
    '/warnaserror+',
    '/codepage:65001',
    "/out:$outputFullPath",
    '/reference:System.Core.dll',
    '/reference:System.Web.Extensions.dll',
    "/reference:$sldworksInterop",
    "/reference:$swconstInterop",
    $sourcePath
)
$compilerOutput = @(& $compiler @arguments 2>&1 | ForEach-Object { $_.ToString() })
$compilerExitCode = $LASTEXITCODE
if ($compilerExitCode -ne 0 -or -not (Test-Path -LiteralPath $outputFullPath -PathType Leaf)) {
    throw "C# compile failed ($compilerExitCode): $($compilerOutput -join [Environment]::NewLine)"
}

$outputDirectory = Split-Path -Parent $outputFullPath
foreach ($interopPath in @($sldworksInterop, $swconstInterop)) {
    $targetInterop = Join-Path $outputDirectory (Split-Path -Leaf $interopPath)
    Copy-Item -LiteralPath $interopPath -Destination $targetInterop -Force
    if ((Get-FileHash -LiteralPath $interopPath -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $targetInterop -Algorithm SHA256).Hash) {
        throw "Interop copy hash mismatch: $targetInterop"
    }
}

$cadAfter = @(
    Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue |
        Sort-Object ProcessName, Id |
        ForEach-Object { "$($_.ProcessName):$($_.Id)" }
)
$cadDelta = @(Compare-Object -ReferenceObject $cadBefore -DifferenceObject $cadAfter)
if ($cadDelta.Count -ne 0) {
    throw "Static compilation changed the CAD process set: $($cadDelta -join ', ')"
}

$repoForward = $repoRoot.Replace('\', '/').TrimEnd('/') + '/'
function Repo-Relative([string]$Path) {
    $full = (Resolve-Path -LiteralPath $Path).Path.Replace('\', '/')
    if (-not $full.StartsWith($repoForward, [StringComparison]::OrdinalIgnoreCase)) {
        throw "artifact escaped repository: $full"
    }
    return $full.Substring($repoForward.Length)
}

$sourceNormalizedSha256 = Get-NormalizedSourceSha256 -Path $sourcePath
$manifest = [ordered]@{
    schema = 'winnsen.16029.native_toolchain_manifest.v1'
    generatedBy = 'Build-NativeDoorModule888x14.ps1'
    tool = [ordered]@{
        id = 'native_door_module_888x14_v1'
        sourcePath = Repo-Relative $sourcePath
        sourceNormalizedSha256 = $sourceNormalizedSha256
        executablePath = Repo-Relative $outputFullPath
        executableSha256 = Get-Sha256 $outputFullPath
        verifierPath = Repo-Relative $verifierPath
        verifierSha256 = Get-Sha256 $verifierPath
    }
}
$manifestJson = ($manifest | ConvertTo-Json -Depth 8) + "`n"
[IO.File]::WriteAllText($manifestPath, $manifestJson, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    schema = 'winnsen.16029.native_door_module_build.v1'
    compiler = $compiler
    compiler_exit_code = $compilerExitCode
    compiler_output = $compilerOutput
    source_path = [IO.Path]::GetFullPath($sourcePath)
    source_sha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    source_normalized_sha256 = $sourceNormalizedSha256
    output_path = $outputFullPath
    output_size_bytes = (Get-Item -LiteralPath $outputFullPath).Length
    output_sha256 = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash
    toolchain_manifest_path = [IO.Path]::GetFullPath($manifestPath)
    toolchain_manifest_sha256 = Get-Sha256 $manifestPath
    cad_processes_before = $cadBefore
    cad_processes_after = $cadAfter
    cad_process_set_unchanged = $true
}
