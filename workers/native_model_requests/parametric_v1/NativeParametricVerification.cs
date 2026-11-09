using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private static string PackageTextHash(string value)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }

        private static Dictionary<string, string> PackageInventory(string folder, bool cadOnly)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(folder)) return result;
            foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(path => !cadOnly || IsPackageCad(path)))
            {
                ParametricContext.CheckPath(path, folder);
                result.Add(Path.GetFullPath(path), ParametricContext.Hash(path));
            }
            return result;
        }

        private static string PackageInventoryDigest(Dictionary<string, string> inventory)
        {
            return PackageTextHash(String.Concat(inventory.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => row.Key + "\t" + row.Value + "\n")));
        }

        private static void RequirePackageInventory(Dictionary<string, string> before, string folder, bool cadOnly, string error)
        {
            var after = PackageInventory(folder, cadOnly);
            ParametricContext.Require(before.Count == after.Count && before.All(row => after.ContainsKey(row.Key) && row.Value == after[row.Key]), error);
        }

        private static string[] PackageDependencies(ISldWorks sw, string file)
        {
            Array raw = sw.GetDocumentDependencies2(file, false, false, false) as Array;
            if (raw == null) return new string[0];
            ParametricContext.Require(raw.Length % 2 == 0, "PACKAGE_DEPENDENCY_ROWS_INVALID " + file);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < raw.Length; i += 2)
            {
                string path = Convert.ToString(raw.GetValue(i));
                if (!IsPackageCad(path)) continue;
                ParametricContext.Require(Path.IsPathRooted(path), "PACKAGE_DEPENDENCY_NOT_ABSOLUTE " + path);
                paths.Add(Path.GetFullPath(path));
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static List<ModelDoc2> PackageScopedDocuments(ISldWorks sw, IEnumerable<string> allowed)
        {
            var paths = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
            var documents = (sw.GetDocuments() as Array ?? new object[0]).Cast<ModelDoc2>().ToList();
            foreach (ModelDoc2 document in documents)
            {
                string path = document.GetPathName();
                ParametricContext.Require(!String.IsNullOrEmpty(path) && Path.IsPathRooted(path), "PACKAGE_LOADED_DOCUMENT_UNSAVED " + path);
                ParametricContext.Require(paths.Contains(Path.GetFullPath(path)), "PACKAGE_LOADED_DOCUMENT_OUTSIDE_SCOPE " + path);
            }
            return documents;
        }

        private static void RequirePackageDocuments(ISldWorks sw, IEnumerable<string> allowed, bool readOnly)
        {
            foreach (ModelDoc2 document in PackageScopedDocuments(sw, allowed))
                if (readOnly) ParametricContext.Require(document.IsOpenedReadOnly(), "PACKAGE_LOADED_DOCUMENT_NOT_READONLY " + document.GetPathName());
        }

        private static void ConstrainPackageDocumentsReadOnly(ISldWorks sw, IEnumerable<string> allowed, string conversionFolder, string stage)
        {
            var documents = PackageScopedDocuments(sw, allowed);
            foreach (ModelDoc2 document in documents)
            {
                if (document.IsOpenedReadOnly()) continue;
                string path = Path.GetFullPath(document.GetPathName());
                ParametricContext.Require(path.StartsWith(Path.GetFullPath(conversionFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "PACKAGE_LOADED_DOCUMENT_WRITABLE_OUTSIDE_CONVERSION_SCOPE " + path);
                ParametricContext.CheckPath(path, conversionFolder);
                ParametricContext.Require(document.SetReadOnlyState(true), "PACKAGE_READONLY_CONVERSION_FAILED " + stage + " " + path);
                ParametricContext.Require(document.IsOpenedReadOnly(), "PACKAGE_READONLY_CONVERSION_UNCONFIRMED " + stage + " " + path);
                Console.WriteLine("PACKAGE_READONLY_CONVERTED " + stage + " " + path);
            }
            RequirePackageDocuments(sw, allowed, true);
        }

        private static string PackageNumbers(IEnumerable<double> values)
        {
            return String.Join(",", values.Select(value => {
                ParametricContext.Require(!Double.IsNaN(value) && !Double.IsInfinity(value), "PACKAGE_STATE_NONFINITE");
                return Math.Round(value / 1e-8).ToString("0", CultureInfo.InvariantCulture);
            }).ToArray());
        }

        private static string PackageGeometryState(ModelDoc2 model)
        {
            var state = new List<string>();
            Array bodies = ((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false) as Array ?? new object[0];
            state.Add("BODIES:" + bodies.Length);
            foreach (Body2 body in bodies)
            {
                ParametricContext.Require(body.Check2() == 0, "PACKAGE_INVALID_SOLID " + model.GetPathName());
                double[] mass = body.GetMassProperties(1) as double[];
                ParametricContext.Require(mass != null && mass.Length >= 4 && mass[3] > 0, "PACKAGE_SOLID_VOLUME_UNAVAILABLE");
                state.Add("B:" + body.GetVertexCount() + ":" + body.GetEdgeCount() + ":" + body.GetFaceCount() + ":" + Math.Round(mass[3] / 1e-12).ToString("0", CultureInfo.InvariantCulture) + ":" + PackageNumbers(ExactBounds(body).Select(value => value / 1000)));
                foreach (Vertex vertex in body.GetVertices() as Array ?? new object[0]) state.Add("V:" + PackageNumbers(vertex.GetPoint() as double[]));
                foreach (Edge edge in body.GetEdges() as Array ?? new object[0])
                {
                    Curve curve = edge.GetCurve() as Curve;
                    double[] parameters = edge.GetCurveParams2() as double[];
                    ParametricContext.Require(curve != null && parameters != null && parameters.Length >= 8, "PACKAGE_EDGE_STATE_UNAVAILABLE");
                    var samples = new List<string>();
                    foreach (double t in new[] { 0.0, .25, .5, .75, 1.0 })
                    {
                        double[] point = curve.Evaluate2(parameters[6] + t * (parameters[7] - parameters[6]), 0) as double[];
                        ParametricContext.Require(point != null && point.Length >= 3, "PACKAGE_EDGE_SAMPLE_UNAVAILABLE");
                        samples.Add(PackageNumbers(point.Take(3)));
                    }
                    samples.Sort(StringComparer.Ordinal);
                    state.Add("E:" + curve.Identity() + ":" + String.Join(";", samples.ToArray()));
                }
                foreach (Face2 face in body.GetFaces() as Array ?? new object[0])
                {
                    Surface surface = face.GetSurface() as Surface;
                    ParametricContext.Require(surface != null, "PACKAGE_FACE_STATE_UNAVAILABLE");
                    state.Add("F:" + surface.Identity() + ":" + Math.Round(face.GetArea() / 1e-12).ToString("0", CultureInfo.InvariantCulture) +
                        (surface.IsPlane() ? ":" + PackageNumbers(surface.PlaneParams as double[]) : surface.IsCylinder() ? ":" + PackageNumbers(surface.CylinderParams as double[]) : ""));
                }
            }
            state.Sort(StringComparer.Ordinal);
            return PackageTextHash(String.Join("\n", state.ToArray()));
        }

        private static List<object> PackageDocumentState(ISldWorks sw, string path, Dictionary<string, string> logicalPaths, IEnumerable<string> allowed)
        {
            sw.CloseAllDocuments(true);
            ModelDoc2 model = Open(sw, path, true);
            var result = new List<object>();
            try
            {
                ConstrainPackageDocumentsReadOnly(sw, allowed, Path.GetDirectoryName(path), "open " + Path.GetFileName(path));
                ParametricContext.Require(model.IsOpenedReadOnly(), "PACKAGE_STATE_DOCUMENT_NOT_READONLY " + path);
                string initial = model.ConfigurationManager.ActiveConfiguration.Name;
                Array configurations = model.GetConfigurationNames() as Array;
                ParametricContext.Require(configurations != null && configurations.Length > 0, "PACKAGE_STATE_CONFIGURATIONS_MISSING " + path);
                foreach (string configuration in configurations.Cast<object>().Select(Convert.ToString).OrderBy(name => name, StringComparer.Ordinal))
                {
                    ActivatePhysicalConfiguration(model, configuration, Path.GetFileName(path));
                    ConstrainPackageDocumentsReadOnly(sw, allowed, Path.GetDirectoryName(path), "activate " + Path.GetFileName(path) + "/" + configuration);
                    ParametricContext.Require(model.ForceRebuild3(false), "PACKAGE_STATE_REBUILD_FAILED " + path + "/" + configuration);
                    ConstrainPackageDocumentsReadOnly(sw, allowed, Path.GetDirectoryName(path), "rebuild " + Path.GetFileName(path) + "/" + configuration);
                    var features = PhysicalFeatures(model).Select(feature => new { name = feature.Name, type = feature.GetTypeName2(), suppressed = feature.IsSuppressed(), error = feature.GetErrorCode() }).OrderBy(feature => feature.name, StringComparer.Ordinal).ToArray();
                    string geometry = "", assemblyState = "";
                    if (model is PartDoc) geometry = PackageGeometryState(model);
                    else
                    {
                        var components = new List<object>();
                        Array children = ((AssemblyDoc)model).GetComponents(false) as Array;
                        ParametricContext.Require(children != null, "PACKAGE_STATE_COMPONENTS_MISSING " + path);
                        foreach (Component2 component in children)
                        {
                            string actual = Path.GetFullPath(component.GetPathName()), logical;
                            ParametricContext.Require(logicalPaths.TryGetValue(actual, out logical), "PACKAGE_STATE_UNMAPPED_COMPONENT " + actual);
                            MathTransform transform = component.GetTotalTransform(true) as MathTransform;
                            ParametricContext.Require(transform != null, "PACKAGE_STATE_TRANSFORM_MISSING " + actual);
                            components.Add(new { component = component.Name2, sourcePath = logical, configuration = component.ReferencedConfiguration, suppressed = component.IsSuppressed(), hidden = component.IsHidden(true), transform = PackageNumbers((double[])transform.ArrayData) });
                        }
                        assemblyState = PackageTextHash(ParametricContext.Json.Serialize(components.OrderBy(item => ParametricContext.Json.Serialize(item), StringComparer.Ordinal).ToArray()));
                    }
                    result.Add(new { configuration = configuration, documentType = model is PartDoc ? "part" : "assembly", geometrySha256 = geometry,
                        featureStateSha256 = PackageTextHash(ParametricContext.Json.Serialize(features)), assemblyStateSha256 = assemblyState });
                }
                ActivatePhysicalConfiguration(model, initial, Path.GetFileName(path));
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(model); }
            return result;
        }

        private static void PackageRoot(ISldWorks sw, List<object> rows)
        {
            string source = Find("标准寄存柜1917×1000×550(总装配).SLDASM");
            string working = ParametricContext.CadDirectory, doors = Path.Combine(ParametricContext.AttemptDirectory, "doors");
            var workingBefore = PackageInventory(working, true);
            var doorsBefore = PackageInventory(doors, false);
            var sourcePaths = workingBefore.Keys.ToDictionary(path => path, path => path, StringComparer.OrdinalIgnoreCase);
            string isolation = Path.Combine(ParametricContext.AttemptDirectory, "package_input");
            string output = Path.Combine(ParametricContext.AttemptDirectory, "package");
            var graph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> visit = null;
            visit = delegate(string file) {
                if (!closure.Add(file)) return;
                ParametricContext.Require(workingBefore.ContainsKey(file), "PACKAGE_REQUIRED_SOURCE_OUTSIDE_WORKING_INVENTORY " + file);
                string[] dependencies = PackageDependencies(sw, file).Select(dependency => {
                    string canonical;
                    ParametricContext.Require(sourcePaths.TryGetValue(dependency, out canonical), "PACKAGE_REQUIRED_REFERENCE_OUTSIDE_WORKING_INVENTORY " + dependency);
                    return canonical;
                }).OrderBy(path => path, StringComparer.Ordinal).ToArray(); graph.Add(file, dependencies);
                foreach (string dependency in dependencies) visit(dependency);
            };
            visit(source);
            var isolatedBySource = closure.ToDictionary(path => path, path => Path.Combine(isolation, Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase);
            string targetName = "16029_" + ParametricContext.WidthMm.ToString("0.######", CultureInfo.InvariantCulture) + "W_" + ParametricContext.HeightMm.ToString("0.######", CultureInfo.InvariantCulture) + "H_" + ParametricContext.DepthMm.ToString("0.######", CultureInfo.InvariantCulture) + "D.SLDASM";
            var targetBySource = closure.ToDictionary(path => path, path => Path.Combine(output, path.Equals(source, StringComparison.OrdinalIgnoreCase) ? targetName : Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase);
            ParametricContext.Require(isolatedBySource.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == closure.Count && targetBySource.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == closure.Count, "PACKAGE_NAME_COLLISION");
            var logicalPaths = isolatedBySource.ToDictionary(row => row.Value, row => row.Key, StringComparer.OrdinalIgnoreCase);
            foreach (string path in closure) logicalPaths.Add(path, path);
            var states = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
            int preference = (int)swUserPreferenceToggle_e.swExtRefOpenReadOnly;
            bool previous = sw.GetUserPreferenceToggle(preference);
            ModelDoc2 model = null;
            try
            {
                sw.CloseAllDocuments(true);
                sw.SetUserPreferenceToggle(preference, true);
                ParametricContext.Require(sw.GetUserPreferenceToggle(preference), "PACKAGE_READONLY_REFERENCE_POLICY_FAILED");
                ParametricContext.AssertMutation(output); ParametricContext.AssertMutation(isolation);
                ParametricContext.Require(!Directory.Exists(output) && !Directory.Exists(isolation), "PACKAGE_ALREADY_EXISTS");
                Directory.CreateDirectory(output); Directory.CreateDirectory(isolation);
                foreach (string path in closure)
                {
                    File.Copy(path, isolatedBySource[path], false);
                    ParametricContext.Require(ParametricContext.Hash(isolatedBySource[path]) == workingBefore[path], "PACKAGE_ISOLATED_COPY_HASH_MISMATCH");
                }
                sw.SetCurrentWorkingDirectory(isolation);
                foreach (string path in closure.OrderBy(path => path, StringComparer.Ordinal))
                    states.Add(path, PackageDocumentState(sw, isolatedBySource[path], logicalPaths, logicalPaths.Keys));
                foreach (string path in closure) ParametricContext.Require(ParametricContext.Hash(isolatedBySource[path]) == workingBefore[path], "PACKAGE_BASELINE_OPEN_CHANGED_ISOLATED_COPY " + path);
                foreach (string path in closure)
                {
                    string copy = isolatedBySource[path];
                    foreach (string dependency in graph[path])
                        ParametricContext.Require(sw.ReplaceReferencedDocument(copy, dependency, isolatedBySource[dependency]), "PACKAGE_ISOLATED_REFERENCE_REBIND_FAILED " + copy + " " + dependency);
                    ParametricContext.Require(PackageDependencies(sw, copy).SequenceEqual(graph[path].Select(dependency => isolatedBySource[dependency]).OrderBy(value => value, StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase), "PACKAGE_ISOLATED_REFERENCE_GRAPH_MISMATCH " + copy);
                }
                var packInputs = PackageInventory(isolation, true);
                model = Open(sw, isolatedBySource[source], true);
                ConstrainPackageDocumentsReadOnly(sw, isolatedBySource.Values, isolation, "pack-root-open");
                PackAndGo pack = model.Extension.GetPackAndGo() as PackAndGo;
                ParametricContext.Require(pack != null, "PACK_AND_GO_UNAVAILABLE");
                pack.IncludeDrawings = false;
                pack.IncludeSimulationResults = false;
                pack.IncludeSuppressed = false;
                pack.IncludeToolboxComponents = true;
                pack.FlattenToSingleFolder = true;
                ParametricContext.Require(pack.SetSaveToName(true, output), "PACKAGE_DIRECTORY_REJECTED");
                object documents;
                ParametricContext.Require(pack.GetDocumentNames(out documents), "PACKAGE_SOURCE_NAMES_UNAVAILABLE");
                Array names = documents as Array;
                ParametricContext.Require(names != null && names.Length == closure.Count, "PACKAGE_DOCUMENT_SET_DIFFERS_FROM_REQUIRED_CLOSURE");
                var isolatedSources = names.Cast<object>().Select(value => Path.GetFullPath(Convert.ToString(value))).ToArray();
                ParametricContext.Require(isolatedSources.Distinct(StringComparer.OrdinalIgnoreCase).Count() == closure.Count && isolatedSources.All(logicalPaths.ContainsKey) && isolatedSources.All(path => isolatedBySource.Values.Contains(path, StringComparer.OrdinalIgnoreCase)), "PACKAGE_DOCUMENT_SOURCE_OUTSIDE_ISOLATION");
                var targets = isolatedSources.Select(path => targetBySource[logicalPaths[path]]).ToList();
                ParametricContext.Require(pack.SetDocumentSaveToNames(targets.ToArray()), "PACKAGE_RENAME_FAILED");
                Array result = model.Extension.SavePackAndGo(pack) as Array;
                ParametricContext.Require(result != null && result.Cast<object>().All(value => Convert.ToInt32(value) == 0), "PACKAGE_SAVE_FAILED");
                sw.CloseAllDocuments(true); Marshal.ReleaseComObject(model); model = null;
                var final = PackageInventory(output, true);
                ParametricContext.Require(final.Count == closure.Count && targetBySource.Values.All(final.ContainsKey), "PACKAGE_FINAL_DOCUMENT_SET_MISMATCH");
                var targetLogicalPaths = targetBySource.ToDictionary(row => row.Value, row => row.Key, StringComparer.OrdinalIgnoreCase);
                var isolatedAfterLogicalPaths = isolatedBySource.ToDictionary(row => row.Value, row => row.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var target in targetLogicalPaths) isolatedAfterLogicalPaths.Add(target.Key, target.Value);
                var packageRows = new List<Dictionary<string, object>>();
                foreach (string path in closure.OrderBy(path => path, StringComparer.Ordinal))
                {
                    string target = targetBySource[path];
                    string isolatedAfterSha256 = ParametricContext.Hash(isolatedBySource[path]);
                    string[] isolatedAfterDependencies = PackageDependencies(sw, isolatedBySource[path]);
                    ParametricContext.Require(isolatedAfterDependencies.All(isolatedAfterLogicalPaths.ContainsKey) && isolatedAfterDependencies.Select(dependency => isolatedAfterLogicalPaths[dependency]).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(graph[path], StringComparer.OrdinalIgnoreCase), "PACKAGE_ISOLATED_AFTER_REFERENCE_GRAPH_CHANGED " + path);
                    var isolatedAfterState = PackageDocumentState(sw, isolatedBySource[path], isolatedAfterLogicalPaths, isolatedAfterLogicalPaths.Keys);
                    ParametricContext.Require(ParametricContext.Json.Serialize(states[path]) == ParametricContext.Json.Serialize(isolatedAfterState), "PACKAGE_ISOLATED_NATIVE_STATE_CHANGED " + path);
                    ParametricContext.Require((packInputs[isolatedBySource[path]] == workingBefore[path] && isolatedAfterSha256 == packInputs[isolatedBySource[path]]) || graph[path].Length > 0, "PACKAGE_UNEXPLAINED_ISOLATED_FILE_CHANGE " + path);
                    string[] dependencies = PackageDependencies(sw, target);
                    ParametricContext.Require(dependencies.SequenceEqual(graph[path].Select(dependency => targetBySource[dependency]).OrderBy(value => value, StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase), "PACKAGE_TARGET_REFERENCE_GRAPH_MISMATCH " + target);
                    var targetState = PackageDocumentState(sw, target, targetLogicalPaths, targetLogicalPaths.Keys);
                    ParametricContext.Require(ParametricContext.Json.Serialize(states[path]) == ParametricContext.Json.Serialize(targetState), "PACKAGE_NATIVE_STATE_CHANGED " + target);
                    string reason = path.Equals(source, StringComparison.OrdinalIgnoreCase) ? "root_rename_and_reference_rewrite" : workingBefore[path] == final[target] ? "byte_identical" : "reference_rewrite";
                    ParametricContext.Require(reason != "reference_rewrite" || graph[path].Length > 0, "PACKAGE_UNEXPLAINED_FILE_CHANGE " + target);
                    packageRows.Add(new Dictionary<string, object> { { "path", target }, { "sha256", final[target] }, { "sourcePath", path }, { "sourceSha256", workingBefore[path] },
                        { "isolatedPath", isolatedBySource[path] }, { "isolatedCopySha256", workingBefore[path] }, { "packInputSha256", packInputs[isolatedBySource[path]] }, { "isolatedAfterSha256", isolatedAfterSha256 }, { "isolatedAfterState", isolatedAfterState }, { "isolatedAfterDependencies", isolatedAfterDependencies },
                        { "allowedChangeReason", reason }, { "sourceDependencies", graph[path] }, { "targetDependencies", dependencies }, { "sourceState", states[path] }, { "targetState", targetState } });
                }
                var verificationRows = new List<object>();
#if WINNSEN_PRODUCTION_PACKAGE
                VerifyPackagedProduction(sw, output, targetBySource[source], targetBySource, verificationRows);
#endif
                RequirePackageInventory(final, output, true, "PACKAGE_VERIFICATION_CHANGED_TARGET");
                RequirePackageInventory(workingBefore, working, true, "PACKAGE_CHANGED_WORKING_SOURCE");
                RequirePackageInventory(doorsBefore, doors, false, "PACKAGE_CHANGED_PROTECTED_DOOR_FILES");
                packageRows.Single(row => ((string)row["sourcePath"]).Equals(source, StringComparison.OrdinalIgnoreCase)).Add("provenance", new {
                    schema = "winnsen.locker16029.package_provenance.v1", taskId = ParametricContext.Text(ParametricContext.Plan, "taskId"), planSha256 = ParametricContext.PlanHash,
                    executableSha256 = ParametricContext.Hash(System.Reflection.Assembly.GetExecutingAssembly().Location), sourceRoot = source, isolatedRoot = isolatedBySource[source], targetRoot = targetBySource[source],
                    includeSuppressed = false, sourceInventorySha256 = PackageInventoryDigest(workingBefore), protectedDoorFilesBeforeSha256 = PackageInventoryDigest(doorsBefore),
                    protectedDoorFilesAfterSha256 = PackageInventoryDigest(PackageInventory(doors, false)), rootDependencyClosure = closure.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                    excluded = workingBefore.Where(row => !closure.Contains(row.Key)).OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new { sourcePath = row.Key, sourceSha256 = row.Value, reason = "outside_root_dependency_closure" }).ToArray(),
                    productionVerificationRows = verificationRows });
                rows.AddRange(packageRows);
            }
            finally
            {
                try { sw.CloseAllDocuments(true); }
                finally
                {
                    if (model != null) Marshal.ReleaseComObject(model);
                    sw.SetUserPreferenceToggle(preference, previous);
                    ParametricContext.Require(sw.GetUserPreferenceToggle(preference) == previous, "PACKAGE_REFERENCE_POLICY_NOT_RESTORED");
                    RequirePackageInventory(workingBefore, working, true, "PACKAGE_CHANGED_WORKING_SOURCE");
                    RequirePackageInventory(doorsBefore, doors, false, "PACKAGE_CHANGED_PROTECTED_DOOR_FILES");
                }
            }
        }

#if WINNSEN_PRODUCTION_PACKAGE
        private static object RemapPackageEvidence(object value, Dictionary<string, string> mapping)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                var remapped = dictionary.ToDictionary(row => row.Key, row => RemapPackageEvidence(row.Value, mapping));
                if (dictionary.ContainsKey("sourceSha256") && dictionary.ContainsKey("sourcePath"))
                {
                    string source = ParametricContext.Text(dictionary, "sourcePath"), packaged;
                    ParametricContext.Require(mapping.TryGetValue(source, out packaged) && ParametricContext.Hash(source) == ParametricContext.Text(dictionary, "sourceSha256"), "PACKAGE_MARK_SOURCE_HASH_MAPPING_FAILED " + source);
                    remapped["sourceSha256"] = ParametricContext.Hash(packaged);
                }
                return remapped;
            }
            string text = value as string, target;
            if (text != null) return mapping.TryGetValue(text, out target) ? target : text;
            if (value is IEnumerable) return ((IEnumerable)value).Cast<object>().Select(item => RemapPackageEvidence(item, mapping)).ToArray();
            return value;
        }

        private static Dictionary<string, string> ReadPackageSourceMapping(string folder)
        {
            string evidencePath = Path.Combine(ParametricContext.AttemptDirectory, "evidence", "package.json");
            var evidence = ParametricContext.Read(evidencePath);
            ParametricContext.Require(Convert.ToBoolean(evidence["success"]), "PACKAGE_SOURCE_MAPPING_NOT_SUCCESSFUL");
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, object> row in (IEnumerable)evidence["rows"])
            {
                string source = ParametricContext.Text(row, "sourcePath"), target = Path.Combine(folder, Path.GetFileName(ParametricContext.Text(row, "path")));
                ParametricContext.CheckPath(source, Path.Combine(ParametricContext.AttemptDirectory, "native_cad", "working_pack"));
                ParametricContext.CheckPath(target, folder);
                ParametricContext.Require(IsPackageCad(source) && IsPackageCad(target) && !mapping.ContainsKey(source) && targets.Add(target) &&
                    ParametricContext.Hash(source) == ParametricContext.Text(row, "sourceSha256") && ParametricContext.Hash(target) == ParametricContext.Text(row, "sha256"), "PACKAGE_SOURCE_MAPPING_FILE_BINDING_FAILED");
                mapping.Add(source, target);
            }
            ParametricContext.Require(mapping.Count > 0 && PackageInventory(folder, true).Count == mapping.Count, "PACKAGE_SOURCE_MAPPING_INVENTORY_MISMATCH");
            return mapping;
        }

        private static void VerifyPackagedProduction(ISldWorks sw, string folder, string rootPath, Dictionary<string, string> mapping, List<object> rows)
        {
            RequireProductionProfile();
            string working = ParametricContext.CadDirectory;
            var original = ParametricContext.Object(VerifiedPhysicalProductionStage("production-apply"), "evidence");
            var verified = ParametricContext.Object(VerifiedPhysicalProductionStage("production-verify"), "evidence");
            var verifiedRows = ((IEnumerable)verified["rows"]).Cast<Dictionary<string, object>>().ToList();
            VerifyProductionMarksEvidence(original, verifiedRows.Cast<object>().ToList());
            foreach (var mark in ((IEnumerable)original["rows"]).Cast<Dictionary<string, object>>().Where(row => ParametricContext.Text(row, "inspection") == "production_marks_apply"))
            {
                var baseline = verifiedRows.Single(row => ParametricContext.Text(row, "inspection") == "production_marks_verify" &&
                    ParametricContext.Text(row, "path").Equals(ParametricContext.Text(mark, "path"), StringComparison.OrdinalIgnoreCase) && ParametricContext.Text(row, "configuration") == ParametricContext.Text(mark, "configuration"));
                mark["marks"] = baseline["marks"];
            }
            var applied = (Dictionary<string, object>)RemapPackageEvidence(original, mapping);
            var changes = ((IEnumerable)applied["rows"]).Cast<Dictionary<string, object>>().ToList();
            var originals = ((IEnumerable)original["rows"]).Cast<Dictionary<string, object>>().Where(row => ParametricContext.Text(row, "inspection") == "production_part_revision").ToList();
            foreach (var revision in changes.Where(row => ParametricContext.Text(row, "inspection") == "production_part_revision"))
            {
                string path = ParametricContext.Text(revision, "path");
                ParametricContext.Require(mapping.Values.Contains(path, StringComparer.OrdinalIgnoreCase), "PACKAGE_PRODUCTION_REVISION_UNMAPPED " + path);
                revision["newProductionSha256"] = ParametricContext.Hash(path);
            }
            var before = PackageInventory(folder, true);
            ModelDoc2 root = null;
            try
            {
                sw.CloseAllDocuments(true); sw.SetCurrentWorkingDirectory(folder);
                ParametricContext.CadDirectory = folder;
                root = Open(sw, rootPath, true);
                ConstrainPackageDocumentsReadOnly(sw, mapping.Values, folder, "packaged-production-open");
                ParametricContext.Require(root.IsOpenedReadOnly(), "PACKAGE_PRODUCTION_ROOT_NOT_READONLY");
                ((AssemblyDoc)root).ResolveAllLightWeightComponents(false);
                ParametricContext.Require(root.ForceRebuild3(false), "PACKAGE_PRODUCTION_REBUILD_FAILED");
                ConstrainPackageDocumentsReadOnly(sw, mapping.Values, folder, "packaged-production-rebuild");
                var components = ProductionComponents(root);
                foreach (var group in components.Where(component => PhysicalPartRole(Path.GetFileName(component.GetPathName())) == "door_panel").GroupBy(component => component.GetPathName(), StringComparer.OrdinalIgnoreCase))
                    VerifyProductionDoor((ModelDoc2)group.First().GetModelDoc2(), group.ToList(), rows);
                VerifyProductionMarks(sw, root, rows);
                foreach (var change in changes)
                {
                    string inspection = ParametricContext.Text(change, "inspection");
                    if (inspection == "production_pin_change") VerifyProductionPinBaseline(change, components, rows);
                    if (inspection == "production_locator_change") VerifyProductionLocatorChange(sw, change, components, rows);
                }
                foreach (var source in originals)
                {
                    string sourcePath = ParametricContext.Text(source, "path"), path;
                    ParametricContext.Require(mapping.TryGetValue(sourcePath, out path) && ParametricContext.Hash(sourcePath) == ParametricContext.Text(source, "newProductionSha256"), "PACKAGE_PRODUCTION_SOURCE_REVISION_CHANGED " + sourcePath);
                    var instances = components.Where(component => component.GetPathName().Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
                    ParametricContext.Require(instances.Count > 0, "PACKAGE_PRODUCTION_REVISION_NOT_LOADED " + path);
                    ModelDoc2 part = (ModelDoc2)instances[0].GetModelDoc2();
                    ParametricContext.Require(part != null && part.IsOpenedReadOnly() && Path.GetFullPath(part.GetPathName()).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase), "PACKAGE_PRODUCTION_REVISION_MODEL_BINDING_FAILED");
                    ProductionPartHealth(part);
                    rows.Add(new { inspection = "production_packaged_part_verification", sourcePath = sourcePath, sourceSha256 = ParametricContext.Text(source, "newProductionSha256"),
                        path = path, file = Path.GetFileName(path), role = ParametricContext.Text(source, "role"), sha256 = ParametricContext.Hash(path), productionProfile = ProductionProcessVersion, pass = true });
                }
                VerifyProductionMarksEvidence(applied, rows);
                ConstrainPackageDocumentsReadOnly(sw, mapping.Values, folder, "packaged-production-final");
            }
            finally
            {
                try { sw.CloseAllDocuments(true); }
                finally { ParametricContext.CadDirectory = working; if (root != null) Marshal.ReleaseComObject(root); }
            }
            RequirePackageInventory(before, folder, true, "PACKAGE_PRODUCTION_READONLY_CHANGED_CAD");
        }
#endif

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
            int preference = (int)swUserPreferenceToggle_e.swExtRefOpenReadOnly;
            bool previous = sw.GetUserPreferenceToggle(preference);
            try
            {
                sw.CloseAllDocuments(true);
                sw.SetUserPreferenceToggle(preference, true);
                ParametricContext.Require(sw.GetUserPreferenceToggle(preference), "PACKAGE_VERIFY_READONLY_REFERENCE_POLICY_FAILED");
                VerifyPackageContents(sw, rows, folderName);
            }
            finally
            {
                try { sw.CloseAllDocuments(true); }
                finally
                {
                    sw.SetUserPreferenceToggle(preference, previous);
                    ParametricContext.Require(sw.GetUserPreferenceToggle(preference) == previous, "PACKAGE_VERIFY_REFERENCE_POLICY_NOT_RESTORED");
                }
            }
        }

        private static void VerifyPackageContents(ISldWorks sw, List<object> rows, string folderName)
        {
            string folder = Path.Combine(ParametricContext.AttemptDirectory, folderName);
            string root = Directory.GetFiles(folder, "16029_*.SLDASM").Single();
            sw.SetCurrentWorkingDirectory(folder);
            Dictionary<string, string> hashes = Directory.GetFiles(folder).ToDictionary(file => file, ParametricContext.Hash, StringComparer.OrdinalIgnoreCase);
            var physicalInstances = new List<AuditInstance>();
            string rootConfiguration;
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
                ConstrainPackageDocumentsReadOnly(sw, hashes.Keys, folder, "verify-open");
                AssemblyDoc assembly = model as AssemblyDoc;
                assembly.ResolveAllLightWeightComponents(false);
                ParametricContext.Require(model.ForceRebuild3(false), "PACKAGE_REBUILD_FAILED");
                ConstrainPackageDocumentsReadOnly(sw, hashes.Keys, folder, "verify-rebuild");
                rootConfiguration = model.ConfigurationManager.ActiveConfiguration.Name;
                Array all = assembly.GetComponents(false) as Array;
                ParametricContext.Require(all != null, "PACKAGE_COMPONENTS_UNAVAILABLE");
                int actualDoors = 0, actualTongues = 0, featureIssueCount = 0;
                var tonguePositions = new List<double[]>();
                double xmin = double.PositiveInfinity, xmax = double.NegativeInfinity;
                double ymax=double.NegativeInfinity,zmin=double.PositiveInfinity,zmax=double.NegativeInfinity;
                foreach (Component2 component in all)
                {
                    AuditInstance physicalInstance = CapturePhysicalInstance(component, folder, hashes);
                    physicalInstances.Add(physicalInstance);
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
                    var worldBodyBounds = new List<double[]>();
                    foreach (AuditBodyEvidence body in physicalInstance.bodies)
                    {
                        double[] bounds = body.boxMm; worldBodyBounds.Add(bounds);
                        xmin = Math.Min(xmin, bounds[0]); xmax = Math.Max(xmax, bounds[3]);
                        ymax = Math.Max(ymax, bounds[4]); zmin = Math.Min(zmin, bounds[2]); zmax = Math.Max(zmax, bounds[5]);
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
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(model); }
            List<AuditPartConfiguration> physicalParts;
            rows.Add(CapturePhysicalCoverage(sw, folder, root, rootConfiguration, hashes, physicalInstances, "package_verification", out physicalParts));
            if(File.Exists(Path.Combine(folder,MaintenanceWeldmentFile)))VerifyEngineerFeedback(sw,folder,rows);
#if WINNSEN_PRODUCTION_PACKAGE
            VerifyPackagedProduction(sw, folder, root, ReadPackageSourceMapping(folder), rows);
#endif
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
