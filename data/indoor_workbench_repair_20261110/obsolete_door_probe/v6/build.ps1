$ErrorActionPreference = 'Stop'
$taskProbe = $PSScriptRoot
$taskAllowed = 'D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\v6'
if ([IO.Path]::GetFullPath($taskProbe) -ne $taskAllowed) { throw 'BUILD_DIRECTORY_NOT_AUTHORIZED' }
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskInterop = 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
$taskConstants = 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll'
$taskSource = Join-Path $taskProbe 'IdentityProbe.cs'
$taskSourceSha = (Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
$taskBin = Join-Path $taskProbe ('bin\' + $taskSourceSha.Substring(0,12))
$taskReport = Join-Path (Split-Path -Parent $taskProbe) 'build-v6.json'
$taskExe = Join-Path $taskBin 'IdentityProbe.exe'
if (Test-Path -LiteralPath $taskReport) { throw 'BUILD_REPORT_EXISTS_DO_NOT_OVERWRITE' }
if (Test-Path -LiteralPath $taskBin) { throw 'BUILD_ARTIFACT_DIRECTORY_EXISTS_DO_NOT_OVERWRITE' }
New-Item -ItemType Directory -Path $taskBin -Force | Out-Null
Copy-Item -LiteralPath $taskInterop -Destination $taskBin
Copy-Item -LiteralPath $taskConstants -Destination $taskBin
$taskAssembly = [Reflection.Assembly]::LoadFrom($taskInterop)
$taskApis = @{
    ISldWorks = @('GetProcessID','RevisionNumber','GetDocumentCount','GetDocuments','OpenDoc6','CloseAllDocuments','ExitApp')
    IModelDoc2 = @('GetPathName','GetType','IsOpenedReadOnly','GetSaveFlag','FirstFeature','GetConfigurationNames','GetActiveConfiguration','ListExternalFileReferences2','Extension','ForceRebuild3','Save3','ClearSelection2','SelectionManager')
    IModelDocExtension = @('GetPersistReference3','DeleteSelection2')
    IFeature = @('Name','GetTypeName2','GetID','IsSuppressed','GetErrorCode','GetErrorCode2','GetNextFeature','GetFirstSubFeature','GetNextSubFeature','ListExternalFileReferences2','GetSpecificFeature2')
    IComponent2 = @('Name2','ReferencedConfiguration','IsVirtual','GetPathName','GetSuppression','GetSuppression2','IsSuppressed','GetModelDoc2','GetChildren','ListExternalFileReferences2','GetBodies3','Transform2','GetTotalTransform','IsFixed','Select4')
    IAssemblyDoc = @('GetComponents')
    IMate2 = @('Type','Alignment','Flipped','CanBeFlipped','GetMateEntityCount','MateEntity')
    IMateEntity2 = @('ReferenceComponent','Reference','ReferenceType','EntityParams')
    IBody2 = @('Name','GetType','Check3','Visible','IsSheetMetal','GetFaceCount','GetEdgeCount','GetBodyBox','GetMassProperties')
    IFaultEntity = @('Count')
    IConfiguration = @('Name')
    IPartDoc = @('GetBodies2')
    IMathTransform = @('ArrayData')
    ISelectionMgr = @('GetSelectedObjectCount2','GetSelectedObject6')
}
$taskSignatures = @()
foreach ($taskName in ($taskApis.Keys | Sort-Object)) {
    $taskType = $taskAssembly.GetType('SolidWorks.Interop.sldworks.' + $taskName, $true)
    foreach ($taskMember in ($taskType.GetMembers() | Where-Object { $taskApis[$taskName] -contains $_.Name } | Sort-Object Name)) {
        $taskEntry = [ordered]@{type=$taskName;member=$taskMember.Name;signature=$taskMember.ToString()}
        if ($taskMember -is [Reflection.MethodInfo]) {
            $taskEntry.parameters = @($taskMember.GetParameters() | ForEach-Object { [ordered]@{name=$_.Name;type=$_.ParameterType.ToString();isOut=$_.IsOut;isIn=$_.IsIn} })
        }
        $taskSignatures += $taskEntry
    }
}
$taskApiReport = Join-Path $taskBin 'api-signatures.json'
[ordered]@{assembly=$taskInterop;sha256=(Get-FileHash -LiteralPath $taskInterop -Algorithm SHA256).Hash;signatures=$taskSignatures} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskApiReport -Encoding utf8
$taskArgs = @('/nologo','/target:exe','/platform:x64','/optimize+','/warn:4',('/out:'+$taskExe),'/reference:System.Core.dll','/reference:System.Web.Extensions.dll',('/reference:'+$taskInterop),('/reference:'+$taskConstants),$taskSource)
$taskOutput = @(& $taskCompiler @taskArgs 2>&1)
$taskExit = $LASTEXITCODE
$taskOutput | Set-Content -LiteralPath (Join-Path $taskBin 'compile.log') -Encoding utf8
$taskResult = [ordered]@{
    schema='obsolete-door-isolated-probe-build-v1';builtAtUtc=[DateTime]::UtcNow.ToString('o');buildPassed=($taskExit -eq 0)
    source=$taskSource;sourceSha256=$taskSourceSha;executable=$taskExe;compiler=$taskCompiler;compilerArguments=$taskArgs
    compilerExitCode=$taskExit;compilerOutput=$taskOutput;apiSignatureEvidence=$taskApiReport
    buildScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    nativeCadExecuted=$false;scope='throwaway single-checkpoint migration probe only'
}
if ($taskExit -eq 0) { $taskResult.executableSha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash }
if ($taskExit -ne 0) { $taskReport=Join-Path $taskBin 'build-failed.json' }
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskReport -Encoding utf8
$taskResult | ConvertTo-Json -Depth 8
if ($taskExit -ne 0) { throw ('COMPILE_FAILED '+$taskExit) }
