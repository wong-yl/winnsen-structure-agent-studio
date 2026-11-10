$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskBuild=Get-Content -LiteralPath (Join-Path $taskDir 'build.json') -Raw|ConvertFrom-Json
$taskSource=[IO.File]::ReadAllText($taskBuild.source)
$taskRunner=[IO.File]::ReadAllText((Join-Path $taskDir 'run-right-identity.mjs'))
$taskObserver=[IO.File]::ReadAllText((Join-Path $taskDir 'observe-right-identity.py'))
$taskRows=New-Object 'Collections.Generic.List[object]'
function Check($name,$condition,$detail){$taskRows.Add([ordered]@{name=$name;pass=[bool]$condition;detail=$detail});if(-not $condition){Write-Output ('FAILED: '+$name)}}
Check 'source and emitted binary hashes match build' (((Get-FileHash -LiteralPath $taskBuild.source -Algorithm SHA256).Hash -eq $taskBuild.sourceSha256) -and ((Get-FileHash -LiteralPath $taskBuild.executable -Algorithm SHA256).Hash -eq $taskBuild.executableSha256)) 'No Main invocation'
Check 'all opens are fixed part type1 and options3' ($taskSource.Contains('sw.OpenDoc6(path, 1, 3, "", ref errors, ref warnings)') -and ([regex]::Matches($taskSource,'\.OpenDoc6\(').Count -eq 1)) 'Readonly silent OpenDoc6 only'
Check 'no model mutation or CAD termination call in source' (-not [regex]::IsMatch($taskSource,'\.(Save\w*|ForceRebuild\w*|EditRebuild\w*|Delete\w*|ReplaceReferenced\w*|ReplaceComponents\w*|Select[234]\w*|ClearSelection\w*|ShowConfiguration\w*|Kill|Terminate)\s*\(') -and -not [regex]::IsMatch($taskSource,'File\.(Copy|Replace|Move)\(')) 'No rebuild/save/replace/delete/selection or process termination'
Check 'raw warning33 retained without bypassing production Open' ($taskSource.Contains('"warnings", warnings') -and $taskSource.Contains('"idMismatch", (warnings & 1) != 0') -and $taskSource.Contains('"needsRegen", (warnings & 32) != 0') -and $taskSource.Contains('"warning33IsDiagnosticOnly", true')) 'Standalone diagnostic never changes original Open helper'
Check 'left closes by CloseDoc before right open' ($taskSource.IndexOf('sw.CloseDoc(title)') -lt $taskSource.IndexOf('Open(sw, Input.right.path, "right")') -and $taskSource.Contains('Documents(sw, "after-left-close-before-right")')) 'CloseAll only at final shutdown'
Check 'IUnknown held until traversal scope dispose' ($taskSource.Contains('held.Add(pointer); return true;') -and $taskSource.Contains('foreach (IntPtr pointer in held) Marshal.Release(pointer)') -and $taskSource.Contains('if (!keys.Add(pointer.ToInt64())) { Marshal.Release(pointer); return false; }')) 'No released-address-only deduplication'
Check 'owner journal precedes every OpenDoc6' ($taskSource.IndexOf('Record(D("inspection", "session_owner"') -lt $taskSource.LastIndexOf('ModelDoc2 left = Open(') -and $taskSource.Contains('handle = process.Handle') -and $taskSource.Contains('processHandleRetained')) 'Retained OS process handle and exact PID/start/path checks'
Check 'natural exit has explicit45s ceiling and no force' ($taskSource.Contains('timer.ElapsedMilliseconds < 45000') -and $taskSource.Contains('"forcedTermination", false') -and $taskSource.Contains('Deadline = DateTime.UtcNow.AddSeconds(180)')) 'Only ExitApp and wait for owned CAD'
Check 'loaded identities include path title readonly saveflag before and after' ($taskSource.Contains('"actualPath", path') -and $taskSource.Contains('"saveFlag", Read(delegate { return model.GetSaveFlag(); })') -and $taskSource.Contains('stage + ":before-open"') -and $taskSource.Contains('stage + ":after-open"')) 'Full loaded-document boundary observations'
Check 'file feature refs and raw dependency calls remain read-only' ($taskSource.Contains('model.ListExternalFileReferences2') -and $taskSource.Contains('feature.ListExternalFileReferences2') -and $taskSource.Contains('sw.GetDocumentDependencies2(path, traverse, false, false)') -and $taskSource.Contains('"unknownPathsAccepted", false')) 'No reference mutation and unknown paths are never accepted'
Check 'runner uses shared lock and read-only portal gate' ($taskRunner.Contains('acquireNativeWorkerInstanceLock({ dataDir: resolve(ROOT, ''data''), workerId })') -and $taskRunner.Contains('store.listSnapshot()') -and -not [regex]::IsMatch($taskRunner,'store\.(create|claim|transition|heartbeat|update)')) 'No independent private lock or portal writes'
Check 'fresh baseline replaces historical hash assumptions' ($taskRunner.Contains('old.rows.map(row => regular(row.path))') -and $taskRunner.Contains('currentProtectionHashesCapturedNow: true') -and $taskRunner.Contains('walk(PROBE, true)') -and $taskRunner.Contains('regular(resolve(PROBE, ''../PROGRESS.md''))')) 'Includes explicit current PROGRESS bytes and all sibling probe files'
Check 'timeout stops next arm and retains shared lock' ($taskObserver.Contains("'nextArmAllowed': False") -and $taskObserver.Contains("'cadTerminated': False") -and $taskRunner.Contains('while (observerRunning || childLive() || state?.cadAndMonitorEmpty !== true)') -and $taskRunner.Contains('await pulse()')) 'Timeout report then manual disposition with ongoing heartbeats'
Check 'missing child identity or session evidence retains shared lock' ($taskRunner.Contains('observerRunning = true; nativeChildUnconfirmed = true') -and $taskRunner.Contains('if (!Number.isInteger(pid) || pid <= 0) return true') -and $taskRunner.Contains('if (!exactIdentity) return current != null') -and $taskRunner.Contains('native child identity unresolved; global lock retained.') -and $taskObserver.Contains("put(out / 'native-child-started.json'") -and -not $taskRunner.Contains('process.kill(') -and $taskRunner.Contains('unchanged(validation.artifactVersions)')) 'Unknown identity is only observed; unreadable process state also retains lock'
Check 'observer only cleans externally witnessed exact orphan monitor' ($taskObserver.Contains("monitor['ppid'] != owner['cadPid']") -and $taskObserver.Contains("'startTimeUtcTicks'") -and $taskObserver.Contains('MONITOR_IDENTITY_OR_ORPHAN_GATE_CHANGED') -and $taskObserver.Contains("natural and not named(final, 'SLDWORKS.exe')")) 'No CAD kill and no unconfirmed process cleanup'
Check 'separate arm directories and sequential observer waits' ($taskRunner.Contains('for (const arm of ARMS)') -and $taskRunner.Contains('const armOut = resolve(out, arm)') -and $taskRunner.Contains('session.nextArmAllowed === true')) 'No overlapping CAD sessions'

Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskBuild.executable)
$taskType=$taskAssembly.GetType('RightIdentityProbe',$true)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Static'
$taskBindingPath=Join-Path $taskDir 'source-binding.json'
$taskBindingSha=(Get-FileHash -LiteralPath $taskBindingPath -Algorithm SHA256).Hash
$taskType.GetMethod('LoadBindings',$taskFlags).Invoke($null,[object[]]@([string]$taskBindingPath,[string]$taskBindingSha))|Out-Null
Check 'compiled binding validates actual119 pinned input files' ($taskType.GetField('Known',$taskFlags).GetValue($null).Count -eq 119) 'Actual fixed CE439B evidence and L/R/parameter hashes validated without CAD'
$taskArmMethod=$taskType.GetMethod('ArmSequence',$taskFlags)
Check 'compiled arm behavior matches three intended sequences' (($taskArmMethod.Invoke($null,[object[]]@('right-only')) -join '|') -eq 'right' -and ($taskArmMethod.Invoke($null,[object[]]@('parameter-source-only')) -join '|') -eq 'parameterSource' -and ($taskArmMethod.Invoke($null,[object[]]@('left-close-right')) -join '|') -eq 'left|right') 'Pure compiled method only'
$taskParser=$taskType.GetMethod('ParseDependencies',$taskFlags)
$taskArg=New-Object object[] 1
$taskArg[0]=$null
$taskParsed=$taskParser.Invoke($null,$taskArg)
Check 'null dependency return preserved' ($taskParsed['rawNull'] -and $taskParsed['namePathPairParseSucceeded'] -and $taskParsed['pairs'].Count -eq 0) 'Null not coerced to a path'
$taskArg[0]=[object[]]@('old','Z:\unverified\part.SLDPRT')
$taskParsed=$taskParser.Invoke($null,$taskArg)
Check 'unknown returned path stays raw and is never whitelisted' ($taskParsed['pairs'][0]['path'] -ceq 'Z:\unverified\part.SLDPRT' -and -not $taskParsed['pairs'][0]['insideFixedInventory'] -and -not $taskParsed['returnedPathsLoaded'] -and -not $taskParsed['returnedPathsWhitelisted']) 'No path lookup or model loading from API returns'
$taskArg[0]=[object[]]@('odd','path','unpaired')
Check 'malformed dependency return remains diagnosable' (-not $taskParser.Invoke($null,$taskArg)['namePathPairParseSucceeded']) 'No invented missing pair'
$taskArg[0]='scalar'
Check 'scalar dependency return remains diagnosable' (-not $taskParser.Invoke($null,$taskArg)['namePathPairParseSucceeded']) 'Raw scalar preserved'

$taskOpCodes=@{}
foreach($taskField in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')){$taskOp=$taskField.GetValue($null);$taskOpCodes[[int]$taskOp.Value]=$taskOp}
$taskCadCalls=New-Object 'Collections.Generic.HashSet[string]'
$taskFileMutations=New-Object 'Collections.Generic.List[string]'
$taskProcessMutations=New-Object 'Collections.Generic.List[string]'
foreach($taskAuditType in $taskAssembly.GetTypes()){
 foreach($taskMethod in @($taskAuditType.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'))+@($taskAuditType.GetConstructors([Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'))){
  $taskBody=$taskMethod.GetMethodBody();if($null -eq $taskBody){continue};$taskIl=$taskBody.GetILAsByteArray();$taskAt=0
  while($taskAt -lt $taskIl.Length){
   $taskValue=[int]$taskIl[$taskAt];$taskAt++;if($taskValue -eq 254){$taskValue=([int]$taskIl[$taskAt])-512;$taskAt++}
   $taskOp=$taskOpCodes[$taskValue];if($null -eq $taskOp){throw ('UNKNOWN_IL_OPCODE '+$taskValue)}
   $taskSize=0
   switch($taskOp.OperandType.ToString()){
    'InlineNone' {$taskSize=0}
    {$_ -in 'ShortInlineBrTarget','ShortInlineI','ShortInlineVar'} {$taskSize=1}
    'InlineVar' {$taskSize=2}
    {$_ -in 'InlineBrTarget','InlineField','InlineI','InlineMethod','InlineSig','InlineString','InlineTok','InlineType','ShortInlineR'} {$taskSize=4}
    {$_ -in 'InlineI8','InlineR'} {$taskSize=8}
    'InlineSwitch' {$taskSize=4+4*[BitConverter]::ToInt32($taskIl,$taskAt)}
    default {throw ('UNKNOWN_IL_OPERAND '+$taskOp.OperandType)}
   }
   if($taskOp.OperandType.ToString() -eq 'InlineMethod'){
    $taskMember=$taskMethod.Module.ResolveMethod([BitConverter]::ToInt32($taskIl,$taskAt));$taskFullName=$taskMember.DeclaringType.FullName+'::'+$taskMember.Name
    if($taskMember.DeclaringType.FullName.StartsWith('SolidWorks.Interop.')){$taskCadCalls.Add($taskFullName)|Out-Null}
    if($taskFullName -match '^System.IO.(File|Directory)::(Delete|Copy|Move|Replace|Write|CreateDirectory)'){$taskFileMutations.Add($taskFullName)}
    if($taskFullName -match '^System.Diagnostics.Process::(Kill|CloseMainWindow)'){$taskProcessMutations.Add($taskFullName)}
   }
   $taskAt+=$taskSize
  }
 }
}
$taskAllowed=@('ISldWorks::GetDocuments','ISldWorks::GetDocumentCount','ISldWorks::GetOpenDocumentByName','ISldWorks::OpenDoc6','ISldWorks::GetDocumentDependencies2','ISldWorks::GetProcessID','ISldWorks::RevisionNumber','ISldWorks::SetCurrentWorkingDirectory','ISldWorks::CloseDoc','ISldWorks::CloseAllDocuments','ISldWorks::ExitApp','IModelDoc2::GetPathName','IModelDoc2::GetTitle','IModelDoc2::IsOpenedReadOnly','IModelDoc2::GetSaveFlag','IModelDoc2::ListExternalFileReferences2','IModelDoc2::FirstFeature','IFeature::get_Name','IFeature::GetTypeName2','IFeature::GetNextSubFeature','IFeature::GetNextFeature','IFeature::GetFirstSubFeature','IFeature::ListExternalFileReferences2')|ForEach-Object {'SolidWorks.Interop.sldworks.'+$_}
$taskUnexpected=@($taskCadCalls|Where-Object {$_ -notin $taskAllowed})
Check 'compiled IL permits only listed read and session lifecycle CAD APIs' ($taskUnexpected.Count -eq 0 -and $taskCadCalls.Contains('SolidWorks.Interop.sldworks.ISldWorks::OpenDoc6') -and $taskCadCalls.Contains('SolidWorks.Interop.sldworks.IFeature::ListExternalFileReferences2')) 'All methods and constructors audited; no mutation APIs emitted'
Check 'compiled IL has no file copy replace delete or CAD kill' ($taskFileMutations.Count -eq 0 -and $taskProcessMutations.Count -eq 0) 'Only owned evidence FileStream writes; paths guarded in Main'
$taskNodeOutput=& node (Join-Path $taskDir 'tests-offline.mjs')
if($LASTEXITCODE -ne 0){throw 'NODE_BEHAVIOR_CHECK_FAILED'}
$taskNode=$taskNodeOutput|ConvertFrom-Json
Check 'runner portal arms and fail-safe behaviors pass' ($taskNode.failed -eq 0 -and $taskNode.count -eq 5) 'Pure Node imports and injected process observations do not execute Main or spawn processes'
$taskReport=[ordered]@{schema='right-identity-static-v1';generatedAtUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$taskBuild.sourceSha256;executableSha256=$taskBuild.executableSha256;count=$taskRows.Count;passed=@($taskRows|Where-Object pass).Count;failed=@($taskRows|Where-Object {-not $_.pass}).Count;checks=$taskRows;compiledCadCalls=@($taskCadCalls|Sort-Object);unexpectedCadCalls=$taskUnexpected;fileMutationCalls=$taskFileMutations;processMutationCalls=$taskProcessMutations;nodeBehavior=$taskNode;artifactVersions=@('RightIdentityProbe.cs','build.ps1','build.json','run-right-identity.mjs','observe-right-identity.py','source-binding.json','static-check.ps1','tests-offline.mjs'|ForEach-Object {$taskPath=Join-Path $taskDir $_;[ordered]@{path=$taskPath;sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash}});mainInvoked=$false;nativeCadExecuted=$false;modelAccepted=$false;runtimeAcceptance='UNVERIFIED: no CAD execution in worker scope'}
$taskReport|ConvertTo-Json -Depth 15|Set-Content -LiteralPath (Join-Path $taskDir 'static-validation.json') -Encoding utf8
[PSCustomObject]$taskReport|Select-Object count,passed,failed,sourceSha256,executableSha256,nativeCadExecuted|ConvertTo-Json
if($taskReport.failed -ne 0){throw 'OFFLINE_STATIC_VALIDATION_FAILED'}
