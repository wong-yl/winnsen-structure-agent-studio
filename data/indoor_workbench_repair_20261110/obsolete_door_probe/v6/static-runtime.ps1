$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
if ([IO.Path]::GetFullPath($taskRoot) -ne 'D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\v6') { throw 'STATIC_RUNTIME_DIRECTORY_NOT_AUTHORIZED' }
$taskBuild = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $taskRoot) 'build-v6.json') -Raw | ConvertFrom-Json
$taskRows = New-Object 'System.Collections.Generic.List[object]'
function Check([string]$name, [bool]$value, [string]$detail) { $taskRows.Add([ordered]@{name=$name;passed=$value;detail=$detail}) }
Add-Type -AssemblyName System.Web.Extensions
$taskAssembly = [Reflection.Assembly]::LoadFrom($taskBuild.executable)
$taskType = $taskAssembly.GetType('IdentityProbe', $true)
$taskFlags = [Reflection.BindingFlags]'NonPublic,Static'
$taskJson = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$taskJson.MaxJsonLength = [int]::MaxValue
$taskManifestType = $taskAssembly.GetType('IdentityProbe+Manifest', $true)
$taskDtoJson = '{"sourcePack":"source","candidatePack":"candidate","files":[{"fileName":"sample.SLDPRT","sourcePath":"source\\sample.SLDPRT","path":"candidate\\sample.SLDPRT","sha256":"SOURCE","size":123,"candidateSha256":"RELINKED","candidateSize":456}],"sourceReceipt":{"path":"receipt","sha256":"R"},"sourceEvidence":{"path":"source-proof","sha256":"S"},"relinkEvidence":{"path":"relink-proof","sha256":"L"},"baselineEvidence":{"path":"baseline-proof","sha256":"B"},"editEvidence":{"path":"edit-proof","sha256":"E"}}'
$taskParsed = $taskJson.Deserialize($taskDtoJson, $taskManifestType)
Check 'compiled Manifest DTO retains source and candidate hashes and sizes separately' ($taskParsed.files[0].sha256 -eq 'SOURCE' -and $taskParsed.files[0].size -eq 123 -and $taskParsed.files[0].candidateSha256 -eq 'RELINKED' -and $taskParsed.files[0].candidateSize -eq 456) 'JavaScriptSerializer populates DTO fields; CS0649 is expected'
Check 'compiled Manifest DTO accepts all evidence groups' ($taskParsed.sourceReceipt.sha256 -eq 'R' -and $taskParsed.sourceEvidence.sha256 -eq 'S' -and $taskParsed.relinkEvidence.sha256 -eq 'L' -and $taskParsed.baselineEvidence.sha256 -eq 'B' -and $taskParsed.editEvidence.sha256 -eq 'E') 'Pure JSON; Main not invoked'
$taskEqual = $taskType.GetMethod('Equal', $taskFlags)
$taskFixture = '{"components":[{"name":"preserved","transform":[1,0,0,0.25],"solidBodies":{"volumeM3":0.000001,"areaM2":0.001,"boundsM":[0,0,0,0.01,0.02,0.03]},"mates":[{"referenceType":2,"persistentReference":"AABB"}]}],"rootFeatures":[{"mate":{"entities":[{"parameters":[1,0,0,0.45,0.025,1]},{"parameters":[1,0,0,0.50,0.025,1]},{"parameters":[1,0,0,0.55,0.025,1]},{"parameters":[1,0,0,0.60,0.025,1]}]}}]}'
$taskLeft = $taskJson.DeserializeObject($taskFixture)
$taskRight = $taskJson.DeserializeObject($taskFixture)
$taskEqual.Invoke($null, [object[]]@($taskLeft,$taskRight,'independent-cold-baseline-to-before-delete')) | Out-Null
Check 'original Equal accepts complete raw JSON roundtrip' $true 'Pure helper invocation; no COM calls'
foreach ($taskIndex in 0..3) {
    $taskChanged = $taskJson.DeserializeObject($taskFixture)
    $taskChanged['rootFeatures'][0]['mate']['entities'][$taskIndex]['parameters'][3] = [double]$taskChanged['rootFeatures'][0]['mate']['entities'][$taskIndex]['parameters'][3] + 0.4
    $taskRejected = $false
    try { $taskEqual.Invoke($null, [object[]]@($taskLeft,$taskChanged,'entity-params-axis-x')) | Out-Null }
    catch { $taskRejected = $_.Exception.ToString().Contains('NUMERIC_STATE_DRIFT') -and $_.Exception.ToString().Contains('/parameters[3]') }
    Check ('original Equal rejects raw EntityParams axis-x drift '+$taskIndex) $taskRejected 'No parameter normalization or dropped field'
}
foreach ($taskMutation in @(
    @('component identity drift','"name":"preserved"','"name":"changed"'),
    @('geometry volume drift','0.000001','0.000002'),
    @('physical transform drift','0.25','0.26'),
    @('mate persistent reference drift','AABB','AABC'),
    @('mate reference type drift','"referenceType":2','"referenceType":3')
)) {
    $taskChanged = $taskJson.DeserializeObject($taskFixture.Replace($taskMutation[1],$taskMutation[2]))
    $taskRejected = $false
    try { $taskEqual.Invoke($null,[object[]]@($taskLeft,$taskChanged,'deliberate-drift')) | Out-Null }
    catch { $taskRejected = $_.Exception.ToString().Contains('DRIFT') }
    Check ('original Equal rejects '+$taskMutation[0]) $taskRejected 'Deliberate protected raw state change'
}
Check 'assembly retains CLR4 x64 metadata' ($taskAssembly.ImageRuntimeVersion -eq 'v4.0.30319' -and $taskAssembly.GetName().ProcessorArchitecture.ToString() -eq 'Amd64') ($taskAssembly.ImageRuntimeVersion+' '+$taskAssembly.GetName().ProcessorArchitecture)
$taskResult = [ordered]@{schema='obsolete-door-v6-offline-reflection-v1';sourceSha256=(Get-FileHash -LiteralPath $taskBuild.source -Algorithm SHA256).Hash;executableSha256=(Get-FileHash -LiteralPath $taskBuild.executable -Algorithm SHA256).Hash;count=$taskRows.Count;passed=@($taskRows | Where-Object passed -eq $true).Count;failed=@($taskRows | Where-Object passed -ne $true).Count;checks=$taskRows;mainInvoked=$false;nativeCadExecuted=$false}
$taskResult | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskRoot 'static-runtime.json') -Encoding utf8
$taskResult | ConvertTo-Json -Depth 12
if ($taskResult.failed -ne 0) { throw 'OFFLINE_REFLECTION_VALIDATION_FAILED' }
