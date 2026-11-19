import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, realpathSync, rmSync, writeFileSync, copyFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { tmpdir } from 'node:os'
import { spawnSync } from 'node:child_process'
import { SOURCE, INTEROP_DLLS } from './common.mjs'

const CSC = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const REDIST = 'D:/soildworks2020/SOLIDWORKS/api/redist'

function temporary(t) {
  const parent = realpathSync.native(tmpdir())
  const dir = mkdtempSync(join(parent, 'winnsen-sldprocmon-'))
  t.after(() => {
    const tail = relative(parent, dir)
    assert.ok(tail.startsWith('winnsen-sldprocmon-') && !/[\\/]/.test(tail))
    rmSync(dir, { recursive: true, force: true })
  })
  return dir
}

function compileHarness(dir) {
  const dllArgs = INTEROP_DLLS.map(name => {
    const target = join(dir, name)
    copyFileSync(join(REDIST, name), target)
    return '/reference:' + target
  })

  const fixtureCs = join(dir, 'MonitorFixture.cs')
  const parentExe = join(dir, 'fake-cad.exe')
  const monitorExe = join(dir, 'fake-monitor.exe')
  const disguisedDir = join(dir, 'disguised')
  mkdirSync(disguisedDir, { recursive: true })
  const disguisedExe = join(disguisedDir, 'fake-monitor.exe')

  writeFileSync(fixtureCs, String.raw`
using System;
using System.Diagnostics;
using System.Threading;
internal static class MonitorFixture
{
    static int ParseMs(string[] args, int index, int fallback)
    {
        int value;
        return args.Length > index && int.TryParse(args[index], out value) ? value : fallback;
    }
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "parent")
        {
            string childExe = args[1];
            int childSleepMs = ParseMs(args, 2, 1000);
            int parentSleepMs = ParseMs(args, 3, 200);
            int selfPid = Process.GetCurrentProcess().Id;
            long selfTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
            var psi = new ProcessStartInfo(childExe, "--ppid=" + selfPid + " child " + childSleepMs) { UseShellExecute = false, CreateNoWindow = true };
            using (Process child = Process.Start(psi))
            {
                Console.WriteLine(selfPid + "|" + selfTicks + "|" + child.Id + "|" + child.StartTime.ToUniversalTime().Ticks);
                Console.Out.Flush();
                Thread.Sleep(parentSleepMs);
            }
            return 0;
        }
        Thread.Sleep(ParseMs(args, 2, 60000));
        return 0;
    }
}
`, 'utf8')

  const compileFixture = spawnSync(CSC, ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/out:' + parentExe, fixtureCs], { windowsHide: true, encoding: 'utf8' })
  assert.equal(compileFixture.status, 0, compileFixture.stdout + compileFixture.stderr)
  copyFileSync(parentExe, monitorExe)
  copyFileSync(parentExe, disguisedExe)

  const driverCs = join(dir, 'SldProcMonDriver.cs')
  const driverExe = join(dir, 'SldProcMonDriver.exe')
  writeFileSync(driverCs, String.raw`
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;
internal static class SldProcMonDriver
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static void Need(bool ok, string msg) { if (!ok) throw new InvalidOperationException(msg); }
    static Process StartProcess(string exe, string args)
    {
        return Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
    }
    public static int Main(string[] args)
    {
        string approvedExe = args[0];
        string disguisedExe = args[1];
        string parentExe = args[2];

        // 1. Identity matcher contract
        long t0 = 639303000000000000L;
        Need(VariableTrial.IsOwnedMonitorIdentity("fake-monitor.exe", 123, approvedExe, "\"" + approvedExe + "\" --ppid=123 --x=1", t0 + 1, 123, t0, approvedExe), "VALID_IDENTITY_REJECTED");
        Need(VariableTrial.IsOwnedMonitorIdentity("FAKE-MONITOR.EXE", 123, approvedExe, "--ppid=123", t0, 123, t0, approvedExe), "EQUAL_TICKS_CASE_REJECTED");
        Need(!VariableTrial.IsOwnedMonitorIdentity("other.exe", 123, approvedExe, "--ppid=123", t0 + 1, 123, t0, approvedExe), "WRONG_NAME_ACCEPTED");
        Need(!VariableTrial.IsOwnedMonitorIdentity("fake-monitor.exe", 999, approvedExe, "--ppid=123", t0 + 1, 123, t0, approvedExe), "WRONG_PPID_ACCEPTED");
        Need(!VariableTrial.IsOwnedMonitorIdentity("fake-monitor.exe", 123, approvedExe, "--ppid=123", t0 - 1, 123, t0, approvedExe), "PREDATING_START_ACCEPTED");
        Need(!VariableTrial.IsOwnedMonitorIdentity("fake-monitor.exe", 123, disguisedExe, "--ppid=123", t0 + 1, 123, t0, approvedExe), "DISGUISED_PATH_ACCEPTED");
        Need(!VariableTrial.IsOwnedMonitorIdentity("fake-monitor.exe", 123, approvedExe, "--ppid=1234", t0 + 1, 123, t0, approvedExe), "PPID_PREFIX_COLLISION_ACCEPTED");
        Need(!VariableTrial.IsOwnedMonitorIdentity("fake-monitor.exe", 123, approvedExe, "", t0 + 1, 123, t0, approvedExe), "EMPTY_CMD_ACCEPTED");

        // 2. Refuse cleanup while main CAD is alive; clean graceful wait when child exits fast
        using (Process bystander = StartProcess(approvedExe, "--ppid=999999 child 60000"))
        {
            try
            {
                using (Process fastParent = StartProcess(parentExe, "parent \"" + approvedExe + "\" 1800 1000"))
                {
                    string[] parts = fastParent.StandardOutput.ReadLine().Split('|');
                    int cadPid = int.Parse(parts[0]); long cadTicks = long.Parse(parts[1]); int childPid = int.Parse(parts[2]);
                    Need(VariableTrial.FindOwnedMonitorPids(cadPid, cadTicks, approvedExe).Length == 1, "FIND_OWNED_MISSING_FAST_CHILD");
                    bool threwWhileAlive = false;
                    try { VariableTrial.CleanupOwnedMonitors(cadPid, cadTicks, new[] { childPid }, 100, "while_alive", _ => {}, approvedExe); }
                    catch (InvalidOperationException ex) { threwWhileAlive = ex.Message.Contains("MAIN_CAD_MUST_BE_ABSENT_BEFORE_MONITOR_CLEANUP"); }
                    Need(threwWhileAlive, "CLEANUP_ALLOWED_WHILE_CAD_ALIVE");
                    fastParent.WaitForExit(5000);
                    var graceEvents = new List<object>();
                    bool graceOk = VariableTrial.CleanupOwnedMonitors(cadPid, cadTicks, new[] { childPid }, 3000, "grace_exit", e => graceEvents.Add(e), approvedExe);
                    Need(graceOk && graceEvents.Count == 0, "NATURAL_EXIT_SHOULD_NOT_KILL");
                }

                // 3. Force terminate genuinely hung residual monitor after main CAD exits while leaving bystander/disguised processes untouched
                using (Process disguisedChild = StartProcess(disguisedExe, "--ppid=1 child 60000"))
                using (Process hangParent = StartProcess(parentExe, "parent \"" + approvedExe + "\" 60000 100"))
                {
                    try
                    {
                        string[] parts = hangParent.StandardOutput.ReadLine().Split('|');
                        int cadPid = int.Parse(parts[0]); long cadTicks = long.Parse(parts[1]); int hungPid = int.Parse(parts[2]);
                        hangParent.WaitForExit(5000);
                        Need(!VariableTrial.IsCadProcessAlive(cadPid, cadTicks), "HANG_PARENT_STILL_ALIVE");
                        var killEvents = new List<object>();
                        bool ok = VariableTrial.CleanupOwnedMonitors(cadPid, cadTicks, new[] { hungPid }, 400, "after_cad_exit", e => killEvents.Add(e), approvedExe);
                        Need(ok, "HUNG_MONITOR_CLEANUP_FAILED");
                        Need(killEvents.Count == 1, "EXPECTED_ONE_TERMINATION_EVENT");
                        var payload = Json.Serialize(killEvents[0]);
                        Need(payload.Contains("\"inspection\":\"residual_monitor_terminated\"") && payload.Contains("\"exited\":true") && payload.Contains("\"mainCadAlreadyExited\":true"), "BAD_TERMINATION_EVENT " + payload);
                        bool childDead = false;
                        try { using (Process check = Process.GetProcessById(hungPid)) childDead = check.HasExited; } catch (ArgumentException) { childDead = true; }
                        Need(childDead, "HUNG_MONITOR_STILL_ALIVE");
                        bystander.Refresh(); disguisedChild.Refresh();
                        Need(!bystander.HasExited && !disguisedChild.HasExited, "BYSTANDER_OR_DISGUISED_WAS_TOUCHED");
                    }
                    finally { try { if (!disguisedChild.HasExited) { disguisedChild.Kill(); disguisedChild.WaitForExit(3000); } } catch {} }
                }
            }
            finally { try { if (!bystander.HasExited) { bystander.Kill(); bystander.WaitForExit(3000); } } catch {} }
        }
        Console.WriteLine("SLDPROCMON_OFFLINE_PASS");
        return 0;
    }
}
`, 'utf8')

  const sources = ['TrialAssembly.cs', 'TrialOutput.cs', 'TrialProductionCraft.cs', 'TrialProductionDxfMarks.cs', 'VariableTrial.cs'].map(name => join(SOURCE, name))
  const compileDriver = spawnSync(CSC, [
    '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001',
    '/out:' + driverExe, '/main:SldProcMonDriver',
    '/reference:System.Web.Extensions.dll', '/reference:System.Management.dll',
    ...dllArgs, ...sources, driverCs,
  ], { windowsHide: true, encoding: 'utf8' })
  assert.equal(compileDriver.status, 0, compileDriver.stdout + compileDriver.stderr)

  return { driverExe, monitorExe, disguisedExe, parentExe }
}

test('sldProcMon identity validation and post-CAD residual cleanup pass offline harness', t => {
  const dir = temporary(t)
  const { driverExe, monitorExe, disguisedExe, parentExe } = compileHarness(dir)
  const run = spawnSync(driverExe, [monitorExe, disguisedExe, parentExe], { windowsHide: true, encoding: 'utf8' })
  assert.equal(run.error, undefined)
  assert.equal(run.status, 0, run.stdout + run.stderr)
  assert.match(run.stdout, /SLDPROCMON_OFFLINE_PASS/)
})
