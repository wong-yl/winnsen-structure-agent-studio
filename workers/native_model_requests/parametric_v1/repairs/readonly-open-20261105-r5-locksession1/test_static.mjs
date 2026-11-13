import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, mkdirSync, readdirSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'

const repair = dirname(fileURLToPath(import.meta.url))
const root = resolve(repair, '../../../../..')
const evidence = resolve(root, 'data/review_portal_runtime/readonly_open_repair_20261105_1920/ac_recovery_20261106212518/continue4_20261107105542')
const baseline = resolve(repair, '../readonly-open-20261105-r5-monitor1')
const text = name => readFileSync(resolve(repair, 'source', name), 'utf8').replaceAll('\r\n', '\n')
const old = name => readFileSync(resolve(baseline, 'source', name), 'utf8').replaceAll('\r\n', '\n')
const sha = p => createHash('sha256').update(readFileSync(p)).digest('hex').toUpperCase()
const locks = text('NativeParametricLocks.cs'), assembly = text('NativeParametricAssembly.cs')
const baseLocks = old('NativeParametricLocks.cs')
const normalize = s => s.replace(/\s+/g, '')
const section = (s, start, end) => s.slice(s.indexOf(start), s.indexOf(end, s.indexOf(start)))
function method(source, marker) {
  const start = source.indexOf(marker); assert.ok(start >= 0, marker)
  const brace = source.indexOf('{', start)
  let depth = 1, i = brace + 1
  for (; i < source.length && depth; i++) { if (source[i] === '{') depth++; else if (source[i] === '}') depth-- }
  assert.equal(depth, 0)
  return source.slice(start, i)
}
const checks = {}
checks.originalNativeGenerationBodyEqual = normalize(section(locks, 'foreach (string name in LegacyPatternNames)', '                    if (side == "R")'.trimStart())) === normalize(section(baseLocks, 'foreach (string name in LegacyPatternNames)', '                    if (side == "R")'.trimStart()))
// The source capture and dimensional body above include the original force rebuild and save gates.
assert.ok(checks.originalNativeGenerationBodyEqual)
checks.mirrorGeometryBodyEqual = normalize(section(locks, 'Feature mirrorFeature=((PartDoc)model).MirrorPart2', 'int errors=0,warnings=0;')) === normalize(section(baseLocks, 'Feature mirrorFeature=((PartDoc)model).MirrorPart2', 'errors=0;warnings=0;').replace('geometry=CaptureGeometry', 'GeometrySnapshot geometry=CaptureGeometry'))
assert.ok(checks.mirrorGeometryBodyEqual)
checks.mirrorLinkedAndNativeBendsUnchanged = locks.includes('.MirrorPart2(false,(int)swMirrorPartOptions_e.swMirrorPartOptions_ImportSolids,out mirrored)') && locks.includes('.InsertBends2(bendRadius,"",kFactor,bendAllowance,autoRelief,reliefRatio,true)')
assert.ok(checks.mirrorLinkedAndNativeBendsUnchanged)
checks.dispatchOutsideGenericSession = assembly.indexOf('NativeParametricLocks.RunIsolated(rows, phase == "locks-verify")') < assembly.indexOf('else NativeParametricDoor.WithSession(phase, delegate(ISldWorks sw)') && !assembly.includes('NativeParametricLocks.Run(sw,')
assert.ok(checks.dispatchOutsideGenericSession)
const inspect = method(locks, 'private static void InspectRightOnly(')
checks.rightReadbackDirectOnly = (inspect.match(/NativeParametricAssembly\.Open\(/g) || []).length === 1 && inspect.includes('Open(sw, path, true)') && inspect.includes('sourcePreloaded = false') && !inspect.includes('Open(sw, right.Source')
assert.ok(checks.rightReadbackDirectOnly)
checks.noReadonlyRebuildOrSave = !/\.(?:Save|GuardedSaveAs|ForceRebuild3|EditRebuild3|Save3)\(/.test(inspect)
assert.ok(checks.noReadonlyRebuildOrSave)
checks.sourceAndTargetReadbackHashesBound = inspect.includes('ParametricContext.Hash(right.Source)==sourceHash') && inspect.includes('sourceHash==right.SourceSha256')
assert.ok(checks.sourceAndTargetReadbackHashesBound)
checks.saveGuardsRetained = locks.includes('NativeParametricAssembly.Save(model)') && locks.includes('NativeParametricAssembly.GuardedSaveAs(model,rightSource,') && locks.includes('NativeParametricAssembly.GuardedSaveAs(mirrored,built,')
assert.ok(checks.saveGuardsRetained)
checks.noWarningGateChange = method(assembly, 'internal static ModelDoc2 Open(') === method(old('NativeParametricAssembly.cs'), 'internal static ModelDoc2 Open(')
assert.ok(checks.noWarningGateChange)
checks.eightOtherSourcesIdentical = true
for (const name of readdirSync(resolve(repair, 'source')).filter(name => !['NativeParametricAssembly.cs','NativeParametricLocks.cs'].includes(name))) assert.equal(sha(resolve(repair,'source',name)),sha(resolve(baseline,'source',name)),name)
const orchestration = method(locks, 'internal static void RunIsolated(')
const output = resolve(evidence, 'locksession1_impl_orchestration_' + Date.now())
mkdirSync(output)
const harnessHead = `using System; using System.IO; using System.Collections.Generic;
using File=FakeFile;
interface ISldWorks {}
class StubSw:ISldWorks {}
static class ParametricContext { internal static string CadDirectory="D:/new-attempt/native_cad/working_pack", AttemptDirectory="D:/new-attempt", Phase="locks"; internal static void AssertMutation(string p) {} internal static string Hash(string p){return "HASH";} }
static class NativeParametricAssembly { internal static void AssertProtectedTemplateInputs(){Harness.Guards++;} }
static class FakeFile { public static bool Exists(string p){return false;} public static void Copy(string a,string b,bool c){Harness.Events.Add("copy");} public static void Replace(string a,string b,string c){Harness.Step("replace");} }
namespace Winnsen.StructureAgent.NativeDoorModule888x14 { static class NativeParametricDoor { public static void WithSession(string p,Action<ISldWorks> action,string suffix){Harness.Sessions++; action(new StubSw());} } }
static class NativeParametricLocks {
class RightPreparation { internal string Source="D:/new-attempt/source.SLDPRT",Target="D:/new-attempt/R.SLDPRT"; }
static void Require(bool v,string c,int stage,string path){if(!v)throw new Exception(c);}
static void BeginIsolated(ISldWorks sw,List<object> r,string s){if(s.StartsWith("prepare-"))Harness.Step(s);}
static RightPreparation RunSide(ISldWorks sw,List<object> r,bool ro,string side,string source){return !ro&&side=="R"?new RightPreparation():null;}
static void CreateRightFromPersistedSource(ISldWorks sw,List<object> r,RightPreparation p,string built){Harness.Step("mirror");}
static void InspectRightOnly(List<object> r,RightPreparation p,string path,string stage){Harness.Step(stage);}
static void InspectOwnerOnly(List<object> r,string path,string stage){Harness.Step(stage);}
`
const harnessTail = `
}
static class Harness {
internal static List<string> Events=new List<string>(); internal static string Fault=""; internal static int Sessions,Guards;
internal static void Step(string s) { if(s=="replace"&&!Events.Contains("temporary-right-only"))throw new Exception("REPLACE_BEFORE_TEMP_READBACK"); Events.Add(s);if(s==Fault)throw new Exception("INJECTED"); }
static void Check(bool v,string message){if(!v)throw new Exception(message+":"+String.Join(",",Events));}
static void Run(string fault,bool ro){Events.Clear();Sessions=0;Guards=0;Fault=fault;bool threw=false;try{NativeParametricLocks.RunIsolated(new List<object>(),ro);}catch{threw=true;}
if(ro){Check(!threw&&String.Join(",",Events)=="prepare-L,prepare-R","READONLY_MUTATED");return;}
if(fault==""){Check(!threw,"HAPPY_FAILED");Check(String.Join(",",Events)=="copy,prepare-L,prepare-R,mirror,temporary-right-only,replace,committed-right-only,right-weldment-only,root-owner-only","ORDER_DRIFT");Check(Sessions==3&&Guards>=2,"BOUNDARY_OR_GUARD_MISSING");}
else {Check(threw,"FAULT_SWALLOWED");if(fault=="mirror"||fault=="temporary-right-only")Check(!Events.Contains("replace"),"REPLACED_AFTER_FAILURE");if(fault=="committed-right-only")Check(!Events.Contains("right-weldment-only"),"CONTINUED_AFTER_FINAL_FAILURE");if(fault=="right-weldment-only")Check(!Events.Contains("root-owner-only"),"CONTINUED_AFTER_OWNER_FAILURE");}}
static int Main(){try{Run("",false);Run("mirror",false);Run("temporary-right-only",false);Run("committed-right-only",false);Run("right-weldment-only",false);Run("",true);Console.WriteLine("6 actual orchestration cases passed");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
`
function runHarness(label, source) {
 const cs=resolve(output,label+'.cs'),exe=resolve(output,label+'.exe');writeFileSync(cs,harnessHead+source+harnessTail)
 const compiled=spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe',['/nologo','/target:exe','/platform:x64','/main:Harness','/out:'+exe,cs],{encoding:'utf8',windowsHide:true})
 assert.equal(compiled.status,0,compiled.stdout+compiled.stderr)
 const run=spawnSync(exe,[],{encoding:'utf8',windowsHide:true})
 return {compiled:compiled.status,exitCode:run.status,stdout:run.stdout,stderr:run.stderr,source:cs,sourceSha256:sha(cs),executable:exe,executableSha256:sha(exe)}
}
const harness=runHarness('actual_orchestration',orchestration)
assert.equal(harness.exitCode,0,harness.stderr)
const missingTemporary=runHarness('mutation_missing_temporary',orchestration.replace('InspectRightOnly(records, right, built, "temporary-right-only");',''))
assert.notEqual(missingTemporary.exitCode,0)
const missingFinal=runHarness('mutation_missing_final',orchestration.replace('InspectRightOnly(records, right, right.Target, "committed-right-only");',''))
assert.notEqual(missingFinal.exitCode,0)
const result={at:new Date().toISOString(),sourceHashes:readdirSync(resolve(repair,'source')).map(name=>({name,sha256:sha(resolve(repair,'source',name))})),checks,harness,mutationChecks:{missingTemporaryRejected:true,missingFinalRejected:true},mutations:[missingTemporary,missingFinal],cadStarted:false,productionMainInvoked:false,pass:true}
writeFileSync(resolve(evidence,'locksession1_impl_static_'+Date.now()+'.json'),JSON.stringify(result,null,2)+'\n',{flag:'wx'})
console.log(JSON.stringify({pass:true,checks,harnessCases:6,mutationCases:2,output,cadStarted:false}))
