using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class ProbePartBodies
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ProbePartBodies.exe <source-part> <out-json>");
                return 2;
            }

            string sourcePath = Path.GetFullPath(args[0]);
            string outJson = Path.GetFullPath(args[1]);
            bool exitSession = false;
            for (int i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--exit-session", StringComparison.OrdinalIgnoreCase)) exitSession = true;
            }
            var result = new ProbeResult
            {
                SourcePath = sourcePath,
                Exists = File.Exists(sourcePath),
                ReadOnlyRequested = true,
            };

            try
            {
                if (!result.Exists)
                {
                    result.Error = "source part missing";
                    WriteJson(outJson, result);
                    return 3;
                }

                ISldWorks sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 4;
                }

                sw.Visible = true;
                result.SolidWorksProcessId = TryValue(() => sw.GetProcessID(), 0);
                Try(() => sw.CloseAllDocuments(true));
                int errors = 0;
                int warnings = 0;
                ModelDoc2 model = sw.OpenDoc6(
                    sourcePath,
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                    "",
                    ref errors,
                    ref warnings) as ModelDoc2;
                result.OpenErrors = errors;
                result.OpenWarnings = warnings;
                result.Opened = model != null;
                if (model == null)
                {
                    result.Error = "OpenDoc6 failed";
                    WriteJson(outJson, result);
                    return 5;
                }

                PartDoc part = model as PartDoc;
                if (part == null)
                {
                    result.Error = "opened document is not a part";
                    WriteJson(outJson, result);
                    return 6;
                }

                object rawBodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true);
                object[] bodies = rawBodies as object[];
                if (bodies == null && rawBodies is Array)
                {
                    Array a = (Array)rawBodies;
                    bodies = new object[a.Length];
                    a.CopyTo(bodies, 0);
                }
                if (bodies == null) bodies = new object[0];

                for (int i = 0; i < bodies.Length; i++)
                {
                    Body2 body = bodies[i] as Body2;
                    if (body == null) continue;
                    var row = new BodyResult
                    {
                        Index = i,
                        Name = TryValue(() => body.Name, ""),
                    };
                    object rawBox = TryValue(() => body.GetBodyBox(), null);
                    double[] box = rawBox as double[];
                    if (box != null && box.Length >= 6)
                    {
                        row.XMinMm = Mm(box[0]);
                        row.YMinMm = Mm(box[1]);
                        row.ZMinMm = Mm(box[2]);
                        row.XMaxMm = Mm(box[3]);
                        row.YMaxMm = Mm(box[4]);
                        row.ZMaxMm = Mm(box[5]);
                        row.XLenMm = Mm(box[3] - box[0]);
                        row.YLenMm = Mm(box[4] - box[1]);
                        row.ZLenMm = Mm(box[5] - box[2]);
                    }
                    object rawMass = TryValue(() => body.GetMassProperties(1.0), null);
                    double[] mass = rawMass as double[];
                    if (mass != null && mass.Length >= 6)
                    {
                        row.VolumeMm3 = Math.Round(mass[3] * 1000000000.0, 3);
                        row.SurfaceAreaMm2 = Math.Round(mass[4] * 1000000.0, 3);
                    }
                    result.Bodies.Add(row);
                }

                result.BodyCount = result.Bodies.Count;
                Try(() => sw.CloseAllDocuments(true));
                if (exitSession)
                {
                    result.Exited = TryValue(() => { sw.ExitApp(); return true; }, false);
                }
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 0;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
        }

        private static double Mm(double meters)
        {
            return Math.Round(meters * 1000.0, 3);
        }

        private static ISldWorks GetOrCreateSolidWorks()
        {
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
                try
                {
                    object active = Marshal.GetActiveObject(progId);
                    if (active != null) return active as ISldWorks;
                }
                catch { }
            }
            foreach (string progId in new[] { "SldWorks.Application.28", "SldWorks.Application" })
            {
                try
                {
                    Type t = Type.GetTypeFromProgID(progId);
                    if (t != null) return Activator.CreateInstance(t) as ISldWorks;
                }
                catch { }
            }
            return null;
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        private static T TryValue<T>(Func<T> action, T fallback)
        {
            try { return action(); } catch { return fallback; }
        }

        private static void WriteJson(string path, ProbeResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, result.ToJson(), new UTF8Encoding(false));
        }

        private sealed class ProbeResult
        {
            public string SourcePath;
            public bool Exists;
            public bool Opened;
            public bool ReadOnlyRequested;
            public int SolidWorksProcessId;
            public bool Exited;
            public int OpenErrors;
            public int OpenWarnings;
            public int BodyCount;
            public string Error;
            public readonly List<BodyResult> Bodies = new List<BodyResult>();

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                JsonProp(sb, "source_path", SourcePath, true);
                JsonProp(sb, "exists", Exists, true);
                JsonProp(sb, "opened", Opened, true);
                JsonProp(sb, "read_only_requested", ReadOnlyRequested, true);
                JsonProp(sb, "solidworks_process_id", SolidWorksProcessId, true);
                JsonProp(sb, "exited", Exited, true);
                JsonProp(sb, "open_errors", OpenErrors, true);
                JsonProp(sb, "open_warnings", OpenWarnings, true);
                JsonProp(sb, "body_count", BodyCount, true);
                JsonProp(sb, "error", Error, true);
                sb.AppendLine("  \"bodies\": [");
                for (int i = 0; i < Bodies.Count; i++)
                {
                    sb.Append(Bodies[i].ToJson("    "));
                    sb.AppendLine(i == Bodies.Count - 1 ? "" : ",");
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                return sb.ToString();
            }
        }

        private sealed class BodyResult
        {
            public int Index;
            public string Name;
            public double XMinMm;
            public double YMinMm;
            public double ZMinMm;
            public double XMaxMm;
            public double YMaxMm;
            public double ZMaxMm;
            public double XLenMm;
            public double YLenMm;
            public double ZLenMm;
            public double VolumeMm3;
            public double SurfaceAreaMm2;

            public string ToJson(string indent)
            {
                var sb = new StringBuilder();
                sb.AppendLine(indent + "{");
                JsonProp(sb, "index", Index, true, indent + "  ");
                JsonProp(sb, "name", Name, true, indent + "  ");
                JsonProp(sb, "x_min_mm", XMinMm, true, indent + "  ");
                JsonProp(sb, "y_min_mm", YMinMm, true, indent + "  ");
                JsonProp(sb, "z_min_mm", ZMinMm, true, indent + "  ");
                JsonProp(sb, "x_max_mm", XMaxMm, true, indent + "  ");
                JsonProp(sb, "y_max_mm", YMaxMm, true, indent + "  ");
                JsonProp(sb, "z_max_mm", ZMaxMm, true, indent + "  ");
                JsonProp(sb, "x_len_mm", XLenMm, true, indent + "  ");
                JsonProp(sb, "y_len_mm", YLenMm, true, indent + "  ");
                JsonProp(sb, "z_len_mm", ZLenMm, true, indent + "  ");
                JsonProp(sb, "volume_mm3", VolumeMm3, true, indent + "  ");
                JsonProp(sb, "surface_area_mm2", SurfaceAreaMm2, false, indent + "  ");
                sb.Append(indent + "}");
                return sb.ToString();
            }
        }

        private static void JsonProp(StringBuilder sb, string key, string value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ");
            if (value == null) sb.Append("null");
            else sb.Append('"').Append(Escape(value)).Append('"');
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static void JsonProp(StringBuilder sb, string key, bool value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ").Append(value ? "true" : "false");
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static void JsonProp(StringBuilder sb, string key, int value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static void JsonProp(StringBuilder sb, string key, double value, bool comma, string indent = "  ")
        {
            sb.Append(indent).Append('"').Append(Escape(key)).Append("\": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }

        private static string Escape(string value)
        {
            return value == null ? "" : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
