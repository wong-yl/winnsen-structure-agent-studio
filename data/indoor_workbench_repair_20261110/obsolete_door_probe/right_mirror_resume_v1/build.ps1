$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
if([IO.Path]::GetFullPath($taskDir) -cne 'D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\right_mirror_resume_v1'){throw 'BUILD_DIRECTORY_NOT_AUTHORIZED'}
$taskSource=Join-Path $taskDir 'RightMirrorFreshProbe.cs'
$taskGeometry=Join-Path (Split-Path -Parent $taskDir) 'right_mirror_fresh_v1\OriginalGeometry.cs'
$taskDiff=Get-Content -LiteralPath (Join-Path $taskDir 'source-diff.json') -Raw -Encoding UTF8|ConvertFrom-Json
$taskSha=(Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash
if($taskDiff.changedLineCount -ne 2 -or -not $taskDiff.onlyDurationAndRequestLogChanged -or $taskSha -ne $taskDiff.resumed.sha256 -or (Get-FileHash -LiteralPath $taskDiff.original.path -Algorithm SHA256).Hash -ne $taskDiff.original.sha256){throw 'EXACT_DURATION_DIFF_REQUIRED'}
$taskManifest=Join-Path $taskDir 'build.json'
if(Test-Path -LiteralPath $taskManifest){throw 'BUILD_REPORT_EXISTS_DO_NOT_OVERWRITE'}
$taskBin=Join-Path $taskDir ('bin\'+$taskSha.Substring(0,12))
New-Item -ItemType Directory -Path $taskBin|Out-Null
$taskInterop='D:\Winnsen_Structure_Agent_Studio\data\parametric_attempts\PARAM-20261110111643911-CE439B\tools\SolidWorks.Interop.sldworks.dll'
$taskConstants=Join-Path (Split-Path -Parent $taskInterop) 'SolidWorks.Interop.swconst.dll'
Copy-Item -LiteralPath $taskInterop,$taskConstants -Destination $taskBin
$taskCompiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskExe=Join-Path $taskBin 'RightMirrorFreshProbe.exe'
$taskArgs=@('/nologo','/target:exe','/platform:x64','/optimize+','/warn:4',('/out:'+$taskExe),'/reference:System.Core.dll','/reference:System.Web.Extensions.dll',('/reference:'+$taskInterop),('/reference:'+$taskConstants),$taskSource,$taskGeometry)
$taskOutput=@(& $taskCompiler @taskArgs 2>&1)
$taskExit=$LASTEXITCODE
$taskOutput|Set-Content -LiteralPath (Join-Path $taskBin 'compile.log') -Encoding utf8
$taskResult=[ordered]@{schema='right-mirror-resume-build-v1';builtAtUtc=[DateTime]::UtcNow.ToString('o');buildPassed=($taskExit -eq 0);source=$taskSource;sourceSha256=$taskSha;originalGeometry=[ordered]@{path=$taskGeometry;sha256=(Get-FileHash -LiteralPath $taskGeometry -Algorithm SHA256).Hash};executable=$taskExe;compiler=$taskCompiler;compilerArguments=$taskArgs;compilerExitCode=$taskExit;compilerOutput=$taskOutput;sourceDiff=(Join-Path $taskDir 'source-diff.json');maximumDiagnosticSeconds=600;requestLogMaximumDiagnosticSeconds=600;naturalExitSeconds=45;nativeCadExecuted=$false;mainInvoked=$false;modelAccepted=$false}
if($taskExit -eq 0){$taskResult.executableSha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash}
$taskResult|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $taskManifest -Encoding utf8
[PSCustomObject]$taskResult|Select-Object buildPassed,sourceSha256,executable,executableSha256,compilerExitCode,nativeCadExecuted|ConvertTo-Json -Depth 3
if($taskExit -ne 0){throw 'COMPILE_FAILED'}
