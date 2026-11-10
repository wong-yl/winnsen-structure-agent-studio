$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskBase=Split-Path -Parent $taskDir
$taskBuild=Get-Content -LiteralPath (Join-Path $taskDir 'build.json') -Raw|ConvertFrom-Json
$taskSource=[IO.File]::ReadAllText($taskBuild.source)
$taskCold=[IO.File]::ReadAllText((Join-Path $taskBase 'cold_diagnostic\DiagnosticProbe.cs'))
$taskRows=New-Object 'Collections.Generic.List[object]'
function Check($name,$condition,$detail){$taskRows.Add([ordered]@{name=$name;pass=[bool]$condition;detail=$detail});if(-not $condition){Write-Output ('FAILED: '+$name)}}
Check 'exact compiled artifact hashes' (((Get-FileHash -LiteralPath $taskBuild.source -Algorithm SHA256).Hash -eq $taskBuild.sourceSha256) -and ((Get-FileHash -LiteralPath $taskBuild.executable -Algorithm SHA256).Hash -eq $taskBuild.executableSha256)) 'Covers source and emitted binary'
Check 'no CAD document open or mutation call in source' (-not [regex]::IsMatch($taskSource,'\.(OpenDoc\w*|ActivateDoc\w*|DeleteSelection2|ForceRebuild3|EditRebuild3|Save\w*|ClearSelection2|Select4|ShowConfiguration2|ReplaceReferencedDocument|ReplaceComponents|Resolve\w*|SetUserPreference\w*)\s*\(') -and -not [regex]::IsMatch($taskSource,'\bSnapshot\s*\(')) 'No Open, Snapshot, mutation or reference replacement code'
Check 'single compatibility CLI mode' ($taskSource.Contains('args.Length != 4 || args[0] != "diagnostic35"') -and -not $taskSource.Contains('diagnostic33')) 'diagnostic35 means closed-file collection, not OpenDoc6 option 35'
Check 'four targets and two exact traverse calls' ($taskSource.Contains('foreach (string fileName in DependencyTargets)') -and $taskSource.Contains('foreach (bool traverse in new[] { false, true })') -and $taskSource.Contains('sw.GetDocumentDependencies2(path, traverse, false, false)') -and $taskSource.Contains('Require(completedCalls == 8')) 'Search and AddReadOnlyInfo false; 4 x 2 calls'
$taskOldStart=$taskCold.IndexOf('    private static void ValidateManifest()',[StringComparison]::Ordinal)
$taskOldEnd=$taskCold.IndexOf('    private static void LoadReferenceSourceWhitelist()',[StringComparison]::Ordinal)
$taskOldManifest=$taskCold.Substring($taskOldStart,$taskOldEnd-$taskOldStart).Replace('        LoadReferenceSourceWhitelist();'+"`r`n",'').Replace('        LoadReferenceSourceWhitelist();'+"`n",'')
$taskNewStart=$taskSource.IndexOf('    private static void ValidateManifest()',[StringComparison]::Ordinal)
$taskNewEnd=$taskSource.IndexOf('    private static string BindPath(',[StringComparison]::Ordinal)
Check 'frozen 75-file and successful-edit manifest protection retained' ($taskOldManifest -ceq $taskSource.Substring($taskNewStart,$taskNewEnd-$taskNewStart)) 'Original validation; unused reference-source alias loading removed'
$taskParseStart=$taskSource.IndexOf('    private static Dictionary<string, object> ParseDependencies(',[StringComparison]::Ordinal)
$taskParseEnd=$taskSource.IndexOf('    private static void EmptyDocuments(',[StringComparison]::Ordinal)
$taskParseSource=$taskSource.Substring($taskParseStart,$taskParseEnd-$taskParseStart)
Check 'returned paths never bound, loaded or whitelisted' (-not [regex]::IsMatch($taskParseSource,'BindPath|Activator|sw\.|ExpectedHashes|RemovedExclusivePaths|Binding\.')) 'Raw strings remain raw; name/path pairing only'
Check 'document count and array remain empty around each call' ($taskSource.Contains('Require(count == 0 && documents.Length == 0') -and $taskSource.Contains('EmptyDocuments(sw, "before:" + fileName') -and $taskSource.Contains('EmptyDocuments(sw, "after:" + fileName') -and $taskSource.Contains('EmptyDocuments(sw, "final-empty-session")')) 'Every call bracketed by empty-document checks'
Check 'candidate request hash is checked' ($taskSource.Contains('string path = BindPath(Path.Combine(Pack, fileName))') -and $taskSource.Contains('Require(Same(BindPath(path), path)')) 'Only exact candidate manifest paths passed to dependency API'
Check 'both packs immutable and natural owned exit' ($taskSource.Contains('CompareFiles(sourceBefore, sourceAfter, false, "all75-source-unchanged")') -and $taskSource.Contains('CompareFiles(packBefore, packAfter, false, "all75-candidate-unchanged")') -and $taskSource.Contains('VerifyOwner(sw, process, retainedHandle') -and $taskSource.Contains('process.WaitForExit(500)') -and -not [regex]::IsMatch($taskSource,'\.(Kill|Terminate)\s*\(')) 'SHA/size/mtime/attributes checked after owned natural exit'
Check 'diagnostic completion distinct from acceptance' ($taskSource.Contains('"operationComplete", false, "diagnosticCompleted", diagnosticCompleted') -and $taskSource.Contains('"modelAccepted", false, "strictColdAcceptancePassed", false') -and $taskSource.Contains('operationChecked && completedCalls == 8')) 'Only collection and cleanup can complete'
Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskBuild.executable)
$taskType=$taskAssembly.GetType('DependencyProbe',$true)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Static'
$taskTargets=$taskType.GetField('DependencyTargets',$taskFlags).GetValue($null)
Check 'compiled exact four target names' (($taskTargets -join '|') -ceq '上盖壳体底板.sldprt|上盖焊接.SLDASM|箱体竖隔板L焊接.SLDASM|标准寄存柜1917×1000×550(总装配).SLDASM') 'Names checked in compiled metadata'
$taskEdit=Join-Path $taskBase 'runs\20261110065558794-edit-33604\probe.json'
$taskRoot=Join-Path $taskBase 'candidate_pack_v5\标准寄存柜1917×1000×550(总装配).SLDASM'
Check 'compiled pins match live successful edit and root' ($taskType.GetField('SuccessfulEditEvidenceSha256',$taskFlags).GetRawConstantValue() -eq (Get-FileHash -LiteralPath $taskEdit -Algorithm SHA256).Hash -and $taskType.GetField('SavedRootSha256',$taskFlags).GetRawConstantValue() -eq (Get-FileHash -LiteralPath $taskRoot -Algorithm SHA256).Hash) 'FD396562... and 1A14C86D...'
$taskApi=Get-Content -LiteralPath $taskBuild.apiSignatureEvidence -Raw|ConvertFrom-Json
$taskDependencyApi=@($taskApi.signatures|Where-Object member -eq 'GetDocumentDependencies2')
Check 'local official dependency API signature verified' ($taskDependencyApi.Count -eq 1 -and $taskDependencyApi[0].signature -eq 'System.Object GetDocumentDependencies2(System.String, Boolean, Boolean, Boolean)' -and ($taskDependencyApi[0].parameters.name -join ',') -eq 'Document,Traverseflag,Searchflag,AddReadOnlyInfo') 'Reflection evidence from installed official interop assembly'
$taskParser=$taskType.GetMethod('ParseDependencies',$taskFlags)
$taskArg=New-Object object[] 1
$taskArg[0]=$null
$taskParsed=$taskParser.Invoke($null,$taskArg)
Check 'null return preserved' ($taskParsed['rawNull'] -eq $true -and $null -eq $taskParsed['raw'] -and $taskParsed['namePathPairParseSucceeded'] -eq $true -and $taskParsed['pairs'].Count -eq 0) 'Null is recorded distinctly; no artificial path'
$taskPairs=[object[]]@('first','Z:\unloaded\first.SLDASM','second','X:\historical\second.SLDPRT')
$taskArg[0]=$taskPairs
$taskParsed=$taskParser.Invoke($null,$taskArg)
Check 'name/path pairs and raw strings preserved' ($taskParsed['namePathPairParseSucceeded'] -eq $true -and $taskParsed['rawArrayLength'] -eq 4 -and $taskParsed['pairs'].Count -eq 2 -and $taskParsed['pairs'][0]['name'] -eq 'first' -and $taskParsed['pairs'][0]['path'] -eq 'Z:\unloaded\first.SLDASM' -and $taskParsed['pairs'][1]['path'] -eq 'X:\historical\second.SLDPRT' -and [object]::ReferenceEquals($taskPairs,$taskParsed['raw'])) 'Unloaded and historical sample paths are not normalized or whitelisted'
$taskArg[0]=[object[]]@('odd','path','unpaired')
$taskOdd=$taskParser.Invoke($null,$taskArg)
$taskArg[0]='scalar-return'
$taskScalar=$taskParser.Invoke($null,$taskArg)
Check 'malformed returns retained with parse failure' ($taskOdd['namePathPairParseSucceeded'] -eq $false -and $taskOdd['rawArrayLength'] -eq 3 -and $taskScalar['namePathPairParseSucceeded'] -eq $false -and $taskScalar['raw'] -eq 'scalar-return') 'Collection records raw before parsing failure stops completion'
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
$taskForbidden=@($taskCadCalls|Where-Object {$_ -match '::(set_|Open|Activate|Save|ForceRebuild|EditRebuild|Delete|Select|ClearSelection|ShowConfiguration|Replace|Resolve|SetUserPreference)' -or $_ -notmatch '^SolidWorks.Interop.sldworks.ISldWorks::'})
$taskAllowed=@('CloseAllDocuments','ExitApp','GetDocumentCount','GetDocumentDependencies2','GetDocuments','GetProcessID','RevisionNumber')|ForEach-Object {'SolidWorks.Interop.sldworks.ISldWorks::'+$_}
Check 'compiled IL contains only seven allowed application APIs' ($taskForbidden.Count -eq 0 -and (@($taskCadCalls|Sort-Object) -join '|') -ceq (@($taskAllowed|Sort-Object) -join '|')) 'Every emitted method inspected; no Open or model-document API'
Check 'frozen cold diagnostic preserved' ((Get-FileHash -LiteralPath (Join-Path $taskBase 'cold_diagnostic\DiagnosticProbe.cs') -Algorithm SHA256).Hash -eq 'D088CE76A7983CFE495412548CCDD8ACD71736EA6CB1C1012BFB32DF4B562C35') 'Parent source remains frozen'
$taskReport=[ordered]@{
 schema='obsolete-door-closed-dependency-static-v1';generatedAtUtc=[DateTime]::UtcNow.ToString('o')
 source=$taskBuild.source;sourceSha256=$taskBuild.sourceSha256;executable=$taskBuild.executable;executableSha256=$taskBuild.executableSha256
 buildManifestSha256=(Get-FileHash -LiteralPath (Join-Path $taskDir 'build.json') -Algorithm SHA256).Hash
 staticScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
 count=$taskRows.Count;passed=@($taskRows|Where-Object pass -eq $true).Count;failed=@($taskRows|Where-Object pass -ne $true).Count;checks=$taskRows
 compiledCadCalls=@($taskCadCalls|Sort-Object);forbiddenCadCalls=$taskForbidden
 mainInvoked=$false;nativeCadExecuted=$false;modelAccepted=$false;strictColdAcceptancePassed=$false
 runtimeAcceptance='UNVERIFIED: no CAD execution in worker scope'
}
$taskReport|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $taskDir 'static-validation.json') -Encoding utf8
[PSCustomObject]$taskReport|Select-Object count,passed,failed,sourceSha256,executableSha256,mainInvoked,nativeCadExecuted|ConvertTo-Json
if($taskReport.failed -ne 0){throw 'OFFLINE_VALIDATION_FAILED'}
