[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot 'ConfigureNativeWidth888.exe'
}
$sourcePath = Join-Path $PSScriptRoot 'ConfigureNativeWidth888.cs'
$interopRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..\generated_models\review_generation_requests\v43-int-v37-760w-six-door-l642-r246-r1\work\tools\bin'))
$sldworksInterop = Join-Path $interopRoot 'SolidWorks.Interop.sldworks.dll'
$swconstInterop = Join-Path $interopRoot 'SolidWorks.Interop.swconst.dll'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputFullPath = [IO.Path]::GetFullPath($OutputPath)

foreach ($path in @($sourcePath, $sldworksInterop, $swconstInterop, $compiler)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing build input: $path" }
}

function Get-NormalizedSourceHash([string]$text) {
    $normalized = $text -replace "`r`n?", "`n"
    $normalized = [regex]::Replace($normalized,
        'private const string ExpectedSourceSha256 = "[A-Z0-9_]+";',
        'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";')
    return ([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($normalized)) |
        ForEach-Object { $_.ToString('X2') }) -join ''
}

$before = @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue | ForEach-Object { "$($_.ProcessName):$($_.Id)" } | Sort-Object)
$source = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::UTF8)
$sourceHash = Get-NormalizedSourceHash $source
$stamped = [regex]::Replace($source,
    'private const string ExpectedSourceSha256 = "[A-Z0-9_]+";',
    ('private const string ExpectedSourceSha256 = "' + $sourceHash + '";'))
[IO.File]::WriteAllText($sourcePath, $stamped, (New-Object Text.UTF8Encoding($false)))

$arguments = @('/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$outputFullPath", '/reference:System.Management.dll', '/reference:System.Web.Extensions.dll',
    "/link:$sldworksInterop", "/link:$swconstInterop", $sourcePath)
$compilerOutput = @(& $compiler @arguments 2>&1 | ForEach-Object { $_.ToString() })
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputFullPath -PathType Leaf)) {
    throw "C# compile failed ($LASTEXITCODE): $($compilerOutput -join [Environment]::NewLine)"
}

$after = @(Get-Process -Name SLDWORKS, sldProcMon -ErrorAction SilentlyContinue | ForEach-Object { "$($_.ProcessName):$($_.Id)" } | Sort-Object)
if (@(Compare-Object $before $after).Count -ne 0) { throw 'Static build changed CAD process set.' }

[pscustomobject]@{
    source_normalized_sha256 = $sourceHash
    executable_sha256 = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash
    compiler_output = $compilerOutput
    cad_process_set_unchanged = $true
}
