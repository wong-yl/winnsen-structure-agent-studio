using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class BuildDoorWeldModule
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 7)
            {
                Console.Error.WriteLine("Usage: BuildDoorWeldModule.exe <out-asm> <out-json> <door-height-mm> <panel> <stiffener> <latch-plate> <hook-pad> [left|right] [door-width-mm]");
                return 2;
            }

            string outAsm = Path.GetFullPath(args[0]);
            string outJson = Path.GetFullPath(args[1]);
            double doorHeight = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            string panel = Path.GetFullPath(args[3]);
            string stiffener = Path.GetFullPath(args[4]);
            string latch = Path.GetFullPath(args[5]);
            string hook = Path.GetFullPath(args[6]);
            string handedness = "left";
            double doorWidth = 437.0;
            for (int i = 7; i < args.Length; i++)
            {
                string value = args[i].Trim();
                string lower = value.ToLowerInvariant();
                double parsed;
                if (lower == "left" || lower == "right")
                {
                    handedness = lower;
                }
                else if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
                {
                    doorWidth = parsed;
                }
                else
                {
                    Console.Error.WriteLine("optional arguments must be handedness or door-width-mm: " + value);
                    return 2;
                }
            }
            if (handedness != "left" && handedness != "right")
            {
                Console.Error.WriteLine("handedness must be 'left' or 'right'");
                return 2;
            }
            if (doorWidth <= 0)
            {
                Console.Error.WriteLine("door-width-mm must be positive");
                return 2;
            }
            double side = handedness == "right" ? -1.0 : 1.0;

            var result = new BuildResult
            {
                OutAsmPath = outAsm,
                DoorHeightMm = doorHeight,
                DoorWidthMm = doorWidth,
                Handedness = handedness,
            };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outAsm));
                ISldWorks sw = GetOrCreateSolidWorks();
                if (sw == null)
                {
                    result.Error = "SolidWorks unavailable";
                    WriteJson(outJson, result);
                    return 3;
                }
                sw.Visible = true;
                Try(() => sw.CloseAllDocuments(true));

                ModelDoc2 model = sw.NewAssembly() as ModelDoc2;
                AssemblyDoc asm = model as AssemblyDoc;
                if (model == null || asm == null)
                {
                    result.Error = "NewAssembly failed";
                    WriteJson(outJson, result);
                    return 4;
                }
                result.NewAssembly = true;

                MathUtility math = sw.GetMathUtility() as MathUtility;
                if (math == null)
                {
                    result.Error = "GetMathUtility failed";
                    WriteJson(outJson, result);
                    return 5;
                }

                double latchTopTy = doorHeight / 2.0 - 0.8;
                double latchBottomTy = -doorHeight / 2.0 + 28.8;

                double doorHalfWidth = doorWidth / 2.0;
                double latchPlateTx = -(doorHalfWidth - 10.0) * side;
                double hookPadTx = handedness == "right" ? -(doorHalfWidth - 15.0) : doorHalfWidth - 12.2;

                var placements = new[]
                {
                    new Placement("door_panel_rule_part", panel, Identity(), 0, 0, 0),
                    new Placement("door_stiffener_rule_part", stiffener, Identity(), 0, 0, -14.8),
                    new Placement("latch_plate_top", latch, RotateX90(), latchPlateTx, latchTopTy, -14.0),
                    new Placement("latch_plate_bottom", latch, RotateX90(), latchPlateTx, latchBottomTy, -14.0),
                    new Placement("u_hook_pad", hook, Identity(), hookPadTx, 0, -1.6),
                };

                foreach (Placement p in placements)
                {
                    result.Placements.Add(Add(sw, model, asm, math, p));
                }

                result.Rebuilt = TryValue(() => model.ForceRebuild3(false), false);
                result.Saved = TryValue(() => model.SaveAs(outAsm), false);
                result.ReferenceCount = CountReferenceFeatures(model);
                Try(() => sw.CloseDoc(model.GetTitle()));

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Saved ? 0 : 6;
            }
            catch (Exception ex)
            {
                result.Error = SafeExceptionText(ex);
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
        }

        private static PlacementResult Add(ISldWorks sw, ModelDoc2 asmModel, AssemblyDoc asm, MathUtility math, Placement p)
        {
            var row = new PlacementResult
            {
                Role = p.Role,
                Path = p.Path,
                Exists = File.Exists(p.Path),
                Rotation = p.Rotation,
                TxMm = p.TxMm,
                TyMm = p.TyMm,
                TzMm = p.TzMm,
            };
            if (!row.Exists)
            {
                row.Error = "missing source";
                return row;
            }

            int errors = 0;
            int warnings = 0;
            int docType = p.Path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART;
            ModelDoc2 partDoc = sw.OpenDoc6(p.Path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2;
            row.Opened = partDoc != null;
            row.OpenErrors = errors;
            row.OpenWarnings = warnings;
            if (partDoc == null)
            {
                row.Error = "open failed";
                return row;
            }

            Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
            Component2 comp = asm.AddComponent5(
                p.Path,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "",
                false,
                "",
                p.TxMm / 1000.0,
                p.TyMm / 1000.0,
                p.TzMm / 1000.0) as Component2;
            row.Added = comp != null;
            if (comp == null)
            {
                row.Error = "add failed";
                return row;
            }
            Try(() => { comp.Name2 = p.Role; });

            MathTransform xf = math.CreateTransform(ToTransformData(p.Rotation, p.TxMm, p.TyMm, p.TzMm)) as MathTransform;
            row.TransformCreated = xf != null;
            if (xf == null)
            {
                row.Error = "transform create failed";
                return row;
            }
            row.TransformApplied = TryValue(() => comp.SetTransformAndSolve2(xf), false);
            if (!row.TransformApplied)
            {
                row.Error = "transform apply failed";
            }
            return row;
        }

        private static object ToTransformData(double[] r, double txMm, double tyMm, double tzMm)
        {
            return new double[]
            {
                r[0], r[1], r[2],
                r[3], r[4], r[5],
                r[6], r[7], r[8],
                txMm / 1000.0, tyMm / 1000.0, tzMm / 1000.0,
                1.0, 0.0, 0.0, 0.0
            };
        }

        private static int CountReferenceFeatures(ModelDoc2 model)
        {
            int count = 0;
            Feature feat = TryValue(() => model.FirstFeature() as Feature, null);
            int guard = 0;
            while (feat != null && guard < 3000)
            {
                guard++;
                string type = TryValue(() => feat.GetTypeName2(), "");
                if (type == "Reference") count++;
                feat = TryValue(() => feat.GetNextFeature() as Feature, null);
            }
            return count;
        }

        private static double[] Identity()
        {
            return new[] { 1.0, 0, 0, 0, 1.0, 0, 0, 0, 1.0 };
        }

        private static double[] RotateX90()
        {
            return new[] { 1.0, 0, 0, 0, 0, 1.0, 0, -1.0, 0 };
        }

        private static ISldWorks GetOrCreateSolidWorks()
        {
            try { return Marshal.GetActiveObject("SldWorks.Application") as ISldWorks; }
            catch { }
            try
            {
                Type t = Type.GetTypeFromProgID("SldWorks.Application");
                return t == null ? null : Activator.CreateInstance(t) as ISldWorks;
            }
            catch { return null; }
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }

        private static T TryValue<T>(Func<T> func, T fallback)
        {
            try { return func(); } catch { return fallback; }
        }

        private static void WriteJson(string path, BuildResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson(result), new UTF8Encoding(false));
        }

        private static string SafeExceptionText(Exception ex)
        {
            if (ex == null) return "";
            try { return ex.ToString(); }
            catch
            {
                try { return ex.GetType().FullName + ": " + (ex.Message ?? ""); }
                catch { return "unprintable exception"; }
            }
        }

        private static string ToJson(BuildResult r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Prop(sb, "outAsmPath", r.OutAsmPath, true);
            Prop(sb, "doorHeightMm", r.DoorHeightMm);
            Prop(sb, "doorWidthMm", r.DoorWidthMm);
            Prop(sb, "handedness", r.Handedness);
            Prop(sb, "newAssembly", r.NewAssembly);
            Prop(sb, "rebuilt", r.Rebuilt);
            Prop(sb, "saved", r.Saved);
            Prop(sb, "referenceCount", r.ReferenceCount);
            Prop(sb, "error", r.Error);
            sb.Append(",\"placements\":[");
            for (int i = 0; i < r.Placements.Count; i++)
            {
                if (i > 0) sb.Append(",");
                PlacementResult p = r.Placements[i];
                sb.Append("{");
                Prop(sb, "role", p.Role, true);
                Prop(sb, "path", p.Path);
                Prop(sb, "exists", p.Exists);
                Prop(sb, "opened", p.Opened);
                Prop(sb, "added", p.Added);
                Prop(sb, "transformCreated", p.TransformCreated);
                Prop(sb, "transformApplied", p.TransformApplied);
                ArrayProp(sb, "rotation", p.Rotation);
                Prop(sb, "txMm", p.TxMm);
                Prop(sb, "tyMm", p.TyMm);
                Prop(sb, "tzMm", p.TzMm);
                Prop(sb, "openErrors", p.OpenErrors);
                Prop(sb, "openWarnings", p.OpenWarnings);
                Prop(sb, "error", p.Error);
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void Prop(StringBuilder sb, string name, string value, bool first = false)
        {
            if (!first) sb.Append(",");
            sb.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? "")).Append("\"");
        }

        private static void Prop(StringBuilder sb, string name, bool value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value ? "true" : "false");
        }

        private static void Prop(StringBuilder sb, string name, int value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value);
        }

        private static void Prop(StringBuilder sb, string name, double value)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void ArrayProp(StringBuilder sb, string name, double[] values)
        {
            sb.Append(",\"").Append(Escape(name)).Append("\":[");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(values[i].ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.Append("]");
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class Placement
        {
            public readonly string Role;
            public readonly string Path;
            public readonly double[] Rotation;
            public readonly double TxMm;
            public readonly double TyMm;
            public readonly double TzMm;

            public Placement(string role, string path, double[] rotation, double txMm, double tyMm, double tzMm)
            {
                Role = role;
                Path = path;
                Rotation = rotation;
                TxMm = txMm;
                TyMm = tyMm;
                TzMm = tzMm;
            }
        }

        private sealed class BuildResult
        {
            public string OutAsmPath = "";
            public double DoorHeightMm;
            public double DoorWidthMm;
            public string Handedness = "left";
            public bool NewAssembly;
            public bool Rebuilt;
            public bool Saved;
            public int ReferenceCount;
            public string Error = "";
            public readonly List<PlacementResult> Placements = new List<PlacementResult>();
        }

        private sealed class PlacementResult
        {
            public string Role = "";
            public string Path = "";
            public bool Exists;
            public bool Opened;
            public bool Added;
            public bool TransformCreated;
            public bool TransformApplied;
            public double[] Rotation = new double[0];
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public int OpenErrors;
            public int OpenWarnings;
            public string Error = "";
        }
    }
}
