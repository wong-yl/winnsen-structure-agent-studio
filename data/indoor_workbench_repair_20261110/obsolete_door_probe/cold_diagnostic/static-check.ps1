$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskBase=Split-Path -Parent $taskDir
$taskBuild=Get-Content -LiteralPath (Join-Path $taskDir 'build.json') -Raw | ConvertFrom-Json
$taskSource=[IO.File]::ReadAllText($taskBuild.source)
$taskOriginal=[IO.File]::ReadAllText((Join-Path $taskBase 'v5\IdentityProbe.cs'))
$taskRows=New-Object 'Collections.Generic.List[object]'
function Check($name,$condition,$detail){$taskRows.Add([ordered]@{name=$name;pass=[bool]$condition;detail=$detail});if(-not $condition){Write-Output ('FAILED: '+$name)}}
Check 'compiled source and executable hashes' (((Get-FileHash -LiteralPath $taskBuild.source -Algorithm SHA256).Hash -eq $taskBuild.sourceSha256) -and ((Get-FileHash -LiteralPath $taskBuild.executable -Algorithm SHA256).Hash -eq $taskBuild.executableSha256)) 'Covers the exact build artifact'
Check 'only two diagnostic modes; one options selector' ($taskSource.Contains('(args[0] != "diagnostic35" && args[0] != "diagnostic33")') -and ([regex]::Matches($taskSource,'Mode\s*==').Count -eq 1) -and $taskSource.Contains('openOptions = Mode == "diagnostic35" ? 35 : 33;')) 'Mode changes only OpenDoc6 options; read-only expectation derives from bit 2'
Check 'no mutation APIs anywhere in source' (-not [regex]::IsMatch($taskSource,'\.(DeleteSelection2|ForceRebuild3|EditRebuild3|Save3|SaveAs|ClearSelection2|Select4|ShowConfiguration2|ReplaceReferencedDocument|ReplaceComponents|SetUserPreference\w*)\s*\(') -and -not $taskSource.Contains('DeleteOldDoors')) 'Entire deletion helper removed; no unreachable mutation branch'
$taskOldStart=$taskOriginal.IndexOf('    private static string BindPath(',[StringComparison]::Ordinal)
$taskOldEnd=$taskOriginal.IndexOf('    private static void DeleteOldDoors(',[StringComparison]::Ordinal)
$taskNewStart=$taskSource.IndexOf('    private static string BindPath(',[StringComparison]::Ordinal)
$taskNewEnd=$taskSource.IndexOf('    private static void RootHealthy(',[StringComparison]::Ordinal)
Check 'v5 path/reference/geometry/snapshot/loaded helpers unchanged' ($taskOriginal.Substring($taskOldStart,$taskOldEnd-$taskOldStart) -ceq $taskSource.Substring($taskNewStart,$taskNewEnd-$taskNewStart)) 'Exact text from BindPath through Loaded; inherited numeric tolerance and 80/41/19 contracts'
Check 'cold snapshot only after-delete contract' ($taskSource.Contains('Snapshot(sw, model, "cold-diagnostic", false)') -and -not $taskSource.Contains('Snapshot(sw, model, "before-delete", true)')) 'No deletion or rebuild before capture'
Check 'saved checkpoint and removal whitelist initialized for both modes' ($taskSource.Contains('Binding.expectedRootSha256 == SavedRootSha256') -and $taskSource.Contains('RemovedExclusivePaths.Add(full);') -and $taskSource.Contains('ExpectedHashes[Target] = Binding.expectedRootSha256;') -and $taskSource.Contains('files.Length == 75') -and $taskSource.Contains('receipt.after.Length == 75')) '75 source receipt rows plus saved-root override and prior edit removed paths'
Check 'identity warnings and root mode still reject' ($taskSource.Contains('(openWarnings & (1 | 32768)) != 0') -and $taskSource.Contains('openErrors == 0 && !identityWarning') -and $taskSource.Contains('model.IsOpenedReadOnly() == ((openOptions & 2) != 0)')) '96 is diagnostic expected value; unknown loaded path remains rejected by unchanged Loaded helper'
Check 'dirty root recorded without requirement' (-not [regex]::IsMatch($taskSource,'Require\([^\r\n]*GetSaveFlag') -and $taskSource.Contains('"after-diagnostic-snapshot"')) 'GetSaveFlag does not stop diagnostic capture'
Check 'comparison and health failures recorded independently' ($taskSource.Contains('"equal", false, "diagnosticOnly", true, "exception", error.ToString()') -and $taskSource.Contains('"healthy", false, "diagnosticOnly", true, "exception", error.ToString()')) 'Snapshot remains in journal if Equal or RootHealthy fails'
Check 'diagnostic completion never model acceptance' ($taskSource.Contains('"operationComplete", false, "diagnosticCompleted", diagnosticCompleted') -and $taskSource.Contains('"modelAccepted", false, "strictColdAcceptancePassed", false') -and $taskSource.Contains('return diagnosticCompleted ? 0 : 1;')) 'Exit 0 reports completed diagnostic collection only'
Check 'owned cold process and natural discard exit retained' ($taskSource.Contains('COLD_EXCLUSIVE_CAD_SESSION_REQUIRED') -and $taskSource.Contains('VerifyOwner(sw, process, retainedHandle') -and $taskSource.Contains('sw.CloseAllDocuments(true)') -and $taskSource.Contains('process.WaitForExit(500)') -and -not [regex]::IsMatch($taskSource,'\.(Kill|Terminate)\s*\(')) 'No force termination or shared CAD session'
Check 'all source and candidate files immutable' ($taskSource.Contains('CompareFiles(packBefore, packAfter, false, "all75-candidate-unchanged")') -and $taskSource.Contains('CompareFiles(sourceBefore, sourceAfter, false, "all75-source-unchanged")')) 'SHA-256, size, timestamps, attributes guarded after natural exit'
Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskBuild.executable)
$taskType=$taskAssembly.GetType('DiagnosticProbe',$true)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Static'
$taskEdit=Join-Path $taskBase 'runs\20261110065558794-edit-33604\probe.json'
$taskRoot=Join-Path $taskBase 'candidate_pack_v5\标准寄存柜1917×1000×550(总装配).SLDASM'
Check 'compiled exact pins and live files' ($taskType.GetField('SuccessfulEditEvidenceSha256',$taskFlags).GetRawConstantValue() -eq (Get-FileHash -LiteralPath $taskEdit -Algorithm SHA256).Hash -and $taskType.GetField('SavedRootSha256',$taskFlags).GetRawConstantValue() -eq (Get-FileHash -LiteralPath $taskRoot -Algorithm SHA256).Hash) 'FD396562... successful edit and 1A14C86D... saved root'
$taskEvent=$taskType.GetMethod('EventFrom',$taskFlags)
$taskPrior=$taskEvent.Invoke($null,[object[]]@($taskEdit.PSObject.BaseObject,'snapshot','after-save'))
$taskState=$taskPrior['state']
$taskType.GetMethod('Equal',$taskFlags).Invoke($null,[object[]]@($taskState.PSObject.BaseObject,$taskState.PSObject.BaseObject,[string]'offline-identical-after-save')) | Out-Null
$taskType.GetMethod('RootHealthy',$taskFlags).Invoke($null,[object[]]@($taskState.PSObject.BaseObject)) | Out-Null
Check 'compiled pure helpers read saved state' $true 'EventFrom, Equal and RootHealthy only; Main and COM methods never invoked'
$taskJson=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$taskLeft=$taskJson.DeserializeObject('{"rootFeatures":[{"name":"fixture","errorCode":0,"errorCode2":0}],"value":[1,2]}')
$taskRight=$taskJson.DeserializeObject('{"rootFeatures":[{"name":"fixture","errorCode":1,"errorCode2":0}],"value":[1,2]}')
$taskEqualRejected=$false;$taskHealthRejected=$false
try{$taskType.GetMethod('Equal',$taskFlags).Invoke($null,[object[]]@($taskLeft.PSObject.BaseObject,$taskRight.PSObject.BaseObject,[string]'offline-deliberate-drift'))|Out-Null}catch{$taskEqualRejected=$_.Exception.ToString().Contains('STATE_DRIFT')}
try{$taskType.GetMethod('RootHealthy',$taskFlags).Invoke($null,[object[]]@($taskRight.PSObject.BaseObject))|Out-Null}catch{$taskHealthRejected=$_.Exception.ToString().Contains('ROOT_FEATURE_ERROR_NONZERO')}
Check 'diagnostic helpers retain precise failures' ($taskEqualRejected -and $taskHealthRejected) 'Deliberate feature error exposes both mismatch and health failure offline'
$taskOpCodes=@{}
foreach($taskField in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')){$taskOp=$taskField.GetValue($null);$taskOpCodes[[int]$taskOp.Value]=$taskOp}
$taskCadCalls=New-Object 'Collections.Generic.HashSet[string]'
foreach($taskAuditType in $taskAssembly.GetTypes()){
 foreach($taskMethod in $taskAuditType.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly')){
  $taskBody=$taskMethod.GetMethodBody();if($null -eq $taskBody){continue};$taskIl=$taskBody.GetILAsByteArray();$taskAt=0
  while($taskAt -lt $taskIl.Length){
   $taskValue=[int]$taskIl[$taskAt];$taskAt++
   if($taskValue -eq 254){$taskValue=([int]$taskIl[$taskAt])-512;$taskAt++}
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
    $taskMember=$taskMethod.Module.ResolveMethod([BitConverter]::ToInt32($taskIl,$taskAt))
    if($taskMember.DeclaringType.FullName.StartsWith('SolidWorks.Interop.')){$taskCadCalls.Add($taskMember.DeclaringType.FullName+'::'+$taskMember.Name)|Out-Null}
   }
   $taskAt+=$taskSize
  }
 }
}
$taskMutationCalls=@($taskCadCalls|Where-Object {$_ -match '::(set_|Save|ForceRebuild|EditRebuild|Delete|Select|ClearSelection|ShowConfiguration|Replace|Resolve|SetUserPreference)'})
Check 'compiled IL contains no CAD mutation call' ($taskMutationCalls.Count -eq 0) ('Inspected every emitted method; CAD member count='+$taskCadCalls.Count)
Check 'frozen v5 source untouched' ((Get-FileHash -LiteralPath (Join-Path $taskBase 'v5\IdentityProbe.cs') -Algorithm SHA256).Hash -eq '852CE11FBC7E75A73E70BB1FDF59B5684C27324D1B9EB73EF3C5D7A079416918') 'Original frozen source preserved'
$taskReport=[ordered]@{
 schema='obsolete-door-cold-diagnostic-static-v1';generatedAtUtc=[DateTime]::UtcNow.ToString('o')
 source=$taskBuild.source;sourceSha256=$taskBuild.sourceSha256;executable=$taskBuild.executable;executableSha256=$taskBuild.executableSha256
 buildManifestSha256=(Get-FileHash -LiteralPath (Join-Path $taskDir 'build.json') -Algorithm SHA256).Hash
 staticScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
 count=$taskRows.Count;passed=@($taskRows|Where-Object pass -eq $true).Count;failed=@($taskRows|Where-Object pass -ne $true).Count;checks=$taskRows
 compiledCadCalls=@($taskCadCalls|Sort-Object);mutationCalls=$taskMutationCalls
 mainInvoked=$false;nativeCadExecuted=$false;modelAccepted=$false;strictColdAcceptancePassed=$false
 runtimeAcceptance='UNVERIFIED: only primary may run the two cold CAD diagnostics'
}
$taskReport|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $taskDir 'static-validation.json') -Encoding utf8
$taskReport|Select-Object count,passed,failed,sourceSha256,executableSha256,mainInvoked,nativeCadExecuted|ConvertTo-Json
if($taskReport.failed -ne 0){throw 'OFFLINE_VALIDATION_FAILED'}