[CmdletBinding()]

param([switch]$SkipVerify)



$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest



$toolDir = $PSScriptRoot

$repoRoot = (Resolve-Path (Join-Path $toolDir '..\..\..\..\..\..')).Path

$sourcePath = Join-Path $toolDir 'TopCoverPartsNative.cs'

$executablePath = Join-Path $toolDir 'TopCoverPartsNative.exe'

$configPath = Join-Path $toolDir 'TopCoverPartsNative.exe.config'

$verifierPath = Join-Path $toolDir 'verify_static.mjs'

$manifestPath = Join-Path $toolDir 'toolchain_manifest.json'

$staticPath = Join-Path $toolDir 'static_verification.json'

$checksumsPath = Join-Path $toolDir 'SHA256SUMS.txt'

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

$interopRoot = [IO.Path]::GetFullPath((Join-Path $toolDir '..\bin'))

$sldworksInteropSource = Join-Path $interopRoot 'SolidWorks.Interop.sldworks.dll'

$swconstInteropSource = Join-Path $interopRoot 'SolidWorks.Interop.swconst.dll'

$sldworksInterop = Join-Path $toolDir 'SolidWorks.Interop.sldworks.dll'

$swconstInterop = Join-Path $toolDir 'SolidWorks.Interop.swconst.dll'



foreach ($required in @($sourcePath, $configPath, $verifierPath,

        (Join-Path $toolDir 'Verify-TopCoverPartsNative.ps1'),

        (Join-Path $toolDir 'README.md'), $compiler, $sldworksInteropSource,

        $swconstInteropSource)) {

    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {

        throw "Required build input is missing: $required"

    }

}



function Get-FileSha256([string]$Path) {

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()

}



$runtimeDependencies = @(

    [ordered]@{

        source = $sldworksInteropSource

        destination = $sldworksInterop

        expectedSha256 = 'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC'

    },

    [ordered]@{

        source = $swconstInteropSource

        destination = $swconstInterop

        expectedSha256 = '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359'

    }

)

foreach ($dependency in $runtimeDependencies) {

    if ((Get-FileSha256 $dependency.source) -cne $dependency.expectedSha256) {

        throw "Pinned interop source hash mismatch: $($dependency.source)"

    }

    if (Test-Path -LiteralPath $dependency.destination) {

        if ((Get-FileSha256 $dependency.destination) -cne $dependency.expectedSha256) {

            throw "Existing local interop hash mismatch: $($dependency.destination)"

        }

    } else {

        Copy-Item -LiteralPath $dependency.source -Destination $dependency.destination

    }

}



$sourceStampPattern = '(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)' 



function Get-NormalizedSourceSha256([string]$Path) {

    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)

    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")

    if ([regex]::Matches($text, $sourceStampPattern).Count -ne 1) {

        throw 'Source must contain exactly one ExpectedSourceSha256 stamp'

    }

    $normalized = [regex]::Replace($text, $sourceStampPattern,

        '${1}__SOURCE_SHA256__${2}')

    $sha = [Security.Cryptography.SHA256]::Create()

    try {

        return ([BitConverter]::ToString($sha.ComputeHash(

            [Text.UTF8Encoding]::new($false).GetBytes($normalized)))).Replace('-', '')

    } finally { $sha.Dispose() }

}



function Get-CadProcessSnapshot {

    return @(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue |

        Sort-Object ProcessName,Id |

        ForEach-Object { "$($_.ProcessName):$($_.Id)" })

}



$before = @(Get-CadProcessSnapshot)

if ($before.Count -ne 0) { throw 'Build requires zero CAD processes' }



$normalizedSha = Get-NormalizedSourceSha256 $sourcePath

$sourceText = [IO.File]::ReadAllText($sourcePath, [Text.Encoding]::UTF8)

$stamped = [regex]::Replace($sourceText, $sourceStampPattern,

    '${1}' + $normalizedSha + '${2}')

[IO.File]::WriteAllText($sourcePath, $stamped, [Text.UTF8Encoding]::new($false))

if ((Get-NormalizedSourceSha256 $sourcePath) -ne $normalizedSha) {

    throw 'Normalized source SHA changed after stamping'

}



$compilerOutput = @(& $compiler /nologo /target:exe /platform:x64 /optimize+ /warnaserror+ `

    /codepage:65001 "/out:$executablePath" /reference:System.Core.dll `

    /reference:System.Management.dll /reference:System.Web.Extensions.dll `

    "/reference:$sldworksInterop" "/reference:$swconstInterop" $sourcePath 2>&1)

if ($LASTEXITCODE -ne 0) {

    throw "C# compile failed: $($compilerOutput -join [Environment]::NewLine)"

}



$afterCompile = @(Get-CadProcessSnapshot)

if (@(Compare-Object -ReferenceObject $before -DifferenceObject $afterCompile).Count -ne 0) {

    throw 'Compilation changed the CAD process set'

}



$manifest = [ordered]@{

    schema = 'winnsen.16029.native_toolchain_manifest.v1'

    generatedBy = 'Build-TopCoverPartsNative.ps1'

    tool = [ordered]@{

        id = 'topcover_parts_native_v1'

        sourcePath = 'workers/native_model_requests/development/v1/tools/topcover_parts_native_v1/TopCoverPartsNative.cs'

        sourceNormalizedSha256 = $normalizedSha

        executablePath = 'workers/native_model_requests/development/v1/tools/topcover_parts_native_v1/TopCoverPartsNative.exe'

        executableSha256 = Get-FileSha256 $executablePath

        verifierPath = 'workers/native_model_requests/development/v1/tools/topcover_parts_native_v1/verify_static.mjs'

        verifierSha256 = Get-FileSha256 $verifierPath

        phase = 'front_760_native_pilot_phase2'

        cadImplemented = $true

        runtimeDependencies = @(

            [ordered]@{

                path = 'workers/native_model_requests/development/v1/tools/topcover_parts_native_v1/SolidWorks.Interop.sldworks.dll'

                sha256 = Get-FileSha256 $sldworksInterop

            },

            [ordered]@{

                path = 'workers/native_model_requests/development/v1/tools/topcover_parts_native_v1/SolidWorks.Interop.swconst.dll'

                sha256 = Get-FileSha256 $swconstInterop

            }

        )

    }

}

[IO.File]::WriteAllText($manifestPath,

    (($manifest | ConvertTo-Json -Depth 8) + "`n"), [Text.UTF8Encoding]::new($false))



if (-not $SkipVerify) {

    & (Join-Path $toolDir 'Verify-TopCoverPartsNative.ps1')

    if ($LASTEXITCODE -ne 0) { throw 'Static verification failed' }

}



$checksumFiles = @(

    'Build-TopCoverPartsNative.ps1', 'README.md', 'TopCoverPartsNative.cs',

    'TopCoverPartsNative.exe', 'TopCoverPartsNative.exe.config',

    'SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll',

    'toolchain_manifest.json', 'verify_static.mjs', 'Verify-TopCoverPartsNative.ps1'

)

$checksumLines = $checksumFiles | ForEach-Object {

    "$(Get-FileSha256 (Join-Path $toolDir $_))  $_"

}

[IO.File]::WriteAllText($checksumsPath,

    (($checksumLines -join "`n") + "`n"), [Text.Encoding]::ASCII)



[ordered]@{

    status = 'PASS'

    sourceNormalizedSha256 = $normalizedSha

    executableSha256 = Get-FileSha256 $executablePath

    manifestSha256 = Get-FileSha256 $manifestPath

    cadProcessesBefore = $before

    cadProcessesAfter = @(Get-CadProcessSnapshot)

    cadImplemented = $true

} | ConvertTo-Json -Depth 6

