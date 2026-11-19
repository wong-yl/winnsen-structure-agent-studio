import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, realpathSync, rmSync, writeFileSync, readFileSync, copyFileSync, existsSync } from 'node:fs'
import { join, relative, dirname } from 'node:path'
import { tmpdir } from 'node:os'
import { spawnSync } from 'node:child_process'
import { EventEmitter } from 'node:events'
import { PassThrough } from 'node:stream'
import { SOURCE, INTEROP_DLLS, acquireLock, directory, hash, read, write, TOOLCHAIN_SCHEMA } from './common.mjs'
import { archiveSources } from './build.mjs'
import { runNativeJob, runProtectedPhase } from './run.mjs'

const CSC = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const REDIST = 'D:/soildworks2020/SOLIDWORKS/api/redist'

function temporary(t) {
  const parent = realpathSync.native(tmpdir())
  const dir = mkdtempSync(join(parent, 'winnsen-com-timeout-'))
  t.after(() => {
    const tail = relative(parent, dir)
    assert.ok(tail.startsWith('winnsen-com-timeout-') && !/[\\/]/.test(tail))
    rmSync(dir, { recursive: true, force: true })
  })
  return dir
}

function rawFile(path, content = 'fixture') {
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, content)
  return path
}

function stripQuotedStrings(source) {
  return source
    .replace(/@"(?:""|[^"])*"/g, '""')
    .replace(/"(?:\\.|[^"\\])*"/g, '""')
}

test('all blocking SolidWorks COM calls route through timeout wrappers with zero unwrapped GetDocuments calls', () => {
  const blockedPattern = /\.(GetDocuments|GetDocumentCount|CloseDoc|CloseAllDocuments|ExitApp|GetProcessID|OpenDoc6|NewDocument|ActivateDoc3|ActiveDoc|ForceRebuild3|Save3|SaveAs|ShowConfiguration2|UpdateExternalFileReferences|AddComponent5|SetTransformAndSolve2|GetInterferences|GetInterferenceCount|Done|SetUserPreferenceToggle|GetUserPreferenceToggle|GetUserPreferenceStringValue|GetMathUtility|GetDocumentDependencies2|ReplaceReferencedDocument)\b/g
  for (const name of ['VariableTrial.cs', 'TrialAssembly.cs', 'TrialOutput.cs']) {
    let text = readFileSync(join(SOURCE, name), 'utf8')
    if (name === 'VariableTrial.cs') {
      const start = text.indexOf('    internal static object[] SwGetDocuments(')
      const endAnchor = '    internal static void InterferenceDone('
      const endStart = text.indexOf(endAnchor, start)
      const endBrace = text.indexOf('\n    }', endStart)
      assert.ok(start > 0 && endStart > start && endBrace > endStart, 'wrapper region missing in VariableTrial.cs')
      text = text.slice(0, start) + '\n// WRAPPER_REGION_SKIPPED\n' + text.slice(endBrace + '\n    }'.length)
    }
    const stripped = stripQuotedStrings(text)
    const matches = stripped.match(blockedPattern) || []
    assert.deepEqual(matches, [], `${name} has unwrapped COM calls: ${matches.join(', ')}`)
  }
})

test('CallComWithTimeout and CallComWithArgTimeout enforce timeouts, refuse follow-ups, and bound cleanup', t => {
  const dir = temporary(t)
  const dllArgs = INTEROP_DLLS.map(name => {
    const target = join(dir, name)
    copyFileSync(join(REDIST, name), target)
    return '/reference:' + target
  })
  const driverCs = join(dir, 'ComTimeoutDriver.cs')
  const driverExe = join(dir, 'ComTimeoutDriver.exe')
  const resultJson = join(dir, 'native-result.json')

  writeFileSync(driverCs, String.raw`
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

internal static class ComTimeoutDriver
{
    static void Need(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }

    static void SetPrivateStatic(string name, object value)
    {
        var field = typeof(VariableTrial).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        Need(field != null, "MISSING_FIELD " + name);
        field.SetValue(null, value);
    }

    static void ResetState(string resultPath, Dictionary<string, object> timeouts)
    {
        var job = new Dictionary<string, object> { { "action", "com-timeout-unit" } };
        if (timeouts != null) job["comTimeouts"] = timeouts;
        SetPrivateStatic("Job", job);
        SetPrivateStatic("ResultPath", resultPath);
        SetPrivateStatic("Events", new List<object>());
        SetPrivateStatic("EventTimes", new List<object>());
        VariableTrial.ComTimedOut = false;
        SetPrivateStatic("LastComTimeoutError", null);
        SetPrivateStatic("DiagnosticDelayConsumed", 0);
        VariableTrial.ComTimeoutObserver = null;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        string resultPath = args[0];

        // 1. Normal synchronous-like completion and secondary arg forwarding under Job["comTimeouts"]
        ResetState(resultPath, new Dictionary<string, object> { { "NormalOp", 2000 }, { "ArgOp", 2000 } });
        string v1 = VariableTrial.CallComWithTimeout(new object(), "NormalOp", _ => "ok-normal", 1000, "UnitCtx");
        Need(v1 == "ok-normal", "UNEXPECTED_V1");
        int v2 = VariableTrial.CallComWithArgTimeout("left", "right", "ArgOp", (a, b) => (a + ":" + b).Length, 1000, "UnitCtx");
        Need(v2 == "left:right".Length, "UNEXPECTED_V2");
        Need(!VariableTrial.ComTimedOut, "UNEXPECTED_TIMEOUT_FLAG");

        // 2. Context-specific override beats method-only key and fires timeout, observer, and native-result event
        ResetState(resultPath, new Dictionary<string, object> { { "SlowOp", 5000 }, { "SlowCtx:SlowOp", 150 } });
        var observed = new List<Dictionary<string, object>>();
        VariableTrial.ComTimeoutObserver = data => observed.Add(data);
        Exception caught = null;
        try
        {
            VariableTrial.CallComWithTimeout(new object(), "SlowOp", _ => { Thread.Sleep(900); return 1; }, 5000, "SlowCtx");
        }
        catch (TimeoutException ex) { caught = ex; }
        Need(caught != null, "EXPECTED_TIMEOUT_EXCEPTION");
        Need(caught.Message.StartsWith("CAD_COM_CALL_TIMEOUT method=SlowOp context=SlowCtx waitedMs=", StringComparison.Ordinal), "BAD_MSG " + caught.Message);
        Need(caught.Message.Contains("timeoutMs=150"), "MISSING_TIMEOUT_MS " + caught.Message);
        Need(VariableTrial.ComTimedOut, "EXPECTED_COM_TIMED_OUT_TRUE");
        Need(observed.Count == 1, "EXPECTED_OBSERVER_ONCE");
        Need(Convert.ToString(observed[0]["method"]) == "SlowOp", "BAD_OBSERVER_METHOD");
        Need(Convert.ToString(observed[0]["context"]) == "SlowCtx", "BAD_OBSERVER_CONTEXT");
        Need(Convert.ToInt32(observed[0]["timeoutMs"]) == 150, "BAD_OBSERVER_TIMEOUT");
        Need(Convert.ToBoolean(observed[0]["allowAfterTimeout"]) == false, "BAD_OBSERVER_ALLOW");

        // 3. Subsequent non-cleanup COM calls fail immediately without running delegate
        bool postTimeoutRan = false;
        Exception refused = null;
        try
        {
            VariableTrial.CallComWithTimeout(new object(), "AfterTimeoutCall", _ => { postTimeoutRan = true; return 99; }, 1000, "PostCtx", false);
        }
        catch (TimeoutException ex) { refused = ex; }
        Need(refused != null && refused.Message.Contains("CAD_COM_CALL_TIMEOUT"), "EXPECTED_POST_TIMEOUT_REFUSAL");
        Need(!postTimeoutRan, "POST_TIMEOUT_DELEGATE_MUST_NOT_RUN");

        // 4. allowAfterTimeout=true allows fast cleanup call, and still bounds slow cleanup call
        int cleanupVal = VariableTrial.CallComWithTimeout(new object(), "FastCleanup", _ => 77, 1000, "CleanupCtx", true);
        Need(cleanupVal == 77, "EXPECTED_CLEANUP_VALUE");

        var jobField = typeof(VariableTrial).GetField("Job", BindingFlags.NonPublic | BindingFlags.Static);
        var jobMap = (Dictionary<string, object>)jobField.GetValue(null);
        var timeouts = (Dictionary<string, object>)jobMap["comTimeouts"];
        timeouts["CleanupCtx:SlowCleanup"] = 150;
        var sw = Stopwatch.StartNew();
        Exception cleanupTimeout = null;
        try
        {
            VariableTrial.CallComWithTimeout(new object(), "SlowCleanup", _ => { Thread.Sleep(900); return 0; }, 1000, "CleanupCtx", true);
        }
        catch (TimeoutException ex) { cleanupTimeout = ex; }
        sw.Stop();
        Need(cleanupTimeout != null && cleanupTimeout.Message.Contains("CAD_COM_CALL_TIMEOUT method=SlowCleanup context=CleanupCtx"), "BAD_CLEANUP_TIMEOUT");
        Need(sw.ElapsedMilliseconds < 700, "CLEANUP_TIMEOUT_TOO_SLOW " + sw.ElapsedMilliseconds);

        Console.WriteLine("COM_TIMEOUT_UNIT_PASS");
        return 0;
    }
}
`, 'utf8')

  const sources = ['TrialAssembly.cs', 'TrialOutput.cs', 'TrialProductionCraft.cs', 'TrialProductionDxfMarks.cs', 'VariableTrial.cs'].map(name => join(SOURCE, name))
  const compile = spawnSync(CSC, [
    '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001',
    '/out:' + driverExe, '/main:ComTimeoutDriver',
    '/reference:System.Web.Extensions.dll', '/reference:System.Management.dll',
    ...dllArgs, ...sources, driverCs,
  ], { windowsHide: true, encoding: 'utf8' })
  assert.equal(compile.status, 0, compile.stdout + compile.stderr)

  const run = spawnSync(driverExe, [resultJson], { windowsHide: true, encoding: 'utf8' })
  assert.equal(run.error, undefined)
  assert.equal(run.status, 0, run.stdout + run.stderr)
  assert.match(run.stdout, /COM_TIMEOUT_UNIT_PASS/)

  const recorded = read(resultJson)
  const timeoutEvents = recorded.events.filter(e => e.inspection === 'com_call_timeout')
  assert.equal(timeoutEvents.length, 2)
  assert.equal(timeoutEvents[0].method, 'SlowOp')
  assert.equal(timeoutEvents[0].context, 'SlowCtx')
  assert.equal(timeoutEvents[0].timeoutMs, 150)
  assert.equal(timeoutEvents[1].method, 'SlowCleanup')
  assert.equal(timeoutEvents[1].allowAfterTimeout, true)
})

test('runProtectedPhase releases shared lock and writes supervision + protection-result after CAD_COM_CALL_TIMEOUT exit', async t => {
  const root = temporary(t)
  const dataRoot = join(root, 'data', 'trial')
  const sourceRoot = join(root, 'source')
  const seed = join(root, 'seed')
  const bin = join(dataRoot, 'toolchain', 'fixture-v2')

  rawFile(join(sourceRoot, 'VariableTrial.cs'), 'source fixture\n')
  rawFile(join(sourceRoot, 'TrialAssembly.cs'), 'assembly fixture\n')
  rawFile(join(sourceRoot, 'TrialOutput.cs'), 'output fixture\n')
  rawFile(join(seed, 'panel.SLDPRT'), 'protected seed bytes')
  mkdirSync(join(root, 'tools'), { recursive: true })
  mkdirSync(join(root, 'data', 'master_v2', 'reports'), { recursive: true })
  rawFile(join(root, 'docs', 'PLAN_方案A+_工艺内建参数化母版_20261112.md'), 'protected plan fixture')
  mkdirSync(join(root, 'data', 'native_model_requests'), { recursive: true })

  directory(bin, dataRoot, true)
  const sources = archiveSources(sourceRoot, bin, { dataRoot })
  const exe = rawFile(join(bin, 'VariableTrial.exe'), 'offline fake executable')
  const dlls = INTEROP_DLLS.map(name => { const path = rawFile(join(bin, name), 'fake ' + name); return { path, sha256: hash(path) } })
  write(join(dataRoot, 'toolchain-current.json'), { schema: TOOLCHAIN_SCHEMA, exe, sha256: hash(exe), sources, dlls }, { root: dataRoot })

  const lockDirectory = join(root, 'data', 'native_model_requests', '.native-worker-instance.lock')
  let lock = null
  const nativeProc = { name: 'VariableTrial.exe', pid: 2001, parentPid: 1, startedAtUtc: '2026-11-15T00:00:00.0000000Z', executable: exe }
  const cadProc = { name: 'SLDWORKS.exe', pid: 2002, parentPid: 2001, startedAtUtc: '2026-11-15T00:00:01.0000000Z', executable: 'D:/soildworks2020/SOLIDWORKS/SLDWORKS.exe' }

  let phaseStep = 0
  let capturedRunDir = null

  await assert.rejects(
    runProtectedPhase(
      { tag: 'timeout-mock', sourceDirectory: seed, job: { action: 'inspect' } },
      {
        root,
        dataRoot,
        seed,
        checkIdle() {},
        lockFactory() { lock = acquireLock({ lockDirectory, heartbeatMs: 60000 }); return lock },
        executeNative: (job, dir, opts) => {
          capturedRunDir = dir
          const child = new EventEmitter()
          child.pid = nativeProc.pid
          child.stdout = new PassThrough()
          child.stderr = new PassThrough()
          child.kill = () => {}
          return runNativeJob(job, dir, {
            ...opts,
            sourceRoot,
            pollMs: 5,
            spawnChild: () => child,
            queryProcesses: () => phaseStep === 0 ? [nativeProc, cadProc] : [],
            wait: async () => {
              if (phaseStep === 0) {
                phaseStep = 1
                child.stdout.write(JSON.stringify({ inspection: 'cad_owned', pid: cadProc.pid, startedAtUtc: cadProc.startedAtUtc, executable: cadProc.executable }) + '\n')
                child.stdout.write(JSON.stringify({ inspection: 'com_call_timeout', method: 'GetDocuments', context: 'FullBuild.AssertLoadedScope', waitedMs: 30005, timeoutMs: 30000 }) + '\n')
                child.stdout.write(JSON.stringify({ inspection: 'failure', error: 'System.TimeoutException: CAD_COM_CALL_TIMEOUT method=GetDocuments context=FullBuild.AssertLoadedScope waitedMs=30005 timeoutMs=30000' }) + '\n')
                child.stderr.write('CAD_COM_CALL_TIMEOUT method=GetDocuments context=FullBuild.AssertLoadedScope waitedMs=30005 timeoutMs=30000\n')
                child.emit('close', 1, null)
              }
            },
          })
        },
        queryProcesses: () => [],
        wait: async () => {},
      }
    ),
    /CAD_COM_CALL_TIMEOUT/
  )

  assert.ok(capturedRunDir)
  assert.equal(read(join(capturedRunDir, 'supervision.json')).state, 'cad_exited')
  assert.equal(read(join(capturedRunDir, 'supervision.json')).nativeEnded, true)
  assert.deepEqual(read(join(capturedRunDir, 'supervision.json')).cad, [])
  assert.equal(read(join(capturedRunDir, 'protection-result.json')).pass, true)
  assert.equal(existsSync(join(lockDirectory, 'owner.json')), false)
})
