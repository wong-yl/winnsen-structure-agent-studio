$ErrorActionPreference = 'Stop'
$taskProbe = $PSScriptRoot
$taskAllowed = 'D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\relink_v6'
if ([IO.Path]::GetFullPath($taskProbe) -ne $taskAllowed) { throw 'BUILD_DIRECTORY_NOT_AUTHORIZED' }
$taskCompiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskInterop = 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
$taskConstants = 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll'
$taskSource = Join-Path $taskProbe 'RelinkProbe.cs'
$taskSourceSha = (Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
$taskBin = Join-Path $taskProbe ('bin\' + $taskSourceSha.Substring(0,12))
$taskReport = Join-Path $taskProbe 'build.json'
$taskExe = Join-Path $taskBin 'RelinkProbe.exe'
if (Test-Path -LiteralPath $taskReport) { throw 'BUILD_REPORT_EXISTS_DO_NOT_OVERWRITE' }
New-Item -ItemType Directory -Path $taskBin -Force | Out-Null
Copy-Item -LiteralPath $taskInterop -Destination $taskBin
Copy-Item -LiteralPath $taskConstants -Destination $taskBin
$taskAssembly = [Reflection.Assembly]::LoadFrom($taskInterop)
$taskApis = @{
    ISldWorks = @('GetProcessID','RevisionNumber','GetDocumentCount','GetDocuments','GetDocumentDependencies2','ReplaceReferencedDocument','CloseAllDocuments','ExitApp')
}
$taskDependencyMethods = @($taskAssembly.GetType('SolidWorks.Interop.sldworks.ISldWorks', $true).GetMethods() | Where-Object Name -eq 'GetDocumentDependencies2')
if ($taskDependencyMethods.Count -ne 1 -or $taskDependencyMethods[0].ToString() -ne 'System.Object GetDocumentDependencies2(System.String, Boolean, Boolean, Boolean)') { throw 'DEPENDENCY_API_SIGNATURE_CHANGED' }
if (($taskDependencyMethods[0].GetParameters().Name -join ',') -ne 'Document,Traverseflag,Searchflag,AddReadOnlyInfo') { throw 'DEPENDENCY_API_PARAMETER_ORDER_CHANGED' }
$taskReplaceMethods = @($taskAssembly.GetType('SolidWorks.Interop.sldworks.ISldWorks', $true).GetMethods() | Where-Object Name -eq 'ReplaceReferencedDocument')
if ($taskReplaceMethods.Count -ne 1 -or $taskReplaceMethods[0].ToString() -ne 'Boolean ReplaceReferencedDocument(System.String, System.String, System.String)') { throw 'REPLACE_API_SIGNATURE_CHANGED' }
if (($taskReplaceMethods[0].GetParameters().Name -join ',') -ne 'ReferencingDocument,ReferencedDocument,NewReference') { throw 'REPLACE_API_PARAMETER_ORDER_CHANGED' }
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
    schema='obsolete-door-closed-relink-build-v1';builtAtUtc=[DateTime]::UtcNow.ToString('o');buildPassed=($taskExit -eq 0)
    source=$taskSource;sourceSha256=$taskSourceSha;executable=$taskExe;compiler=$taskCompiler;compilerArguments=$taskArgs
    compilerExitCode=$taskExit;compilerOutput=$taskOutput;apiSignatureEvidence=$taskApiReport
    buildScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    nativeCadExecuted=$false;scope='throwaway closed-file clone relink experiment only; no CAD executed during build'
}
if ($taskExit -eq 0) { $taskResult.executableSha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash }
if ($taskExit -ne 0) { $taskReport=Join-Path $taskBin 'build-failed.json' }
$taskResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskReport -Encoding utf8
$taskResult | ConvertTo-Json -Depth 8
if ($taskExit -ne 0) { throw ('COMPILE_FAILED '+$taskExit) }
