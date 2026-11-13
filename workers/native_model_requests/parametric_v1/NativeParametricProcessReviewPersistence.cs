using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private static object[] ProcessOwnerMates(ModelDoc2 model)
        {
            return PhysicalFeatures(model).Where(feature => feature.GetSpecificFeature2() is Mate2)
                .OrderBy(feature => feature.Name, StringComparer.Ordinal).Select(feature => {
                    Mate2 mate = (Mate2)feature.GetSpecificFeature2();
                    DistanceMateFeatureData distance = feature.GetDefinition() as DistanceMateFeatureData;
                    ParametricContext.Require(!feature.IsSuppressed() && feature.GetErrorCode() == 0, "PROCESS_OWNER_MATE_UNHEALTHY " + feature.Name);
                    return (object)new { name = feature.Name, type = feature.GetTypeName2(), mateType = mate.Type,
                        mateAlignment = mate.Alignment, mateFlipped = mate.Flipped, canBeFlipped = mate.CanBeFlipped,
                        distance = distance == null ? (double?)null : distance.Distance,
                        flip = distance == null ? (bool?)null : distance.FlipDimension,
                        alignment = distance == null ? (int?)null : distance.MateAlignment,
                        entities = Enumerable.Range(0, mate.GetMateEntityCount()).Select(index => {
                            MateEntity2 entity = mate.MateEntity(index); Component2 component = entity.ReferenceComponent;
                            ParametricContext.Require(entity.Reference != null && component != null, "PROCESS_OWNER_MATE_REFERENCE_MISSING " + feature.Name);
                            Array reference = model.Extension.GetPersistReference3(entity.Reference) as Array;
                            ParametricContext.Require(reference != null && reference.Length > 0, "PROCESS_OWNER_REFERENCE_IDENTITY_MISSING");
                            return new { component = component.Name2, path = component.GetPathName(), configuration = component.ReferencedConfiguration,
                                referenceIdentity = Convert.ToBase64String(reference.Cast<object>().Select(Convert.ToByte).ToArray()) };
                        }).ToArray() };
                }).ToArray();
        }

        private static Dictionary<string, double[]> ProcessRootTransforms(ModelDoc2 root)
        {
            var components = ((Array)((AssemblyDoc)root).GetComponents(false)).Cast<Component2>().Where(c => !c.IsSuppressed()).ToList();
            ParametricContext.Require(components.Count > 0, "PROCESS_ROOT_COMPONENTS_MISSING");
            foreach (Component2 component in components) ParametricContext.CheckPath(component.GetPathName(), ParametricContext.AttemptDirectory);
            return ProductionTransforms(components);
        }

        private static void RequireProcessRootTransforms(ModelDoc2 root, Dictionary<string, double[]> expected)
        {
            var actual = ProcessRootTransforms(root);
            ParametricContext.Require(expected.Count == actual.Count && expected.All(entry => actual.ContainsKey(entry.Key) &&
                entry.Value.Zip(actual[entry.Key], (a,b) => Math.Abs(a-b) < 1e-9).All(equal => equal)), "PROCESS_ROOT_SOLVED_PLACEMENT_CHANGED");
        }

        private static string ProcessOwnerSolids(ModelDoc2 owner)
        {
            return ParametricContext.Json.Serialize(ProductionComponents(owner).OrderBy(c => c.Name2, StringComparer.Ordinal)
                .Select(c => new { name = c.Name2, geometry = PMSolidState((ModelDoc2)c.GetModelDoc2()) }).ToArray());
        }

        private static bool ProcessOwnerSolidsMatch(string expected, string actual)
        {
            var a = ((IEnumerable)ParametricContext.Json.DeserializeObject(expected)).Cast<Dictionary<string,object>>().ToArray();
            var b = ((IEnumerable)ParametricContext.Json.DeserializeObject(actual)).Cast<Dictionary<string,object>>().ToArray();
            ParametricContext.Require(a.Length == b.Length && a.Select(row => ParametricContext.Text(row,"name")).SequenceEqual(b.Select(row => ParametricContext.Text(row,"name"))), "PROCESS_OWNER_SOLID_COMPONENT_SET_CHANGED");
            for (int i = 0; i < a.Length; i++) RequireProductionSolidMatch((Dictionary<string,object>)a[i]["geometry"], (Dictionary<string,object>)b[i]["geometry"]);
            return true;
        }

        private static string ProcessOwnerState(ModelDoc2 model)
        {
            return ParametricContext.Json.Serialize(new { configuration = model.ConfigurationManager.ActiveConfiguration.Name,
                components = ((Array)((AssemblyDoc)model).GetComponents(false)).Cast<Component2>().OrderBy(c => c.Name2, StringComparer.Ordinal).Select(c => new {
                    name = c.Name2, path = c.GetPathName(), configuration = c.ReferencedConfiguration, fixedState = c.IsFixed(), suppressed = c.IsSuppressed()
                }).ToArray(), mates = ProcessOwnerMates(model) });
        }

        private static object[] ProcessOwnerContacts(ModelDoc2 owner)
        {
            var instances = new List<PMInstance>();
            try {
                foreach (Component2 component in ProductionComponents(owner)) {
                    string kind = PMKind(component);
                    if (kind != "side" && kind != "side_rib") {
                        ParametricContext.Require(Path.GetFileName(component.GetPathName()).Equals("箱体侧板加强筋2.sldprt", StringComparison.OrdinalIgnoreCase), "PROCESS_OWNER_UNEXPECTED_PART");
                        continue;
                    }
                    ModelDoc2 model = (ModelDoc2)component.GetModelDoc2(); MathTransform transform = component.GetTotalTransform(false), inverse = transform.IInverse();
                    object bodyInfo; Array raw = component.GetBodies3(0, out bodyInfo) as Array;
                    ParametricContext.Require(raw != null && raw.Length == 1, "PROCESS_OWNER_BODY_MISSING");
                    instances.Add(new PMInstance { component = component, model = model, path = component.GetPathName(), name = component.Name2,
                        configuration = component.ReferencedConfiguration, parent = "", kind = kind, transform = transform, inverse = inverse,
                        transformData = (double[])transform.ArrayData, inverseData = (double[])inverse.ArrayData, bodies = raw.Cast<Body2>().ToList() });
                }
                ParametricContext.Require(ProductionComponents(owner).Count == 5 && instances.Count == 4 && instances.Count(p => p.kind == "side") == 1, "PROCESS_OWNER_COMPONENT_SET_INVALID");
                PMInstance target = instances.Single(p => p.kind == "side");
                return instances.Where(p => p.kind == "side_rib").OrderBy(p => p.name, StringComparer.Ordinal).Select(source => {
                    int axis = PMSideContactAxis(source, target); var contacts = PMContacts(source, target, PMPlanes(target, axis));
                    ParametricContext.Require(contacts.Count == 1, "PROCESS_OWNER_CONTACT_AMBIGUOUS " + source.name);
                    PMContact contact = contacts[0]; double gap = contact.projectionOffset * contact.sourceNormalSign;
                    ParametricContext.Require(Math.Abs(gap - .0002) <= PMMaterialTolerance, "PROCESS_OWNER_STANDOFF_CHANGED " + source.name);
                    return (object)new { sourceComponent = source.name, targetComponent = target.name, normalAxis = axis,
                        gapMm = gap * 1000, sourcePlaneM = contact.sourcePlane, targetPlaneM = contact.plane.value,
                        sourceTransform = source.transformData, sourceBoundsInTargetM = contact.exactBounds };
                }).ToArray();
            } finally { PMReleaseInstances(instances); }
        }

        private static object[] ProcessSolverChanges(ModelDoc2 model, Dictionary<string,double[]> cached, Dictionary<string,double[]> solved, bool root)
        {
            ParametricContext.Require(cached.Count == solved.Count && cached.Keys.All(solved.ContainsKey), "PROCESS_SOLVER_COMPONENT_SET_CHANGED");
            var changes = cached.Where(entry => !entry.Value.Zip(solved[entry.Key], (a,b) => Math.Abs(a-b) < 1e-9).All(equal => equal)).ToArray();
            foreach (var change in changes) {
                Component2 component = ((Array)((AssemblyDoc)model).GetComponents(false)).Cast<Component2>().Single(c => c.Name2 == change.Key);
                string name = component.Name2;
                bool identity = root ? new[] { "箱体左侧板焊接-1/箱体侧板加强筋1-3", "箱体右侧板焊接-1/箱体侧板加强筋1-3" }.Contains(name) : name == "箱体侧板加强筋1-3";
                double[] a = change.Value, b = solved[change.Key];
                ParametricContext.Require(identity && Path.GetFileName(component.GetPathName()).Equals("箱体侧板加强筋1.sldprt", StringComparison.OrdinalIgnoreCase) &&
                    (root || !component.IsFixed()) && Enumerable.Range(0, a.Length).All(i => i == 11 || Math.Abs(a[i]-b[i]) < 1e-9) &&
                    Math.Abs((b[11]-a[11])*1000 + ParametricContext.DepthMm-550) < .001, "PROCESS_UNEXPECTED_SOLVER_MOVEMENT " + name);
            }
            return changes.Select(entry => (object)new { component = entry.Key, before = entry.Value, after = solved[entry.Key] }).ToArray();
        }

        private static void ProcessCloseDocuments(ISldWorks sw)
        {
            sw.CloseAllDocuments(true);
            ParametricContext.Require(sw.GetDocumentCount() == 0, "PROCESS_PERSISTENCE_DOCUMENTS_REMAIN");
        }

        private static void ProcessPersistenceReadOnlyDocuments(ISldWorks sw, Dictionary<string,string> cad, Dictionary<string,string> doors, string stage, List<object> rows)
        {
            var allowed = cad.Keys.Concat(doors.Keys).ToArray();
            var documents = PackageScopedDocuments(sw, allowed);
            rows.Add(new { inspection = "process_persistence_loaded_documents", stage, beforeConversion = documents.Select(document => new {
                path = document.GetPathName(), readOnly = document.IsOpenedReadOnly() }).ToArray() });
            foreach (ModelDoc2 document in documents) {
                string path = Path.GetFullPath(document.GetPathName());
                string scope = cad.ContainsKey(path) ? ParametricContext.CadDirectory : Path.Combine(ParametricContext.AttemptDirectory, "doors");
                ParametricContext.CheckPath(path, scope);
                if (document.IsOpenedReadOnly()) continue;
                ParametricContext.Require(document.SetReadOnlyState(true) && document.IsOpenedReadOnly(), "PROCESS_PERSISTENCE_READONLY_CONVERSION_FAILED " + stage + " " + path);
                rows.Add(new { inspection = "process_persistence_readonly_conversion", stage, path, scope, readOnly = true, saved = false });
            }
            RequirePackageDocuments(sw, allowed, true);
            rows.Add(new { inspection = "process_persistence_readonly_documents_verified", stage,
                documents = PackageScopedDocuments(sw, allowed).Select(document => new { path = document.GetPathName(), readOnly = document.IsOpenedReadOnly() }).ToArray() });
        }

        private static void PersistProcessReviewSideOwners(ISldWorks sw, List<object> rows)
        {
            RequireProductionProfile(); ProcessCloseDocuments(sw);
            var before = ProductionInventory(ParametricContext.CadDirectory);
            var doorsBefore = ProductionInventory(Path.Combine(ParametricContext.AttemptDirectory, "doors"));
            string rootPath = Find("标准寄存柜1917×1000×550(总装配).SLDASM");
            var ownerPaths = new[] { Find("箱体左侧板焊接.SLDASM"), Find("箱体右侧板焊接.SLDASM") };
            var expectedOwners = new Dictionary<string, Dictionary<string, double[]>>(StringComparer.OrdinalIgnoreCase);
            var ownerStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ownerSolids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ModelDoc2 root = Open(sw, rootPath, false);
            Dictionary<string, double[]> solvedRoot;
            string rootState;
            try {
                ((AssemblyDoc)root).ResolveAllLightWeightComponents(false);
                var cachedRoot = ProcessRootTransforms(root); rootState = ProcessOwnerState(root);
                ParametricContext.Require(root.ForceRebuild3(false), "PROCESS_ROOT_EXISTING_MATES_REBUILD_FAILED");
                solvedRoot = ProcessRootTransforms(root);
                var rootChanges = ProcessSolverChanges(root, cachedRoot, solvedRoot, true);
                ParametricContext.Require(rootState == ProcessOwnerState(root), "PROCESS_ROOT_CONSTRAINTS_CHANGED");
                rows.Add(new { inspection = "process_root_existing_mates_solve", solverChanges = rootChanges, constraintsUnchanged = true, saved = false });
            } finally { ProcessCloseDocuments(sw); Marshal.ReleaseComObject(root); }
            foreach (string path in ownerPaths) {
                ModelDoc2 owner = Open(sw, path, false);
                try {
                    ((AssemblyDoc)owner).ResolveAllLightWeightComponents(false);
                    string state = ProcessOwnerState(owner), solids = ProcessOwnerSolids(owner); var cached = ProductionTransforms(ProductionComponents(owner));
                    var mates = ProcessOwnerMates(owner);
                    ParametricContext.Require(mates.Length > 0 && owner.ForceRebuild3(false), "PROCESS_OWNER_EXISTING_MATES_REBUILD_FAILED");
                    var solved = ProductionTransforms(ProductionComponents(owner));
                    var changes = ProcessSolverChanges(owner, cached, solved, false);
                    var contacts = ProcessOwnerContacts(owner);
                    string currentState = ProcessOwnerState(owner), currentSolids = ProcessOwnerSolids(owner);
                    rows.Add(new { inspection = "process_side_owner_presave_comparison", path,
                        stateBefore = ParametricContext.Json.DeserializeObject(state), stateAfter = ParametricContext.Json.DeserializeObject(currentState),
                        solidsBefore = ParametricContext.Json.DeserializeObject(solids), solidsAfter = ParametricContext.Json.DeserializeObject(currentSolids),
                        constraintsUnchanged = state == currentState, solidTextIdentical = solids == currentSolids, solverChanges = changes, contacts, saved = false });
                    ParametricContext.Require(state == currentState && ProcessOwnerSolidsMatch(solids, currentSolids), "PROCESS_OWNER_CONSTRAINTS_OR_SOLIDS_CHANGED");
                    Save(owner); RequireProductionTransforms(owner, solved);
                    ParametricContext.Require(state == ProcessOwnerState(owner) && ProcessOwnerSolidsMatch(solids, ProcessOwnerSolids(owner)), "PROCESS_OWNER_SAVE_CHANGED_CONSTRAINTS_OR_SOLIDS");
                    expectedOwners.Add(path, solved); ownerStates.Add(path, state); ownerSolids.Add(path, solids);
                    rows.Add(new { inspection = "process_side_owner_persistence", path, beforeSha256 = before[path], sha256 = ParametricContext.Hash(path),
                        mates, contacts, cachedTransforms = cached, solvedTransforms = solved, solverChanges = changes,
                        constraintsUnchanged = true, explicitPlacementPerformed = false, saved = true });
                } finally { ProcessCloseDocuments(sw); Marshal.ReleaseComObject(owner); }
            }
            root = Open(sw, rootPath, false);
            try {
                ((AssemblyDoc)root).ResolveAllLightWeightComponents(false);
                ParametricContext.Require(root.ForceRebuild3(false), "PROCESS_ROOT_POST_OWNER_REBUILD_FAILED");
                RequireProcessRootTransforms(root, solvedRoot); Save(root); RequireProcessRootTransforms(root, solvedRoot);
                ParametricContext.Require(rootState == ProcessOwnerState(root), "PROCESS_ROOT_SAVE_CHANGED_CONSTRAINTS");
            } finally { ProcessCloseDocuments(sw); Marshal.ReleaseComObject(root); }
            var savedCad = PackageInventory(ParametricContext.CadDirectory, true);
            string doorsDirectory = Path.Combine(ParametricContext.AttemptDirectory, "doors");
            var savedDoors = PackageInventory(doorsDirectory, true);
            int preference = (int)SolidWorks.Interop.swconst.swUserPreferenceToggle_e.swExtRefOpenReadOnly;
            bool previous = sw.GetUserPreferenceToggle(preference);
            try {
                sw.SetUserPreferenceToggle(preference, true);
                ParametricContext.Require(sw.GetUserPreferenceToggle(preference), "PROCESS_PERSISTENCE_READONLY_POLICY_FAILED");
                foreach (string path in ownerPaths.Concat(new[] { rootPath })) {
                    ModelDoc2 model = Open(sw, path, true);
                    try {
                        ParametricContext.Require(model.IsOpenedReadOnly(), "PROCESS_PERSISTENCE_REOPEN_NOT_READONLY");
                        ProcessPersistenceReadOnlyDocuments(sw, savedCad, savedDoors, "open " + Path.GetFileName(path), rows);
                        ((AssemblyDoc)model).ResolveAllLightWeightComponents(false);
                        ProcessPersistenceReadOnlyDocuments(sw, savedCad, savedDoors, "resolve " + Path.GetFileName(path), rows);
                        var expected = path == rootPath ? solvedRoot : expectedOwners[path];
                        if (path == rootPath) RequireProcessRootTransforms(model, expected); else RequireProductionTransforms(model, expected);
                        ParametricContext.Require(model.ForceRebuild3(false), "PROCESS_PERSISTENCE_REOPEN_REBUILD_FAILED");
                        ProcessPersistenceReadOnlyDocuments(sw, savedCad, savedDoors, "rebuild " + Path.GetFileName(path), rows);
                        if (path == rootPath) RequireProcessRootTransforms(model, expected); else RequireProductionTransforms(model, expected);
                        if (path != rootPath) {
                            ParametricContext.Require(ownerStates[path] == ProcessOwnerState(model) && ProcessOwnerSolidsMatch(ownerSolids[path], ProcessOwnerSolids(model)), "PROCESS_OWNER_REOPEN_CONSTRAINTS_OR_SOLIDS_CHANGED");
                            ProcessOwnerContacts(model);
                        } else ParametricContext.Require(rootState == ProcessOwnerState(model), "PROCESS_ROOT_REOPEN_CONSTRAINTS_CHANGED");
                        rows.Add(new { inspection = "process_side_persistence_reopen", path, componentCount = expected.Count,
                            changedTransforms = new object[0], rebuildSucceeded = true, saved = false });
                    } finally { ProcessCloseDocuments(sw); Marshal.ReleaseComObject(model); }
                    RequirePackageInventory(savedCad, ParametricContext.CadDirectory, true, "PROCESS_PERSISTENCE_REOPEN_CHANGED_CAD");
                    RequirePackageInventory(savedDoors, doorsDirectory, true, "PROCESS_PERSISTENCE_REOPEN_CHANGED_DOORS");
                }
            } finally {
                sw.SetUserPreferenceToggle(preference, previous);
                bool restored = sw.GetUserPreferenceToggle(preference) == previous;
                rows.Add(new { inspection = "process_persistence_readonly_policy_restored", previous, restored });
                ParametricContext.Require(restored, "PROCESS_PERSISTENCE_READONLY_POLICY_NOT_RESTORED");
            }
            RequirePackageInventory(savedCad, ParametricContext.CadDirectory, true, "PROCESS_PERSISTENCE_REOPEN_CHANGED_CAD");
            RequirePackageInventory(savedDoors, doorsDirectory, true, "PROCESS_PERSISTENCE_REOPEN_CHANGED_DOORS");
            var allowed = new HashSet<string>(ownerPaths.Concat(new[] { rootPath }), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in before.Where(entry => !allowed.Contains(entry.Key)))
                ParametricContext.Require(ParametricContext.Hash(entry.Key) == entry.Value, "PROCESS_PERSISTENCE_CHANGED_NON_TARGET_FILE " + entry.Key);
            RequireProductionInventory(doorsBefore, "PROCESS_PERSISTENCE_CHANGED_DOOR_SOURCE");
            rows.Add(new { inspection = "process_side_persistence_scope", targetPaths = allowed.ToArray(), unchangedFileCount = before.Count-allowed.Count,
                allPartFilesUnchanged = true, rootSolvedTransformsUnchanged = true, pass = true });
        }
    }
}
