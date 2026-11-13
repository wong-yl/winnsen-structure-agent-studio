# Only the user's standing authorization for this trial's proven residual monitor applies.
param([Parameter(Mandatory=$true)][string]$RunDirectory,[Parameter(Mandatory=$true)][int]$MonitorPid)
$ErrorActionPreference='Stop'
$trialRoot='D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112'
$runPath=[IO.Path]::GetFullPath($RunDirectory)
if(-not $runPath.StartsWith($trialRoot+'\runs\',[StringComparison]::OrdinalIgnoreCase)){throw 'TRIAL_RUN_REQUIRED'}
$nativePath=Join-Path $runPath 'native-result.json'
$native=Get-Content -LiteralPath $nativePath -Raw|ConvertFrom-Json -DateKind String
$claim=@($native.events|Where-Object inspection -eq 'cad_owned')
if($claim.Count -ne 1 -or $native.events[-1].inspection -ne 'finished' -or $native.events[-1].cadExited -ne $true){throw 'NATIVE_CAD_EXIT_REQUIRED'}
$supervision=Get-Content -LiteralPath (Join-Path $runPath 'supervision.json') -Raw|ConvertFrom-Json -DateKind String
$expected=@($supervision.cad|Where-Object {$_.pid -eq $MonitorPid -and $_.name -eq 'sldProcMon.exe'})
if($expected.Count -ne 1 -or $supervision.nativeEnded -ne $true){throw 'SUPERVISED_MONITOR_REQUIRED'}
if(@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count -ne 0){throw 'MAIN_CAD_MUST_BE_ABSENT'}
$monitor=Get-Process -Id $MonitorPid -ErrorAction Stop
$heldHandle=$monitor.Handle
$observed=Get-CimInstance Win32_Process -Filter "ProcessId=$MonitorPid"
$expectedTicks=([DateTime]::Parse($expected[0].startedAtUtc).ToUniversalTime()).Ticks
$actualTicks=$monitor.StartTime.ToUniversalTime().Ticks
if(($actualTicks-($actualTicks%10)) -ne $expectedTicks -or $observed.CreationDate.ToUniversalTime().Ticks -ne $expectedTicks){throw 'MONITOR_START_IDENTITY_MISMATCH'}
if($actualTicks -lt [long]$claim[0].startTicks){throw 'MONITOR_PREDATES_THIS_TRIAL_CAD'}
if($observed.Name -ne 'sldProcMon.exe' -or $observed.ParentProcessId -ne $claim[0].pid -or $expected[0].parentPid -ne $claim[0].pid){throw 'MONITOR_PARENT_IDENTITY_MISMATCH'}
if($observed.ExecutablePath -ine $expected[0].executable -or $observed.CommandLine -notmatch ('(?:^|\s)--ppid='+$claim[0].pid+'(?:\s|$)')){throw 'MONITOR_PATH_OR_COMMAND_MISMATCH'}
$approvedPaths=@('D:\SOILDW~1\SOLIDW~1\sldProcMon.exe','D:\soildworks2020\SOLIDWORKS\sldProcMon.exe')
if($approvedPaths -inotcontains $observed.ExecutablePath){throw 'MONITOR_EXE_PATH_NOT_APPROVED'}
$exeHash=(Get-FileHash -LiteralPath $observed.ExecutablePath -Algorithm SHA256).Hash
if($exeHash -ne 'A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70'){throw 'MONITOR_EXE_HASH_MISMATCH'}
$evidenceDirectory=Join-Path $trialRoot ('process-'+$MonitorPid+'-authorized-close-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $evidenceDirectory|Out-Null
$before=[ordered]@{authorization='User authorized this and subsequent strictly identified residual monitors from this trial after main CAD exits';atUtc=[DateTime]::UtcNow.ToString('o');run=$runPath;pid=$MonitorPid;parentPid=$observed.ParentProcessId;startedAtUtc=$monitor.StartTime.ToUniversalTime().ToString('o');actualProcessStartTicks=$actualTicks;executable=$observed.ExecutablePath;executableSha256=$exeHash;commandLine=$observed.CommandLine;mainCadAlreadyExited=$true;nativeResultSha256=(Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash;scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;production_release=$false}
$before|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $evidenceDirectory 'before.json') -Encoding utf8
$confirmedAgain=Get-CimInstance Win32_Process -Filter "ProcessId=$MonitorPid"
if($confirmedAgain.ExecutablePath -ine $observed.ExecutablePath -or $confirmedAgain.CommandLine -cne $observed.CommandLine -or $confirmedAgain.ParentProcessId -ne $observed.ParentProcessId -or $confirmedAgain.CreationDate.ToUniversalTime().Ticks -ne $expectedTicks){throw 'MONITOR_IDENTITY_CHANGED_BEFORE_CLOSE'}
$monitor.Refresh()
if($monitor.HasExited -or $monitor.StartTime.ToUniversalTime().Ticks -ne $actualTicks){throw 'MONITOR_CHANGED_BEFORE_CLOSE'}
if(@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count -ne 0){throw 'CAD_APPEARED_BEFORE_CLOSE'}
$monitor.Kill()
if(-not $monitor.WaitForExit(10000)){throw 'MONITOR_EXIT_TIMEOUT'}
$remaining=@(Get-CimInstance Win32_Process -Filter "Name='SLDWORKS.exe' OR Name='sldProcMon.exe'"|Select-Object Name,ProcessId,ParentProcessId,CreationDate,ExecutablePath,CommandLine)
[ordered]@{atUtc=[DateTime]::UtcNow.ToString('o');targetAbsent=$monitor.HasExited;remainingCad=$remaining;production_release=$false}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $evidenceDirectory 'after.json') -Encoding utf8
Write-Output $evidenceDirectory
