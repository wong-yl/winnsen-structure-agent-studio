$ErrorActionPreference = 'Stop'
$testsRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$trialRoot = Split-Path -Parent $testsRoot
$tempDir = Join-Path $testsRoot ('.offline-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir | Out-Null
try {
    $interopDir = 'D:\soildworks2020\SOLIDWORKS\api\redist'
    foreach ($name in @('SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll')) {
        Copy-Item -LiteralPath (Join-Path $interopDir $name) -Destination (Join-Path $tempDir $name)
    }
    $exePath = Join-Path $tempDir 'TrialAssembly.OfflineTests.exe'
    $compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $rootSources = Get-ChildItem -LiteralPath $trialRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }
    $argsList = @(
        '/nologo',
        '/target:exe',
        '/platform:x64',
        '/optimize+',
        '/codepage:65001',
        '/main:TrialAssemblyOfflineTests',
        ('/out:' + $exePath),
        '/reference:System.Web.Extensions.dll',
        '/reference:System.Management.dll',
        ('/reference:' + (Join-Path $tempDir 'SolidWorks.Interop.sldworks.dll')),
        ('/reference:' + (Join-Path $tempDir 'SolidWorks.Interop.swconst.dll'))
    ) + $rootSources + @((Join-Path $testsRoot 'TrialAssembly.OfflineTests.cs'))
    & $compiler @argsList
    if ($LASTEXITCODE -ne 0) { throw 'TRIAL_ASSEMBLY_OFFLINE_COMPILATION_FAILED' }
    & $exePath
    if ($LASTEXITCODE -ne 0) { throw 'TRIAL_ASSEMBLY_OFFLINE_TESTS_FAILED' }
} finally {
    $resolvedTemp = [IO.Path]::GetFullPath($tempDir)
    if (-not $resolvedTemp.StartsWith($testsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolvedTemp).StartsWith('.offline-tests-')) {
        throw 'REFUSE_OFFLINE_TEMP_CLEANUP_OUTSIDE_SCOPE'
    }
    Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
}
