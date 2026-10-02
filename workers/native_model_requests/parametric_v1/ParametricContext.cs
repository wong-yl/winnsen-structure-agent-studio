using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Winnsen.StructureAgent.Generation
{
    internal static class ParametricContext
    {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 120 };
        internal static Dictionary<string, object> Plan;
        internal static string PlanPath;
        internal static string PlanHash;
        internal static string Phase;
        internal static string AttemptDirectory;
        internal static string CadDirectory;
        internal static string SourceRightPartitionSha256;
        internal static double WidthMm;
        internal static double HeightMm;
        internal static double DepthMm;
        internal static double DoorWidthMm { get { return (WidthMm - 126.0) / 2.0; } }
        internal static double ActiveDoorHeightMm;

        internal static void Initialize(string planPath, string phase)
        {
            PlanPath = Path.GetFullPath(planPath);
            CheckPath(PlanPath, Path.GetDirectoryName(PlanPath));
            PlanHash = Environment.GetEnvironmentVariable("WINNSEN_PARAMETRIC_PLAN_SHA256") ?? "";
            Require(PlanHash.Length == 64 && Hash(PlanPath) == PlanHash, "PARAMETRIC_PLAN_HASH_MISMATCH");
            Plan = Read(PlanPath);
            Require(Text(Plan, "schema") == "winnsen.locker16029.parametric_execution.v1", "PARAMETRIC_PLAN_SCHEMA_INVALID");
            AttemptDirectory = Path.GetDirectoryName(PlanPath);
            CadDirectory = Path.Combine(AttemptDirectory, "native_cad", "working_pack");
            Require(Path.GetFileName(PlanPath) == "parametric_execution.json", "PARAMETRIC_PLAN_NAME_INVALID");
            Require(Path.GetFileName(Path.GetDirectoryName(AttemptDirectory)) == "parametric_attempts", "PARAMETRIC_ATTEMPT_PATH_INVALID");
            CheckPath(AttemptDirectory, AttemptDirectory);
            Dictionary<string, object> cabinet = Object(Object(Plan, "contract"), "geometry");
            cabinet = Object(cabinet, "cabinet");
            WidthMm = Number(cabinet, "widthMm");
            HeightMm = Number(cabinet, "heightMm");
            DepthMm = Number(cabinet, "depthMm");
            SourceRightPartitionSha256 = Text(Plan, "sourceRightPartitionSha256");
            Require(WidthMm >= 700 && WidthMm <= 1200 && HeightMm >= 1700 && HeightMm <= 2200 && DepthMm >= 250 && DepthMm <= 650,
                "PARAMETRIC_DIMENSIONS_OUTSIDE_CURRENT_NATIVE_RUNTIME");
            Phase = phase;
            AssertAuthorized(phase);
        }

        internal static void AssertAuthorized(string phase)
        {
            Require(Plan != null && phase == Phase && Hash(PlanPath) == PlanHash, "PARAMETRIC_PHASE_BINDING_INVALID");
            string authorizationPath = Environment.GetEnvironmentVariable("WINNSEN_PARAMETRIC_AUTH_PATH") ?? "";
            string authorizationHash = Environment.GetEnvironmentVariable("WINNSEN_PARAMETRIC_AUTH_SHA256") ?? "";
            CheckPath(authorizationPath, AttemptDirectory);
            Require(authorizationHash.Length == 64 && Hash(authorizationPath) == authorizationHash, "PARAMETRIC_AUTH_HASH_MISMATCH");
            Dictionary<string, object> authorization = Read(authorizationPath);
            Require(Text(authorization, "phase") == phase && Text(authorization, "planSha256") == PlanHash &&
                Text(authorization, "taskId") == Text(Plan, "taskId") && Text(authorization, "workerId") == Text(Plan, "workerId") &&
                Text(authorization, "executableSha256") == Hash(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "PARAMETRIC_AUTH_BINDING_INVALID");
            SourceRightPartitionSha256 = Text(authorization, "rightPartitionSha256");
            Dictionary<string, object> task = Read(Text(Plan, "taskPath"));
            Dictionary<string, object> build = Object(task, "nativeBuild");
            Dictionary<string, object> lease = Object(build, "lease");
            Require(Text(task, "id") == Text(Plan, "taskId") && Text(lease, "id") == Text(authorization, "leaseId") &&
                Text(lease, "workerId") == Text(authorization, "workerId") && Text(lease, "workerId") == Text(Plan, "workerId"),
                "PARAMETRIC_LEASE_NOT_OWNED");
            Require(Text(build, "state") == "building" || Text(build, "state") == "validating", "PARAMETRIC_TASK_NOT_RUNNING");
            Require(DateTime.Parse(Text(lease, "expiresAt"), null, DateTimeStyles.RoundtripKind).ToUniversalTime() > DateTime.UtcNow &&
                DateTime.Parse(Text(authorization, "expiresAt"), null, DateTimeStyles.RoundtripKind).ToUniversalTime() > DateTime.UtcNow,
                "PARAMETRIC_AUTH_OR_LEASE_EXPIRED");
        }

        internal static void AssertMutation(string path)
        {
            AssertAuthorized(Phase);
            CheckPath(path, AttemptDirectory);
        }

        internal static void CheckPath(string path, string root)
        {
            string full = Path.GetFullPath(path);
            string safeRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            Require(full.Equals(safeRoot, StringComparison.OrdinalIgnoreCase) || full.StartsWith(safeRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "PARAMETRIC_PATH_ESCAPE");
            string current = full;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(current) || Directory.Exists(current))
                    Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "PARAMETRIC_REPARSE_PATH");
                if (File.Exists(current))
                    using (FileStream stream = new FileStream(current, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        ByHandleFileInformation information;
                        Require(GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out information) &&
                            information.numberOfLinks == 1, "PARAMETRIC_HARDLINK_PATH");
                    }
                current = Path.GetDirectoryName(current);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint fileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME creationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME lastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME lastWriteTime;
            internal uint volumeSerialNumber;
            internal uint fileSizeHigh;
            internal uint fileSizeLow;
            internal uint numberOfLinks;
            internal uint fileIndexHigh;
            internal uint fileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(IntPtr file, out ByHandleFileInformation information);

        internal static Dictionary<string, object> Read(string path) {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return Json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
        }
        internal static Dictionary<string, object> Object(Dictionary<string, object> item, string key) { return (Dictionary<string, object>)item[key]; }
        internal static string Text(Dictionary<string, object> item, string key) { return item.ContainsKey(key) ? Convert.ToString(item[key], CultureInfo.InvariantCulture) : ""; }
        internal static double Number(Dictionary<string, object> item, string key) { return Convert.ToDouble(item[key], CultureInfo.InvariantCulture); }
        internal static string Hash(string path) { using (SHA256 hash = SHA256.Create()) using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Write(string path, object value) { AssertMutation(path); File.WriteAllText(path, Json.Serialize(value), new UTF8Encoding(false)); }
    }
}
