$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskAllowed='D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_owner_rebind_v1'
if([IO.Path]::GetFullPath($taskDir) -cne $taskAllowed){throw 'BUILD_DIRECTORY_NOT_AUTHORIZED'}
$taskCompiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskInterop='D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110111643911-CE439B\tools\SolidWorks.Interop.sldworks.dll'
$taskConstants=Join-Path (Split-Path -Parent $taskInterop) 'SolidWorks.Interop.swconst.dll'
$taskManifest=Join-Path $taskDir 'build.json'
if(Test-Path -LiteralPath $taskManifest){throw 'BUILD_REPORT_EXISTS_DO_NOT_OVERWRITE'}
$taskSources=@('RelinkProbe.cs','RightOwnerRebindProbe.cs','OriginalGeometry.cs')
$taskPins=@($taskSources|ForEach-Object {$p=Join-Path $taskDir $_;[ordered]@{path=$p;sha256=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}})
$taskBin=Join-Path $taskDir ('bin\'+$taskPins[1].sha256.Substring(0,12))
New-Item -ItemType Directory -Path $taskBin -Force|Out-Null
Copy-Item -LiteralPath $taskInterop,$taskConstants -Destination $taskBin
$taskArtifacts=@()
foreach($taskKind in @('relink','owner')){
 $taskName=if($taskKind -eq 'relink'){'RelinkProbe'}else{'RightOwnerRebindProbe'}
 $taskExe=Join-Path $taskBin ($taskName+'.exe')
 $taskFiles=@((Join-Path $taskDir ($taskName+'.cs')))
 if($taskKind -eq 'owner'){$taskFiles+=Join-Path $taskDir 'OriginalGeometry.cs'}
 $taskArgs=@('/nologo','/target:exe','/platform:x64','/optimize+','/warn:4',('/out:'+$taskExe),'/reference:System.Core.dll','/reference:System.Web.Extensions.dll',('/reference:'+$taskInterop),('/reference:'+$taskConstants))+$taskFiles
 $taskOutput=@(& $taskCompiler @taskArgs 2>&1)
 $taskExit=$LASTEXITCODE
 $taskOutput|Set-Content -LiteralPath (Join-Path $taskBin ($taskKind+'-compile.log')) -Encoding utf8
 $taskArtifact=[ordered]@{kind=$taskKind;executable=$taskExe;buildPassed=($taskExit -eq 0);compilerExitCode=$taskExit;compilerOutput=$taskOutput;compilerArguments=$taskArgs}
 if($taskExit -eq 0){$taskArtifact.executableSha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash}
 $taskArtifacts+=$taskArtifact
}
$taskResult=[ordered]@{schema='right-owner-rebind-build-v1';builtAtUtc=[DateTime]::UtcNow.ToString('o');buildPassed=(@($taskArtifacts|Where-Object {-not $_.buildPassed}).Count -eq 0);artifacts=$taskArtifacts;sourcePins=$taskPins;buildScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;nativeCadExecuted=$false;mainInvoked=$false;modelAccepted=$false;engineeringAccepted=$false}
if($taskResult.buildPassed){$taskResult|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $taskManifest -Encoding utf8}else{$taskResult|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $taskBin 'build-failed.json') -Encoding utf8}
$taskResult|ConvertTo-Json -Depth 12
if(-not $taskResult.buildPassed){throw 'COMPILE_FAILED'}
