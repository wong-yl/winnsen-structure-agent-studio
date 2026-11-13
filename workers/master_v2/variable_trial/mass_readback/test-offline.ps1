$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$taskTemp = Join-Path $taskRoot ('.offline-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskTemp | Out-Null
try {
    $taskInterop = 'D:\soildworks2020\SOLIDWORKS\api\redist'
    foreach ($name in @('SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll')) {
        Copy-Item -LiteralPath (Join-Path $taskInterop $name) -Destination (Join-Path $taskTemp $name)
    }
    $taskExe = Join-Path $taskTemp 'WeightReadback.OfflineTests.exe'
    $taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $taskArguments = @('/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + $taskExe), ('/reference:' + (Join-Path $taskTemp 'SolidWorks.Interop.sldworks.dll')), ('/reference:' + (Join-Path $taskTemp 'SolidWorks.Interop.swconst.dll')), (Join-Path $taskRoot 'WeightReadback.cs'), (Join-Path $taskRoot 'WeightReadback.OfflineTests.cs'))
    & $taskCompiler @taskArguments
    if ($LASTEXITCODE -ne 0) { throw 'WEIGHT_OFFLINE_COMPILATION_FAILED' }
    & $taskExe (Join-Path $taskTemp 'text-fixtures')
    if ($LASTEXITCODE -ne 0) { throw 'WEIGHT_OFFLINE_TESTS_FAILED' }
    & node --test (Join-Path $taskRoot 'build.test.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'WEIGHT_ADAPTER_OFFLINE_TESTS_FAILED' }
} finally {
    $taskResolved = [IO.Path]::GetFullPath($taskTemp)
    if (-not $taskResolved.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($taskResolved).StartsWith('.offline-tests-')) { throw 'REFUSE_OFFLINE_TEMP_CLEANUP_OUTSIDE_SCOPE' }
    Remove-Item -LiteralPath $taskResolved -Recurse -Force
}
