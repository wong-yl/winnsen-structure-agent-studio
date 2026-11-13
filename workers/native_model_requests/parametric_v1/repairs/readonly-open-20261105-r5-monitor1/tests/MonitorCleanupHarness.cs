using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace MonitorCleanupOffline
{
    // Offline stand-ins only: this harness never enumerates or controls OS processes.
    internal sealed class Process : IDisposable
    {
        internal static Func<Process[]> FirstCad;
        internal static Func<Process[]> FinalCad;
        internal static Func<Process[]> FinalMonitors;
        internal static int CadQueries;
        internal int Value;
        internal bool Exited;
        internal bool ThrowHasExited;
        internal bool ThrowId;
        internal bool Disposed;
        internal int Id { get { if (ThrowId) throw new InvalidOperationException("id query failed"); return Value; } }
        internal bool HasExited { get { if (ThrowHasExited) throw new InvalidOperationException("has-exited query failed"); return Exited; } }
        public void Dispose() { Disposed = true; }
        internal static Process[] GetProcessesByName(string name)
        {
            if (name == "SLDWORKS") return (++CadQueries == 1 ? FirstCad : FinalCad)();
            if (name == "sldProcMon") return FinalMonitors();
            throw new InvalidOperationException("unexpected process-name query");
        }
        internal static void Reset()
        {
            CadQueries = 0;
            FirstCad = delegate { return new Process[0]; };
            FinalCad = delegate { return new Process[0]; };
            FinalMonitors = delegate { return new Process[0]; };
        }
    }

    internal sealed class ISldWorks
    {
        internal bool CommandInProgress;
        internal void CloseAllDocuments(bool value) { }
        internal void SetUserPreferenceToggle(int key, bool value) { }
        internal void ExitApp() { }
    }
    internal enum swUserPreferenceToggle_e { swExtRefUpdateCompNames }
    internal static class Marshal
    {
        internal static bool IsComObject(object value) { return false; }
        internal static void FinalReleaseComObject(object value) { throw new InvalidOperationException("unexpected COM release"); }
    }
    internal sealed class RunResult { internal List<SessionEvidence> sessions = new List<SessionEvidence>(); }

    /* OWNED_SESSION */
    /* OWNED_MONITOR_IDENTITY */
    /* SESSION_EVIDENCE */

    internal static class Harness
    {
        private static bool sessionPreviousUpdateNames;
        private static bool NaturalExit;
        private static bool MonitorWaitResult;
        private static bool HelperThrows;
        private static int HelperCalls;
        private static int NaturalExitTimeout;
        private static readonly List<object> Cases = new List<object>();
        private static int Failures;

        /* PROCESS_IDS */
        /* STOP_OWNED_SESSION */
        /* OWNED_CLEANUP_VALUE */
        /* TRY_OWNED_CLEANUP_ACTION */
        /* COLLECT_OWNED_SESSION_REFERENCES */

        private static void CaptureProvenOwnedMonitors(OwnedSession session) { }
        private static void StopProvenOwnedMonitors(OwnedSession session)
        {
            HelperCalls++;
            if (HelperThrows) throw new InvalidOperationException("offline monitor helper failure");
        }
        private static bool WaitForExit(int pid, int timeoutMs) { NaturalExitTimeout = timeoutMs; return NaturalExit; }
        private static bool ProcessIdExists(int pid) { return !NaturalExit; }
        private static bool WaitForProcessNameExit(string name, int timeoutMs)
        {
            Check(name == "sldProcMon" && timeoutMs == 15000, "monitor wait contract changed");
            return MonitorWaitResult;
        }
        private static string SafeExceptionText(Exception error) { return error.GetType().Name + ": " + error.Message; }
        private static void Require(bool value, string code, int exitCode, string message)
        {
            if (!value) throw new InvalidOperationException(code + ": " + message);
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
        }
        private static Process P(int pid, bool exited) { return new Process { Value = pid, Exited = exited }; }
        private static OwnedSession Session()
        {
            return new OwnedSession { phase = "offline-only", process_id = 41,
                process_start_ticks_utc = 1, executable_path = "offline/SLDWORKS.exe",
                baseline_process_ids = new List<int>(), baseline_monitor_ids = new List<int>(),
                sw = new ISldWorks(), started_at_utc = "offline" };
        }
        private static SessionEvidence Stop(bool fail)
        {
            var result = new RunResult();
            var session = Session();
            Exception observed = null;
            try { StopOwnedSession(session, result); }
            catch (Exception error) { observed = error; }
            Check((observed != null) == fail, "unexpected lifecycle acceptance: " + (observed == null ? "accepted" : observed.Message));
            if (fail) Check(observed.Message.Contains("OWNED_CAD_SESSION_EXIT_FAILED"), "wrong lifecycle failure");
            Check(result.sessions.Count == 1, "cleanup evidence missing");
            Check(NaturalExitTimeout == 120000, "natural-exit allowance changed");
            Check(Process.CadQueries == 2, "unexpected CAD snapshot count");
            return result.sessions[0];
        }
        private static void Run(string name, Action test)
        {
            Process.Reset(); NaturalExit = true; MonitorWaitResult = true; HelperThrows = false;
            HelperCalls = 0; NaturalExitTimeout = 0;
            try { test(); Cases.Add(new { name = name, pass = true }); }
            catch (Exception error) { Failures++; Cases.Add(new { name = name, pass = false, error = error.Message }); }
        }
        private static void Throws(Action action, string expected)
        {
            Exception observed = null;
            try { action(); } catch (Exception error) { observed = error; }
            Check(observed != null && observed.Message.Contains(expected), "process query did not fail closed");
        }
        private static int Main()
        {
            Run("process_ids_excludes_exited_and_sorts_live", delegate
            {
                var exited = P(41, true); exited.ThrowId = true;
                var live9 = P(9, false); var live3 = P(3, false);
                Process.FirstCad = delegate { return new[] { exited, live9, live3 }; };
                Check(ProcessIds("SLDWORKS").SequenceEqual(new[] { 3, 9 }), "exited PID leaked into live snapshot or sort changed");
                Check(exited.Disposed && live9.Disposed && live3.Disposed, "process references were not disposed");
            });
            Run("process_ids_all_exited_is_empty", delegate
            {
                Process.FirstCad = delegate { return new[] { P(41, true), P(42, true) }; };
                Check(ProcessIds("SLDWORKS").Count == 0, "exited enumeration was treated as live CAD");
            });
            Run("process_ids_has_exited_query_failure_propagates", delegate
            {
                var process = P(41, false); process.ThrowHasExited = true;
                Process.FirstCad = delegate { return new[] { process }; };
                Throws(delegate { ProcessIds("SLDWORKS"); }, "has-exited query failed");
                Check(process.Disposed, "faulting process reference was not disposed");
            });
            Run("process_ids_enumeration_failure_propagates", delegate
            {
                Process.FirstCad = delegate { throw new InvalidOperationException("enumeration failed"); };
                Throws(delegate { ProcessIds("SLDWORKS"); }, "enumeration failed");
            });
            Run("process_ids_live_id_query_failure_propagates", delegate
            {
                var process = P(41, false); process.ThrowId = true;
                Process.FirstCad = delegate { return new[] { process }; };
                Throws(delegate { ProcessIds("SLDWORKS"); }, "id query failed");
                Check(process.Disposed, "faulting live process reference was not disposed");
            });
            Run("lifecycle_exited_enumeration_calls_monitor_helper", delegate
            {
                Process.FirstCad = delegate { return new[] { P(41, true) }; };
                var evidence = Stop(false);
                Check(HelperCalls == 1 && evidence.monitor_cleanup_helper_called, "monitor helper was skipped for exited enumeration");
                Check(evidence.before_monitor_cleanup_sldworks_process_ids.Count == 0, "pre-cleanup live snapshot is wrong");
                Check(evidence.monitor_cleanup_skip_reason == "" && evidence.cad_baseline_restored, "successful cleanup evidence is wrong");
                Check(evidence.exit_requested && evidence.process_exited, "natural-exit evidence missing");
            });
            Run("lifecycle_other_live_cad_preserves_monitor", delegate
            {
                Process.FirstCad = delegate { return new[] { P(700, false) }; };
                Process.FinalCad = delegate { return new[] { P(700, false) }; };
                var evidence = Stop(true);
                Check(HelperCalls == 0 && !evidence.monitor_cleanup_helper_called, "helper called while other CAD was live");
                Check(evidence.before_monitor_cleanup_sldworks_process_ids.SequenceEqual(new[] { 700 }), "pre-cleanup CAD evidence missing");
                Check(evidence.monitor_cleanup_skip_reason == "cad_processes_remaining" && !evidence.cad_baseline_restored, "live CAD skip reason is wrong");
            });
            Run("lifecycle_final_empty_snapshot_refreshes_acceptance", delegate
            {
                Process.FirstCad = delegate { return new[] { P(700, false) }; };
                var evidence = Stop(false);
                Check(HelperCalls == 0 && evidence.monitor_cleanup_skip_reason == "cad_processes_remaining", "initial nonempty snapshot did not preserve monitor");
                Check(evidence.before_monitor_cleanup_sldworks_process_ids.SequenceEqual(new[] { 700 }), "initial snapshot evidence was overwritten");
                Check(evidence.remaining_sldworks_process_ids.Count == 0 && evidence.cad_baseline_restored, "final empty snapshot was not used for acceptance");
            });
            Run("lifecycle_initial_query_failure_skips_helper_and_rejects", delegate
            {
                var process = P(41, false); process.ThrowHasExited = true;
                Process.FirstCad = delegate { return new[] { process }; };
                var evidence = Stop(true);
                Check(HelperCalls == 0 && !evidence.monitor_cleanup_helper_called, "helper called after initial query failure");
                Check(evidence.before_monitor_cleanup_sldworks_process_ids.SequenceEqual(new[] { -1 }), "unknown baseline sentinel missing");
                Check(evidence.monitor_cleanup_skip_reason == "cad_baseline_query_failed", "query failure skip reason missing");
                Check(evidence.cleanup_errors.Any(value => value.Contains("has-exited query failed")), "query failure evidence missing");
            });
            Run("lifecycle_final_query_failure_rejects", delegate
            {
                Process.FinalCad = delegate { throw new InvalidOperationException("final enumeration failed"); };
                var evidence = Stop(true);
                Check(evidence.remaining_sldworks_process_ids.SequenceEqual(new[] { -1 }) && !evidence.cad_baseline_restored, "final query failure accepted as empty");
                Check(evidence.cleanup_errors.Any(value => value.Contains("final enumeration failed")), "final query failure evidence missing");
            });
            Run("lifecycle_parent_not_naturally_exited_skips_helper", delegate
            {
                NaturalExit = false;
                var evidence = Stop(true);
                Check(HelperCalls == 0 && !evidence.monitor_cleanup_helper_called, "helper called before natural exit");
                Check(evidence.monitor_cleanup_skip_reason == "owned_cad_not_naturally_exited" && !evidence.process_exited, "natural-exit skip reason missing");
            });
            Run("lifecycle_helper_failure_is_recorded_and_rejected", delegate
            {
                HelperThrows = true; MonitorWaitResult = false;
                Process.FinalMonitors = delegate { return new[] { P(8484, false) }; };
                var evidence = Stop(true);
                Check(HelperCalls == 1 && evidence.monitor_cleanup_helper_called, "failed helper call was not recorded");
                Check(evidence.monitor_exit_error.Contains("offline monitor helper failure") && evidence.cleanup_errors.Count > 0, "helper failure was silent");
                Check(!evidence.cad_baseline_restored && evidence.remaining_sldprocmon_process_ids.SequenceEqual(new[] { 8484 }), "remaining monitor was accepted");
            });
            Run("lifecycle_remaining_monitor_rejects", delegate
            {
                MonitorWaitResult = false;
                Process.FinalMonitors = delegate { return new[] { P(8484, false) }; };
                var evidence = Stop(true);
                Check(evidence.monitor_cleanup_helper_called && !evidence.cad_baseline_restored, "remaining monitor accepted");
            });
            Console.WriteLine(new JavaScriptSerializer().Serialize(new { pass = Failures == 0,
                case_count = Cases.Count, failed = Failures, cases = Cases,
                execution = "offline process/COM/monitor stand-ins; no native CAD or process control" }));
            return Failures == 0 ? 0 : 1;
        }
    }
}
