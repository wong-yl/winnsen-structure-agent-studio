using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

internal static class WeightReadback
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
    static readonly string[] Configurations = { "Default", "DefaultSM-FLAT-PATTERN" };
    static readonly Dictionary<string, string> TargetFiles = new Dictionary<string, string>(StringComparer.Ordinal) {
        { "crossbar_R_3", "case1/门框 横隔板R.SLDPRT" },
        { "shelf_R_2", "case1/箱体横层板R.SLDPRT" },
        { "structure_1000_2", "case1/门框 右.sldprt" },
        { "crossbar_R_2", "case3/门框 横隔板R.SLDPRT" }
    };

    sealed class SafetyException : InvalidOperationException
    {
        internal SafetyException(string text) : base(text) { }
        internal SafetyException(string text, Exception inner) : base(text, inner) { }
    }

    sealed class Namespace
    {
        internal string Relative;
        internal string Copy;
        internal string Original;
    }

    sealed class Target
    {
        internal string Identity;
        internal string Origin;
        internal string Relative;
        internal string Path;
        internal string SourceSha256;
        internal Namespace Space;
    }

    sealed class Plan
    {
        internal string Root;
        internal List<Namespace> Spaces = new List<Namespace>();
        internal List<Target> Targets = new List<Target>();
    }

    sealed class OwnedDocument
    {
        internal ModelDoc2 Model;
        internal string Path;
    }

    sealed class Context
    {
        internal ISldWorks Sw;
        internal Plan Plan;
        internal int Pid;
        internal long Ticks;
        internal string Executable;
        internal List<OwnedDocument> Owned = new List<OwnedDocument>();
        internal HashSet<string> AllowedDocuments;
        internal string PrimaryDocumentPath;
        internal Dictionary<string, string> CopyBefore;
        internal Dictionary<string, string> CopyAfterRewire;
        internal Dictionary<string, string> Originals;
        internal Dictionary<string, string[]> Dependencies;
        internal List<object> Errors = new List<object>();
        internal List<object> Targets = new List<object>();
        internal Dictionary<string, object> Result;
        internal Action<object> Progress;
    }

    static void Need(bool value, string message) { if (!value) throw new SafetyException(message); }
    static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    static object[] Items(object value) { return value == null ? new object[0] : ((IEnumerable)value).Cast<object>().ToArray(); }
    static Dictionary<string, object> Map(object value) { var result = value as Dictionary<string, object>; Need(result != null, "WEIGHT_OBJECT_REQUIRED"); return result; }
    static string Text(Dictionary<string, object> value, string key) { Need(value.ContainsKey(key) && value[key] is string, "WEIGHT_STRING_REQUIRED " + key); return (string)value[key]; }
    static string Full(string path) { Need(!String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path), "WEIGHT_ABSOLUTE_PATH_REQUIRED " + path); return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    static bool Under(string path, string root) { return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
    static bool Native(string path) { string extension = Path.GetExtension(path); return extension.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase) || extension.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase); }

    static void NoReparse(string path)
    {
        for (string current = path; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current)) Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "WEIGHT_REPARSE_PATH_FORBIDDEN " + current);
    }

    static string Relative(string path)
    {
        Need(!String.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains(":"), "WEIGHT_RELATIVE_PATH_REQUIRED " + path);
        string[] components = path.Replace('\\', '/').Split('/');
        Need(components.All(value => value.Length != 0 && value != "." && value != ".." && value.Trim() == value && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !value.EndsWith(".", StringComparison.Ordinal)), "WEIGHT_RELATIVE_PATH_INVALID " + path);
        return String.Join("/", components);
    }

    static string Within(string path, string root)
    {
        string value = Full(path); Need(Under(value, root), "WEIGHT_PATH_OUTSIDE_COPY " + value); NoReparse(value); return value;
    }

    static string Hash(string path)
    {
        NoReparse(path);
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    static Plan ReadPlan(string cadRoot, Dictionary<string, object> job)
    {
        Need(job != null && job.ContainsKey("weightTargets") && job.ContainsKey("weightNamespaces"), "WEIGHT_JOB_MANIFEST_REQUIRED");
        var plan = new Plan { Root = Full(cadRoot) }; NoReparse(plan.Root);
        Need(Directory.Exists(plan.Root) && plan.Root.Length > Path.GetPathRoot(plan.Root).Length && !plan.Root.StartsWith("\\\\", StringComparison.Ordinal), "WEIGHT_NEW_LOCAL_COPY_ROOT_REQUIRED");
        object[] spaces = Items(job["weightNamespaces"]), targets = Items(job["weightTargets"]);
        Need(spaces.Length == 4 && targets.Length == 8, "WEIGHT_EXACT_FOUR_NAMESPACES_EIGHT_TARGETS_REQUIRED");
        var expectedSpaces = new HashSet<string>(new[] { "T3/case1", "T3/case3", "T4/case1", "T4/case3" }, StringComparer.Ordinal);
        foreach (object raw in spaces)
        {
            var item = Map(raw); string relative = Relative(Text(item, "relativeDirectory"));
            Need(expectedSpaces.Remove(relative), "WEIGHT_NAMESPACE_IDENTITY_OR_DUPLICATE " + relative);
            var space = new Namespace { Relative = relative, Copy = Within(Path.Combine(plan.Root, relative.Replace('/', Path.DirectorySeparatorChar)), plan.Root), Original = Full(Text(item, "originalDirectory")) };
            NoReparse(space.Original);
            Need(Directory.Exists(space.Copy) && Directory.Exists(space.Original), "WEIGHT_NAMESPACE_DIRECTORY_MISSING " + relative);
            Need(!Paths.Equals(space.Copy, space.Original) && !Under(space.Original, plan.Root) && !Under(plan.Root, space.Original), "WEIGHT_ORIGINAL_COPY_OVERLAP " + relative);
            foreach (Namespace prior in plan.Spaces)
                Need(!Paths.Equals(prior.Original, space.Original) && !Under(prior.Original, space.Original) && !Under(space.Original, prior.Original), "WEIGHT_ORIGINAL_NAMESPACE_OVERLAP");
            plan.Spaces.Add(space);
        }
        Need(expectedSpaces.Count == 0, "WEIGHT_NAMESPACE_SET_MISMATCH");
        var identities = new HashSet<string>(StringComparer.Ordinal); var paths = new HashSet<string>(Paths);
        foreach (object raw in targets)
        {
            var item = Map(raw); string origin = Text(item, "origin"), identity = Text(item, "identity"), suffix;
            Need((origin == "T3" || origin == "T4") && TargetFiles.TryGetValue(identity, out suffix), "WEIGHT_TARGET_IDENTITY_INVALID");
            string relative = Relative(Text(item, "relativePath"));
            Need(Paths.Equals(relative, origin + "/" + TargetFiles[identity]) && identities.Add(origin + "/" + identity), "WEIGHT_TARGET_PAIR_OR_PATH_MISMATCH " + relative);
            string path = Within(Path.Combine(plan.Root, relative.Replace('/', Path.DirectorySeparatorChar)), plan.Root), sha = Text(item, "sourceSha256");
            Need(File.Exists(path) && paths.Add(path) && Regex.IsMatch(sha, "\\A[0-9a-fA-F]{64}\\z"), "WEIGHT_TARGET_FILE_OR_SHA_INVALID " + relative);
            Namespace space = plan.Spaces.Single(value => Under(path, value.Copy));
            plan.Targets.Add(new Target { Identity = identity, Origin = origin, Relative = relative, Path = path, SourceSha256 = sha.ToUpperInvariant(), Space = space });
        }
        return plan;
    }

    static string MapDependency(Namespace space, string dependency, ISet<string> inventory)
    {
        string original = Full(dependency), result;
        if (Under(original, space.Copy)) result = original;
        else
        {
            Need(Under(original, space.Original), "WEIGHT_EXTERNAL_OR_CROSS_NAMESPACE_DEPENDENCY " + original);
            result = Within(Path.Combine(space.Copy, original.Substring(space.Original.Length + 1)), space.Copy);
        }
        Need(inventory.Contains(result) && File.Exists(result), "WEIGHT_DEPENDENCY_NOT_IN_EXACT_COPY " + result);
        NoReparse(original); NoReparse(result); return result;
    }

    static Dictionary<string, string> Inventory(string root)
    {
        var result = new Dictionary<string, string>(Paths);
        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(value => value, Paths))
        {
            string file = Within(path, root); Need(Native(file), "WEIGHT_NON_NATIVE_INPUT_FORBIDDEN " + file); result.Add(file, Hash(file));
        }
        Need(result.Count >= 8 && result.Count <= 512, "WEIGHT_NATIVE_INVENTORY_SIZE_INVALID"); return result;
    }

    static void SameHashes(Dictionary<string, string> baseline, Dictionary<string, string> actual, string stage)
    {
        Need(baseline.Count == actual.Count && baseline.All(pair => actual.ContainsKey(pair.Key) && actual[pair.Key] == pair.Value), "WEIGHT_INPUT_HASH_CHANGED " + stage);
    }

    static Dictionary<string, string> Rehash(Dictionary<string, string> baseline)
    {
        var result = new Dictionary<string, string>(Paths); foreach (string path in baseline.Keys) result.Add(path, Hash(path)); return result;
    }

    static void Guard(Context c)
    {
        try { VariableTrial.RuntimeOwnership(); }
        catch (Exception error) { throw new SafetyException("WEIGHT_RUNTIME_OWNERSHIP_LOST", error); }
        Need(c.Sw.GetProcessID() == c.Pid, "WEIGHT_CAD_PID_CHANGED");
        using (Process process = Process.GetProcessById(c.Pid)) Need(process.StartTime.ToUniversalTime().Ticks == c.Ticks && Paths.Equals(Path.GetFullPath(process.MainModule.FileName), c.Executable), "WEIGHT_CAD_PROCESS_IDENTITY_CHANGED");
    }

    static void Empty(Context c)
    {
        Guard(c); object[] documents = Items(c.Sw.GetDocuments());
        try { Need(documents.Length == 0 && c.Sw.GetDocumentCount() == 0, "WEIGHT_EMPTY_DOCUMENT_SESSION_REQUIRED"); }
        finally { foreach (object value in documents) Release(value); }
    }

    static string[] Dependencies(Context c, string path)
    {
        Guard(c); object[] values = Items(c.Sw.GetDocumentDependencies2(path, false, false, false));
        Need(values.Length % 2 == 0, "WEIGHT_DEPENDENCY_ARRAY_INVALID " + path);
        var result = new List<string>();
        for (int index = 1; index < values.Length; index += 2) result.Add(Full(Convert.ToString(values[index], Invariant)));
        return result.Distinct(Paths).ToArray();
    }

    static void Prepare(Context c)
    {
        Empty(c); c.CopyBefore = Inventory(c.Plan.Root); c.Originals = new Dictionary<string, string>(Paths);
        var fileSpaces = new Dictionary<string, Namespace>(Paths);
        foreach (var pair in c.CopyBefore)
        {
            Namespace[] owners = c.Plan.Spaces.Where(value => Under(pair.Key, value.Copy)).ToArray(); Need(owners.Length == 1, "WEIGHT_FILE_NAMESPACE_NOT_UNIQUE " + pair.Key);
            Namespace space = owners[0]; fileSpaces.Add(pair.Key, space);
            string original = Within(Path.Combine(space.Original, pair.Key.Substring(space.Copy.Length + 1)), space.Original);
            Need(File.Exists(original) && Hash(original) == pair.Value, "WEIGHT_ORIGINAL_COPY_BYTES_MISMATCH " + original); c.Originals.Add(original, pair.Value);
        }
        foreach (Target target in c.Plan.Targets) Need(c.CopyBefore[target.Path] == target.SourceSha256, "WEIGHT_TARGET_ENTRY_SHA_MISMATCH " + target.Relative);
        var inventory = new HashSet<string>(c.CopyBefore.Keys, Paths); var beforeDependencies = new Dictionary<string, string[]>(Paths); c.Dependencies = new Dictionary<string, string[]>(Paths);
        foreach (string file in c.CopyBefore.Keys)
        {
            string[] original = Dependencies(c, file), mapped = original.Select(path => MapDependency(fileSpaces[file], path, inventory)).ToArray();
            Need(mapped.Distinct(Paths).Count() == mapped.Length, "WEIGHT_AMBIGUOUS_DEPENDENCY_MAPPING " + file);
            beforeDependencies.Add(file, original); c.Dependencies.Add(file, mapped);
        }
        var rewrites = new List<object>(); c.Result["referenceRewrites"] = rewrites;
        foreach (var pair in beforeDependencies)
        {
            for (int index = 0; index < pair.Value.Length; index++)
            {
                Guard(c); string old = pair.Value[index], target = c.Dependencies[pair.Key][index]; if (Paths.Equals(old, target)) continue;
                string before = Hash(pair.Key); bool success = c.Sw.ReplaceReferencedDocument(pair.Key, old, target); string after = Hash(pair.Key);
                rewrites.Add(new { file = pair.Key, originalReference = old, targetReference = target, sha256Before = before, sha256After = after, success = success, phase = "explicit_namespace_rewrite_before_readonly_baseline" });
                Need(success, "WEIGHT_EXACT_REFERENCE_REWRITE_FAILED " + pair.Key);
            }
        }
        var closure = new List<object>(); c.Result["dependencyClosure"] = closure;
        foreach (string file in c.CopyBefore.Keys)
        {
            string[] actual = Dependencies(c, file); Namespace space = fileSpaces[file];
            Need(actual.All(path => Under(path, space.Copy) && inventory.Contains(path) && File.Exists(path)), "WEIGHT_REWRITTEN_DEPENDENCY_OUTSIDE_NAMESPACE " + file);
            Need(new HashSet<string>(actual, Paths).SetEquals(c.Dependencies[file]), "WEIGHT_REWRITTEN_DEPENDENCY_SET_MISMATCH " + file);
            closure.Add(new { path = file, dependencies = actual, namespaceDirectory = space.Copy, exactSetVerified = true });
        }
        c.CopyAfterRewire = Inventory(c.Plan.Root);
        Need(new HashSet<string>(c.CopyBefore.Keys, Paths).SetEquals(c.CopyAfterRewire.Keys), "WEIGHT_REWRITE_CHANGED_FILE_SET");
        SameHashes(c.Originals, Rehash(c.Originals), "originals_after_rewrite"); Empty(c);
        c.Result["rewriteStage"] = new { permittedMutation = "ReplaceReferencedDocument in the new copy only", sourceSha256CheckedBeforeRewrite = true, files = c.CopyBefore.Select(pair => new { path = pair.Key, sha256Before = pair.Value, sha256After = c.CopyAfterRewire[pair.Key] }).ToArray(), dependencyClosureVerified = true };
    }

    static HashSet<string> TargetClosure(Context c, string target)
    {
        var result = new HashSet<string>(Paths); Action<string> visit = null;
        visit = path => { if (!result.Add(path)) return; Need(c.Dependencies.ContainsKey(path), "WEIGHT_TARGET_DEPENDENCY_UNRECORDED " + path); foreach (string dependency in c.Dependencies[path]) visit(dependency); };
        visit(target); return result;
    }

    static void EnrollDocuments(Context c)
    {
        Guard(c); object[] documents = Items(c.Sw.GetDocuments()); var rejections = new List<string>();
        try
        {
            try { Need(c.Sw.GetDocumentCount() == documents.Length, "WEIGHT_DOCUMENT_INVENTORY_COUNT_MISMATCH"); }
            catch (Exception error) { rejections.Add(error.ToString()); }
            for (int index = 0; index < documents.Length; index++)
            {
                try
                {
                    ModelDoc2 model = documents[index] as ModelDoc2; Need(model != null, "WEIGHT_UNKNOWN_DOCUMENT_PRESERVED");
                    string path = model.GetPathName(); Need(!String.IsNullOrEmpty(path), "WEIGHT_UNSAVED_FOREIGN_DOCUMENT_PRESERVED"); path = Full(path);
                    Need(c.AllowedDocuments != null && c.AllowedDocuments.Contains(path), "WEIGHT_FOREIGN_DOCUMENT_PRESERVED " + path);
                    OwnedDocument prior = c.Owned.SingleOrDefault(item => Paths.Equals(item.Path, path));
                    if (prior != null) Need(c.Sw.IsSame(prior.Model, model) == 1, "WEIGHT_DOCUMENT_IDENTITY_CHANGED " + path);
                    else { c.Owned.Add(new OwnedDocument { Model = model, Path = path }); documents[index] = null; }
                    Need(model.IsOpenedReadOnly(), "WEIGHT_LOADED_DOCUMENT_NOT_READONLY " + path);
                }
                catch (Exception error) { rejections.Add(error.ToString()); }
            }
        }
        finally { foreach (object value in documents) Release(value); }
        Guard(c); Need(rejections.Count == 0, String.Join(" | ", rejections));
    }

    static void CloseOwned(Context c)
    {
        Guard(c); var errors = new List<string>();
        int maximumAttempts = c.Owned.Count + 1;
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            object[] documents = Items(c.Sw.GetDocuments()); var candidates = new List<KeyValuePair<OwnedDocument, string>>(); var titles = new Dictionary<string, int>(Paths);
            var present = new HashSet<OwnedDocument>(); bool complete = true;
            try
            {
                if (documents.Length != c.Sw.GetDocumentCount()) { complete = false; errors.Add("WEIGHT_CLEANUP_DOCUMENT_COUNT_MISMATCH"); }
                foreach (object value in documents)
                {
                    try
                    {
                        ModelDoc2 model = value as ModelDoc2; if (model == null) { complete = false; errors.Add("WEIGHT_UNKNOWN_CLEANUP_DOCUMENT_PRESERVED"); continue; }
                        string path = model.GetPathName(), title = model.GetTitle();
                        if (!String.IsNullOrEmpty(title)) titles[title] = titles.ContainsKey(title) ? titles[title] + 1 : 1;
                        OwnedDocument owned = c.Owned.SingleOrDefault(item => c.Sw.IsSame(model, item.Model) == 1);
                        if (owned == null) errors.Add("WEIGHT_FOREIGN_CLEANUP_DOCUMENT_PRESERVED " + path);
                        else
                        {
                            present.Add(owned);
                            if (String.IsNullOrEmpty(path) || !Paths.Equals(path, owned.Path)) errors.Add("WEIGHT_CLEANUP_PATH_CHANGED " + path);
                            else candidates.Add(new KeyValuePair<OwnedDocument, string>(owned, title));
                        }
                    }
                    catch (Exception error) { complete = false; errors.Add(error.ToString()); }
                }
            }
            finally { foreach (object value in documents) Release(value); }
            if (!complete) break;
            foreach (OwnedDocument closed in c.Owned.Where(item => !present.Contains(item)).ToArray()) { Release(closed.Model); c.Owned.Remove(closed); }
            if (candidates.Count == 0) break;
            var selected = candidates.Where(item => !String.IsNullOrEmpty(item.Value) && titles[item.Value] == 1).OrderByDescending(item => Paths.Equals(item.Key.Path, c.PrimaryDocumentPath)).FirstOrDefault();
            if (selected.Key == null) { errors.Add("WEIGHT_AMBIGUOUS_CLOSE_TITLE_PRESERVED"); break; }
            Guard(c); Need(Paths.Equals(selected.Key.Model.GetPathName(), selected.Key.Path), "WEIGHT_CLEANUP_PATH_CHANGED");
            c.Sw.CloseDoc(selected.Value); Guard(c);
        }
        if (c.Owned.Count == 0) { c.AllowedDocuments = null; c.PrimaryDocumentPath = null; }
        try { Empty(c); } catch (Exception error) { errors.Add(error.ToString()); }
        Need(errors.Count == 0, String.Join(" | ", errors.Distinct()));
    }

    static string Active(Context c, ModelDoc2 model)
    {
        Guard(c); ConfigurationManager manager = null; Configuration configuration = null;
        try { manager = model.ConfigurationManager; Need(manager != null, "WEIGHT_CONFIGURATION_MANAGER_REQUIRED"); configuration = manager.ActiveConfiguration; Need(configuration != null && !String.IsNullOrEmpty(configuration.Name), "WEIGHT_ACTIVE_CONFIGURATION_REQUIRED"); return configuration.Name; }
        finally { Release(configuration); Release(manager); }
    }

    static void Error(Context c, string stage, Exception error) { c.Errors.Add(new { stage = stage, error = error.ToString() }); }

    static void Checkpoint(Context c, string stage, Target target)
    {
        Guard(c);
        if (c.Progress != null) c.Progress(new { stage = stage, identity = target == null ? null : target.Identity, origin = target == null ? null : target.Origin, result = c.Result });
    }

    static object Read(Context c, Dictionary<string, object> output, string name, string stage, Func<object> action)
    {
        Guard(c);
        try { object value = action(); output[name] = value; return value; }
        catch (SafetyException) { throw; }
        catch (Exception error) { output[name] = null; output[name + "Error"] = error.ToString(); Error(c, stage + "/" + name, error); return null; }
    }

    static double Finite(double value) { if (Double.IsNaN(value) || Double.IsInfinity(value)) throw new InvalidOperationException("WEIGHT_NONFINITE_API_VALUE"); return value; }
    static double[] NumericArray(object value) { return Items(value).Select(item => Finite(Convert.ToDouble(item, Invariant))).ToArray(); }
    static double? Number(Dictionary<string, object> value, string key) { object raw; return value.TryGetValue(key, out raw) && raw is double ? (double?)raw : null; }
    static bool? Boolean(Dictionary<string, object> value, string key) { object raw; return value.TryGetValue(key, out raw) && raw is bool ? (bool?)raw : null; }

    static object ReadCachedWeightBaseline(Context c, ModelDoc2 model, ModelDocExtension extension, string[] scopes, string stage)
    {
        var result = new Dictionary<string, object> { { "phase", stage }, { "capturedBeforeNonCachedReadsActivationOrRebuild", true }, { "activeConfigurationBefore", Active(c, model) } }; var rows = new List<object>(); result["scopes"] = rows;
        foreach (string scope in scopes)
        {
            var row = new Dictionary<string, object> { { "configuration", scope }, { "name", "重量" }, { "useCached", true }, { "activeConfigurationBefore", Active(c, model) } }; rows.Add(row); CustomPropertyManager manager = null;
            try
            {
                manager = extension.CustomPropertyManager[scope]; if (manager == null) throw new InvalidOperationException("WEIGHT_PROPERTY_MANAGER_UNAVAILABLE");
                Guard(c); string raw, resolved; bool wasResolved, linked; int status = manager.Get6("重量", true, out raw, out resolved, out wasResolved, out linked);
                row["raw"] = raw; row["resolved"] = resolved; row["status"] = status; row["statusName"] = Enum.GetName(typeof(swCustomInfoGetResult_e), status); row["wasResolved"] = wasResolved; row["linked"] = linked;
                if (status != 0 && status != 2 && !(scope == "" && status == 1)) throw new InvalidOperationException("WEIGHT_CACHED_WEIGHT_READ_STATUS " + scope + "/" + status);
            }
            catch (SafetyException) { throw; }
            catch (Exception error) { row["error"] = error.ToString(); Error(c, stage + "/" + scope, error); }
            finally { Release(manager); row["activeConfigurationAfter"] = Active(c, model); }
            EnrollDocuments(c);
        }
        result["activeConfigurationAfter"] = Active(c, model); return result;
    }

    static object ReadScopes(Context c, ModelDoc2 model, ModelDocExtension extension, string[] scopes, string stage)
    {
        var result = new Dictionary<string, object>(); var rows = new List<object>(); result["scopes"] = rows; result["phase"] = stage; result["activeConfigurationBefore"] = Active(c, model);
        foreach (string scope in scopes)
        {
            var row = new Dictionary<string, object> { { "configuration", scope }, { "activeConfigurationBefore", Active(c, model) } }; rows.Add(row);
            var properties = new List<object>(); row["properties"] = properties; CustomPropertyManager manager = null;
            try
            {
                manager = extension.CustomPropertyManager[scope]; if (manager == null) throw new InvalidOperationException("WEIGHT_PROPERTY_MANAGER_UNAVAILABLE");
                string[] names = Items(manager.GetNames()).Select(value => Convert.ToString(value, Invariant)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (names.Any(String.IsNullOrEmpty) || names.Distinct(StringComparer.Ordinal).Count() != names.Length || manager.Count != names.Length) throw new InvalidOperationException("WEIGHT_PROPERTY_NAMES_OR_COUNT_INVALID");
                row["propertyNames"] = names; row["propertyCount"] = names.Length;
                foreach (string name in names)
                {
                    Guard(c); var property = new Dictionary<string, object> { { "name", name }, { "useCached", false }, { "activeConfigurationBefore", Active(c, model) } }; properties.Add(property);
                    try
                    {
                        string raw, resolved; bool wasResolved, linked; int status = manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
                        property["raw"] = raw; property["resolved"] = resolved; property["status"] = status; property["statusName"] = Enum.GetName(typeof(swCustomInfoGetResult_e), status); property["wasResolved"] = wasResolved; property["linked"] = linked;
                        if (status != 0 && status != 2) throw new InvalidOperationException("WEIGHT_NAMED_PROPERTY_READ_STATUS " + status);
                    }
                    catch (SafetyException) { throw; }
                    catch (Exception error) { property["error"] = error.ToString(); Error(c, stage + "/" + scope + "/" + name, error); }
                    finally { property["activeConfigurationAfter"] = Active(c, model); }
                }
                if (scope != "" && !names.Contains("重量", StringComparer.Ordinal)) throw new InvalidOperationException("WEIGHT_CONFIGURATION_WEIGHT_PROPERTY_MISSING " + scope);
            }
            catch (SafetyException) { throw; }
            catch (Exception error) { row["error"] = error.ToString(); Error(c, stage + "/" + scope, error); }
            finally { Release(manager); row["activeConfigurationAfter"] = Active(c, model); }
            EnrollDocuments(c);
        }
        result["activeConfigurationAfter"] = Active(c, model); return result;
    }

    static double? KilogramsPerUnit(int value)
    {
        if (value == (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Milligrams) return 0.000001;
        if (value == (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Grams) return 0.001;
        if (value == (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Kilograms) return 1.0;
        if (value == (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Pounds) return 0.45359237;
        return null;
    }

    static object UnitPreference(Context c, ModelDocExtension extension, swUserPreferenceIntegerValue_e preference, Type enumeration, string stage)
    {
        var result = new Dictionary<string, object> { { "preference", preference.ToString() }, { "preferenceId", (int)preference }, { "option", (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified } };
        object raw = Read(c, result, "raw", stage, delegate { return extension.GetUserPreferenceInteger((int)preference, (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified); });
        if (raw != null)
        {
            int value = (int)raw; result["enumName"] = enumeration == null ? null : Enum.GetName(enumeration, value);
            if (enumeration != null && result["enumName"] == null) Error(c, stage, new InvalidOperationException("WEIGHT_UNKNOWN_UNIT_ENUM " + preference + "=" + value));
            if (preference == swUserPreferenceIntegerValue_e.swUnitsMassPropMass) { double? conversion = KilogramsPerUnit(value); result["oneDocumentMassUnitInKg"] = conversion; result["conversionSource"] = "SW2020 swUnitsMassPropMass_e and exact SI definitions; no UserUnit mass API"; }
        }
        return result;
    }

    static object ReadUnits(Context c, ModelDocExtension extension, string stage)
    {
        return new {
            userUnitMassApi = new { supported = false, reason = "swUserUnitsType_e only length/angle in SW2020" },
            mass = UnitPreference(c, extension, swUserPreferenceIntegerValue_e.swUnitsMassPropMass, typeof(swUnitsMassPropMass_e), stage),
            volume = UnitPreference(c, extension, swUserPreferenceIntegerValue_e.swUnitsMassPropVolume, typeof(swUnitsMassPropVolume_e), stage),
            length = UnitPreference(c, extension, swUserPreferenceIntegerValue_e.swUnitsMassPropLength, typeof(swLengthUnit_e), stage),
            decimalPlaces = UnitPreference(c, extension, swUserPreferenceIntegerValue_e.swUnitsMassPropDecimalPlaces, null, stage),
            documentUnitSystem = UnitPreference(c, extension, swUserPreferenceIntegerValue_e.swUnitSystem, typeof(swUnitSystem_e), stage),
            rawResolvedFormattingReinterpreted = false,
            siIndependentMass = new { mass = "kg", volume = "m^3", density = "kg/m^3", source = "MassProperty2.UseSystemUnits=true; SW2020 installed API help" }
        };
    }

    static object ReadOverrides(Context c, MassProperty2 mass, string stage)
    {
        var result = new Dictionary<string, object>(); MassPropertyOverrideOptions options = null;
        try
        {
            options = mass.GetOverrideOptions() as MassPropertyOverrideOptions; if (options == null) throw new InvalidOperationException("WEIGHT_OVERRIDE_OPTIONS_UNAVAILABLE");
            Read(c, result, "overrideMass", stage, delegate { return options.OverrideMass; });
            Read(c, result, "overrideCenterOfMass", stage, delegate { return options.OverrideCenterOfMass; });
            Read(c, result, "overrideMomentsOfInertia", stage, delegate { return options.OverrideMomentsOfInertia; });
            Read(c, result, "massValueRaw", stage, delegate { return Finite(options.GetOverrideMassValue()); });
            result["massValueEffectiveOnlyWhenOverrideMassIsTrue"] = true; result["overrideSettersCalled"] = false;
        }
        catch (SafetyException) { throw; }
        catch (Exception error) { result["error"] = error.ToString(); Error(c, stage, error); }
        finally { Release(options); }
        return result;
    }

    static object ReadBodies(Context c, ModelDoc2 model, string stage)
    {
        var result = new Dictionary<string, object> { { "api", "PartDoc.GetBodies2(swSolidBody,false) + Body2.GetMassProperties(1)" }, { "densityArgument", 1.0 }, { "densityArgumentIsActualMaterialDensity", false }, { "volumeUnit", "m^3" } }; var rows = new List<object>(); result["bodies"] = rows;
        object[] bodies = new object[0]; double sum = 0; bool complete = true;
        try
        {
            bodies = Items(((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false));
            if (bodies.Length == 0) throw new InvalidOperationException("WEIGHT_SOLID_BODIES_MISSING");
            foreach (object value in bodies)
            {
                Guard(c); var row = new Dictionary<string, object>(); rows.Add(row);
                try
                {
                    Body2 body = value as Body2; if (body == null) throw new InvalidOperationException("WEIGHT_BODY_INTERFACE_UNAVAILABLE"); row["name"] = body.Name;
                    double[] properties = NumericArray(body.GetMassProperties(1.0)); row["rawMassPropertiesAtDensityOne"] = properties;
                    if (properties.Length != 12 || properties[3] <= 0) throw new InvalidOperationException("WEIGHT_BODY_MASS_PROPERTIES_INVALID");
                    row["volumeM3"] = properties[3]; sum += properties[3];
                }
                catch (SafetyException) { throw; }
                catch (Exception error) { complete = false; row["error"] = error.ToString(); Error(c, stage, error); }
            }
        }
        catch (SafetyException) { throw; }
        catch (Exception error) { complete = false; result["error"] = error.ToString(); Error(c, stage, error); }
        finally { foreach (object value in bodies) Release(value); }
        result["solidBodyCount"] = bodies.Length; result["complete"] = complete; result["totalVolumeM3"] = complete ? (object)Finite(sum) : null; return result;
    }

    static bool Near(double left, double right, double absolute, double relative) { return Math.Abs(left - right) <= Math.Max(absolute, relative * Math.Max(Math.Abs(left), Math.Abs(right))); }

    static object ReadMass(Context c, ModelDoc2 model, ModelDocExtension extension, string configuration, string stage)
    {
        var result = new Dictionary<string, object> { { "configuration", configuration }, { "massUnit", "kg" }, { "volumeUnit", "m^3" }, { "densityUnit", "kg/m^3" }, { "densitySource", "MassProperty2.Density; for mixed densities SW2020 documents this as Mass/Volume" } }; MassProperty2 mass = null;
        Need(Active(c, model) == configuration, "WEIGHT_MASS_CONFIGURATION_IDENTITY_MISMATCH");
        try
        {
            model.ClearSelection2(true); result["selectionClearedInMemory"] = true; mass = extension.CreateMassProperty2() as MassProperty2;
            if (mass == null) throw new InvalidOperationException("WEIGHT_CREATE_MASS_PROPERTY2_UNAVAILABLE");
            Read(c, result, "useSystemUnitsBefore", stage, delegate { return mass.UseSystemUnits; });
            Read(c, result, "useSystemUnits", stage, delegate { mass.UseSystemUnits = true; bool actual = mass.UseSystemUnits; if (!actual) throw new InvalidOperationException("WEIGHT_SI_UNITS_NOT_APPLIED"); return actual; });
            Read(c, result, "includeHiddenBodiesBefore", stage, delegate { return mass.IncludeHiddenBodiesOrComponents; });
            Read(c, result, "includeHiddenBodies", stage, delegate { mass.IncludeHiddenBodiesOrComponents = true; bool actual = mass.IncludeHiddenBodiesOrComponents; if (!actual) throw new InvalidOperationException("WEIGHT_ALL_SOLID_BODIES_SCOPE_NOT_APPLIED"); return actual; });
            object recalculated = Read(c, result, "recalculated", stage, delegate { bool ok = mass.Recalculate(); if (!ok) throw new InvalidOperationException("WEIGHT_MASS_RECALCULATE_FAILED"); return ok; });
            result["recalculationReturned"] = recalculated;
            if (Boolean(result, "useSystemUnits") == true)
            {
                Read(c, result, "massKg", stage, delegate { return Finite(mass.Mass); });
                Read(c, result, "volumeM3", stage, delegate { return Finite(mass.Volume); });
                Read(c, result, "densityKgPerM3", stage, delegate { return Finite(mass.Density); });
                Read(c, result, "showWeldBeadMass", stage, delegate { return mass.ShowWeldBeadMass; });
                Read(c, result, "weldBeadMassKg", stage, delegate { return Finite(mass.WeldBeadMass); });
            }
            result["overrideOptions"] = ReadOverrides(c, mass, stage + "/overrides");
        }
        catch (SafetyException) { throw; }
        catch (Exception error) { result["error"] = error.ToString(); Error(c, stage, error); }
        finally { Release(mass); }
        var body = (Dictionary<string, object>)ReadBodies(c, model, stage + "/bodyVolume"); result["bodyVolume"] = body;
        double? volume = Number(body, "totalVolumeM3"), massVolume = Number(result, "volumeM3"), density = Number(result, "densityKgPerM3"), actualMass = Number(result, "massKg");
        result["densityTimesBodyVolumeKg"] = volume.HasValue && density.HasValue ? (object)Finite(volume.Value * density.Value) : null;
        result["volumeConsistency"] = volume.HasValue && massVolume.HasValue ? new { absoluteDeltaM3 = Math.Abs(volume.Value - massVolume.Value), absoluteToleranceM3 = 1e-12, relativeTolerance = 1e-7, equal = Near(volume.Value, massVolume.Value, 1e-12, 1e-7) } : null;
        result["massConsistency"] = volume.HasValue && density.HasValue && actualMass.HasValue ? new { absoluteDeltaKg = Math.Abs(volume.Value * density.Value - actualMass.Value), absoluteToleranceKg = 1e-9, relativeTolerance = 1e-7, equal = Near(volume.Value * density.Value, actualMass.Value, 1e-9, 1e-7), interpretation = "Diagnostic agreement only; override flags, weld beads and MassProperty2 effective density must be considered" } : null;
        result["activeConfigurationAfter"] = Active(c, model); Need((string)result["activeConfigurationAfter"] == configuration, "WEIGHT_MASS_CONFIGURATION_CHANGED"); EnrollDocuments(c); return result;
    }

    static void RequireOwnedTarget(Context c, ModelDoc2 model)
    {
        Guard(c); Need(model != null, "WEIGHT_CONFIGURATION_DOCUMENT_REQUIRED");
        string path = model.GetPathName(); Need(!String.IsNullOrEmpty(path) && Paths.Equals(path, c.PrimaryDocumentPath), "WEIGHT_CONFIGURATION_DOCUMENT_PATH_CHANGED");
        OwnedDocument owned = c.Owned.SingleOrDefault(item => Paths.Equals(item.Path, path));
        Need(owned != null && c.Sw.IsSame(owned.Model, model) == 1, "WEIGHT_CONFIGURATION_DOCUMENT_IDENTITY_CHANGED");
        object[] documents = Items(c.Sw.GetDocuments());
        try
        {
            Need(documents.Length == c.Sw.GetDocumentCount(), "WEIGHT_CONFIGURATION_DOCUMENT_COUNT_MISMATCH");
            Need(documents.Count(value => value is ModelDoc2 && c.Sw.IsSame(value, model) == 1) == 1, "WEIGHT_CONFIGURATION_DOCUMENT_IDENTITY_NOT_LIVE");
        }
        finally { foreach (object value in documents) Release(value); }
        Need(model.IsOpenedReadOnly(), "WEIGHT_CONFIGURATION_DOCUMENT_NOT_READONLY");
    }

    static bool EnsureConfiguration(Context c, ModelDoc2 model, string configuration, Dictionary<string, object> result, string stage)
    {
        RequireOwnedTarget(c, model); result["ownedDocumentIdentityVerifiedBefore"] = true;
        Configuration requested = null;
        try
        {
            Guard(c); requested = model.GetConfigurationByName(configuration) as Configuration;
            Need(requested != null && requested.Name == configuration, "WEIGHT_TARGET_CONFIGURATION_UNAVAILABLE " + configuration);
            result["targetConfigurationExists"] = true;
        }
        finally { Release(requested); }
        string before = Active(c, model); bool alreadyActive = before == configuration;
        result["activeConfigurationBefore"] = before; result["configurationAlreadyActive"] = alreadyActive; result["showConfigurationCalled"] = !alreadyActive; result["showConfigurationReturned"] = null;
        result["explicitActivationCalledEvenWhenAlreadyActive"] = false;
        object activated = null;
        if (!alreadyActive) activated = Read(c, result, "showConfigurationReturned", stage, delegate { return model.ShowConfiguration2(configuration); });
        RequireOwnedTarget(c, model); result["ownedDocumentIdentityVerifiedAfter"] = true;
        result["activeConfigurationAfterActivation"] = Active(c, model); EnrollDocuments(c);
        if (!alreadyActive && (!(activated is bool) || !(bool)activated))
        {
            Error(c, stage, new InvalidOperationException("WEIGHT_EXPLICIT_CONFIGURATION_ACTIVATION_FAILED " + configuration)); return false;
        }
        if ((string)result["activeConfigurationAfterActivation"] != configuration)
        {
            Error(c, stage, new InvalidOperationException("WEIGHT_CONFIGURATION_NOT_ACTIVATED " + configuration)); return false;
        }
        result["configurationEnsured"] = true; return true;
    }

    static object ReadConfiguration(Context c, ModelDoc2 model, ModelDocExtension extension, string configuration, string stage)
    {
        var result = new Dictionary<string, object> { { "configuration", configuration } };
        if (!EnsureConfiguration(c, model, configuration, result, stage)) { result["readbackComplete"] = false; return result; }
        Read(c, result, "forceRebuildReturned", stage, delegate { bool ok = model.ForceRebuild3(false); if (!ok) throw new InvalidOperationException("WEIGHT_CONFIGURATION_REBUILD_FAILED " + configuration); return ok; }); EnrollDocuments(c);
        Need(Active(c, model) == configuration, "WEIGHT_REBUILD_CONFIGURATION_CHANGED");
        string[] scopes = { "", configuration };
        result["firstPropertyRead"] = ReadScopes(c, model, extension, scopes, stage + "/first_property_read");
        result["repeatPropertyRead"] = ReadScopes(c, model, extension, scopes, stage + "/repeat_property_read");
        var material = new Dictionary<string, object>(); result["material"] = material;
        Read(c, material, "readback", stage, delegate { string database; string name = ((PartDoc)model).GetMaterialPropertyName2(configuration, out database); return new { name = name, database = database }; });
        result["units"] = ReadUnits(c, extension, stage + "/units");
        result["independentMass"] = ReadMass(c, model, extension, configuration, stage + "/independent_mass");
        result["propertiesAfterIndependentMass"] = ReadScopes(c, model, extension, scopes, stage + "/property_read_after_mass");
        result["activeConfigurationAfter"] = Active(c, model); return result;
    }

    static void EquationFileScope(Context c, ModelDoc2 model, Target target, Dictionary<string, object> row)
    {
        EquationMgr equations = null;
        try
        {
            equations = model.GetEquationMgr(); row["equationManagerPresent"] = equations != null;
            if (equations != null) { bool linked = equations.LinkToFile; string path = equations.FilePath; row["equationsLinkToFile"] = linked; row["equationsFilePath"] = path; Need(!linked, "WEIGHT_LINKED_EQUATION_FILE_REQUIRES_DECLARED_CLOSURE " + target.Relative + " -> " + path); }
        }
        catch (SafetyException) { throw; }
        catch (Exception error) { row["equationScopeError"] = error.ToString(); Error(c, target.Relative + "/equation_scope", error); }
        finally { Release(equations); }
    }

    static void ReadTarget(Context c, Target target)
    {
        Guard(c); Empty(c); SameHashes(c.CopyAfterRewire, Inventory(c.Plan.Root), "before_target"); SameHashes(c.Originals, Rehash(c.Originals), "originals_before_target");
        int errorsBefore = c.Errors.Count; var row = new Dictionary<string, object> { { "identity", target.Identity }, { "origin", target.Origin }, { "relativePath", target.Relative }, { "path", target.Path }, { "entrySourceSha256", target.SourceSha256 }, { "sha256AfterExplicitRewrite", c.CopyAfterRewire[target.Path] }, { "partSaveCalled", false } }; c.Targets.Add(row);
        ModelDoc2 model = null; ModelDocExtension extension = null; bool bodyCompleted = false, baselineCaptured = false; c.AllowedDocuments = TargetClosure(c, target.Path); c.PrimaryDocumentPath = target.Path; row["allowedDocumentClosure"] = c.AllowedDocuments.OrderBy(value => value, Paths).ToArray();
        try
        {
            Checkpoint(c, "target_start", target);
            int errors = 0, warnings = 0; Guard(c);
            model = c.Sw.OpenDoc6(target.Path, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref errors, ref warnings) as ModelDoc2;
            row["openErrors"] = errors; row["openWarnings"] = warnings;
            EnrollDocuments(c); Need(model != null && errors == 0 && model is PartDoc && Paths.Equals(Full(model.GetPathName()), target.Path), "WEIGHT_OPEN_TARGET_IDENTITY_FAILED " + target.Relative);
            Need(model.IsOpenedReadOnly() && Hash(target.Path) == c.CopyAfterRewire[target.Path], "WEIGHT_OPEN_NOT_READONLY_OR_SHA_CHANGED " + target.Relative);
            row["actualOpenedPath"] = model.GetPathName(); row["openedReadOnly"] = model.IsOpenedReadOnly(); row["activeConfigurationAtOpen"] = Active(c, model);
            string[] configurations = Items(model.GetConfigurationNames()).Select(value => Convert.ToString(value, Invariant)).OrderBy(value => value, StringComparer.Ordinal).ToArray(); row["configurations"] = configurations;
            Need(configurations.Length == 2 && new HashSet<string>(configurations, StringComparer.Ordinal).SetEquals(Configurations), "WEIGHT_TARGET_CONFIGURATION_SET_MISMATCH " + target.Relative);
            extension = model.Extension; Need(extension != null, "WEIGHT_MODEL_EXTENSION_REQUIRED");
            string[] scopes = new[] { "" }.Concat(configurations).ToArray();
            row["cachedWeightBaseline"] = ReadCachedWeightBaseline(c, model, extension, scopes, target.Relative + "/cached_weight_baseline"); baselineCaptured = true;
            row["firstColdPropertyRead"] = ReadScopes(c, model, extension, scopes, target.Relative + "/cold_first");
            row["repeatColdPropertyRead"] = ReadScopes(c, model, extension, scopes, target.Relative + "/cold_repeat");
            EquationFileScope(c, model, target, row);
            var configured = new List<object>(); row["configuredReadbacks"] = configured;
            foreach (string configuration in Configurations) configured.Add(ReadConfiguration(c, model, extension, configuration, target.Relative + "/" + configuration));
            bodyCompleted = true;
        }
        catch (Exception error) { row["fatalError"] = error.ToString(); throw; }
        finally
        {
            Exception cleanupFatal = null;
            if (model != null && baselineCaptured)
            {
                try
                {
                    Guard(c); Need(Paths.Equals(model.GetPathName(), target.Path), "WEIGHT_RESTORE_DOCUMENT_IDENTITY_CHANGED");
                    var restore = new Dictionary<string, object> { { "configuration", "Default" } }; row["defaultConfigurationRestore"] = restore;
                    bool restored = EnsureConfiguration(c, model, "Default", restore, target.Relative + "/restore_configuration");
                    row["restoreDefaultReturned"] = restore["showConfigurationReturned"]; row["restoreDefaultCalled"] = restore["showConfigurationCalled"]; row["activeConfigurationAfterRestore"] = restore["activeConfigurationAfterActivation"];
                    if (!restored) throw new InvalidOperationException("WEIGHT_DEFAULT_RESTORE_FAILED");
                }
                catch (SafetyException error) { cleanupFatal = error; row["restoreError"] = error.ToString(); Error(c, target.Relative + "/restore", error); }
                catch (Exception error) { row["restoreError"] = error.ToString(); Error(c, target.Relative + "/restore", error); }
            }
            Release(extension); Release(model);
            try { CloseOwned(c); row["documentsEmptyAfterClose"] = true; }
            catch (Exception error) { cleanupFatal = cleanupFatal == null ? error : new AggregateException(cleanupFatal, error); row["cleanupError"] = error.ToString(); Error(c, target.Relative + "/close", error); }
            try { SameHashes(c.CopyAfterRewire, Inventory(c.Plan.Root), "after_target"); SameHashes(c.Originals, Rehash(c.Originals), "originals_after_target"); row["sourceAndReadOnlyCopyHashesUnchanged"] = true; }
            catch (Exception error) { cleanupFatal = cleanupFatal == null ? error : new AggregateException(cleanupFatal, error); row["hashError"] = error.ToString(); Error(c, target.Relative + "/hashes", error); }
            row["readbackComplete"] = bodyCompleted && c.Errors.Count == errorsBefore && cleanupFatal == null;
            if (cleanupFatal != null) throw cleanupFatal;
        }
        Checkpoint(c, "target_complete", target);
    }

    public static object Run(ISldWorks sw, string cadRoot, Dictionary<string, object> job, Action<object> progress = null)
    {
        var c = new Context { Sw = sw, Progress = progress }; c.Result = new Dictionary<string, object> {
            { "schema", "16029-weight-readback/v1" }, { "diagnosticOnly", true }, { "success", false }, { "readbackComplete", false }, { "acceptancePass", false }, { "stageAcceptance", false }, { "fullPlanPass", false }, { "production_release", false }, { "startedAtUtc", DateTime.UtcNow.ToString("o") }, { "targets", c.Targets }, { "errors", c.Errors }, { "saveCalled", false }, { "targetOrderSource", "job.weightTargets array order" }
        }; bool entered = false;
        try
        {
            c.Plan = ReadPlan(cadRoot, job); Need(sw != null, "WEIGHT_OWNED_CAD_SESSION_REQUIRED"); VariableTrial.RuntimeOwnership(); c.Pid = sw.GetProcessID();
            using (Process process = Process.GetProcessById(c.Pid)) { c.Ticks = process.StartTime.ToUniversalTime().Ticks; c.Executable = Path.GetFullPath(process.MainModule.FileName); }
            c.Result["cadRoot"] = c.Plan.Root; c.Result["session"] = new { pid = c.Pid, startTicks = c.Ticks, executable = c.Executable };
            Empty(c); entered = true; Prepare(c); Checkpoint(c, "rewrite_and_closure_complete", null);
            foreach (Target target in c.Plan.Targets) ReadTarget(c, target);
            Empty(c); c.Result["readbackComplete"] = c.Errors.Count == 0 && c.Targets.Count == 8; c.Result["success"] = c.Result["readbackComplete"];
        }
        catch (Exception error) { c.Result["fatalError"] = error.ToString(); Error(c, "run", error); c.Result["success"] = false; c.Result["readbackComplete"] = false; }
        finally
        {
            if (entered)
            {
                try { CloseOwned(c); c.Result["documentsEmptyAtEnd"] = true; }
                catch (Exception error) { Error(c, "final_document_cleanup", error); c.Result["success"] = false; c.Result["readbackComplete"] = false; }
            }
            if (c.Originals != null)
            {
                try { var after = Rehash(c.Originals); c.Result["sourceFiles"] = c.Originals.Select(pair => new { path = pair.Key, sha256Before = pair.Value, sha256After = after[pair.Key] }).ToArray(); SameHashes(c.Originals, after, "final_originals"); c.Result["sourceFilesUnchanged"] = true; }
                catch (Exception error) { Error(c, "final_source_hashes", error); c.Result["sourceFilesUnchanged"] = false; c.Result["success"] = false; c.Result["readbackComplete"] = false; }
            }
            if (c.CopyAfterRewire != null)
            {
                try { var after = Inventory(c.Plan.Root); c.Result["readOnlyCopyFiles"] = c.CopyAfterRewire.Select(pair => new { path = pair.Key, sha256Before = pair.Value, sha256After = after.ContainsKey(pair.Key) ? after[pair.Key] : null }).ToArray(); SameHashes(c.CopyAfterRewire, after, "final_readonly_copy"); c.Result["readOnlyCopyFilesUnchanged"] = true; }
                catch (Exception error) { Error(c, "final_readonly_hashes", error); c.Result["readOnlyCopyFilesUnchanged"] = false; c.Result["success"] = false; c.Result["readbackComplete"] = false; }
            }
            c.Result["finishedAtUtc"] = DateTime.UtcNow.ToString("o");
        }
        return c.Result;
    }
}
