$ErrorActionPreference = 'Stop'
$taskDir = $PSScriptRoot
$taskExpectedDir = 'D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\mate_finite_witness_v1'
if ([IO.Path]::GetFullPath($taskDir) -ne $taskExpectedDir) { throw 'BUILD_DIRECTORY_NOT_AUTHORIZED' }
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskSdk = 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
$taskHelp = 'D:\soildworks2020\SOLIDWORKS\api\sldworksapi.chm'
$taskSources = @('MateFiniteWitness.cs','PureCases.cs','build.ps1')
$taskBindings = @($taskSources | ForEach-Object { [ordered]@{ file=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $taskDir $_) -Algorithm SHA256).Hash } })
$taskVersionBytes = [Text.Encoding]::UTF8.GetBytes(($taskBindings | ConvertTo-Json -Compress))
$taskHasher = [Security.Cryptography.SHA256]::Create()
try { $taskVersion = ([BitConverter]::ToString($taskHasher.ComputeHash($taskVersionBytes))).Replace('-','') } finally { $taskHasher.Dispose() }
$taskBin = Join-Path $taskDir ('bin\' + $taskVersion.Substring(0,16))
if (Test-Path -LiteralPath $taskBin) { throw 'ARTIFACT_VERSION_EXISTS_DO_NOT_OVERWRITE' }
New-Item -ItemType Directory -Path $taskBin -Force | Out-Null
Copy-Item -LiteralPath $taskSdk -Destination $taskBin
$taskDll = Join-Path $taskBin 'MateFiniteWitness.dll'
$taskExe = Join-Path $taskBin 'PureCases.exe'
$taskReport = Join-Path $taskBin 'pure-results.json'
$taskAssembly = [Reflection.Assembly]::LoadFrom($taskSdk)
$taskApis = @{
    ISldWorks=@('ActiveDoc')
    IModelDoc2=@('GetType','GetPathName','GetActiveConfiguration','Extension')
    IModelDocExtension=@('GetPersistReference3','GetCorrespondingEntity2')
    IConfiguration=@('Name')
    IFeature=@('Name','GetTypeName2','GetID','IsSuppressed','GetSpecificFeature2')
    IMate2=@('Type','Alignment','Flipped','CanBeFlipped','GetMateEntityCount','MateEntity')
    IMateEntity2=@('ReferenceType','EntityParams','ReferenceComponent','Reference')
    IComponent2=@('Name2','GetPathName','ReferencedConfiguration','IsVirtual','IsSuppressed','GetModelDoc2','Transform2','GetTotalTransform','GetCorrespondingEntity')
    IMathTransform=@('ArrayData')
    IEntity=@('GetComponent')
    IFace2=@('GetSurface','FaceInSurfaceSense','GetArea','GetLoopCount','GetEdgeCount','GetLoops','GetEdges')
    ISurface=@('Identity','IsCylinder','CylinderParams')
    ILoop2=@('IsOuter','IsSingular','GetCoEdgeCount','GetEdgeCount','GetVertexCount','GetEdges','GetVertices','GetCoEdges','GetFirstCoEdge')
    ICoEdge=@('GetNext','GetEdge','GetSense','GetCurveParams','GetCurve')
    IEdge=@('GetCurve','GetCurveParams3','GetStartVertex','GetEndVertex')
    ICurve=@('Identity','IsCircle','IsLine','CircleParams','LineParams','GetEndParams','GetLength3')
    ICurveParamData=@('StartPoint','EndPoint','UMinValue','UMaxValue','Sense','CurveType','CurveTag')
    IVertex=@('GetPoint')
}
$taskSignatures = @(foreach ($taskTypeName in ($taskApis.Keys | Sort-Object)) {
    $taskType = $taskAssembly.GetType('SolidWorks.Interop.sldworks.'+$taskTypeName,$true)
    foreach ($taskMemberName in $taskApis[$taskTypeName]) {
        $taskMembers = @($taskType.GetMembers() | Where-Object Name -eq $taskMemberName)
        if ($taskMembers.Count -eq 0) { throw ('SDK_MEMBER_NOT_FOUND '+$taskTypeName+'.'+$taskMemberName) }
        foreach ($taskMember in $taskMembers) {
            [ordered]@{ type=$taskTypeName; member=$taskMemberName; signature=$taskMember.ToString() }
        }
    }
})
$taskApiPath = Join-Path $taskBin 'api-signatures.json'
[ordered]@{assembly=$taskSdk;assemblySha256=(Get-FileHash -LiteralPath $taskSdk).Hash;officialLocalHelp=$taskHelp;officialLocalHelpSha256=(Get-FileHash -LiteralPath $taskHelp).Hash;signatures=$taskSignatures} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskApiPath -Encoding utf8
$taskLibraryArgs = @('/nologo','/target:library','/platform:x64','/optimize+','/warn:4','/warnaserror+',('/out:'+$taskDll),'/reference:System.Core.dll',('/reference:'+$taskSdk),(Join-Path $taskDir 'MateFiniteWitness.cs'))
$taskLibraryOutput = @(& $taskCompiler @taskLibraryArgs 2>&1)
$taskLibraryExit = $LASTEXITCODE
$taskLibraryOutput | Set-Content -LiteralPath (Join-Path $taskBin 'compile-library.log') -Encoding utf8
$taskTestExit = $null; $taskRunExit = $null; $taskTestOutput = @(); $taskRunOutput = @()
if ($taskLibraryExit -eq 0) {
    $taskTestArgs = @('/nologo','/target:exe','/platform:x64','/optimize+','/warn:4','/warnaserror+',('/out:'+$taskExe),'/reference:System.Core.dll','/reference:System.Web.Extensions.dll',('/reference:'+$taskDll),(Join-Path $taskDir 'PureCases.cs'))
    $taskTestOutput = @(& $taskCompiler @taskTestArgs 2>&1); $taskTestExit = $LASTEXITCODE
    $taskTestOutput | Set-Content -LiteralPath (Join-Path $taskBin 'compile-pure.log') -Encoding utf8
    if ($taskTestExit -eq 0) {
        $taskRunOutput = @(& $taskExe $taskReport 2>&1); $taskRunExit = $LASTEXITCODE
        $taskRunOutput | Set-Content -LiteralPath (Join-Path $taskBin 'pure-run.log') -Encoding utf8
    }
}
$taskResult = [ordered]@{
    schema='mate-finite-witness-build-v1';builtAtUtc=[DateTime]::UtcNow.ToString('o');artifactVersion=$taskVersion
    sourceBindings=$taskBindings;compiler=$taskCompiler;compilerSha256=(Get-FileHash -LiteralPath $taskCompiler).Hash
    sdkPath=$taskSdk;sdkSha256=(Get-FileHash -LiteralPath $taskSdk).Hash;apiEvidence=$taskApiPath
    libraryCompilerArguments=$taskLibraryArgs;libraryCompilerExitCode=$taskLibraryExit;libraryCompilerOutput=$taskLibraryOutput
    pureCompilerArguments=$taskTestArgs;pureCompilerExitCode=$taskTestExit;pureCompilerOutput=$taskTestOutput
    pureRunArguments=@($taskExe,$taskReport);pureRunExitCode=$taskRunExit;pureRunOutput=$taskRunOutput
    libraryCompiled=($taskLibraryExit -eq 0);pureTestsPassed=($taskRunExit -eq 0 -and $null -ne $taskRunExit)
    nativeCadExecuted=$false;realFourFaceCaptureVerified=$false;integratedIntoV6=$false;overallModelAccepted=$false
}
if (Test-Path -LiteralPath $taskDll) { $taskResult.librarySha256=(Get-FileHash -LiteralPath $taskDll).Hash }
if (Test-Path -LiteralPath $taskExe) { $taskResult.pureExecutableSha256=(Get-FileHash -LiteralPath $taskExe).Hash }
if (Test-Path -LiteralPath $taskReport) { $taskResult.pureReportSha256=(Get-FileHash -LiteralPath $taskReport).Hash }
$taskResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskBin 'build-result.json') -Encoding utf8
$taskResult | ConvertTo-Json -Depth 10
if ($taskLibraryExit -ne 0 -or $taskTestExit -ne 0 -or $taskRunExit -ne 0 -or $null -eq $taskRunExit) { throw 'BUILD_OR_PURE_TEST_FAILED' }
