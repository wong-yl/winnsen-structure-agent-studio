using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.SolidWorksTools
{
    internal static class BuildDoorArrayModule
    {
        private const double RightColumnMirrorYCompensationMm = -51.7;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 6)
            {
                Console.Error.WriteLine("Usage: BuildDoorArrayModule.exe <out-asm> <out-json> <door-count> <door-height-mm> <ordinary-door-asm> <bottom-panel-y-min-mm>");
                return 2;
            }

            string outAsm = Path.GetFullPath(args[0]);
            string outJson = Path.GetFullPath(args[1]);
            int doorCount = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            double doorHeight = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
            string ordinaryDoorAsm = Path.GetFullPath(args[4]);
            double bottomPanelYMin = double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);

            var result = new BuildResult
            {
                OutAsmPath = outAsm,
                DoorCount = doorCount,
                DoorHeightMm = doorHeight,
                BottomPanelYMinMm = bottomPanelYMin,
                RightColumnMirrorYCompensationMm = RightColumnMirrorYCompensationMm,
            };

            try
            {
                if (doorCount <= 0 || doorCount % 2 != 0)
                {
                    result.Error = "door-count must be a positive even number";
                    WriteJson(outJson, result);
                    return 2;
                }

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

                int rowsPerColumn = doorCount / 2;
                double pitch = doorHeight + 7.0;
                double[] columns = { -258.5, 258.5 };
                string[] columnNames = { "L", "R" };

                for (int c = 0; c < columns.Length; c++)
                {
                    for (int row = 1; row <= rowsPerColumn; row++)
                    {
                        double panelCenterY = bottomPanelYMin + doorHeight / 2.0 + (row - 1) * pitch;
                        double compensatedCenterY = panelCenterY + (c == 0 ? 0.0 : RightColumnMirrorYCompensationMm);
                        string role = "ordinary_door_" + columnNames[c] + row.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                        var p = new Placement(role, ordinaryDoorAsm, c == 0 ? Identity() : RotateZ180(), columns[c], compensatedCenterY, 0);
                        result.Placements.Add(Add(sw, model, asm, math, p));
                    }
                }

                result.RowsPerColumn = rowsPerColumn;
                result.PitchMm = pitch;
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
                result.Error = ex.ToString();
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
            while (feat != null && guard < 5000)
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

        private static double[] RotateZ180()
        {
            return new[] { -1.0, 0, 0, 0, -1.0, 0, 0, 0, 1.0 };
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

        private static string ToJson(BuildResult r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Prop(sb, "outAsmPath", r.OutAsmPath, true);
            Prop(sb, "doorCount", r.DoorCount);
            Prop(sb, "rowsPerColumn", r.RowsPerColumn);
            Prop(sb, "doorHeightMm", r.DoorHeightMm);
            Prop(sb, "pitchMm", r.PitchMm);
            Prop(sb, "bottomPanelYMinMm", r.BottomPanelYMinMm);
            Prop(sb, "rightColumnMirrorYCompensationMm", r.RightColumnMirrorYCompensationMm);
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
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(values[i].ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture));
                }
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
            public int DoorCount;
            public int RowsPerColumn;
            public double DoorHeightMm;
            public double PitchMm;
            public double BottomPanelYMinMm;
            public double RightColumnMirrorYCompensationMm;
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
            public double[] Rotation = new double[0];
            public bool Opened;
            public bool Added;
            public bool TransformCreated;
            public bool TransformApplied;
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public int OpenErrors;
            public int OpenWarnings;
            public string Error = "";
        }
    }
}
