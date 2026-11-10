$ErrorActionPreference='Stop'
$taskDir=$PSScriptRoot
$taskBuild=Get-Content -LiteralPath (Join-Path $taskDir 'build.json') -Raw -Encoding UTF8|ConvertFrom-Json
$taskSource=[IO.File]::ReadAllText($taskBuild.source)
$taskRunner=[IO.File]::ReadAllText((Join-Path $taskDir 'run-right-mirror.mjs'))
$taskObserver=[IO.File]::ReadAllText((Join-Path $taskDir 'observe-right-mirror.py'))
$taskGeometry=[IO.File]::ReadAllText($taskBuild.originalGeometry.path)
$taskExtract=Get-Content -LiteralPath (Join-Path $taskDir 'extraction.json') -Raw -Encoding UTF8|ConvertFrom-Json
$taskRows=New-Object 'Collections.Generic.List[object]'
function Check($name,$condition,$detail){$taskRows.Add([ordered]@{name=$name;pass=[bool]$condition;detail=$detail});if(-not $condition){Write-Output ('FAILED: '+$name)}}
Check 'compiled source and geometry hashes match build' (((Get-FileHash -LiteralPath $taskBuild.source -Algorithm SHA256).Hash -eq $taskBuild.sourceSha256) -and ((Get-FileHash -LiteralPath $taskBuild.executable -Algorithm SHA256).Hash -eq $taskBuild.executableSha256) -and ((Get-FileHash -LiteralPath $taskBuild.originalGeometry.path -Algorithm SHA256).Hash -eq $taskBuild.originalGeometry.sha256)) 'No Main invocation'
$taskExact=$true
foreach($taskChunk in $taskExtract.chunks){$taskOriginal=[IO.File]::ReadAllText($taskChunk.source.path);$taskHash=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($taskChunk.exactText))).Replace('-','');if(-not $taskOriginal.Contains($taskChunk.exactText) -or -not $taskGeometry.Contains($taskChunk.exactText) -or $taskHash -ne $taskChunk.textSha256){$taskExact=$false}}
Check '36 original methods and types preserved verbatim' ($taskExact -and $taskExtract.chunks.Count -eq 36) 'Includes CaptureGeometry GeometryGate MirrorGeometryGate SelectRightPlane and VerifyReflectedVertices'
Check 'source readonly and strict cold warning identity gate' ($taskSource.Contains('sw.OpenDoc6(path, 1, 3, "", ref errors, ref warnings)') -and $taskSource.Contains('errors == 0 && warnings == 0 && model.IsOpenedReadOnly() && Same(model.GetPathName(), path) && !model.GetSaveFlag()')) 'Warnings recorded before any rejection; no rebuild in cold path'
Check 'one source readonly and one new candidate save' ($taskSource.Contains('Open(sw, Input.parameterSource.path, "saved-parameter-source-before-mirror")') -and $taskSource.Contains('mirrored.Extension.SaveAs(Candidate, 0, 1, null, ref errors, ref warnings)') -and ([regex]::Matches($taskSource,'\.SaveAs\(').Count -eq 1) -and -not [regex]::IsMatch($taskSource,'\bmodel\.(ForceRebuild|Save)')) 'Original parameter source never saved rebuilt copied or replaced'
Check 'same linked mirror and original bend pipeline' ($taskSource.Contains('MirrorPart2(false,(int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSolids,out mirrored)') -and $taskSource.Contains('InsertBends2(bendRadius,"",kFactor,bendAllowance,autoRelief,reliefRatio,true)') -and $taskSource.Contains('mirrored.ForceRebuild3(false)') -and $taskSource.Contains('NativeParametricDoor.VerifyReflectedVertices(model,mirrored)') -and $taskSource.Contains('referenceStatuses.Length == referenceFiles.Length') -and $taskSource.Contains('referenceStatuses.All(status => Convert.ToInt32(status) == 3 || Convert.ToInt32(status) == 4)') -and ([regex]::Matches($taskSource,'bool bound = LinkedSourceBound\(files, statuses\);').Count -eq 2)) 'Exact source paths and only status 3 or 4 required before save and after cold open; no break-link or new geometric strategy'
Check 'bend and R9 parameters come from pinned evidence' ($taskSource.Contains('Input.bend.v35BendRadiusMm / 1000.0') -and $taskSource.Contains('Input.bend.v35FixedPlane') -and $taskSource.Contains('Input.rightRows') -and $taskSource.Contains('Hash(Input.plan.path) == Input.plan.sha256')) 'No handwritten dimensions'
Check 'all loaded documents must be inventory or exact candidate' ($taskSource.Contains('LOADED_DOCUMENT_OUTSIDE_FIXED_INVENTORY_OR_CANDIDATE') -and $taskSource.Contains('SameComObject(doc, NewMirror)') -and $taskSource.Contains('Documents(sw, "before-mirror-source-only")') -and $taskSource.Contains('Documents(sw, "after-mirror-and-bends")')) 'Exact unsaved mirror COM identity allowed during creation only'
Check 'native candidate bounded to new owned run' ($taskSource.Contains('Inside(Path.Combine(OwnedRoot, "runs"), Candidate)') -and $taskSource.Contains('!Known.ContainsKey(Candidate)') -and $taskSource.Contains('CandidateSha == "new" && !File.Exists(Candidate)')) 'No source attempt writes'
Check 'actual original lock readonly and mirror gates run in cold path' ($taskSource.Contains('ResearchGeometryGate(geometry, "R", Input.rightRows)') -and $taskSource.Contains('ResearchMirrorGeometryGate(geometry, Input.rightRows)') -and $taskSource.Contains('originalReadOnlyGate && mirrorGate && bound && !model.GetSaveFlag()')) 'Original geometry gates and actual linked source validation'
Check 'unchanged mature observer reused' (((Get-FileHash -LiteralPath (Join-Path $taskDir 'observe-right-mirror.py') -Algorithm SHA256).Hash) -eq ((Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $taskDir) 'right_identity_v1\observe-right-identity.py') -Algorithm SHA256).Hash)) 'Exact owner external witness and orphan monitor handling unchanged'
Check 'shared global lock heartbeat and per-arm portal gate preserved' ($taskRunner.Contains('acquireNativeWorkerInstanceLock({ dataDir: resolve(ROOT, ''data''), workerId })') -and $taskRunner.Contains('store.listSnapshot()') -and $taskRunner.Contains('await portalGate(); need(processSnapshot().cadAndMonitorEmpty')) 'No portal task creation or lease writes'
Check 'temporary cold gate precedes candidate File.Replace' ($taskRunner.Contains('results.length === 2 && results[1].pass === true && temporaryPin') -and $taskRunner.Contains('constants.COPYFILE_EXCL') -and $taskRunner.Contains('[IO.File]::Replace(') -and $taskRunner.Contains('finalPin.sha256 === temporaryPin.sha256') -and $taskRunner.Contains("hash(finalCandidate + '.before-parametric-lock.bak') === context.binding.right.sha256")) 'Original right only copied to final candidate; source untouched'
Check 'fresh current complete protection baseline used' ($taskRunner.Contains('old.rows.map(row => regular(row.path))') -and $taskRunner.Contains('walk(PROBE, true)') -and $taskRunner.Contains('regular(resolve(PROBE, ''../PROGRESS.md''))') -and $taskRunner.Contains('unchanged(validation.artifactVersions)')) 'No sibling subtree excluded'
Check 'timeout unknown identity and missing session retain lock' ($taskRunner.Contains('nativeChildUnconfirmed = true') -and $taskRunner.Contains('if (!Number.isInteger(pid) || pid <= 0) return true') -and $taskRunner.Contains('while (observerRunning || childLive() || state?.cadAndMonitorEmpty !== true)') -and $taskObserver.Contains("'cadTerminated': False")) '180 second arm plus45 second natural wait; no CAD termination'

Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskBuild.executable)
$taskType=$taskAssembly.GetType('RightMirrorFreshProbe',$true)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Static'
$taskBindingPath=Join-Path $taskDir 'source-binding.json'
$taskBindingSha=(Get-FileHash -LiteralPath $taskBindingPath -Algorithm SHA256).Hash
$taskType.GetMethod('LoadBindings',$taskFlags).Invoke($null,[object[]]@([string]$taskBindingPath,[string]$taskBindingSha))|Out-Null
Check 'compiled pure binding validates119 files and pinned parameters' ($taskType.GetField('Known',$taskFlags).GetValue($null).Count -eq 119) 'Does not start COM or CAD'
$taskLockType=$taskAssembly.GetType('Winnsen.StructureAgent.Native888.NativeParametricLocks',$true)
$taskSnapshotType=$taskAssembly.GetType('Winnsen.StructureAgent.Native888.NativeParametricLocks+GeometrySnapshot',$true)
$taskSlotType=$taskAssembly.GetType('Winnsen.StructureAgent.Native888.NativeParametricLocks+SlotAnalysis',$true)
$taskCircleType=$taskAssembly.GetType('Winnsen.StructureAgent.Native888.NativeParametricLocks+CircleAnalysis',$true)
$taskBinding=Get-Content -LiteralPath $taskBindingPath -Raw -Encoding UTF8|ConvertFrom-Json
$taskRightRows=[double[]]$taskBinding.rightRows
$taskGood=[Activator]::CreateInstance($taskSnapshotType)
$taskGood.side='R';$taskGood.body_count=1;$taskGood.feature_count=5;$taskGood.has_sheet_metal=$true;$taskGood.has_flat_pattern=$true;$taskGood.edge_count=1;$taskGood.mirror_feature_count=1;$taskGood.unique_slot_profile_signature_count=1;$taskGood.slot_rows_mm=$taskRightRows;$taskGood.circle_rows_mm=[double[]]@($taskRightRows|ForEach-Object {$_-60})
$taskEdgeType=$taskAssembly.GetType('Winnsen.StructureAgent.Native888.NativeParametricLocks+EdgeRow',$true)
$taskGood.edges.Add([Activator]::CreateInstance($taskEdgeType))
foreach($taskRow in $taskRightRows){$taskSlot=[Activator]::CreateInstance($taskSlotType);$taskSlot.full_fingerprint=$true;$taskGood.slot_analyses.Add($taskSlot);$taskCircle=[Activator]::CreateInstance($taskCircleType);$taskCircle.full_diameter5_pair=$true;$taskGood.circle_analyses.Add($taskCircle)}
$taskGate=$taskLockType.GetMethod('ResearchMirrorGeometryGate',$taskFlags)
Check 'original compiled mirror gate accepts synthetic complete R9 witness' ($taskGate.Invoke($null,[object[]]@($taskGood,$taskRightRows))) 'Pure gate behavior; not CAD evidence'
$taskGood.warning_feature_count=1
Check 'original compiled mirror gate rejects feature warning' (-not $taskGate.Invoke($null,[object[]]@($taskGood,$taskRightRows))) 'Negative control'
$taskGood.warning_feature_count=0;$taskGood.slot_rows_mm=[double[]]@($taskRightRows|Select-Object -Skip 1)
Check 'original compiled mirror gate rejects missing row' (-not $taskGate.Invoke($null,[object[]]@($taskGood,$taskRightRows))) 'Negative control'
$taskGood.slot_rows_mm=$taskRightRows;$taskGood.mirror_feature_count=0
Check 'original compiled mirror gate rejects absent mirror' (-not $taskGate.Invoke($null,[object[]]@($taskGood,$taskRightRows))) 'Negative control'
$taskNodeOutput=& node (Join-Path $taskDir 'tests-offline.mjs')
if($LASTEXITCODE -ne 0){throw 'NODE_BEHAVIOR_CHECK_FAILED'}
$taskNode=$taskNodeOutput|ConvertFrom-Json
Check 'runner stages portal and fail-safe child behaviors pass' ($taskNode.failed -eq 0 -and $taskNode.count -eq 5) 'Injected observations only'

$taskReport=[ordered]@{schema='right-mirror-fresh-static-v1';generatedAtUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$taskBuild.sourceSha256;executableSha256=$taskBuild.executableSha256;count=$taskRows.Count;passed=@($taskRows|Where-Object pass).Count;failed=@($taskRows|Where-Object {-not $_.pass}).Count;checks=$taskRows;originalMethodChunks=36;nodeBehavior=$taskNode;artifactVersions=@('RightMirrorFreshProbe.cs','OriginalGeometry.cs','extraction.json','build.ps1','build.json','run-right-mirror.mjs','observe-right-mirror.py','source-binding.json','static-check.ps1','tests-offline.mjs'|ForEach-Object {$taskPath=Join-Path $taskDir $_;[ordered]@{path=$taskPath;sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash}});mainInvoked=$false;nativeCadExecuted=$false;modelAccepted=$false;runtimeAcceptance='UNVERIFIED: no CAD execution in worker scope'}
$taskReport|ConvertTo-Json -Depth 15|Set-Content -LiteralPath (Join-Path $taskDir 'static-validation.json') -Encoding utf8
[PSCustomObject]$taskReport|Select-Object count,passed,failed,sourceSha256,executableSha256,nativeCadExecuted|ConvertTo-Json
if($taskReport.failed -ne 0){throw 'OFFLINE_STATIC_VALIDATION_FAILED'}
