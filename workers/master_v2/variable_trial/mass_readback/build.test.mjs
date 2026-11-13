import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, mkdtempSync, rmSync, readdirSync, realpathSync, existsSync } from 'node:fs'
import { join, dirname, relative } from 'node:path'
import { tmpdir } from 'node:os'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { adaptWeightMain, buildWeightToolchain } from './build.mjs'
import { SOURCE, DATA, hash, read, validateToolchain } from '../common.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const rootPaths = ['VariableTrial.cs', 'TrialAssembly.cs', 'TrialOutput.cs'].map(name => join(SOURCE, name))
const rootBefore = rootPaths.map(path => ({ path, sha256: hash(path) }))
const original = readFileSync(rootPaths[0], 'utf8')
const newline = original.includes('\r\n') ? '\r\n' : '\n'
const actionAnchor = '            if(Text("action")=="rewire-inspect") {Rewire(Text("cadDirectory"));Inspect();}'
const cleanupStart = '                    var ownedTitles=new List<string>();var foreignPaths=new List<string>();'
const cleanupEnd = '                    Need(Items(Sw.GetDocuments()).Length==0,"DOCUMENTS_REMAIN_SESSION_PRESERVED");Sw.ExitApp();'
const originalCleanup = original.slice(original.indexOf(cleanupStart), original.indexOf(cleanupEnd) + cleanupEnd.length)
const actionStart = '            if(Text("action")=="weight-readback-diagnostic")'
const originalElse = '            else if(Text("action")=="rewire-inspect") {Rewire(Text("cadDirectory"));Inspect();}'
const finalStart = '                    if(Text("action")=="weight-readback-diagnostic")'
const finalElse = '                    else' + newline + '                    {' + newline

function adaptedParts() {
  const adapted = adaptWeightMain(original)
  const actionIndex = adapted.source.indexOf(actionStart)
  const actionEnd = adapted.source.indexOf(originalElse, actionIndex)
  const cleanupIndex = adapted.source.indexOf(finalStart)
  const cleanupEndIndex = adapted.source.indexOf(finalElse, cleanupIndex)
  return {
    ...adapted,
    action: adapted.source.slice(actionIndex, actionEnd),
    cleanup: adapted.source.slice(cleanupIndex, cleanupEndIndex).trimEnd(),
    wrappedCleanup: adapted.source.slice(cleanupIndex, cleanupEndIndex + finalElse.length + originalCleanup.length + newline.length + '                    }'.length),
  }
}

function temporary(t) {
  const parent = realpathSync.native(tmpdir()), path = mkdtempSync(join(parent, 'winnsen-weight-offline-'))
  t.after(() => {
    const tail = relative(parent, path)
    assert.ok(tail.startsWith('winnsen-weight-offline-') && !/[\\/]/.test(tail))
    rmSync(path, { recursive: true, force: true })
  })
  return path
}

test('temporary Main adapter changes exactly the two intended spans', () => {
  const adapted = adaptedParts()
  assert.equal(adapted.transformations.length, 2)
  assert.deepEqual(adapted.transformations.map(item => [item.id, item.matches]), [['main_action', 1], ['main_finally_cleanup', 1]])
  assert.ok(adapted.source.includes(originalCleanup))
  let restored = adapted.source.replace('// temporary diagnostic adapter; original sources remain frozen; see adapter-provenance.json' + newline, '')
  restored = restored.replace(adapted.action + originalElse, actionAnchor)
  restored = restored.replace(adapted.wrappedCleanup, originalCleanup)
  assert.equal(restored, original)
  assert.match(adapted.action, /WeightReadback\.Run\(Sw,Text\("cadDirectory"\),Job,/)
  assert.match(adapted.action, /inspection="weight_readback_progress"/)
  assert.match(adapted.action, /inspection="weight_readback_diagnostic"/)
})

test('missing, duplicate or previously adapted action and cleanup anchors fail closed', () => {
  assert.throws(() => adaptWeightMain(original.replace(actionAnchor, '')), /EXACT_ONE_REQUIRED main_action count=0/)
  assert.throws(() => adaptWeightMain(original + newline + actionAnchor), /EXACT_ONE_REQUIRED main_action count=2/)
  assert.throws(() => adaptWeightMain(original.replace(originalCleanup, '')), /EXACT_ONE_REQUIRED main_finally_cleanup count=0/)
  assert.throws(() => adaptWeightMain(original + newline + originalCleanup), /EXACT_ONE_REQUIRED main_finally_cleanup count=2/)
  assert.throws(() => adaptWeightMain(adaptWeightMain(original).source), /FRESH_MAIN_REQUIRED/)
})

test('weight final cleanup only inventories documents and has no broad closing call', () => {
  const adapted = adaptedParts()
  assert.doesNotMatch(adapted.cleanup, /CloseDoc|CloseOwned|OwnedDocument|cadDirectory/)
  assert.match(adapted.cleanup, /inspection="weight_readback_remaining_documents"/)
  assert.match(adapted.cleanup, /remainingPaths\.Count==remainingCount/)
  assert.match(adapted.cleanup, /remainingPaths\.Count==0&&remainingCount==0/)
  assert.equal((adapted.cleanup.match(/Sw\.ExitApp\(\)/g) || []).length, 1)
  assert.ok(adapted.cleanup.indexOf('WEIGHT_FINAL_DOCUMENTS_REMAIN_SESSION_PRESERVED') < adapted.cleanup.indexOf('Sw.ExitApp()'))
})

test('cold cache baseline precedes every noncached read, explicit activation and rebuild', () => {
  const source = readFileSync(join(HERE, 'WeightReadback.cs'), 'utf8')
  const start = source.indexOf('    static void ReadTarget('), end = source.indexOf('    public static object Run(', start)
  const target = source.slice(start, end)
  const cached = target.indexOf('row["cachedWeightBaseline"] = ReadCachedWeightBaseline(')
  assert.ok(cached > 0 && cached < target.indexOf('row["firstColdPropertyRead"]'))
  assert.ok(cached < target.indexOf('ReadConfiguration(c, model'))
  assert.match(target, /if \(model != null && baselineCaptured\)/)
  const baseline = source.slice(source.indexOf('    static object ReadCachedWeightBaseline('), source.indexOf('    static object ReadScopes('))
  assert.match(baseline, /Get6\("重量", true,/)
  assert.doesNotMatch(baseline, /Get6\([^;]*false|ShowConfiguration2|ForceRebuild3/)
  for (const name of ['raw', 'resolved', 'status', 'wasResolved', 'linked', 'activeConfigurationBefore', 'activeConfigurationAfter']) assert.ok(baseline.includes('"' + name + '"'))
})

test('generated action and final cleanup behavior pass a managed offline harness', t => {
  const path = temporary(t), csPath = join(path, 'AdapterBehavior.cs'), exe = join(path, 'AdapterBehavior.exe')
  const adapted = adaptedParts()
  const fixture = String.raw`using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
internal sealed class ModelDoc2 { internal string Path; internal string GetPathName(){return Path;} }
internal sealed class ManagedSw {
    internal object[] Documents=new object[0]; internal int DeclaredCount; internal int Exits; internal int Closes;
    internal object GetDocuments(){return Documents;} internal int GetDocumentCount(){return DeclaredCount;}
    internal void ExitApp(){Exits++;} internal void CloseDoc(string title){Closes++;throw new Exception("BROAD_CLOSE_FORBIDDEN");}
}
internal static class WeightReadback {
    internal static object Result;
    internal static object Run(object sw,string root,Dictionary<string,object> job,Action<object> progress){progress(new {stage="offline"});return Result;}
}
internal static class AdapterBehavior {
    static ManagedSw Sw=new ManagedSw();static Dictionary<string,object> Job=new Dictionary<string,object>();static int Passed;static int Recorded;
    static string Text(string key){return key=="action"?"weight-readback-diagnostic":"offline-copy";}
    static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Record(object value){Recorded++;}static void Release(object value){}
    static object[] Items(object value){return ((IEnumerable)value).Cast<object>().ToArray();}
    static void RunAction(){
` + adapted.action + String.raw`
    }
    static void RunCleanup(){
` + adapted.cleanup + String.raw`
    }
    static void Pass(string name,bool value){if(!value)throw new Exception("FAIL "+name);Passed++;Console.WriteLine("PASS "+name);}
    static void Reject(string name,Action call,string expected){try{call();}catch(Exception e){Pass(name,e.Message.Contains(expected));return;}throw new Exception("FAIL accepted "+name);}
    public static int Main(){
        try{
            object[] invalid={null,new object(),new Dictionary<string,object>(),new Dictionary<string,object>{{"success",true}},new Dictionary<string,object>{{"readbackComplete",true}},new Dictionary<string,object>{{"success","true"},{"readbackComplete",true}},new Dictionary<string,object>{{"success",true},{"readbackComplete","true"}},new Dictionary<string,object>{{"success",1},{"readbackComplete",true}},new Dictionary<string,object>{{"success",false},{"readbackComplete",true}},new Dictionary<string,object>{{"success",true},{"readbackComplete",false}}};
            for(int index=0;index<invalid.Length;index++){WeightReadback.Result=invalid[index];Reject("malformed or incomplete diagnostic "+index,RunAction,"WEIGHT_READBACK_DIAGNOSTIC_INCOMPLETE");}
            WeightReadback.Result=new Dictionary<string,object>{{"success",true},{"readbackComplete",true}};RunAction();Pass("strict boolean success and readback complete accepted",Recorded==22);
            Sw=new ManagedSw();RunCleanup();Pass("empty inventory is the only exit path",Sw.Exits==1&&Sw.Closes==0);
            Sw=new ManagedSw{Documents=new object[]{new ModelDoc2{Path="offline-copy/owned-looking.fixture"}},DeclaredCount=1};Reject("remaining copy-root document preserves session",RunCleanup,"WEIGHT_FINAL_DOCUMENTS_REMAIN_SESSION_PRESERVED");Pass("copy-root path grants no close permission",Sw.Exits==0&&Sw.Closes==0&&Sw.Documents.Length==1);
            Sw=new ManagedSw{Documents=new object[]{new object()},DeclaredCount=1};Reject("unknown document preserves session",RunCleanup,"WEIGHT_FINAL_DOCUMENTS_REMAIN_SESSION_PRESERVED");Pass("unknown document is untouched",Sw.Exits==0&&Sw.Closes==0);
            Sw=new ManagedSw{DeclaredCount=1};Reject("inconsistent empty inventory preserves session",RunCleanup,"WEIGHT_FINAL_DOCUMENT_COUNT_MISMATCH_SESSION_PRESERVED");Pass("count mismatch cannot exit",Sw.Exits==0&&Sw.Closes==0);
            Console.WriteLine("ADAPTER_BEHAVIOR_PASS "+Passed);return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}`
  writeFileSync(csPath, fixture, { flag: 'wx' })
  const compiled = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', '/out:' + exe, csPath], { windowsHide: true, encoding: 'utf8' })
  assert.equal(compiled.error, undefined)
  assert.equal(compiled.status, 0, compiled.stdout + compiled.stderr)
  const executed = spawnSync(exe, [], { windowsHide: true, encoding: 'utf8' })
  assert.equal(executed.error, undefined)
  assert.equal(executed.status, 0, executed.stdout + executed.stderr)
  assert.match(executed.stdout, /ADAPTER_BEHAVIOR_PASS 18/)
  console.log(executed.stdout.trim())
})

test('isolated x64 build validates generated source manifest without changing root sources or current pointer', () => {
  const current = join(DATA, 'toolchain-current.json'), currentBefore = existsSync(current) ? readFileSync(current) : null
  const built = buildWeightToolchain()
  assert.equal(validateToolchain(built.manifest, { sourceRoot: built.sourceRoot }), built.manifest)
  assert.deepEqual(readdirSync(built.sourceRoot).sort(), ['TrialAssembly.cs', 'TrialOutput.cs', 'VariableTrial.cs', 'WeightReadback.cs'])
  assert.equal(dirname(built.sourceRoot), dirname(built.manifest.exe))
  const provenance = read(built.provenancePath)
  assert.equal(provenance.description, 'temporary diagnostic adapter')
  assert.equal(provenance.rootSourcesUnmodified, true)
  assert.equal(provenance.production_release, false)
  assert.deepEqual(provenance.originalSources.map(({ path, sha256 }) => ({ path, sha256 })).sort((a, b) => a.path.localeCompare(b.path)), [...rootBefore].sort((a, b) => a.path.localeCompare(b.path)))
  const generatedMain = readFileSync(join(built.sourceRoot, 'VariableTrial.cs'), 'utf8')
  assert.equal(generatedMain, adaptWeightMain(original).source)
  assert.deepEqual(rootPaths.map(path => ({ path, sha256: hash(path) })), rootBefore)
  if (currentBefore === null) assert.equal(existsSync(current), false)
  else assert.deepEqual(readFileSync(current), currentBefore)
  assert.equal(hash(join(built.sourceRoot, 'WeightReadback.cs')), hash(join(HERE, 'WeightReadback.cs')))
  for (const item of built.manifest.sources) assert.equal(hash(item.archivePath), item.sha256)
  for (const item of provenance.transformations) assert.match(item.originalSha256, /^[A-F0-9]{64}$/)
  console.log('WEIGHT_OFFLINE_BUILT ' + JSON.stringify({ ...built, rootSources: rootBefore, currentPointerUnchanged: true, solidWorksStarted: false }))
})
