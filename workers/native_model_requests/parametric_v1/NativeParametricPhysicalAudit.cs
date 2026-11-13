using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private const string PhysicalCoverageSchema = "winnsen.locker16029.physical_coverage.v1";
        private static readonly HashSet<string> ReferenceModelFiles = new HashSet<string>(new[] {
            "标准寄存柜 模型.SLDPRT", "上盖 模型.sldprt", "底座 模型.sldprt"
        }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> StructuralSheetFiles = new HashSet<string>(new[] {
            "上盖壳体前侧板.sldprt", "上盖壳体右侧板.SLDPRT", "上盖壳体后侧板.sldprt", "上盖壳体左侧板.sldprt", "上盖壳体底板.sldprt",
            "底座加强筋.sldprt", "底座外框.sldprt", "底座底板.sldprt", "箱体侧板加强筋1.sldprt", "箱体侧板加强筋2.sldprt",
            "箱体右侧板.sldprt", "箱体左侧板.sldprt", "箱体横层板L.sldprt", "箱体横层板R.SLDPRT", "箱体横层板加强筋.SLDPRT",
            "箱体竖隔板L.sldprt", "箱体竖隔板R.SLDPRT", "箱体竖隔板R参数源.SLDPRT", "箱体竖隔板加强件.sldprt", "锁控维护条_源钣金.SLDPRT", "维护门锁支架_原生.SLDPRT",
            "门框 上.sldprt", "门框 下.sldprt", "门框 右.sldprt", "门框 左.sldprt", "门框 横隔板.sldprt", "门框 横隔板R.SLDPRT", "门框 竖隔板L.sldprt", "门框 竖隔板R.SLDPRT"
        }, StringComparer.OrdinalIgnoreCase);

        private sealed class AuditBodyEvidence
        {
            public string id, component, configuration, bodyName;
            public int bodyIndex;
            public double[] boxMm;
        }

        private sealed class AuditInstance
        {
            public string component, file, sha256, configuration, documentType, disposition, exclusionReason;
            public bool suppressed, hidden;
            public int solidBodyCount;
            public AuditBodyEvidence[] bodies;
        }

        private sealed class AuditPartConfiguration
        {
            public string file, sha256, configuration, role, exclusionReason;
            public bool hasSheetMetal, requiredFlatPattern;
            public int solidBodyCount;
            public string[] flatPatternNames;
        }

        private sealed class AuditBody
        {
            internal AuditBodyEvidence evidence;
            internal Body2 body;
        }

        private static string PhysicalPartRole(string file)
        {
            if (ReferenceModelFiles.Contains(file)) return "reference_model";
            foreach (string prefix in new[] { "door_panel_", "door_stiffener_", "hinge_latch_plate_", "u_hook_pad_" })
                if (file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return prefix.TrimEnd('_');
            foreach (string prefix in new[] { "circlip_", "door_hinge_pin_", "mechanical_lock_tongue_", "plastic_bushing_", "top_latch_" })
                if (file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return prefix.TrimEnd('_');
            if (file.Equals("螺母M12.SLDPRT", StringComparison.OrdinalIgnoreCase) || file.Equals("调整脚 M12X60(模型).SLDPRT", StringComparison.OrdinalIgnoreCase)) return "levelling_hardware";
            return StructuralSheetFiles.Contains(file) ? "structural_sheet_metal" : "unknown";
        }

        private static bool RequiresRoleFlatPattern(string role)
        {
            return new[] { "door_panel", "door_stiffener", "hinge_latch_plate", "u_hook_pad", "structural_sheet_metal" }.Contains(role);
        }

        private static bool IsPackageCad(string path)
        {
            return !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal) &&
                (Path.GetExtension(path).Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase));
        }

        private static double[] ExactBounds(Body2 body)
        {
            var box = new double[6];
            for (int axis = 0; axis < 3; axis++)
            {
                double x, y, z;
                ParametricContext.Require(body.GetExtremePoint(axis == 0 ? -1 : 0, axis == 1 ? -1 : 0, axis == 2 ? -1 : 0, out x, out y, out z), "BODY_EXTREME_POINT_UNAVAILABLE");
                box[axis] = new[] { x, y, z }[axis] * 1000;
                ParametricContext.Require(body.GetExtremePoint(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0, out x, out y, out z), "BODY_EXTREME_POINT_UNAVAILABLE");
                box[axis + 3] = new[] { x, y, z }[axis] * 1000;
                ParametricContext.Require(!double.IsNaN(box[axis]) && !double.IsInfinity(box[axis]) && !double.IsNaN(box[axis + 3]) && !double.IsInfinity(box[axis + 3]) && box[axis] <= box[axis + 3], "BODY_BOUNDS_INVALID");
            }
            return box;
        }

        private static List<Feature> PhysicalFeatures(ModelDoc2 model)
        {
            var result = new List<Feature>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Action<Feature, int> visit = null;
            visit = delegate(Feature feature, int depth) {
                ParametricContext.Require(depth < 64, "AUDIT_FEATURE_TRAVERSAL_INCOMPLETE");
                if (!seen.Add(feature.Name)) return;
                result.Add(feature);
                Feature child = feature.GetFirstSubFeature() as Feature;
                while (child != null) { visit(child, depth + 1); child = child.GetNextSubFeature() as Feature; }
            };
            Feature top = model.FirstFeature() as Feature;
            while (top != null) { visit(top, 0); top = top.GetNextFeature() as Feature; }
            return result;
        }

        private static string CanonicalPartConfiguration(Component2 component, string requested)
        {
            ModelDoc2 source = component.GetModelDoc2() as ModelDoc2;
            ParametricContext.Require(source != null, "AUDIT_COMPONENT_MODEL_UNAVAILABLE " + component.Name2);
            Array configurations = source.GetConfigurationNames() as Array;
            ParametricContext.Require(configurations != null && configurations.Length > 0, "AUDIT_COMPONENT_CONFIGURATIONS_UNAVAILABLE " + component.Name2);
            var names = configurations.Cast<object>().Select(value => Convert.ToString(value)).Where(name => !String.IsNullOrWhiteSpace(name)).ToArray();
            foreach (string name in names) if (String.Equals(name, requested, StringComparison.Ordinal)) return name;
            ParametricContext.Require(false, "AUDIT_PART_CONFIGURATION_NAME_UNRESOLVED " + component.Name2 + "/" + requested);
            return null;
        }

        private static void ActivatePhysicalConfiguration(ModelDoc2 model, string configuration, string file)
        {
            Array available = model.GetConfigurationNames() as Array;
            ParametricContext.Require(available != null && available.Cast<object>().Any(value => String.Equals(Convert.ToString(value), configuration, StringComparison.Ordinal)), "AUDIT_PART_CONFIGURATION_UNAVAILABLE " + file + "/" + configuration);
            if (!String.Equals(model.ConfigurationManager.ActiveConfiguration.Name, configuration, StringComparison.Ordinal))
                ParametricContext.Require(model.ShowConfiguration2(configuration), "AUDIT_PART_CONFIGURATION_ACTIVATION_FAILED " + file + "/" + configuration);
            ParametricContext.Require(String.Equals(model.ConfigurationManager.ActiveConfiguration.Name, configuration, StringComparison.Ordinal), "AUDIT_PART_CONFIGURATION_READBACK_FAILED " + file + "/" + configuration);
        }

        private static AuditInstance CapturePhysicalInstance(Component2 component, string folder, Dictionary<string, string> hashes, List<AuditBody> retained = null)
        {
            string path = component.GetPathName();
            var row = new AuditInstance {
                component = component.Name2, file = Path.GetFileName(path), configuration = component.ReferencedConfiguration,
                documentType = Path.GetExtension(path).Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) ? "assembly" : "part",
                suppressed = component.IsSuppressed(), hidden = component.IsHidden(true), bodies = new AuditBodyEvidence[0], exclusionReason = ""
            };
            ParametricContext.Require(!string.IsNullOrWhiteSpace(row.component) && !string.IsNullOrWhiteSpace(row.configuration), "AUDIT_INSTANCE_IDENTITY_UNAVAILABLE");
            if (row.documentType == "part") row.configuration = CanonicalPartConfiguration(component, row.configuration);
            ParametricContext.CheckPath(path, folder);
            ParametricContext.Require(hashes.ContainsKey(path) && IsPackageCad(path), "AUDIT_INSTANCE_PACKAGE_BINDING_FAILED " + row.component);
            row.sha256 = hashes[path];
            if (row.suppressed) { row.disposition = "excluded"; row.exclusionReason = "suppressed_configuration"; return row; }
            if (row.documentType == "assembly") { row.disposition = "container"; return row; }
            bool reference = ReferenceModelFiles.Contains(row.file);
            ParametricContext.Require(!reference || row.hidden, "AUDIT_REFERENCE_MODEL_NOT_HIDDEN " + row.component);
            ParametricContext.Require(!row.hidden || reference, "AUDIT_HIDDEN_PHYSICAL_PART " + row.component);
            row.disposition = reference ? "excluded" : "included";
            if (reference) row.exclusionReason = "hidden_reference_model";
            object info;
            Array sources = component.GetBodies3((int)swBodyType_e.swSolidBody, out info) as Array;
            ParametricContext.Require(sources != null && sources.Length > 0, "AUDIT_INSTANCE_SOLID_BODIES_UNAVAILABLE " + row.component);
            var candidates = new List<Tuple<string, Body2, double[]>>();
            try {
                foreach (Body2 source in sources)
                {
                    Body2 body = source.Copy() as Body2;
                    try {
                        ParametricContext.Require(body != null && body.ApplyTransform(component.GetTotalTransform(true)), "AUDIT_BODY_TRANSFORM_FAILED " + row.component);
                        candidates.Add(Tuple.Create(source.Name, body, ExactBounds(body)));
                        body = null;
                    } finally { if (body != null) Marshal.ReleaseComObject(body); }
                }
                candidates = candidates.OrderBy(item => item.Item1, StringComparer.Ordinal)
                    .ThenBy(item => item.Item3[0]).ThenBy(item => item.Item3[1]).ThenBy(item => item.Item3[2])
                    .ThenBy(item => item.Item3[3]).ThenBy(item => item.Item3[4]).ThenBy(item => item.Item3[5]).ToList();
                var evidence = new List<AuditBodyEvidence>();
                foreach (var candidate in candidates)
                {
                    Body2 body = candidate.Item2;
                    bool keep = false;
                    try {
                        var item = new AuditBodyEvidence { component = row.component, configuration = row.configuration, bodyName = candidate.Item1, bodyIndex = evidence.Count, boxMm = candidate.Item3 };
                        item.id = ParametricContext.Json.Serialize(new object[] { item.component, item.configuration, item.bodyName, item.bodyIndex });
                        evidence.Add(item);
                        if (retained != null && row.disposition == "included") { retained.Add(new AuditBody { evidence = item, body = body }); keep = true; }
                    } finally { if (!keep) Marshal.ReleaseComObject(body); }
                }
                row.bodies = evidence.ToArray(); row.solidBodyCount = evidence.Count;
            } catch { foreach (var candidate in candidates) { try { Marshal.ReleaseComObject(candidate.Item2); } catch { } } throw; }
            return row;
        }

        private static object CapturePhysicalCoverage(ISldWorks sw, string folder, string root, string rootConfiguration, Dictionary<string, string> hashes, List<AuditInstance> instances, string purpose, out List<AuditPartConfiguration> parts)
        {
            var doorProofPaths=VerifiedDoorProofPaths().ToList();
            var productionRevisions = VerifiedPhysicalProductionRevisions();
            parts = new List<AuditPartConfiguration>();
            foreach (string path in hashes.Keys.Where(path => Path.GetExtension(path).Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase) && IsPackageCad(path)).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                ModelDoc2 model = Open(sw, path, true);
                try {
                    string file = Path.GetFileName(path);
                    var configurations = instances.Where(row => !row.suppressed && row.file.Equals(file, StringComparison.OrdinalIgnoreCase)).Select(row => row.configuration).Distinct(StringComparer.Ordinal).ToArray();
                    if (configurations.Length == 0) configurations = new[] { model.ConfigurationManager.ActiveConfiguration.Name };
                    foreach (string configuration in configurations)
                    {
                        ActivatePhysicalConfiguration(model, configuration, file);
                        ParametricContext.Require(model.ForceRebuild3(false), "AUDIT_PART_REBUILD_FAILED " + file + "/" + configuration);
                        Array solids = ((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false) as Array;
                        ParametricContext.Require(solids != null && solids.Length > 0, "AUDIT_PART_SOLID_BODIES_UNAVAILABLE " + file + "/" + configuration);
                        var features = PhysicalFeatures(model);
                        string role = PhysicalPartRole(file);
                        ParametricContext.Require(role != "unknown", "AUDIT_PART_ROLE_UNRECOGNIZED " + file);
                        string[] flats = features.Where(feature => feature.GetTypeName2() == "FlatPattern").Select(feature => feature.Name).ToArray();
                        bool sheet = features.Any(feature => feature.GetTypeName2() == "SheetMetal") || solids.Cast<Body2>().Any(body => body.IsSheetMetal());
                        bool required = role != "reference_model" && (RequiresRoleFlatPattern(role) || sheet || flats.Length > 0);
                        ParametricContext.Require(!required || flats.Length > 0, "AUDIT_REQUIRED_FLAT_PATTERN_MISSING " + file + "/" + configuration);
                        parts.Add(new AuditPartConfiguration { file = file, sha256 = hashes[path], configuration = configuration, role = role, exclusionReason = role == "reference_model" ? "reference_model_not_manufactured" : "", solidBodyCount = solids.Length, hasSheetMetal = sheet, requiredFlatPattern = required, flatPatternNames = flats });
                    }
                } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            }
            var partEdits = new List<object>();
            foreach (string evidencePath in doorProofPaths)
            {
                Dictionary<string, object> evidence = ParametricContext.Read(evidencePath);
                if (!evidence.ContainsKey("result") || !(evidence["result"] is Dictionary<string, object>)) continue;
                Dictionary<string, object> result = (Dictionary<string, object>)evidence["result"];
                if (!result.ContainsKey("part_edits") || !(result["part_edits"] is IEnumerable)) continue;
                foreach (object raw in (IEnumerable)result["part_edits"])
                {
                    Dictionary<string, object> edit = raw as Dictionary<string, object>;
                    if (edit == null) continue;
                    string file = Path.GetFileName(ParametricContext.Text(edit, "path"));
                    string packagePath = Path.Combine(folder, file);
                    if (!hashes.ContainsKey(packagePath)) continue;
                    Dictionary<string, object> after = edit.ContainsKey("after") ? edit["after"] as Dictionary<string, object> : null;
                    string sourceSha = ParametricContext.Text(edit, "sha256_after_save");
                    Dictionary<string, object> productionRevision;
                    if (productionRevisions.TryGetValue(file, out productionRevision)) {
                        ParametricContext.Require(ParametricContext.Text(productionRevision, "oldDoorSha256") == sourceSha &&
                            ParametricContext.Text(productionRevision, "doorEvidenceSha256") == ParametricContext.Hash(evidencePath) &&
                            ParametricContext.Text(productionRevision, "newProductionSha256") == hashes[packagePath], "AUDIT_DOOR_PRODUCTION_REVISION_BINDING_FAILED " + file);
                    } else ParametricContext.Require(sourceSha.Length == 64 && String.Equals(sourceSha, hashes[packagePath], StringComparison.OrdinalIgnoreCase), "AUDIT_DOOR_PART_EDIT_PACKAGE_HASH_DRIFT " + file);
                    ParametricContext.Require(after != null && after.ContainsKey("has_sheet_metal") && after.ContainsKey("has_flat_pattern"), "AUDIT_DOOR_PART_EDIT_HEALTH_MISSING " + file);
                    var boundEdit = new Dictionary<string, object> { { "evidence", Path.GetFileName(evidencePath) }, { "file", file }, { "role", ParametricContext.Text(edit, "role") },
                        { "hasSheetMetal", Convert.ToBoolean(after["has_sheet_metal"]) }, { "hasFlatPattern", Convert.ToBoolean(after["has_flat_pattern"]) }, { "sourceSha256", sourceSha }, { "packageSha256", hashes[packagePath] } };
                    if (productionRevision != null) boundEdit.Add("productionRevision", productionRevision);
                    partEdits.Add(boundEdit);
                }
            }
            ParametricContext.Require(partEdits.Count > 0, "AUDIT_DOOR_PART_EDITS_MISSING");
            return new { inspection = "native_physical_coverage", schema = PhysicalCoverageSchema, purpose = purpose,
                root = new { file = Path.GetFileName(root), sha256 = hashes[root], configuration = rootConfiguration },
                packageFiles = hashes.Where(row => IsPackageCad(row.Key)).OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase).Select(row => new { file = Path.GetFileName(row.Key), sha256 = row.Value }).ToArray(),
                parts = parts, instances = instances, partEdits = partEdits, pairFilter = new { minimumOverlapMm = .001, excludeSameInstance = true } };
        }

        private static Dictionary<string, object> VerifiedPhysicalProductionStage(string phase)
        {
            string attempt = ParametricContext.AttemptDirectory, planPath = Path.Combine(attempt, "parametric_execution.json");
            string receiptPath = Path.Combine(attempt, "receipts", phase + ".json"), evidencePath = Path.Combine(attempt, "evidence", phase + ".json");
            string authorizationPath = Path.Combine(attempt, "authorizations", phase + ".json"), executablePath = Path.Combine(attempt, "tools", "NativeParametricAssembly.exe");
            foreach (string path in new[] { planPath, receiptPath, evidencePath, authorizationPath, executablePath }) ParametricContext.CheckPath(path, attempt);
            var receipt = ParametricContext.Read(receiptPath); var evidence = ParametricContext.Read(evidencePath); var authorization = ParametricContext.Read(authorizationPath);
            var plan = ParametricContext.Plan;
            ParametricContext.Require(ParametricContext.Text(receipt, "phase") == phase && ParametricContext.Text(evidence, "phase") == phase &&
                ParametricContext.Text(authorization, "phase") == phase && Convert.ToBoolean(evidence["success"]) && Convert.ToBoolean(receipt["protectedSourcesUnchanged"]) &&
                ParametricContext.Text(receipt, "taskId") == ParametricContext.Text(plan, "taskId") && ParametricContext.Text(authorization, "taskId") == ParametricContext.Text(plan, "taskId") &&
                ParametricContext.Text(authorization, "workerId") == ParametricContext.Text(plan, "workerId") &&
                ParametricContext.Text(receipt, "planSha256") == ParametricContext.Hash(planPath) && ParametricContext.Text(authorization, "planSha256") == ParametricContext.Hash(planPath) &&
                ParametricContext.Text(receipt, "evidenceSha256") == ParametricContext.Hash(evidencePath) && ParametricContext.Text(receipt, "authorizationSha256") == ParametricContext.Hash(authorizationPath) &&
                ParametricContext.Text(authorization, "executableSha256") == ParametricContext.Hash(executablePath) &&
                ParametricContext.Json.Serialize(authorization["preInventory"]) == ParametricContext.Json.Serialize(receipt["before"]), "AUDIT_PRODUCTION_STAGE_BINDING_FAILED " + phase);
            return new Dictionary<string, object> { { "receipt", receipt }, { "evidence", evidence }, { "receiptSha256", ParametricContext.Hash(receiptPath) }, { "evidenceSha256", ParametricContext.Hash(evidencePath) } };
        }

        private static Dictionary<string, Dictionary<string, object>> VerifiedPhysicalProductionRevisions()
        {
            var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            var contract = ParametricContext.Object(ParametricContext.Plan, "contract"); var input = ParametricContext.Object(contract, "input");
            string profile = ParametricContext.Text(input, "productionProfile");
            if (String.IsNullOrEmpty(profile)) return result;
            ParametricContext.Require(profile == "indoor-weld-locators-20261029-r1", "AUDIT_PRODUCTION_PROFILE_UNRECOGNIZED");
            var apply = VerifiedPhysicalProductionStage("production-apply"); var verify = VerifiedPhysicalProductionStage("production-verify");
            var applyReceipt = (Dictionary<string, object>)apply["receipt"]; var verifyReceipt = (Dictionary<string, object>)verify["receipt"];
            ParametricContext.Require(ParametricContext.Text(verifyReceipt, "predecessor") == (string)apply["receiptSha256"] &&
                ParametricContext.Json.Serialize(applyReceipt["after"]) == ParametricContext.Json.Serialize(verifyReceipt["before"]) &&
                ParametricContext.Json.Serialize(verifyReceipt["before"]) == ParametricContext.Json.Serialize(verifyReceipt["after"]) &&
                ParametricContext.Json.Serialize(applyReceipt["beforeDoorInventory"]) == ParametricContext.Json.Serialize(applyReceipt["doorInventory"]) &&
                ParametricContext.Json.Serialize(verifyReceipt["beforeDoorInventory"]) == ParametricContext.Json.Serialize(verifyReceipt["doorInventory"]), "AUDIT_PRODUCTION_VERIFY_MUTATED_CAD");
            var evidence = (Dictionary<string, object>)apply["evidence"]; var verified = (Dictionary<string, object>)verify["evidence"];
            var checks = ((IEnumerable)verified["rows"]).Cast<Dictionary<string, object>>().Where(row => ParametricContext.Text(row, "inspection") == "production_part_verification").ToList();
            foreach (var row in ((IEnumerable)evidence["rows"]).Cast<Dictionary<string, object>>().Where(row => ParametricContext.Text(row, "inspection") == "production_part_revision")) {
                string file = ParametricContext.Text(row, "file"), path = ParametricContext.Text(row, "path"), working = Path.Combine(ParametricContext.AttemptDirectory, "native_cad", "working_pack");
                ParametricContext.CheckPath(path, working);
                ParametricContext.Require(Path.GetFileName(path) == file && Path.GetDirectoryName(path).Equals(working, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetExtension(path).Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase) && !result.ContainsKey(file) &&
                    ParametricContext.Text(row, "role") == PhysicalPartRole(file) && ParametricContext.Text(row, "productionProfile") == profile &&
                    ParametricContext.Text(row, "oldSha256").Length == 64 && ParametricContext.Text(row, "newProductionSha256").Length == 64, "AUDIT_PRODUCTION_REVISION_IDENTITY_FAILED " + file);
                var matches = checks.Where(check => ParametricContext.Text(check, "file") == file && ParametricContext.Text(check, "path") == path).ToList();
                ParametricContext.Require(matches.Count == 1 && Convert.ToBoolean(matches[0]["pass"]) && ParametricContext.Text(matches[0], "role") == ParametricContext.Text(row, "role") &&
                    ParametricContext.Text(matches[0], "productionProfile") == profile && ParametricContext.Text(matches[0], "sha256") == ParametricContext.Text(row, "newProductionSha256"), "AUDIT_PRODUCTION_REVISION_VERIFICATION_FAILED " + file);
                var binding = new Dictionary<string, object> {
                    { "taskId", ParametricContext.Text(ParametricContext.Plan, "taskId") }, { "planSha256", ParametricContext.Hash(Path.Combine(ParametricContext.AttemptDirectory, "parametric_execution.json")) },
                    { "contractSha256", ParametricContext.Text(ParametricContext.Plan, "contractSha256") }, { "executableSha256", ParametricContext.Hash(Path.Combine(ParametricContext.AttemptDirectory, "tools", "NativeParametricAssembly.exe")) },
                    { "applyReceiptSha256", apply["receiptSha256"] }, { "applyEvidenceSha256", apply["evidenceSha256"] }, { "verificationReceiptSha256", verify["receiptSha256"] }, { "verificationEvidenceSha256", verify["evidenceSha256"] } };
                foreach (string field in new[] { "productionProfile", "role", "oldSha256", "newProductionSha256", "oldDoorSha256", "doorEvidenceSha256", "hasSheetMetal", "hasFlatPattern" }) binding.Add(field, row[field]);
                result.Add(file, binding);
            }
            ParametricContext.Require(result.Count > 0 && checks.Count == result.Count, "AUDIT_PRODUCTION_REVISION_COVERAGE_MISMATCH");
            return result;
        }

        private static IEnumerable<string> VerifiedDoorProofPaths()
        {
            var proofs=new Dictionary<string,string>(StringComparer.Ordinal);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current=ParametricContext.AttemptDirectory,root=Directory.GetParent(current).FullName;
            string input=ParametricContext.Json.Serialize(ParametricContext.Object(ParametricContext.Plan,"contract"));
            while(!String.IsNullOrEmpty(current)) {
                ParametricContext.Require(seen.Count<20&&seen.Add(current)&&Directory.GetParent(current).FullName.Equals(root,StringComparison.OrdinalIgnoreCase),"AUDIT_DOOR_PROOF_CHAIN_INVALID");
                ParametricContext.CheckPath(current,root);
                string planPath=Path.Combine(current,"parametric_execution.json");var plan=ParametricContext.Read(planPath);
                ParametricContext.Require(ParametricContext.Json.Serialize(ParametricContext.Object(plan,"contract"))==input,"AUDIT_DOOR_PROOF_CONTRACT_CHANGED");
                foreach(string receiptPath in Directory.GetFiles(Path.Combine(current,"receipts"),"door-*.json")) {
                    string name=Path.GetFileName(receiptPath);if(proofs.ContainsKey(name))continue;
                    ParametricContext.Require(System.Text.RegularExpressions.Regex.IsMatch(name,@"^door-\d+(?:p\d+)?\.json$"),"AUDIT_DOOR_PROOF_PHASE_INVALID");
                    string evidencePath=Path.Combine(current,"evidence",name);ParametricContext.CheckPath(evidencePath,current);ParametricContext.CheckPath(receiptPath,current);
                    var receipt=ParametricContext.Read(receiptPath);var evidence=ParametricContext.Read(evidencePath);
                    ParametricContext.Require(ParametricContext.Text(receipt,"planSha256")==ParametricContext.Hash(planPath)&&ParametricContext.Text(receipt,"evidenceSha256")==ParametricContext.Hash(evidencePath)&&Convert.ToBoolean(evidence["success"]),"AUDIT_DOOR_PROOF_RECEIPT_MISMATCH");
                    proofs.Add(name,evidencePath);
                }
                if(!plan.ContainsKey("resumedEvidence")||!(plan["resumedEvidence"] is Dictionary<string,object>))break;
                var resumed=ParametricContext.Object(plan,"resumedEvidence");string parent=ParametricContext.Text(resumed,"sourceAttempt");
                ParametricContext.CheckPath(parent,root);
                string parentReceipt=Path.Combine(parent,"receipts",ParametricContext.Text(resumed,"completedPhase")+".json");ParametricContext.CheckPath(parentReceipt,parent);
                ParametricContext.Require(ParametricContext.Hash(parentReceipt)==ParametricContext.Text(resumed,"receiptSha256"),"AUDIT_DOOR_PROOF_RESUME_BINDING_FAILED");
                current=parent;
            }
            return proofs.Values;
        }

        private static void PhysicalAudit(ISldWorks sw, List<object> records)
        {
            string folder = Path.Combine(ParametricContext.AttemptDirectory, "package");
            string root = Directory.GetFiles(folder, "16029_*.SLDASM").Single();
            sw.SetCurrentWorkingDirectory(folder);
            var hashes = Directory.GetFiles(folder).Where(IsPackageCad).ToDictionary(path => path, ParametricContext.Hash, StringComparer.OrdinalIgnoreCase);
            var bodies = new List<AuditBody>(); var instances = new List<AuditInstance>();
            string rootConfiguration;
            ModelDoc2 model = Open(sw, root, true);
            try {
                var assembly = (AssemblyDoc)model;
                assembly.ResolveAllLightWeightComponents(false);
                ParametricContext.Require(model.ForceRebuild3(false), "AUDIT_ROOT_REBUILD_FAILED");
                rootConfiguration = model.ConfigurationManager.ActiveConfiguration.Name;
                Array components = assembly.GetComponents(false) as Array;
                ParametricContext.Require(components != null && components.Length > 0, "AUDIT_COMPONENTS_UNAVAILABLE");
                foreach (Component2 component in components) instances.Add(CapturePhysicalInstance(component, folder, hashes, bodies));
                ParametricContext.Require(bodies.Count > 0, "AUDIT_NO_PHYSICAL_BODIES");
                Modeler modeler = sw.GetModeler() as Modeler;
                ParametricContext.Require(modeler != null, "AUDIT_MODELER_UNAVAILABLE");
                records.Add(new { inspection = "native_world_body_bounds", bodies = bodies.Select(body => body.evidence).ToArray() });
                var collisions = new List<object>(); var pairs = new List<object>();
                for (int i = 0; i < bodies.Count; i++) for (int j = i + 1; j < bodies.Count; j++)
                {
                    var a = bodies[i]; var b = bodies[j];
                    if (a.evidence.component == b.evidence.component) continue;
                    if (Enumerable.Range(0, 3).Any(axis => Math.Min(a.evidence.boxMm[axis + 3], b.evidence.boxMm[axis + 3]) - Math.Max(a.evidence.boxMm[axis], b.evidence.boxMm[axis]) < .001)) continue;
                    pairs.Add(new { bodyA = a.evidence.id, bodyB = b.evidence.id });
                    object facesA, facesB, intersections;
                    bool overlaps = modeler.CheckInterferenceBetweenTwoBodies(a.body, b.body, false, out facesA, out facesB, out intersections);
                    Array intersectionsArray = intersections as Array;
                    if (!overlaps && (intersectionsArray == null || intersectionsArray.Length == 0)) continue;
                    double volume = 0; var intersectionBounds = new List<double[]>();
                    if (intersectionsArray != null) foreach (Body2 intersection in intersectionsArray)
                    {
                        try {
                            double[] mass = intersection.GetMassProperties(1.0) as double[];
                            ParametricContext.Require(mass != null && mass.Length > 3 && !double.IsNaN(mass[3]) && !double.IsInfinity(mass[3]) && mass[3] >= 0, "AUDIT_INTERSECTION_MASS_UNAVAILABLE");
                            volume += mass[3] * 1e9; intersectionBounds.Add(ExactBounds(intersection));
                        } finally { Marshal.ReleaseComObject(intersection); }
                    }
                    collisions.Add(new { bodyA = a.evidence.id, bodyB = b.evidence.id, componentA = a.evidence.component, componentB = b.evidence.component, volumeMm3 = volume, interference = overlaps, intersectionBoundsMm = intersectionBounds });
                }
                records.Add(new { inspection = "native_exact_solid_interference", bodyCount = bodies.Count, checkedPairs = pairs.Count, pairs = pairs, collisions = collisions });
                Console.WriteLine("PHYSICAL_INTERFERENCE_MEASURED pairs=" + pairs.Count + " contacts=" + collisions.Count);
            } finally {
                foreach (var body in bodies) Marshal.ReleaseComObject(body.body);
                sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model);
            }
            List<AuditPartConfiguration> parts;
            records.Add(CapturePhysicalCoverage(sw, folder, root, rootConfiguration, hashes, instances, "physical_audit", out parts));
            foreach (var part in parts.Where(part => part.requiredFlatPattern))
            {
                model = Open(sw, Path.Combine(folder, part.file), true);
                try {
                    ActivatePhysicalConfiguration(model, part.configuration, part.file);
                    var features = PhysicalFeatures(model);
                    foreach (string name in part.flatPatternNames)
                    {
                        Feature flat = features.Single(feature => feature.Name == name && feature.GetTypeName2() == "FlatPattern");
                        bool priorSuppressed = flat.IsSuppressed();
                        try {
                            bool changed = !priorSuppressed || flat.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                            bool unsuppressed = changed && !flat.IsSuppressed();
                            bool rebuilt = unsuppressed && model.ForceRebuild3(false);
                            var issues = PhysicalFeatures(model).Where(feature => !feature.IsSuppressed() && feature.GetErrorCode() > 0).Select(feature => new { feature = feature.Name, code = feature.GetErrorCode() }).ToArray();
                            double[] box = rebuilt ? BodyBox(model) : null;
                            records.Add(new { inspection = "native_flat_pattern", file = part.file, sha256 = part.sha256, configuration = part.configuration, role = part.role, feature = name, unsuppressed = unsuppressed, rebuilt = rebuilt, flatBoxMm = box, issues = issues });
                            Console.WriteLine("FLAT_PATTERN_MEASURED " + part.file + "/" + part.configuration + "/" + name + " rebuilt=" + rebuilt + " issues=" + issues.Length);
                        } finally {
                            if (priorSuppressed) ParametricContext.Require(flat.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null) && model.ForceRebuild3(false), "AUDIT_FLAT_FOLD_RESTORE_FAILED");
                        }
                    }
                } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }
            }
            foreach (var item in hashes) ParametricContext.Require(ParametricContext.Hash(item.Key) == item.Value, "AUDIT_MODIFIED_PACKAGE_FILE");
        }
    }
}
