using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private static void PackageRoot(ISldWorks sw, List<object> rows)
        {
            string source = Find("标准寄存柜1917×1000×550(总装配).SLDASM");
            ModelDoc2 model = Open(sw, source, true);
            string output = Path.Combine(ParametricContext.AttemptDirectory, "package");
            try
            {
                ParametricContext.AssertMutation(output);
                ParametricContext.Require(!Directory.Exists(output), "PACKAGE_ALREADY_EXISTS");
                Directory.CreateDirectory(output);
                PackAndGo pack = model.Extension.GetPackAndGo() as PackAndGo;
                ParametricContext.Require(pack != null, "PACK_AND_GO_UNAVAILABLE");
                pack.IncludeDrawings = false;
                pack.IncludeSimulationResults = false;
                pack.IncludeSuppressed = false;
                pack.IncludeToolboxComponents = true;
                pack.FlattenToSingleFolder = true;
                ParametricContext.Require(pack.SetSaveToName(true, output), "PACKAGE_DIRECTORY_REJECTED");
                object documents, statuses;
                ParametricContext.Require(pack.GetDocumentSaveToNames(out documents, out statuses), "PACKAGE_NAMES_UNAVAILABLE");
                Array names = documents as Array;
                var targets = new List<string>();
                for (int i = 0; i < names.Length; i++)
                {
                    string name = Path.GetFileName(Convert.ToString(names.GetValue(i)));
                    if (name.Equals(Path.GetFileName(source), StringComparison.OrdinalIgnoreCase)) name = "16029_" + ParametricContext.WidthMm.ToString("0.######", CultureInfo.InvariantCulture) + "W_" + ParametricContext.HeightMm + "H_" + ParametricContext.DepthMm + "D.SLDASM";
                    targets.Add(Path.Combine(output, name));
                }
                ParametricContext.Require(targets.Distinct(StringComparer.OrdinalIgnoreCase).Count() == targets.Count, "PACKAGE_NAME_COLLISION");
                ParametricContext.Require(pack.SetDocumentSaveToNames(targets.ToArray()), "PACKAGE_RENAME_FAILED");
                Array result = model.Extension.SavePackAndGo(pack) as Array;
                ParametricContext.Require(result != null && result.Cast<object>().All(value => Convert.ToInt32(value) == 0), "PACKAGE_SAVE_FAILED");
                foreach (string file in Directory.GetFiles(output)) rows.Add(new { path = file, sha256 = ParametricContext.Hash(file) });
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
        }

        private static void VerifyRelocatedPackage(ISldWorks sw,List<object> rows)
        {
            string source=Path.Combine(ParametricContext.AttemptDirectory,"package");
            string archive=Path.Combine(ParametricContext.AttemptDirectory,"native_model.zip");
            string relocated=Path.Combine(ParametricContext.AttemptDirectory,"unzipped_native_model");
            ParametricContext.AssertMutation(archive);ParametricContext.AssertMutation(relocated);
            ParametricContext.Require(!File.Exists(archive)&&!Directory.Exists(relocated),"RELOCATION_OUTPUT_ALREADY_EXISTS");
            System.IO.Compression.ZipFile.CreateFromDirectory(source,archive,System.IO.Compression.CompressionLevel.Optimal,false);
            System.IO.Compression.ZipFile.ExtractToDirectory(archive,relocated);
            var original=Directory.GetFiles(source).ToDictionary(Path.GetFileName,ParametricContext.Hash,StringComparer.OrdinalIgnoreCase);
            var extracted=Directory.GetFiles(relocated).ToDictionary(Path.GetFileName,ParametricContext.Hash,StringComparer.OrdinalIgnoreCase);
            ParametricContext.Require(original.Count==extracted.Count&&original.All(row=>extracted.ContainsKey(row.Key)&&extracted[row.Key]==row.Value),"ZIP_EXTRACTED_HASH_MISMATCH");
            VerifyPackage(sw,rows,"unzipped_native_model");
            rows.Add(new{archive=archive,sha256=ParametricContext.Hash(archive),cadFileCount=original.Count,extractedFilesByteIdentical=true});
        }

        private static void VerifyPackage(ISldWorks sw, List<object> rows,string folderName="package")
        {
            string folder = Path.Combine(ParametricContext.AttemptDirectory, folderName);
            string root = Directory.GetFiles(folder, "16029_*.SLDASM").Single();
            sw.SetCurrentWorkingDirectory(folder);
            Dictionary<string, string> hashes = Directory.GetFiles(folder).ToDictionary(file => file, ParametricContext.Hash);
            var shallowInterfacePaths = new HashSet<string>(new[] {
                "箱体横层板L.sldprt", "箱体横层板R.SLDPRT", "箱体竖隔板L.sldprt", "箱体竖隔板R.SLDPRT",
                "箱体竖隔板加强件.sldprt", "箱体左侧板.sldprt", "箱体右侧板.sldprt"
            }.Select(file => Path.GetFullPath(Path.Combine(folder, file))), StringComparer.OrdinalIgnoreCase);
            var measuredInterfacePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Array dependencies = sw.GetDocumentDependencies2(root, true, true, false) as Array;
            ParametricContext.Require(dependencies != null, "PACKAGE_DEPENDENCIES_UNAVAILABLE");
            for (int i = 1; i < dependencies.Length; i += 2) ParametricContext.CheckPath(Convert.ToString(dependencies.GetValue(i)), folder);
            ModelDoc2 model = Open(sw, root, true);
            try
            {
                ParametricContext.Require(model.ForceRebuild3(false), "PACKAGE_REBUILD_FAILED");
                AssemblyDoc assembly = model as AssemblyDoc;
                Array all = assembly.GetComponents(false) as Array;
                ParametricContext.Require(all != null, "PACKAGE_COMPONENTS_UNAVAILABLE");
                int actualDoors = 0, actualTongues = 0, featureIssueCount = 0;
                var tonguePositions = new List<double[]>();
                double xmin = double.PositiveInfinity, xmax = double.NegativeInfinity;
                double ymax=double.NegativeInfinity,zmin=double.PositiveInfinity,zmax=double.NegativeInfinity;
                foreach (Component2 component in all)
                {
                    if (component.IsSuppressed()) continue;
                    string path = component.GetPathName();
                    ParametricContext.CheckPath(path, folder);
                    if (Path.GetFileName(path).StartsWith("ordinary_door_")) actualDoors++;
                    if (Path.GetFileName(path).StartsWith("mechanical_lock_tongue_")) { actualTongues++; double[] t = component.GetTotalTransform(true).ArrayData as double[]; tonguePositions.Add(new[] { t[9] * 1000, t[10] * 1000, t[11] * 1000 }); }
                    var issues = new List<object>();
                    ModelDoc2 partModel = component.GetModelDoc2() as ModelDoc2;
                    Feature feature = partModel == null ? null : partModel.FirstFeature() as Feature;
                    while (feature != null)
                    {
                        int code = feature.GetErrorCode();
                        if (!feature.IsSuppressed() && code > 0) issues.Add(new { name = feature.Name, type = feature.GetTypeName2(), code = code });
                        feature = feature.GetNextFeature() as Feature;
                    }
                    if (ParametricContext.DepthMm < 450 && shallowInterfacePaths.Contains(Path.GetFullPath(path)) && measuredInterfacePaths.Add(Path.GetFullPath(path)))
                        InspectShallowNativeInterfaces(partModel, path, rows);
                    object info;
                    Array bodies = component.GetBodies3(0, out info) as Array;
                    var worldBodyBounds = new List<double[]>();
                    if (bodies != null) foreach (Body2 sourceBody in bodies)
                    {
                        Body2 body = sourceBody.Copy() as Body2;
                        ParametricContext.Require(body != null && body.ApplyTransform(component.GetTotalTransform(true)), "PACKAGE_BODY_TRANSFORM_FAILED");
                        double[] bounds = ExactBounds(body); worldBodyBounds.Add(bounds);
                        xmin = Math.Min(xmin, bounds[0]); xmax = Math.Max(xmax, bounds[3]);
                        ymax = Math.Max(ymax, bounds[4]); zmin = Math.Min(zmin, bounds[2]); zmax = Math.Max(zmax, bounds[5]);
                        Marshal.ReleaseComObject(body);
                    }
                    rows.Add(new { name = component.Name2, path = path, issues = issues, worldBodyBoundsMm = worldBodyBounds, transform = component.GetTotalTransform(true).ArrayData });
                    featureIssueCount += issues.Count;
                }
                int expectedDoors = Convert.ToInt32(ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "geometry")["doorCount"]);
                rows.Add(new { actualDoors = actualDoors, actualTongues = actualTongues, expectedDoors = expectedDoors, xminMm = xmin, xmaxMm = xmax, measuredWidthMm = xmax - xmin, measuredTopHeightMm=ymax,measuredDepthMm=zmax-zmin, featureIssueCount = featureIssueCount, tonguePositionsMm = tonguePositions });
                ParametricContext.Require(actualDoors == expectedDoors && actualTongues == expectedDoors, "PACKAGE_DOOR_TONGUE_COUNT_MISMATCH");
                ParametricContext.Require(Math.Abs(xmax - xmin - ParametricContext.WidthMm) < 0.2, "PACKAGE_MEASURED_WIDTH_MISMATCH");
                ParametricContext.Require(Math.Abs(ymax-ParametricContext.HeightMm)<.2&&Math.Abs(zmax-zmin-ParametricContext.DepthMm)<.2,"PACKAGE_MEASURED_HEIGHT_DEPTH_MISMATCH");
                ParametricContext.Require(featureIssueCount == 0, "PACKAGE_REBUILD_FEATURE_ISSUES " + featureIssueCount);
                var expectedRows = ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "rowsByColumn");
                foreach (string side in new[] { "L", "R" }) foreach (Dictionary<string, object> row in (IEnumerable)expectedRows[side])
                    ParametricContext.Require(tonguePositions.Count(position => Math.Abs(position[0] - (side == "L" ? -55 : 55)) < .01 && Math.Abs(position[1] - ParametricContext.Number(row, "centerYmm")) < .01 && Math.Abs(position[2] + 11.3) < .01) == 1, "PACKAGE_TONGUE_SLOT_ALIGNMENT_FAILED " + side + row["index"]);
                model.ShowNamedView2("*Isometric", 7); model.ViewZoomtofit2();
                string preview = Path.Combine(ParametricContext.AttemptDirectory, "evidence", "package-isometric.bmp");
                ParametricContext.AssertMutation(preview);
                ParametricContext.Require(model.SaveBMP(preview, 1280, 960), "PACKAGE_PREVIEW_FAILED");
            }
            finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            foreach (var hash in hashes) ParametricContext.Require(ParametricContext.Hash(hash.Key) == hash.Value, "PACKAGE_READONLY_CHANGED");
        }

        private static void InspectShallowNativeInterfaces(ModelDoc2 partModel, string path, List<object> rows)
        {
            PartDoc part = partModel as PartDoc;
            ParametricContext.Require(part != null, "SHALLOW_INTERFACE_PART_NOT_LOADED " + path);
            ParametricContext.Require(Path.GetFullPath(partModel.GetPathName()).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase), "SHALLOW_INTERFACE_MODEL_PATH_MISMATCH " + path);
            Array bodies = part.GetBodies2(0, false) as Array;
            ParametricContext.Require(bodies != null, "SHALLOW_INTERFACE_BODIES_UNAVAILABLE " + path);
            var cylinders = new List<object>();
            var bodyBounds = new List<object>();
            int bodyIndex = 0;
            foreach (Body2 body in bodies)
            {
                double[] bodyBox = body.GetBodyBox() as double[];
                ParametricContext.Require(bodyBox != null && bodyBox.Length == 6, "SHALLOW_INTERFACE_BODY_BOX_UNAVAILABLE " + path);
                bodyBounds.Add(new { bodyIndex = bodyIndex, name = body.Name, boxMm = bodyBox.Select(value => value * 1000).ToArray() });
                Array faces = body.GetFaces() as Array;
                ParametricContext.Require(faces != null, "SHALLOW_INTERFACE_FACES_UNAVAILABLE " + path);
                int faceIndex = 0;
                foreach (Face2 face in faces)
                {
                    Surface surface = face.GetSurface() as Surface;
                    ParametricContext.Require(surface != null, "SHALLOW_INTERFACE_SURFACE_UNAVAILABLE " + path);
                    if (surface.IsCylinder())
                    {
                        double[] parameters = surface.CylinderParams as double[];
                        double[] faceBox = face.GetBox() as double[];
                        ParametricContext.Require(parameters != null && parameters.Length >= 7 && faceBox != null && faceBox.Length == 6, "SHALLOW_INTERFACE_CYLINDER_DATA_UNAVAILABLE " + path);
                        cylinders.Add(new {
                            bodyIndex = bodyIndex, faceIndex = faceIndex, radiusMm = parameters[6] * 1000,
                            axis = new[] { parameters[3], parameters[4], parameters[5] },
                            originMm = new[] { parameters[0] * 1000, parameters[1] * 1000, parameters[2] * 1000 },
                            faceBoxMm = faceBox.Select(value => value * 1000).ToArray()
                        });
                    }
                    faceIndex++;
                }
                bodyIndex++;
            }
            rows.Add(new {
                inspection = "shallow_native_interfaces", file = Path.GetFileName(path), path = path,
                coordinateSystem = "part_local", lengthUnit = "mm", bodyCount = bodies.Length,
                bodyBounds = bodyBounds, cylinders = cylinders
            });
        }
    }
}
