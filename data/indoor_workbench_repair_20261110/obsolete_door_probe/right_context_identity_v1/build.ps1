$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskAllowed='D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_context_identity_v1'
if([IO.Path]::GetFullPath($taskDir) -cne $taskAllowed){throw 'BUILD_DIRECTORY_NOT_AUTHORIZED'}
$taskCompiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskSource=Join-Path $taskDir 'RightIdentityProbe.cs'
$taskSha=(Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
$taskBin=Join-Path $taskDir ('bin\'+$taskSha.Substring(0,12))
$taskManifest=Join-Path $taskDir 'build.json'
if(Test-Path -LiteralPath $taskManifest){throw 'BUILD_REPORT_EXISTS_DO_NOT_OVERWRITE'}
New-Item -ItemType Directory -Path $taskBin -Force|Out-Null
$taskInterop='D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110111643911-CE439B\tools\SolidWorks.Interop.sldworks.dll'
$taskConstants=Join-Path (Split-Path -Parent $taskInterop) 'SolidWorks.Interop.swconst.dll'
Copy-Item -LiteralPath $taskInterop,$taskConstants -Destination $taskBin
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskInterop)
$taskApis=@{ISldWorks=@('OpenDoc6','GetDocumentDependencies2','GetDocuments','GetDocumentCount','GetOpenDocumentByName','SetCurrentWorkingDirectory','GetProcessID','RevisionNumber','CloseDoc','CloseAllDocuments','ExitApp');IModelDoc2=@('GetPathName','GetTitle','GetSaveFlag','IsOpenedReadOnly','FirstFeature','ListExternalFileReferences2');IFeature=@('get_Name','GetTypeName2','GetNextFeature','GetFirstSubFeature','GetNextSubFeature','ListExternalFileReferences2')}
$taskSignatures=@(foreach($taskName in $taskApis.Keys|Sort-Object){foreach($taskMethod in $taskAssembly.GetType('SolidWorks.Interop.sldworks.'+$taskName,$true).GetMethods()|Where-Object {$_.Name -in $taskApis[$taskName]}){[ordered]@{type=$taskName;member=$taskMethod.Name;signature=$taskMethod.ToString();parameters=@($taskMethod.GetParameters()|ForEach-Object {[ordered]@{name=$_.Name;type=$_.ParameterType.ToString();isOut=$_.IsOut}})}}})
$taskApiPath=Join-Path $taskBin 'api-signatures.json'
$taskSignatures|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $taskApiPath -Encoding utf8
$taskExe=Join-Path $taskBin 'RightIdentityProbe.exe'
$taskArgs=@('/nologo','/target:exe','/platform:x64','/optimize+','/warn:4',('/out:'+$taskExe),'/reference:System.Core.dll','/reference:System.Web.Extensions.dll',('/reference:'+$taskInterop),('/reference:'+$taskConstants),$taskSource)
$taskOutput=@(& $taskCompiler @taskArgs 2>&1)
$taskExit=$LASTEXITCODE
$taskOutput|Set-Content -LiteralPath (Join-Path $taskBin 'compile.log') -Encoding utf8
$taskResult=[ordered]@{schema='right-context-identity-build-v1';builtAtUtc=[DateTime]::UtcNow.ToString('o');buildPassed=($taskExit -eq 0);source=$taskSource;sourceSha256=$taskSha;executable=$taskExe;compiler=$taskCompiler;compilerArguments=$taskArgs;compilerExitCode=$taskExit;compilerOutput=$taskOutput;apiSignatureEvidence=$taskApiPath;buildScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;nativeCadExecuted=$false;mainInvoked=$false;modelAccepted=$false}
if($taskExit -eq 0){$taskResult.executableSha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash}else{$taskManifest=Join-Path $taskBin 'build-failed.json'}
$taskResult|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $taskManifest -Encoding utf8
[PSCustomObject]$taskResult|Select-Object buildPassed,sourceSha256,executable,executableSha256,compilerExitCode,compilerOutput,nativeCadExecuted|ConvertTo-Json -Depth 5
if($taskExit -ne 0){throw 'COMPILE_FAILED'}
