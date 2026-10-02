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
    internal static class BuildCenteredBackSeamAssembly
    {
        private const double CabinetWidthMm = 740.0;
        private const double BackPanelYMinMm = 26.8;
        private const double BackPanelYMaxMm = 1839.2;
        private const double BackPanelOuterZMm = -551.2;
        private const double PanelThicknessMm = 1.2;
        private const double CenterGapMm = 1.0;
        private const string LeftPanelRole = "\u540E\u80CC\u677F\u4E2D\u5FC3\u63A5\u7F1D_centered_left_Xneg_to_center";
        private const string RightPanelRole = "\u540E\u80CC\u677F\u4E2D\u5FC3\u63A5\u7F1D_centered_right_center_to_Xpos";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("Usage: BuildCenteredBackSeamAssembly.exe <source-asm> <out-dir> <out-asm> <out-json>");
                return 2;
            }

            string sourceAsm = Path.GetFullPath(args[0]);
            string outDir = Path.GetFullPath(args[1]);
            string outAsm = Path.GetFullPath(args[2]);
            string outJson = Path.GetFullPath(args[3]);

            var result = new BuildResult
            {
                SourceAssemblyPath = sourceAsm,
                OutDir = outDir,
                OutAssemblyPath = outAsm,
                CabinetWidthMm = CabinetWidthMm,
                BackPanelYMinMm = BackPanelYMinMm,
                BackPanelYMaxMm = BackPanelYMaxMm,
                PanelThicknessMm = PanelThicknessMm,
                CenterGapMm = CenterGapMm,
            };

            try
            {
                if (!File.Exists(sourceAsm))
                {
                    result.Error = "source assembly not found";
                    WriteJson(outJson, result);
                    return 2;
                }

                Directory.CreateDirectory(outDir);
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

                double fullHalfWidth = CabinetWidthMm / 2.0;
                double panelHeight = BackPanelYMaxMm - BackPanelYMinMm;
                double panelWidth = fullHalfWidth - CenterGapMm / 2.0;
                double centerY = (BackPanelYMinMm + BackPanelYMaxMm) / 2.0;
                double leftCenterX = -fullHalfWidth / 2.0 - CenterGapMm / 4.0;
                double rightCenterX = fullHalfWidth / 2.0 + CenterGapMm / 4.0;

                string leftPart = Path.Combine(outDir, "\u540E\u80CC\u677F\u4E2D\u5FC3\u63A5\u7F1D_centered_left.SLDPRT");
                string rightPart = Path.Combine(outDir, "\u540E\u80CC\u677F\u4E2D\u5FC3\u63A5\u7F1D_centered_right.SLDPRT");

                result.Panels.Add(CreatePanelPart(sw, leftPart, LeftPanelRole, panelWidth, panelHeight));
                result.Panels.Add(CreatePanelPart(sw, rightPart, RightPanelRole, panelWidth, panelHeight));
                if (HasPanelErrors(result.Panels))
                {
                    result.Error = "panel creation failed; see panels[].error";
                    WriteJson(outJson, result);
                    return 4;
                }

                if (File.Exists(outAsm)) File.Delete(outAsm);
                File.Copy(sourceAsm, outAsm, true);
                result.CopiedSourceAssembly = true;

                ModelDoc2 asmModel = OpenAssemblyDocument(sw, outAsm, result);
                AssemblyDoc asm = asmModel as AssemblyDoc;
                if (asmModel == null || asm == null)
                {
                    result.Error = "open copied assembly failed";
                    WriteJson(outJson, result);
                    return 5;
                }

                MathUtility math = sw.GetMathUtility() as MathUtility;
                if (math == null)
                {
                    result.Error = "GetMathUtility failed";
                    WriteJson(outJson, result);
                    return 6;
                }

                result.Placements.Add(AddComponent(sw, asmModel, asm, math, LeftPanelRole, leftPart, leftCenterX, centerY, BackPanelOuterZMm));
                result.Placements.Add(AddComponent(sw, asmModel, asm, math, RightPanelRole, rightPart, rightCenterX, centerY, BackPanelOuterZMm));

                result.LeftPanelXMinMm = -fullHalfWidth;
                result.LeftPanelXMaxMm = -CenterGapMm / 2.0;
                result.RightPanelXMinMm = CenterGapMm / 2.0;
                result.RightPanelXMaxMm = fullHalfWidth;
                result.SeamCenterXMm = 0.0;
                result.SeamGapMm = CenterGapMm;

                if (HasPlacementErrors(result.Placements))
                {
                    result.Error = "assembly placement failed; see placements[].error";
                    WriteJson(outJson, result);
                    Try(() => sw.CloseDoc(asmModel.GetTitle()));
                    return 7;
                }

                result.Rebuilt = TryValue(() => asmModel.ForceRebuild3(false), false);
                result.Saved = SaveModelWithRetry(asmModel, outAsm, result);
                Try(() => sw.CloseDoc(asmModel.GetTitle()));

                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return result.Saved ? 0 : 8;
            }
            catch (Exception ex)
            {
                result.Error = SafeExceptionText(ex);
                WriteJson(outJson, result);
                Console.WriteLine(outJson);
                return 9;
            }
        }

        private static PanelResult CreatePanelPart(ISldWorks sw, string path, string role, double widthMm, double heightMm)
        {
            var result = new PanelResult
            {
                Role = role,
                Path = path,
                WidthMm = widthMm,
                HeightMm = heightMm,
                ThicknessMm = PanelThicknessMm,
            };

            Try(() => { if (File.Exists(path)) File.Delete(path); });
            ModelDoc2 part = NewPartDocument(sw);
            if (part == null)
            {
                result.Error = "new part failed";
                return result;
            }
            result.NewPart = true;

            bool planeSelected = SelectFrontPlane(part);
            result.FrontPlaneSelected = planeSelected;
            if (!planeSelected)
            {
                result.Error = "front plane select failed";
                Try(() => sw.CloseDoc(part.GetTitle()));
                return result;
            }

            SketchManager sketch = part.SketchManager;
            FeatureManager features = part.FeatureManager;
            if (sketch == null || features == null)
            {
                result.Error = "sketch or feature manager unavailable";
                Try(() => sw.CloseDoc(part.GetTitle()));
                return result;
            }

            Try(() => sketch.InsertSketch(true));
            result.SketchOpened = true;
            object rectangle = TryValue(
                () => sketch.CreateCenterRectangle(0, 0, 0, widthMm / 2000.0, heightMm / 2000.0, 0),
                null);
            result.RectangleCreated = rectangle != null;
            if (!result.RectangleCreated)
            {
                result.Error = "center rectangle failed";
                Try(() => sw.CloseDoc(part.GetTitle()));
                return result;
            }

            Feature extrusion = TryValue(
                () => features.FeatureExtrusion2(
                    true,
                    false,
                    false,
                    0,
                    0,
                    PanelThicknessMm / 1000.0,
                    0.0,
                    false,
                    false,
                    false,
                    false,
                    0.0,
                    0.0,
                    false,
                    false,
                    false,
                    false,
                    true,
                    true,
                    true,
                    0,
                    0.0,
                    false) as Feature,
                null);
            result.ExtrusionCreated = extrusion != null;
            if (extrusion == null)
            {
                result.Error = "extrusion failed";
                Try(() => sw.CloseDoc(part.GetTitle()));
                return result;
            }
            Try(() => { extrusion.Name = role + "_thin_sheet"; });
            Try(() => part.ForceRebuild3(false));
            ApplyNeutralGrey(part);
            result.Saved = SavePart(part, path);
            Try(() => sw.CloseDoc(part.GetTitle()));
            if (!result.Saved) result.Error = "part save failed";
            return result;
        }

        private static bool SelectFrontPlane(ModelDoc2 model)
        {
            ModelDocExtension ext = model.Extension;
            if (ext == null) return false;
            foreach (string name in new[] { "Front Plane", "前视基准面", "前基准面" })
            {
                if (TryValue(() => ext.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0), false)) return true;
            }
            return false;
        }

        private static void ApplyNeutralGrey(ModelDoc2 model)
        {
            Try(() =>
            {
                model.MaterialPropertyValues = new[]
                {
                    0.48, 0.50, 0.56,
                    1.0,
                    0.65, 0.35, 0.25,
                    0.0, 0.0
                };
            });
        }

        private static PlacementResult AddComponent(ISldWorks sw, ModelDoc2 asmModel, AssemblyDoc asm, MathUtility math, string role, string path, double txMm, double tyMm, double tzMm)
        {
            var row = new PlacementResult
            {
                Role = role,
                Path = path,
                Exists = File.Exists(path),
                TxMm = txMm,
                TyMm = tyMm,
                TzMm = tzMm,
            };
            if (!row.Exists)
            {
                row.Error = "component path missing";
                return row;
            }

            int errors = 0;
            int warnings = 0;
            int docType = path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART;
            ModelDoc2 doc = TryValue(
                () => sw.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2,
                null);
            row.Opened = doc != null;
            row.OpenErrors = errors;
            row.OpenWarnings = warnings;
            string docTitle = TryValue(() => doc == null ? "" : doc.GetTitle(), "");
            if (doc == null)
            {
                row.Error = "open failed";
                return row;
            }

            try
            {
                Try(() => sw.ActivateDoc2(asmModel.GetTitle(), false, ref errors));
                Component2 comp = TryValue(
                    () => asm.AddComponent5(
                        path,
                        (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                        "",
                        false,
                        "",
                        txMm / 1000.0,
                        tyMm / 1000.0,
                        tzMm / 1000.0) as Component2,
                    null);
                row.Added = comp != null;
                if (comp == null)
                {
                    row.Error = "add failed";
                    return row;
                }
                Try(() => { comp.Name2 = role; });
                MathTransform xf = math.CreateTransform(new[]
                {
                    1.0, 0.0, 0.0,
                    0.0, 1.0, 0.0,
                    0.0, 0.0, 1.0,
                    txMm / 1000.0, tyMm / 1000.0, tzMm / 1000.0,
                    1.0, 0.0, 0.0, 0.0
                }) as MathTransform;
                row.TransformCreated = xf != null;
                row.TransformApplied = xf != null && TryValue(() => comp.SetTransformAndSolve2(xf), false);
                if (!row.TransformApplied) row.Error = "transform apply failed";
                return row;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(docTitle) && !string.Equals(docTitle, asmModel.GetTitle(), StringComparison.OrdinalIgnoreCase))
                {
                    Try(() => sw.CloseDoc(docTitle));
                }
            }
        }

        private static ModelDoc2 NewPartDocument(ISldWorks sw)
        {
            ModelDoc2 model = TryValue(() => sw.NewPart() as ModelDoc2, null);
            if (model != null) return model;

            string template = TryValue(
                () => sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart),
                "");
            if (!string.IsNullOrWhiteSpace(template) && File.Exists(template))
            {
                model = TryValue(() => sw.NewDocument(template, 0, 0, 0) as ModelDoc2, null);
                if (model != null) return model;
            }
            foreach (string candidate in new[]
            {
                @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2020\templates\gb_part.prtdot",
                @"D:\soildworks2020\SOLIDWORKS\lang\chinese-simplified\Tutorial\part.prtdot",
                @"D:\soildworks2020\SOLIDWORKS\lang\english\Tutorial\part.prtdot",
            })
            {
                if (!File.Exists(candidate)) continue;
                model = TryValue(() => sw.NewDocument(candidate, 0, 0, 0) as ModelDoc2, null);
                if (model != null) return model;
            }
            return null;
        }

        private static ModelDoc2 OpenAssemblyDocument(ISldWorks sw, string path, BuildResult result)
        {
            int errors = 0;
            int warnings = 0;
            ModelDoc2 model = TryValue(
                () => sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings) as ModelDoc2,
                null);
            result.OpenCopiedAssemblyErrors = errors;
            result.OpenCopiedAssemblyWarnings = warnings;
            result.OpenedCopiedAssembly = model != null;
            return model;
        }

        private static bool SavePart(ModelDoc2 model, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            int errors = 0;
            int warnings = 0;
            ModelDocExtension ext = model.Extension;
            bool saved = TryValue(() => model.SaveAs(path), false);
            if (saved && File.Exists(path)) return true;
            if (ext != null)
            {
                saved = TryValue(
                    () => ext.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings),
                    false);
            }
            return saved && File.Exists(path);
        }

        private static bool SaveModelWithRetry(ModelDoc2 model, string path, BuildResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                result.SaveAttempts = attempt;
                Try(() => model.ForceRebuild3(false));
                bool saved = TryValue(() => model.SaveAs(path), false);
                if (saved && File.Exists(path)) return true;

                int errors = 0;
                int warnings = 0;
                ModelDocExtension ext = TryValue(() => model.Extension, null);
                if (ext != null)
                {
                    saved = TryValue(
                        () => ext.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings),
                        false);
                    result.SaveErrors = errors;
                    result.SaveWarnings = warnings;
                    if (saved && File.Exists(path)) return true;
                }
                System.Threading.Thread.Sleep(500);
            }
            return File.Exists(path);
        }

        private static bool HasPanelErrors(List<PanelResult> panels)
        {
            foreach (PanelResult row in panels)
            {
                if (!string.IsNullOrWhiteSpace(row.Error)) return true;
                if (!row.NewPart || !row.FrontPlaneSelected || !row.RectangleCreated || !row.ExtrusionCreated || !row.Saved) return true;
            }
            return false;
        }

        private static bool HasPlacementErrors(List<PlacementResult> placements)
        {
            foreach (PlacementResult row in placements)
            {
                if (!string.IsNullOrWhiteSpace(row.Error)) return true;
                if (!row.Exists || !row.Opened || !row.Added || !row.TransformCreated || !row.TransformApplied) return true;
            }
            return false;
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

        private static T TryValue<T>(Func<T> func, T fallback)
        {
            try { return func(); } catch { return fallback; }
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

        private static void WriteJson(string path, BuildResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson(result), new UTF8Encoding(false));
        }

        private static string ToJson(BuildResult r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            Prop(sb, "sourceAssemblyPath", r.SourceAssemblyPath, true);
            Prop(sb, "outDir", r.OutDir);
            Prop(sb, "outAssemblyPath", r.OutAssemblyPath);
            Prop(sb, "cabinetWidthMm", r.CabinetWidthMm);
            Prop(sb, "backPanelYMinMm", r.BackPanelYMinMm);
            Prop(sb, "backPanelYMaxMm", r.BackPanelYMaxMm);
            Prop(sb, "panelThicknessMm", r.PanelThicknessMm);
            Prop(sb, "centerGapMm", r.CenterGapMm);
            Prop(sb, "leftPanelXMinMm", r.LeftPanelXMinMm);
            Prop(sb, "leftPanelXMaxMm", r.LeftPanelXMaxMm);
            Prop(sb, "rightPanelXMinMm", r.RightPanelXMinMm);
            Prop(sb, "rightPanelXMaxMm", r.RightPanelXMaxMm);
            Prop(sb, "seamCenterXMm", r.SeamCenterXMm);
            Prop(sb, "seamGapMm", r.SeamGapMm);
            Prop(sb, "copiedSourceAssembly", r.CopiedSourceAssembly);
            Prop(sb, "openedCopiedAssembly", r.OpenedCopiedAssembly);
            Prop(sb, "openCopiedAssemblyErrors", r.OpenCopiedAssemblyErrors);
            Prop(sb, "openCopiedAssemblyWarnings", r.OpenCopiedAssemblyWarnings);
            Prop(sb, "rebuilt", r.Rebuilt);
            Prop(sb, "saved", r.Saved);
            Prop(sb, "saveAttempts", r.SaveAttempts);
            Prop(sb, "saveErrors", r.SaveErrors);
            Prop(sb, "saveWarnings", r.SaveWarnings);
            Prop(sb, "error", r.Error);
            sb.Append(",\"panels\":[");
            for (int i = 0; i < r.Panels.Count; i++)
            {
                if (i > 0) sb.Append(",");
                PanelResult p = r.Panels[i];
                sb.Append("{");
                Prop(sb, "role", p.Role, true);
                Prop(sb, "path", p.Path);
                Prop(sb, "widthMm", p.WidthMm);
                Prop(sb, "heightMm", p.HeightMm);
                Prop(sb, "thicknessMm", p.ThicknessMm);
                Prop(sb, "newPart", p.NewPart);
                Prop(sb, "frontPlaneSelected", p.FrontPlaneSelected);
                Prop(sb, "sketchOpened", p.SketchOpened);
                Prop(sb, "rectangleCreated", p.RectangleCreated);
                Prop(sb, "extrusionCreated", p.ExtrusionCreated);
                Prop(sb, "saved", p.Saved);
                Prop(sb, "error", p.Error);
                sb.Append("}");
            }
            sb.Append("],\"placements\":[");
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
            sb.Append(",\"").Append(Escape(name)).Append("\":").Append(value.ToString("0.#########", CultureInfo.InvariantCulture));
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class BuildResult
        {
            public string SourceAssemblyPath = "";
            public string OutDir = "";
            public string OutAssemblyPath = "";
            public double CabinetWidthMm;
            public double BackPanelYMinMm;
            public double BackPanelYMaxMm;
            public double PanelThicknessMm;
            public double CenterGapMm;
            public double LeftPanelXMinMm;
            public double LeftPanelXMaxMm;
            public double RightPanelXMinMm;
            public double RightPanelXMaxMm;
            public double SeamCenterXMm;
            public double SeamGapMm;
            public bool CopiedSourceAssembly;
            public bool OpenedCopiedAssembly;
            public int OpenCopiedAssemblyErrors;
            public int OpenCopiedAssemblyWarnings;
            public bool Rebuilt;
            public bool Saved;
            public int SaveAttempts;
            public int SaveErrors;
            public int SaveWarnings;
            public string Error = "";
            public readonly List<PanelResult> Panels = new List<PanelResult>();
            public readonly List<PlacementResult> Placements = new List<PlacementResult>();
        }

        private sealed class PanelResult
        {
            public string Role = "";
            public string Path = "";
            public double WidthMm;
            public double HeightMm;
            public double ThicknessMm;
            public bool NewPart;
            public bool FrontPlaneSelected;
            public bool SketchOpened;
            public bool RectangleCreated;
            public bool ExtrusionCreated;
            public bool Saved;
            public string Error = "";
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
            public double TxMm;
            public double TyMm;
            public double TzMm;
            public int OpenErrors;
            public int OpenWarnings;
            public string Error = "";
        }
    }
}
