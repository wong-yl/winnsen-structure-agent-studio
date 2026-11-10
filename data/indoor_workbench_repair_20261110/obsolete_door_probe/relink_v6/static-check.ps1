$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskBase=Split-Path -Parent $taskDir
$taskBuild=Get-Content -LiteralPath (Join-Path $taskDir 'build.json') -Raw|ConvertFrom-Json
$taskSource=[IO.File]::ReadAllText($taskBuild.source)
$taskRows=New-Object 'Collections.Generic.List[object]'
function Check($name,$condition,$detail){$taskRows.Add([ordered]@{name=$name;pass=[bool]$condition;detail=$detail});if(-not $condition){Write-Output ('FAILED: '+$name)}}
function Fails($code,$expected){try{& $code|Out-Null;return $false}catch{return $_.Exception.GetBaseException().Message -match $expected}}
Check 'exact compiled artifact hashes' ($taskBuild.buildPassed -eq $true -and (Get-FileHash -LiteralPath $taskBuild.source -Algorithm SHA256).Hash -eq $taskBuild.sourceSha256 -and (Get-FileHash -LiteralPath $taskBuild.executable -Algorithm SHA256).Hash -eq $taskBuild.executableSha256) 'Source and emitted binary covered; Main never invoked'
Check 'single relink CLI and fresh source checkpoint' ($taskSource.Contains('args.Length != 4 || args[0] != "relink"') -and $taskSource.Contains('sourceReceipt, sourceEvidence') -and -not [regex]::IsMatch($taskSource,'SuccessfulEditEvidence|SavedRootSha256|editEvidence|candidate_pack_v5')) 'No v5 edit/root overrides'
Check 'no document open, save, rebuild, delete or selection' (-not [regex]::IsMatch($taskSource,'\.(OpenDoc\w*|ActivateDoc\w*|DeleteSelection2|ForceRebuild3|EditRebuild3|Save\w*|ClearSelection2|Select4|ShowConfiguration2|ReplaceComponents|Resolve\w*|SetUserPreference\w*)\s*\(')) 'Closed-file API only'
Check 'full 75 plan precedes relink and post verification' ($taskSource.IndexOf('List<OwnerPlan> plans = Preflight(') -lt $taskSource.IndexOf('Relink(sw, process, retainedHandle') -and $taskSource.IndexOf('Relink(sw, process, retainedHandle') -lt $taskSource.IndexOf('VerifyDependencies(sw, process, retainedHandle') -and $taskSource.Contains('attempted == 75 && plans.Count == 75 && failures.Count == 0 && CandidateDependencyCalls == 75') -and $taskSource.Contains('"relink_plan", "all75EnumeratedBeforeMutation", true')) 'All owner failures collected before any mutation'
Check 'single exact dependency API and exact replace API call sites' ([regex]::Matches($taskSource,'sw\.GetDocumentDependencies2\(path, false, false, false\)').Count -eq 1 -and [regex]::Matches($taskSource,'sw\.ReplaceReferencedDocument\(plan.owner, edge.originalPath, edge.candidatePath\)').Count -eq 1 -and $taskSource.Contains('Require(replaced, "REPLACE_REFERENCED_DOCUMENT_RETURNED_FALSE')) 'No search/traverse; each planned source edge must return true'
Check 'empty documents required around reads and replacements' ($taskSource.Contains('Require(count == 0 && documents.Length == 0') -and $taskSource.Contains('EmptyDocuments(sw, "before:" + stage') -and $taskSource.Contains('EmptyDocuments(sw, "after:" + stage') -and $taskSource.Contains('EmptyDocuments(sw, "before-replace:') -and $taskSource.Contains('EmptyDocuments(sw, "after-replace:')) 'Document count and array both must remain zero'
Check 'source, nonowners and each single mutation scope protected' ($taskSource.Contains('CompareFiles(before, after, new HashSet<string>(new[] { plan.owner }') -and $taskSource.Contains('CompareFiles(packBefore, packAfter, MutableOwners') -and $taskSource.Contains('CompareFiles(SourceBefore, sourceAfter, NoPaths') -and $taskSource.Contains('info.links == 1')) 'Hash, size, mtime and attributes; no reparse paths or hardlinks'
Check 'post state exact and source references forbidden' ($taskSource.Contains('checkedOwners == 75 && oldSourceEdges == 0 && failures.Count == 0 && CandidateDependencyCalls == 150') -and $taskSource.Contains('!observed.edges.Any(e => e.replacementRequired)') -and $taskSource.Contains('expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase)')) 'Every mapped dependency and empty representation rechecked'
Check 'owned natural exit and no forced termination' ($taskSource.Contains('VerifyOwner(sw, process, retainedHandle') -and $taskSource.Contains('process.WaitForExit(500)') -and -not [regex]::IsMatch($taskSource,'\.(Kill|Terminate)\s*\(')) 'Retained process handle, PID, start time, executable and revision'
Check 'diagnostic completion distinct from model acceptance' ($taskSource.Contains('"operationComplete", false, "diagnosticCompleted", diagnosticCompleted') -and $taskSource.Contains('"modelAccepted", false, "strictColdAcceptancePassed", false') -and $taskSource.Contains('"all75SourceUnchanged", sourceUnchanged') -and $taskSource.Contains('Inventory(Pack, "candidate-after")')) 'Completion preserves runner and migration evidence contract'
Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskBuild.executable)
$taskType=$taskAssembly.GetType('RelinkProbe',$true)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Static'
$taskPublic=[Reflection.BindingFlags]'Public,Instance'
$taskFileType=$taskAssembly.GetType('RelinkProbe+FileBinding',$true)
$taskParser=$taskType.GetMethod('ParseDependencies',$taskFlags)
$taskPlanner=$taskType.GetMethod('PlanOwner',$taskFlags)
$taskEmptyCheck=$taskType.GetMethod('CheckEmptySource',$taskFlags)
$taskPostCheck=$taskType.GetMethod('CompareDependencyPlan',$taskFlags)
function Base($value){if($null -eq $value){return $null};return ,($value.PSObject.BaseObject)}
function Parse($raw){$a=New-Object object[] 1;$a[0]=Base $raw;return $taskParser.Invoke($null,$a)}
function Plan($owner,$raw,$files){$a=New-Object object[] 3;$a[0]=Base $owner;$a[1]=Base $raw;$a[2]=Base $files;return $taskPlanner.Invoke($null,$a)}
function EmptyCheck($plan,$raw){$a=New-Object object[] 2;$a[0]=Base $plan;$a[1]=Base $raw;$taskEmptyCheck.Invoke($null,$a)|Out-Null}
function PostCheck($before,$after){$a=New-Object object[] 2;$a[0]=Base $before;$a[1]=Base $after;$taskPostCheck.Invoke($null,$a)|Out-Null}
function FileBinding($name){$f=[Activator]::CreateInstance($taskFileType);$f.fileName=$name;$f.sourcePath='Z:\relink-fixture\source\'+$name;$f.path='Z:\relink-fixture\candidate\'+$name;$f.sha256=('A'*64);$f.size=1;return $f}
$taskOwner=FileBinding 'owner.SLDASM'
$taskLeaf=FileBinding 'leaf.SLDPRT'
$taskOther=FileBinding 'other.SLDPRT'
$taskBindings=[Array]::CreateInstance($taskFileType,3)
$taskBindings.SetValue((Base $taskOwner),0);$taskBindings.SetValue((Base $taskLeaf),1);$taskBindings.SetValue((Base $taskOther),2)
$taskRaw=[object[]]@('leaf',$taskLeaf.sourcePath,'other',$taskOther.path)
$taskPlanned=Plan $taskOwner $taskRaw $taskBindings
Check 'exact source mapping and existing candidate preserved' ($taskPlanned.mayChange -and $taskPlanned.edges.Count -eq 2 -and $taskPlanned.edges[0].originalPath -eq $taskLeaf.sourcePath -and $taskPlanned.edges[0].candidatePath -eq $taskLeaf.path -and $taskPlanned.edges[0].replacementRequired -and -not $taskPlanned.edges[1].replacementRequired) 'Pure public-data fixture; no filesystem or CAD calls'
Check 'external same-basename path rejected' (Fails {Plan $taskOwner ([object[]]@('leaf','Y:\foreign\leaf.SLDPRT')) $taskBindings} 'UNAUTHORIZED_EXTERNAL_DEPENDENCY') 'No basename fallback or whitelist expansion'
Check 'duplicate and source/candidate alias duplicate rejected' ((Fails {Plan $taskOwner ([object[]]@('leaf',$taskLeaf.sourcePath,'again',$taskLeaf.sourcePath)) $taskBindings} 'DUPLICATE_OR_AMBIGUOUS_DEPENDENCY') -and (Fails {Plan $taskOwner ([object[]]@('leaf',$taskLeaf.sourcePath,'again',$taskLeaf.path)) $taskBindings} 'DUPLICATE_OR_AMBIGUOUS_DEPENDENCY')) 'One unique mapped candidate per dependency'
Check 'relative and path-normalization alias rejected' ((Fails {Plan $taskOwner ([object[]]@('leaf','leaf.SLDPRT')) $taskBindings} 'DEPENDENCY_PATH_NOT_EXACT_ABSOLUTE') -and (Fails {Plan $taskOwner ([object[]]@('leaf','Z:\relink-fixture\source\x\..\leaf.SLDPRT')) $taskBindings} 'DEPENDENCY_PATH_NOT_EXACT_ABSOLUTE')) 'Stored source path must be exact absolute path'
$taskAmbiguous=[Array]::CreateInstance($taskFileType,4)
for($i=0;$i -lt 3;$i++){$taskAmbiguous.SetValue($taskBindings.GetValue($i),$i)}
$taskAmbiguous.SetValue((Base $taskLeaf),3)
Check 'duplicate binding map rejected' (Fails {Plan $taskOwner $taskRaw $taskAmbiguous} 'same key|相同键|已经添加|已添加') 'Ambiguous manifests cannot choose a target'
$taskNull=Parse $null
$taskZero=Parse ([object[]]@())
$taskPairs=Parse $taskRaw
Check 'raw null zero and paired return retained distinctly' ($taskNull.rawNull -and $taskNull.emptyRepresentation -eq 'null' -and $taskZero.rawArrayLength -eq 0 -and $taskZero.emptyRepresentation -eq 'zero-length-array:System.Object[]' -and $taskPairs.parseSucceeded -and $taskPairs.pairs.Count -eq 2 -and [object]::ReferenceEquals((Base $taskRaw),$taskPairs.raw)) 'Actual raw type/length/rank/lower bound retained'
$taskOdd=Parse ([object[]]@('odd','path','extra'))
$taskScalar=Parse 'scalar'
$taskBlank=Parse ([object[]]@('leaf',''))
$taskRank=Parse ([Array]::CreateInstance([string],2,2))
$taskLower=Parse ([Array]::CreateInstance([object],[int[]]@(2),[int[]]@(1)))
Check 'malformed shapes and blank pairs fail parsing' (-not $taskOdd.parseSucceeded -and -not $taskScalar.parseSucceeded -and -not $taskBlank.parseSucceeded -and -not $taskRank.parseSucceeded -and -not $taskLower.parseSucceeded) 'Odd/scalar/blank/multidimensional/nonzero-lower-bound variants rejected'
Check 'malformed dependency blocks planning' (Fails {Plan $taskOwner ([object[]]@('odd')) $taskBindings} 'MALFORMED_DEPENDENCY_RETURN') 'Malformed is not interpreted as an empty owner'
$taskLeafNull=Plan $taskLeaf $null $taskBindings
$taskLeafZero=Plan $taskLeaf ([object[]]@()) $taskBindings
EmptyCheck $taskLeafNull $null
EmptyCheck $taskLeafZero ([object[]]@())
Check 'only bound part empties enter finite policy' ($taskLeafNull.dependencyState -eq 'api-reported-no-dependencies' -and -not $taskLeafNull.mayChange -and $taskLeafNull.edges.Count -eq 0 -and $taskLeafZero.emptyRepresentation -eq $taskZero.emptyRepresentation) 'Existence/hash/source equality enforced in runtime binding and cross-check'
$taskDrawing=FileBinding 'drawing.SLDDRW'
Check 'assembly and drawing empty results rejected' ((Fails {Plan $taskOwner $null $taskBindings} 'EMPTY_NONPART_DEPENDENCIES_UNRESOLVED') -and (Fails {Plan $taskOwner ([object[]]@()) $taskBindings} 'EMPTY_NONPART_DEPENDENCIES_UNRESOLVED') -and (Fails {Plan $taskDrawing $null $taskBindings} 'EMPTY_NONPART_DEPENDENCIES_UNRESOLVED')) 'No mutation when assembly/drawing dependencies are unresolved'
Check 'source empty mismatch rejected' ((Fails {EmptyCheck $taskLeafNull ([object[]]@())} 'EMPTY_SOURCE_CANDIDATE_REPRESENTATION_MISMATCH') -and (Fails {EmptyCheck $taskLeafNull ([object[]]@('other',$taskOther.sourcePath))} 'EMPTY_SOURCE_CANDIDATE_REPRESENTATION_MISMATCH')) 'Null and zero-array may not substitute for one another'
$taskObserved=Plan $taskOwner ([object[]]@('other',$taskOther.path,'leaf',$taskLeaf.path)) $taskBindings
PostCheck $taskPlanned $taskObserved
Check 'post compare accepts exact mapped set independent of order' (-not $taskObserved.mayChange -and $taskObserved.edges.Count -eq 2) 'Both target paths and dependency names covered'
Check 'post compare rejects residual source and missing pair' ((Fails {PostCheck $taskPlanned $taskPlanned} 'SOURCE_DEPENDENCY_REMAINS') -and (Fails {PostCheck $taskPlanned (Plan $taskOwner ([object[]]@('leaf',$taskLeaf.path)) $taskBindings)} 'POST_DEPENDENCIES_DIFFER_FROM_FROZEN_PLAN')) 'No missing or unconverted dependency may complete'
Check 'post compare rejects changed name and empty representation' ((Fails {PostCheck $taskPlanned (Plan $taskOwner ([object[]]@('renamed',$taskLeaf.path,'other',$taskOther.path)) $taskBindings)} 'POST_DEPENDENCIES_DIFFER_FROM_FROZEN_PLAN') -and (Fails {PostCheck $taskLeafNull $taskLeafZero} 'POST_DEPENDENCY_OWNER_OR_EMPTY_REPRESENTATION_CHANGED')) 'Unexpected changes are not normalized away'
$taskPolicy=$taskType.GetField('EmptyPolicy',$taskFlags).GetRawConstantValue()
Check 'empty policy records unsupported error distinction' ($taskPolicy.Contains('has no error code') -and $taskPolicy.Contains('does not independently exclude read failure') -and $taskSource.Contains('ReadDependencies(sw, process, handle, pid, ticks, executable, file, true, stage + "-source-leaf")') -and $taskSource.Contains('Hash(BindPath(file.path)) == file.sha256 && Hash(file.sourcePath) == file.sha256')) 'Policy is explicitly limited and cross-check is performed pre and post'
$taskPins=@('ReceiptPath','EvidencePath','HelpVb','HelpCs')
$taskPinRows=@()
foreach($taskPin in $taskPins){$taskPath=$taskType.GetField($taskPin,$taskFlags).GetRawConstantValue();$taskSha=$taskType.GetField(($taskPin.Replace('Path','')+'Sha256'),$taskFlags).GetRawConstantValue();$taskActual=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash;$taskPinRows+=[ordered]@{path=$taskPath;sha256=$taskActual};Check ('compiled live pin '+$taskPin) ($taskActual -eq $taskSha) 'Read-only local official/checkpoint evidence hash'}
$taskSourcePack=$taskType.GetField('SourcePack',$taskFlags).GetRawConstantValue()
$taskRoot=Join-Path $taskSourcePack ($taskType.GetField('RootFile',$taskFlags).GetRawConstantValue())
Check 'compiled source root pin matches checkpoint' ((Get-FileHash -LiteralPath $taskRoot -Algorithm SHA256).Hash -eq $taskType.GetField('SourceRootSha256',$taskFlags).GetRawConstantValue()) '78A794E6...; candidate root begins as same SHA'
$taskApi=Get-Content -LiteralPath $taskBuild.apiSignatureEvidence -Raw|ConvertFrom-Json
$taskDep=@($taskApi.signatures|Where-Object member -eq 'GetDocumentDependencies2')
$taskReplace=@($taskApi.signatures|Where-Object member -eq 'ReplaceReferencedDocument')
Check 'official dependency and replacement signatures bound' ($taskDep.Count -eq 1 -and $taskDep[0].signature -eq 'System.Object GetDocumentDependencies2(System.String, Boolean, Boolean, Boolean)' -and ($taskDep[0].parameters.name -join ',') -eq 'Document,Traverseflag,Searchflag,AddReadOnlyInfo' -and $taskReplace.Count -eq 1 -and $taskReplace[0].signature -eq 'Boolean ReplaceReferencedDocument(System.String, System.String, System.String)' -and ($taskReplace[0].parameters.name -join ',') -eq 'ReferencingDocument,ReferencedDocument,NewReference') 'Reflection from installed official interop; no COM activation'

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

$taskAllowed=@('CloseAllDocuments','ExitApp','GetDocumentCount','GetDocumentDependencies2','GetDocuments','GetProcessID','ReplaceReferencedDocument','RevisionNumber')|ForEach-Object {'SolidWorks.Interop.sldworks.ISldWorks::'+$_}
$taskForbidden=@($taskCadCalls|Where-Object {$_ -notin $taskAllowed})
Check 'compiled IL only eight allowed application APIs' ($taskForbidden.Count -eq 0 -and (@($taskCadCalls|Sort-Object) -join '|') -ceq (@($taskAllowed|Sort-Object) -join '|')) 'Every emitted method inspected; no document Open/Save/Rebuild/Delete'
Check 'frozen dependency diagnostic source unchanged' ((Get-FileHash -LiteralPath (Join-Path $taskBase 'closed_dependency\DependencyProbe.cs') -Algorithm SHA256).Hash -eq 'B1D4C3E2195784D43E8B5ACCF4E64D0B930C56B57EF393E19205AF6BF10C529C') 'Reuse preserved original evidence collector'
$taskReport=[ordered]@{
 schema='obsolete-door-closed-relink-static-v1';generatedAtUtc=[DateTime]::UtcNow.ToString('o')
 source=$taskBuild.source;sourceSha256=$taskBuild.sourceSha256;executable=$taskBuild.executable;executableSha256=$taskBuild.executableSha256
 buildManifestSha256=(Get-FileHash -LiteralPath (Join-Path $taskDir 'build.json') -Algorithm SHA256).Hash
 staticScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
 count=$taskRows.Count;passed=@($taskRows|Where-Object pass -eq $true).Count;failed=@($taskRows|Where-Object pass -ne $true).Count;checks=$taskRows
 compiledCadCalls=@($taskCadCalls|Sort-Object);forbiddenCadCalls=$taskForbidden;officialAndCheckpointPins=$taskPinRows;emptyReturnPolicy=$taskPolicy
 mainInvoked=$false;nativeCadExecuted=$false;modelAccepted=$false;strictColdAcceptancePassed=$false
 runtimeAcceptance='UNVERIFIED: no CAD execution in worker scope'
}
$taskReport|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $taskDir 'static-validation.json') -Encoding utf8
[PSCustomObject]$taskReport|Select-Object count,passed,failed,sourceSha256,executableSha256,mainInvoked,nativeCadExecuted|ConvertTo-Json
if($taskReport.failed -ne 0){throw 'OFFLINE_VALIDATION_FAILED'}