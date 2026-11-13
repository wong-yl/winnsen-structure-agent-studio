using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WidthMonitorCleanupOffline
{
    internal sealed class ProcessModule
    {
        internal Process Owner;
        internal string FileName { get { if (!Owner.HandlePinned) throw new InvalidOperationException("path read before handle pin"); if (Owner.ThrowPath) throw new InvalidOperationException("path unreadable"); return Owner.PathValue; } }
    }

    internal sealed class Process : IDisposable
    {
        internal static readonly Dictionary<int, Process> Running = new Dictionary<int, Process>();
        internal static int LookupCalls;
        internal static bool ThrowLookup;
        internal const long DefaultStartTicks = 638800000000000000L;
        internal int Pid;
        internal string NameValue = "sldProcMon";
        internal string PathValue = @"D:\soildworks2020\SOLIDWORKS\sldProcMon.exe";
        internal long StartTicks = DefaultStartTicks;
        internal bool Exited;
        internal bool ThrowName, ThrowStart, ThrowPath, ThrowExited, ThrowKill, ThrowHandle, ThrowWait, ThrowStartArgument;
        internal bool ExitAfterKill = true, WaitResult = true, ExitDuringWait, HandlePinned;
        internal Action AfterStartRead, BeforeKill;
        internal int Kills, Waits, Handles;
        internal int Id { get { return Pid; } }
        internal bool HasExited { get { if (ThrowExited) throw new InvalidOperationException("exit state unreadable"); return Exited; } }
        internal string ProcessName { get { if (ThrowName) throw new InvalidOperationException("name unreadable"); return NameValue; } }
        internal IntPtr Handle { get { Handles++; if (ThrowHandle) throw new InvalidOperationException("handle unavailable"); HandlePinned = true; return new IntPtr(Pid + 1); } }
        internal DateTime StartTime
        {
            get
            {
                if (!HandlePinned) throw new InvalidOperationException("creation time read before handle pin");
                if (ThrowStart) throw new InvalidOperationException("start unreadable");
                if (ThrowStartArgument) throw new ArgumentException("start property failed");
                long ticks = StartTicks;
                if (AfterStartRead != null) { Action action = AfterStartRead; AfterStartRead = null; action(); }
                return new DateTime(ticks, DateTimeKind.Utc);
            }
        }
        internal ProcessModule MainModule { get { return new ProcessModule { Owner = this }; } }
        internal void Kill()
        {
            if (!HandlePinned) throw new InvalidOperationException("kill before handle pin");
            if (BeforeKill != null) { Action action = BeforeKill; BeforeKill = null; action(); }
            Kills++;
            if (ThrowKill) throw new InvalidOperationException("kill denied");
            if (Exited) throw new InvalidOperationException("original process already exited");
            if (ExitAfterKill) Exited = true;
        }
        internal bool WaitForExit(int timeout)
        {
            if (timeout <= 0) throw new InvalidOperationException("invalid timeout");
            if (!HandlePinned) throw new InvalidOperationException("wait before handle pin");
            Waits++;
            if (ThrowWait) throw new InvalidOperationException("wait unreadable");
            if (ExitDuringWait) Exited = true;
            return WaitResult;
        }
        public void Dispose() { }
        internal static Process GetProcessById(int pid)
        {
            LookupCalls++;
            if (ThrowLookup) throw new InvalidOperationException("lookup denied");
            Process process;
            if (!Running.TryGetValue(pid, out process)) throw new ArgumentException("PID absent");
            return process;
        }
        internal static Process[] GetProcessesByName(string name)
        {
            return Running.Values.Where(value => !value.Exited && string.Equals(value.NameValue, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        internal static void Reset() { Running.Clear(); LookupCalls = 0; ThrowLookup = false; }
    }

    internal static class NativeParametricWidth
    {
        internal sealed class Result
        {
            public bool success = true;
            public string status = "FUNCTIONAL_PASS", error = "";
            public readonly List<int> baseline_sldworks_process_ids = new List<int>();
            public readonly List<int> baseline_sldprocmon_process_ids = new List<int>();
            public readonly List<int> created_sldprocmon_process_ids = new List<int>();
            public readonly List<int> final_sldworks_process_ids = new List<int>();
            public readonly List<int> final_sldprocmon_process_ids = new List<int>();
            public readonly List<int> cleanup_refused_baseline_process_ids = new List<int>();
            public readonly List<int> cleanup_force_stopped_process_ids = new List<int>();
            public readonly List<int> cleanup_already_exited_process_ids = new List<int>();
            public readonly List<string> cleanup_errors = new List<string>();
            public readonly List<SessionRecord> sessions = new List<SessionRecord>();
        }

        /*__SESSION__*/
        /*__IDENTITY__*/
        /*__METHODS__*/

        private static void FinalGate(Result result)
        {
            int exitCode = 0;
            /*__ENUMERATE__*/
            /*__FINAL_GATE__*/
            GC.KeepAlive(exitCode);
        }

        private static readonly List<object> Cases = new List<object>();
        private static int Failed;
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static Process P(int pid, string name = "sldProcMon")
        {
            var process = new Process { Pid = pid, NameValue = name };
            if (name != "sldProcMon") process.PathValue = @"D:\unrelated\" + name + ".exe";
            Process.Running[pid] = process;
            return process;
        }
        private static Result R(out SessionRecord session)
        {
            var result = new Result();
            session = new SessionRecord { created = true, sldworks_process_id = 91, sldworks_start_utc_ticks = Process.DefaultStartTicks, process_exited = true };
            result.sessions.Add(session);
            return result;
        }
        private static MonitorProcessIdentity Own(SessionRecord session, int pid = 24236)
        {
            var identity = new MonitorProcessIdentity { process_id = pid, start_utc_ticks = Process.DefaultStartTicks,
                executable_path = @"D:\soildworks2020\SOLIDWORKS\sldProcMon.exe", captured_utc_ticks = DateTime.UtcNow.Ticks };
            session.sldprocmon_process_ids.Add(pid);
            session.sldprocmon_processes.Add(identity);
            return identity;
        }
        private static void Run(string name, Action action)
        {
            Process.Reset();
            try { action(); Cases.Add(new { name = name, status = "PASS" }); }
            catch (Exception error) { Failed++; Cases.Add(new { name = name, status = "FAIL", error = error.ToString() }); }
        }
        private static void Refused(Result result, MonitorProcessIdentity identity, Process process)
        {
            Check(result.cleanup_errors.Count > 0 && !identity.process_exited && process.Kills == 0, "unknown identity was treated as absent or killed");
            FinalGate(result);
            Check(!result.success && result.status == "OWNED_PROCESS_CLEANUP_GATE_FAILED", "unknown cleanup failure passed final gate");
        }
        private static int Main()
        {
            Run("capture records PID creation time and executable", delegate {
                SessionRecord session; var result = R(out session); P(91, "SLDWORKS"); var process = P(24236);
                CaptureSessionSldProcMon(result, session, new List<int>());
                Check(session.sldprocmon_processes.Count == 1, "identity not captured");
                var identity = session.sldprocmon_processes[0];
                Check(identity.process_id == process.Pid && identity.start_utc_ticks == process.StartTicks && identity.executable_path == process.PathValue, "captured identity differs");
                Check(identity.captured_utc_ticks >= identity.start_utc_ticks && identity.captured_utc_ticks <= DateTime.UtcNow.Ticks && process.Handles > 0, "capture time or pinned identity is missing");
                Check(result.cleanup_errors.Count == 0, "capture rejected valid identity");
                CaptureSessionSldProcMon(result, session, null);
                Check(session.sldprocmon_processes.Count == 1, "same identity captured twice");
            });
            Run("capture ignores baseline before-snapshot and nonexclusive processes", delegate {
                SessionRecord session; var result = R(out session); P(91, "SLDWORKS"); P(100); P(101);
                result.baseline_sldprocmon_process_ids.Add(100);
                CaptureSessionSldProcMon(result, session, new List<int> { 101 });
                Check(session.sldprocmon_processes.Count == 0, "baseline or before process assigned");
                P(92, "SLDWORKS"); P(102);
                CaptureSessionSldProcMon(result, session, null);
                Check(session.sldprocmon_processes.Count == 0, "monitor captured during nonexclusive CAD session");
            });
            foreach (string field in new[] { "name", "start", "path", "exit", "handle" })
            {
                string current = field;
                Run("capture unreadable " + field + " fails closed", delegate {
                    SessionRecord session; var result = R(out session); P(91, "SLDWORKS"); var process = P(24236);
                    process.ThrowName = current == "name"; process.ThrowStart = current == "start";
                    process.ThrowPath = current == "path"; process.ThrowExited = current == "exit";
                    process.ThrowHandle = current == "handle";
                    CaptureSessionSldProcMon(result, session, null);
                    Check(result.cleanup_errors.Count == 1 && session.sldprocmon_processes.Count == 0 &&
                        result.cleanup_already_exited_process_ids.Count == 0 && process.Kills == 0, "capture uncertainty was ignored");
                });
            }
            Run("capture rejects invalid executable identity", delegate {
                SessionRecord session; var result = R(out session); P(91, "SLDWORKS"); P(24236).PathValue = @"D:\unrelated\python.exe";
                CaptureSessionSldProcMon(result, session, null);
                Check(result.cleanup_errors.Count == 1 && session.sldprocmon_processes.Count == 0, "invalid executable was captured");
            });
            Run("capture allows a later owned monitor to reuse a retired PID", delegate {
                SessionRecord old; var result = R(out old); Own(old).process_exited = true;
                var current = new SessionRecord { created = true, sldworks_process_id = 91, sldworks_start_utc_ticks = Process.DefaultStartTicks, process_exited = true };
                result.sessions.Add(current); P(91, "SLDWORKS"); P(24236).StartTicks++;
                CaptureSessionSldProcMon(result, current, null);
                Check(current.sldprocmon_processes.Count == 1 && current.sldprocmon_processes[0].start_utc_ticks != old.sldprocmon_processes[0].start_utc_ticks, "PID-only assignment blocked new identity");
            });
            Run("capture requires the exact owner creation time", delegate {
                SessionRecord session; var result = R(out session); P(91, "SLDWORKS").StartTicks++; var process = P(24236);
                CaptureSessionSldProcMon(result, session, null);
                Check(session.sldprocmon_processes.Count == 0 && process.Kills == 0, "monitor assigned to reused owner PID");
            });
            Run("capture owner identity uncertainty remains an error", delegate {
                SessionRecord session; var result = R(out session); P(91, "SLDWORKS").ThrowStart = true; var process = P(24236);
                CaptureSessionSldProcMon(result, session, null);
                Check(session.sldprocmon_processes.Count == 0 && result.cleanup_errors.Count == 1 && process.Kills == 0, "unknown owner was used for capture");
            });
            Run("capture excludes monitors older than the owner or later than capture", delegate {
                SessionRecord session; var result = R(out session); P(91, "SLDWORKS"); var older = P(24236); older.StartTicks--;
                var future = P(24237); future.StartTicks = DateTime.UtcNow.AddMinutes(5).Ticks;
                CaptureSessionSldProcMon(result, session, null);
                Check(session.sldprocmon_processes.Count == 0 && older.Kills == 0 && future.Kills == 0, "unassociated monitor was captured");
                FinalGate(result); Check(!result.success, "unassigned monitors bypassed final gate");
            });
            Run("owned exact monitor is stopped and repeated sweep is idempotent", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236);
                CleanupSessionSldProcMon(result, session);
                Check(identity.process_exited && process.Kills == 1 && process.Waits == 1 && process.Handles > 0 && Process.LookupCalls == 1 && result.cleanup_errors.Count == 0, "owned process cleanup failed or used another instance");
                int queries = Process.LookupCalls;
                var reused = P(24236, "python"); reused.ThrowName = true;
                CleanupOnlyCreatedProcesses(result);
                Check(Process.LookupCalls == queries && reused.Kills == 0 && result.cleanup_errors.Count == 0, "retired identity was inspected again");
                FinalGate(result); Check(result.success, "unrelated python failed final monitor gate");
            });
            Run("historical monitor PID reused by python is preserved without false failure", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236, "python");
                CleanupOnlyCreatedProcesses(result);
                Check(identity.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 0 && result.cleanup_already_exited_process_ids.Contains(24236), "python PID reuse was killed or rejected");
                FinalGate(result); Check(result.success, "python PID reuse passed cleanup but failed final gate");
            });
            Run("same-name monitor with different creation time is preserved", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.StartTicks++;
                CleanupSessionSldProcMon(result, session);
                Check(identity.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 0, "different creation time was killed or falsely rejected");
                FinalGate(result); Check(!result.success, "unassigned live monitor bypassed final gate");
            });
            Run("different executable with same PID and time is preserved", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.PathValue = @"D:\unrelated\sldProcMon.exe";
                CleanupSessionSldProcMon(result, session);
                Check(identity.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 0, "different executable was killed");
            });
            Run("exact identity comparison ignores name and path case", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.NameValue = "SLDPROCMON"; process.PathValue = process.PathValue.ToUpperInvariant();
                CleanupSessionSldProcMon(result, session);
                Check(identity.process_exited && process.Kills == 1 && result.cleanup_errors.Count == 0, "case-only identity difference blocked cleanup");
            });
            Run("absent PID is terminal without errors", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session);
                CleanupOnlyCreatedProcesses(result);
                Check(identity.process_exited && result.cleanup_errors.Count == 0, "absent PID was treated as cleanup failure");
            });
            Run("monitor property ArgumentException is not PID absence", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.ThrowStartArgument = true;
                CleanupSessionSldProcMon(result, session); Refused(result, identity, process);
                Check(Process.LookupCalls == 1, "unknown property triggered another PID lookup");
            });
            Run("monitor exiting after identity check never targets reused PID", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var original = P(24236); Process replacement = null;
                original.AfterStartRead = delegate { original.Exited = true; replacement = P(24236, "python"); replacement.ThrowName = true; };
                CleanupSessionSldProcMon(result, session);
                Check(identity.process_exited && replacement.Kills == 0 && Process.LookupCalls == 1 && result.cleanup_errors.Count == 0, "cleanup reacquired and targeted replacement process");
            });
            Run("already exited monitor is terminal without kill", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.Exited = true;
                CleanupSessionSldProcMon(result, session);
                Check(identity.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 0, "exited monitor cleanup failed");
            });
            foreach (string field in new[] { "name", "start", "path", "exit", "handle" })
            {
                string current = field;
                Run("cleanup unreadable " + field + " refuses unknown identity", delegate {
                    SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236);
                    process.ThrowName = current == "name"; process.ThrowStart = current == "start";
                    process.ThrowPath = current == "path"; process.ThrowExited = current == "exit";
                    process.ThrowHandle = current == "handle";
                    CleanupSessionSldProcMon(result, session); Refused(result, identity, process);
                });
            }
            Run("lookup access failure refuses cleanup", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); Process.ThrowLookup = true;
                CleanupSessionSldProcMon(result, session); Refused(result, identity, process);
            });
            Run("missing captured identity is an error", delegate {
                SessionRecord session; var result = R(out session); session.sldprocmon_process_ids.Add(24236); var process = P(24236);
                CleanupOnlyCreatedProcesses(result);
                Check(result.cleanup_errors.Count > 0 && process.Kills == 0, "missing capture was accepted or killed");
            });
            foreach (string field in new[] { "start", "path", "capture" })
            {
                string current = field;
                Run("missing expected " + field + " refuses cleanup", delegate {
                    SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236);
                    if (current == "start") identity.start_utc_ticks = 0;
                    else if (current == "path") identity.executable_path = "";
                    else identity.captured_utc_ticks = 0;
                    CleanupSessionSldProcMon(result, session); Refused(result, identity, process);
                });
            }
            Run("baseline PID is never stopped", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); result.baseline_sldprocmon_process_ids.Add(24236);
                CleanupSessionSldProcMon(result, session);
                Check(process.Kills == 0 && !identity.process_exited && result.cleanup_refused_baseline_process_ids.Contains(24236), "baseline process protection changed");
                FinalGate(result); Check(!result.success, "baseline live monitor passed final gate");
            });
            Run("owned monitor surviving kill fails final gate", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.ExitAfterKill = false; process.WaitResult = false;
                CleanupSessionSldProcMon(result, session);
                Check(process.Kills == 1 && !identity.process_exited && result.cleanup_errors.Count == 1, "owned survivor was accepted");
                FinalGate(result); Check(!result.success, "owned survivor bypassed final gate");
            });
            Run("successful wait without exit proof is rejected", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.ExitAfterKill = false;
                CleanupSessionSldProcMon(result, session);
                Check(process.Kills == 1 && !identity.process_exited && result.cleanup_errors.Count == 1, "wait return replaced actual exit proof");
            });
            Run("false monitor wait is not recorded as completion even if exit is later visible", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.WaitResult = false;
                CleanupSessionSldProcMon(result, session);
                Check(!identity.process_exited && result.cleanup_force_stopped_process_ids.Count == 0 && result.cleanup_errors.Count == 1, "false wait was accepted");
            });
            Run("kill access failure remains an error", delegate {
                SessionRecord session; var result = R(out session); var identity = Own(session); var process = P(24236); process.ThrowKill = true;
                CleanupSessionSldProcMon(result, session);
                Check(process.Kills == 1 && !identity.process_exited && result.cleanup_errors.Count == 1, "kill failure was hidden");
            });
            Run("unassigned monitor remains rejected by unchanged final gate", delegate {
                SessionRecord session; var result = R(out session); var process = P(24236);
                CleanupOnlyCreatedProcesses(result); FinalGate(result);
                Check(process.Kills == 0 && !result.success && result.final_sldprocmon_process_ids.Contains(24236), "unassigned monitor was killed or accepted");
            });
            Run("exact owned SLDWORKS is killed with one pinned instance", delegate {
                SessionRecord session; var result = R(out session); session.process_exited = false; var process = P(91, "SLDWORKS");
                ForceStopOwnedSessionProcess(result, session);
                Check(session.process_exited && process.Kills == 1 && process.Waits == 1 && process.Handles > 0 && Process.LookupCalls == 1 && result.cleanup_errors.Count == 0, "owned SLDWORKS cleanup did not use one verified instance");
                FinalGate(result); Check(result.success, "confirmed SLDWORKS cleanup failed final gate");
            });
            foreach (string replacementName in new[] { "SLDWORKS", "python" })
            {
                string name = replacementName;
                Run("SLDWORKS cleanup preserves reused PID named " + name, delegate {
                    SessionRecord session; var result = R(out session); session.process_exited = false; var process = P(91, name); process.StartTicks++;
                    ForceStopOwnedSessionProcess(result, session);
                    Check(session.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 0 && result.cleanup_already_exited_process_ids.Contains(91), "reused SLDWORKS PID was killed or falsely rejected");
                    FinalGate(result); Check(result.success == (name == "python"), "global SLDWORKS final gate changed");
                });
            }
            Run("SLDWORKS exiting after verification never targets replacement", delegate {
                SessionRecord session; var result = R(out session); session.process_exited = false; var original = P(91, "SLDWORKS"); Process replacement = null;
                original.AfterStartRead = delegate { original.Exited = true; replacement = P(91, "SLDWORKS"); replacement.StartTicks++; replacement.ThrowStart = true; };
                ForceStopOwnedSessionProcess(result, session);
                Check(session.process_exited && replacement.Kills == 0 && Process.LookupCalls == 1 && result.cleanup_errors.Count == 0, "verified process was reacquired after exit");
                FinalGate(result); Check(!result.success, "replacement SLDWORKS bypassed global final gate");
            });
            foreach (string field in new[] { "name", "start", "exit", "handle", "lookup", "startArgument" })
            {
                string current = field;
                Run("SLDWORKS unknown " + field + " is not marked exited", delegate {
                    SessionRecord session; var result = R(out session); session.process_exited = false; var process = P(91, "SLDWORKS");
                    process.ThrowName = current == "name"; process.ThrowStart = current == "start"; process.ThrowExited = current == "exit";
                    process.ThrowHandle = current == "handle"; Process.ThrowLookup = current == "lookup"; process.ThrowStartArgument = current == "startArgument";
                    ForceStopOwnedSessionProcess(result, session);
                    Check(!session.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 1, "unknown SLDWORKS identity was accepted or killed");
                    FinalGate(result); Check(!result.success, "unknown SLDWORKS cleanup bypassed final gate");
                });
            }
            Run("SLDWORKS missing expected creation time fails closed", delegate {
                SessionRecord session; var result = R(out session); session.process_exited = false; session.sldworks_start_utc_ticks = 0; var process = P(91, "SLDWORKS");
                ForceStopOwnedSessionProcess(result, session);
                Check(!session.process_exited && process.Kills == 0 && result.cleanup_errors.Count == 1, "missing SLDWORKS identity was treated as exited");
            });
            Run("SLDWORKS baseline PID is never killed", delegate {
                SessionRecord session; var result = R(out session); session.process_exited = false; var process = P(91, "SLDWORKS"); result.baseline_sldworks_process_ids.Add(91);
                ForceStopOwnedSessionProcess(result, session);
                Check(!session.process_exited && process.Kills == 0 && result.cleanup_refused_baseline_process_ids.Contains(91), "baseline SLDWORKS protection changed");
            });
            Run("absent SLDWORKS is confirmed exited", delegate {
                SessionRecord session; var result = R(out session); session.process_exited = false;
                ForceStopOwnedSessionProcess(result, session);
                Check(session.process_exited && result.cleanup_errors.Count == 0, "absent SLDWORKS was rejected");
            });
            foreach (string failure in new[] { "survives", "waitFalseAfterExit", "waitTrueWithoutExit", "killDenied", "waitUnknown" })
            {
                string current = failure;
                Run("SLDWORKS cleanup rejects " + failure, delegate {
                    SessionRecord session; var result = R(out session); session.process_exited = false; var process = P(91, "SLDWORKS");
                    process.ExitAfterKill = current == "waitFalseAfterExit";
                    process.WaitResult = current != "survives" && current != "waitFalseAfterExit";
                    process.ThrowKill = current == "killDenied"; process.ThrowWait = current == "waitUnknown";
                    ForceStopOwnedSessionProcess(result, session);
                    Check(!session.process_exited && process.Kills == 1 && result.cleanup_errors.Count == 1, "failed SLDWORKS cleanup was accepted");
                    FinalGate(result); Check(!result.success, "SLDWORKS cleanup error bypassed final gate");
                });
            }
            foreach (string field in new[] { "name", "start", "exit", "handle", "lookup", "startArgument", "wait" })
            {
                string current = field;
                Run("SLDWORKS wait unknown " + field + " is not exit evidence", delegate {
                    SessionRecord session; var result = R(out session); session.process_exited = false; var process = P(91, "SLDWORKS");
                    process.ThrowName = current == "name"; process.ThrowStart = current == "start"; process.ThrowExited = current == "exit";
                    process.ThrowHandle = current == "handle"; Process.ThrowLookup = current == "lookup";
                    process.ThrowStartArgument = current == "startArgument"; process.ThrowWait = current == "wait";
                    Check(!WaitForExactProcessExit(session, 1000) && !session.process_exited && process.Kills == 0, "unknown wait was accepted as exit");
                });
            }
            Run("SLDWORKS wait requires both wait success and actual exit", delegate {
                SessionRecord session; var result = R(out session); var process = P(91, "SLDWORKS");
                Check(!WaitForExactProcessExit(session, 1000), "live process with successful wait was accepted");
                process.WaitResult = false; process.ExitDuringWait = true;
                Check(!WaitForExactProcessExit(session, 1000), "false wait was accepted after exit");
                process.Exited = false; process.WaitResult = true;
                Check(WaitForExactProcessExit(session, 1000) && process.Kills == 0, "confirmed exit after wait was rejected");
            });
            Run("SLDWORKS wait confirms absence or replaced identity without killing", delegate {
                SessionRecord session; var result = R(out session);
                Check(WaitForExactProcessExit(session, 1000), "absent process not recognized");
                var process = P(91, "SLDWORKS"); process.StartTicks++;
                Check(WaitForExactProcessExit(session, 1000) && process.Kills == 0, "replaced process not recognized");
            });
            Run("SLDWORKS wait observes pinned original exit after identity read", delegate {
                SessionRecord session; var result = R(out session); var original = P(91, "SLDWORKS"); Process replacement = null;
                original.AfterStartRead = delegate { original.Exited = true; replacement = P(91, "python"); };
                Check(WaitForExactProcessExit(session, 1000) && Process.LookupCalls == 1 && replacement.Kills == 0, "wait followed reused PID");
            });
            Console.WriteLine("WIDTH_MONITOR_TEST_RESULT " + new JavaScriptSerializer().Serialize(new { passed = Cases.Count - Failed, failed = Failed, cases = Cases }));
            return Failed == 0 ? 0 : 1;
        }
    }
}
