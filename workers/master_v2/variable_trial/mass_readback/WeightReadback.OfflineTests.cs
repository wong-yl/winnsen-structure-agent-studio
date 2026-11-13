using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using SolidWorks.Interop.sldworks;

internal static class VariableTrial
{
    internal static bool AllowManagedFixture;
    internal static void RuntimeOwnership() { if (!AllowManagedFixture) throw new InvalidOperationException("OFFLINE_TESTS_MUST_NOT_CALL_NATIVE_RUNTIME"); }
}

internal static class WeightReadbackOfflineTests
{
    static int Passed;
    static readonly Type Subject = typeof(WeightReadback);
    static object Call(string method, params object[] arguments)
    {
        try { return Subject.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static void Pass(string name, bool value) { if (!value) throw new InvalidOperationException("FAIL " + name); Passed++; Console.WriteLine("PASS " + name); }
    static void Rejected(string name, Action action, string expected)
    {
        try { action(); }
        catch (Exception error) { Pass(name, error.Message.Contains(expected)); return; }
        throw new InvalidOperationException("FAIL missing rejection " + name);
    }
    static object Space(string root, string original)
    {
        Type type = Subject.GetNestedType("Namespace", BindingFlags.NonPublic); object value = Activator.CreateInstance(type, true);
        type.GetField("Copy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, root);
        type.GetField("Original", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, original); return value;
    }
    sealed class ManagedProxy : RealProxy
    {
        readonly Func<IMethodCallMessage, object> Handler;
        internal ManagedProxy(Type type, Func<IMethodCallMessage, object> handler) : base(type) { Handler = handler; }
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try
            {
                object result = Handler(call); object[] arguments = call.Properties["offlineOutArguments"] as object[] ?? call.Args;
                return new ReturnMessage(result, arguments, arguments.Length, call.LogicalCallContext, call);
            }
            catch (Exception error) { return new ReturnMessage(error, call); }
        }
    }
    static T Proxy<T>(Func<IMethodCallMessage, object> handler) where T : class
    {
        var result = new ManagedProxy(typeof(T), handler).GetTransparentProxy() as T;
        if (result == null || Marshal.IsComObject(result)) throw new InvalidOperationException("MANAGED_ONLY_OFFLINE_PROXY_REQUIRED");
        return result;
    }
    static void Set(object value, string field, object content) { value.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, content); }
    static object Get(object value, string field) { return value.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value); }
    sealed class ManagedSession
    {
        internal ISldWorks Sw;
        internal object Context;
        internal List<ModelDoc2> Documents = new List<ModelDoc2>();
        internal List<string> Closed = new List<string>();
        internal bool RemoveOnClose = true;
        internal string ActiveName = "Default";
        internal List<string> PropertyCalls = new List<string>();
        internal bool ShiftCachedConfiguration;
        internal bool ShowReturn = true;
        internal string ShowAfterName;
        internal string MissingConfiguration;
        internal int ShowCalls;
        internal ManagedSession()
        {
            Sw = Proxy<ISldWorks>(delegate(IMethodCallMessage call) {
                if (call.MethodName == "GetProcessID") return Process.GetCurrentProcess().Id;
                if (call.MethodName == "GetDocuments") return Documents.Cast<object>().ToArray();
                if (call.MethodName == "GetDocumentCount") return Documents.Count;
                if (call.MethodName == "IsSame") return Object.ReferenceEquals(call.Args[0], call.Args[1]) ? 1 : 0;
                if (call.MethodName == "CloseDoc")
                {
                    string title = (string)call.Args[0]; Closed.Add(title);
                    if (RemoveOnClose) Documents.RemoveAll(value => value.GetTitle() == title);
                    return null;
                }
                throw new InvalidOperationException("UNEXPECTED_MANAGED_SESSION_CALL " + call.MethodName);
            });
            Context = Activator.CreateInstance(Subject.GetNestedType("Context", BindingFlags.NonPublic), true);
            using (Process process = Process.GetCurrentProcess())
            {
                Set(Context, "Pid", process.Id); Set(Context, "Ticks", process.StartTime.ToUniversalTime().Ticks); Set(Context, "Executable", Path.GetFullPath(process.MainModule.FileName));
            }
            Set(Context, "Sw", Sw);
        }
        internal ModelDoc2 Model(string path, string title, bool readOnly)
        {
            Configuration activeConfiguration = Proxy<Configuration>(delegate(IMethodCallMessage call) { if (call.MethodName == "get_Name") return ActiveName; throw new InvalidOperationException("UNEXPECTED_ACTIVE_CONFIGURATION_CALL " + call.MethodName); });
            ConfigurationManager manager = Proxy<ConfigurationManager>(delegate(IMethodCallMessage call) { if (call.MethodName == "get_ActiveConfiguration") return activeConfiguration; throw new InvalidOperationException("UNEXPECTED_CONFIGURATION_MANAGER_CALL " + call.MethodName); });
            return Proxy<ModelDoc2>(delegate(IMethodCallMessage call) {
                if (call.MethodName == "GetPathName") return path;
                if (call.MethodName == "GetTitle") return title;
                if (call.MethodName == "IsOpenedReadOnly") return readOnly;
                if (call.MethodName == "get_ConfigurationManager") return manager;
                if (call.MethodName == "GetConfigurationByName")
                {
                    string requested = (string)call.Args[0]; if (requested == MissingConfiguration) return null;
                    return Proxy<Configuration>(delegate(IMethodCallMessage configurationCall) { if (configurationCall.MethodName == "get_Name") return requested; throw new InvalidOperationException("UNEXPECTED_REQUESTED_CONFIGURATION_CALL " + configurationCall.MethodName); });
                }
                if (call.MethodName == "ShowConfiguration2")
                {
                    ShowCalls++; string requested = (string)call.Args[0]; if (ShowAfterName != null) ActiveName = ShowAfterName; else if (ShowReturn) ActiveName = requested; return ShowReturn;
                }
                throw new InvalidOperationException("UNEXPECTED_MANAGED_MODEL_CALL " + call.MethodName);
            });
        }
        internal ModelDocExtension Extension()
        {
            return Proxy<ModelDocExtension>(delegate(IMethodCallMessage call) {
                if (call.MethodName != "get_CustomPropertyManager") throw new InvalidOperationException("UNEXPECTED_EXTENSION_CALL " + call.MethodName);
                string scope = (string)call.Args[0];
                return Proxy<CustomPropertyManager>(delegate(IMethodCallMessage property) {
                    if (property.MethodName == "GetNames") return new[] { "重量" };
                    if (property.MethodName == "get_Count") return 1;
                    if (property.MethodName != "Get6") throw new InvalidOperationException("UNEXPECTED_PROPERTY_CALL " + property.MethodName);
                    PropertyCalls.Add(scope + "/" + property.Args[0] + "/" + property.Args[1]);
                    bool cached = (bool)property.Args[1];
                    object[] values = property.Args;
                    values[2] = cached ? "cached-expression/" + scope : "current-expression/" + scope;
                    values[3] = cached ? "cached-resolved/" + scope : "current-resolved/" + scope;
                    values[4] = !cached; values[5] = true;
                    ((System.Runtime.Remoting.Messaging.IMethodMessage)property).Properties["offlineOutArguments"] = values;
                    if (cached && ShiftCachedConfiguration) ActiveName = "DefaultSM-FLAT-PATTERN";
                    return 2;
                });
            });
        }
        internal void Allow(params string[] paths) { Set(Context, "AllowedDocuments", new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase)); if (paths.Length != 0) Set(Context, "PrimaryDocumentPath", paths[0]); }
        internal int OwnedCount { get { return ((IList)Get(Context, "Owned")).Count; } }
    }
    static void ManagedOwnershipTests(string fixture)
    {
        VariableTrial.AllowManagedFixture = true;
        try
        {
            string target = Path.Combine(fixture, "copy", "target.fixture"), dependency = Path.Combine(fixture, "copy", "dependency.fixture"), foreign = Path.Combine(fixture, "other", "foreign.fixture");
            var session = new ManagedSession(); session.Allow(target, dependency);
            session.Documents.Add(session.Model(foreign, "foreign", true)); session.Documents.Add(session.Model(target, "target", true)); session.Documents.Add(session.Model(dependency, "dependency", true));
            Rejected("foreign first still reports rejection", delegate { Call("EnrollDocuments", session.Context); }, "WEIGHT_FOREIGN_DOCUMENT_PRESERVED");
            Pass("all allowed identities enrolled after foreign first", session.OwnedCount == 2);
            Rejected("cleanup preserves foreign while closing owned", delegate { Call("CloseOwned", session.Context); }, "WEIGHT_FOREIGN_CLEANUP_DOCUMENT_PRESERVED");
            Pass("owned closure cleaned despite foreign", session.Closed.SequenceEqual(new[] { "target", "dependency" }) && session.Documents.Count == 1 && session.Documents[0].GetPathName() == foreign && session.OwnedCount == 0);

            session = new ManagedSession(); session.Allow(target); session.Documents.Add(session.Model(target, "target", true)); Call("EnrollDocuments", session.Context);
            session.RemoveOnClose = false;
            Rejected("close no-op fails with live owned document", delegate { Call("CloseOwned", session.Context); }, "WEIGHT_EMPTY_DOCUMENT_SESSION_REQUIRED");
            Pass("failed close retains registered identity and closure", session.OwnedCount == 1 && Get(session.Context, "AllowedDocuments") != null && (string)Get(session.Context, "PrimaryDocumentPath") == target);
            session.RemoveOnClose = true; Call("CloseOwned", session.Context);
            Pass("final retry closes retained owned identity", session.Documents.Count == 0 && session.OwnedCount == 0 && Get(session.Context, "AllowedDocuments") == null);

            session = new ManagedSession(); session.Allow(target); session.Documents.Add(session.Model(target, "target", true)); Call("EnrollDocuments", session.Context);
            session.Documents.Clear(); session.Documents.Add(session.Model(target, "same-path-new-identity", true));
            Rejected("same path different identity is preserved", delegate { Call("CloseOwned", session.Context); }, "WEIGHT_FOREIGN_CLEANUP_DOCUMENT_PRESERVED");
            Pass("same path never grants ownership", session.Closed.Count == 0 && session.Documents.Count == 1);

            session = new ManagedSession(); session.Allow(target, dependency); session.Documents.Add(session.Model(target, "duplicate", true)); session.Documents.Add(session.Model(dependency, "duplicate", true)); Call("EnrollDocuments", session.Context);
            Rejected("ambiguous title cannot close either document", delegate { Call("CloseOwned", session.Context); }, "WEIGHT_AMBIGUOUS_CLOSE_TITLE_PRESERVED");
            Pass("ambiguous live identities remain for retry", session.OwnedCount == 2 && session.Closed.Count == 0);

            session = new ManagedSession(); session.Allow(target); session.Documents.Add(session.Model(target, "target", false));
            Rejected("writable allowed document is enrolled then rejected", delegate { Call("EnrollDocuments", session.Context); }, "WEIGHT_LOADED_DOCUMENT_NOT_READONLY");
            Pass("writable closure identity is retained for cleanup", session.OwnedCount == 1);
            Call("CloseOwned", session.Context); Pass("rejected allowed identity can be safely cleaned", session.OwnedCount == 0 && session.Documents.Count == 0);
        }
        finally { VariableTrial.AllowManagedFixture = false; }
    }
    static void ManagedPropertyTests(string fixture)
    {
        VariableTrial.AllowManagedFixture = true;
        try
        {
            string target = Path.Combine(fixture, "copy", "target.fixture");
            var session = new ManagedSession(); session.Allow(target);
            ModelDoc2 model = session.Model(target, "target", true); session.Documents.Add(model); var extension = session.Extension();
            string[] scopes = { "", "Default", "DefaultSM-FLAT-PATTERN" };
            var baseline = (Dictionary<string, object>)Call("ReadCachedWeightBaseline", session.Context, model, extension, scopes, "offline_cached");
            var rows = ((IEnumerable)baseline["scopes"]).Cast<Dictionary<string, object>>().ToArray();
            Pass("cached baseline covers document and both configurations first", session.PropertyCalls.SequenceEqual(scopes.Select(scope => scope + "/重量/True")) && rows.Length == 3);
            Pass("cached baseline preserves raw resolved status resolution and links", rows.All(row => (string)row["raw"] == "cached-expression/" + row["configuration"] && (string)row["resolved"] == "cached-resolved/" + row["configuration"] && (int)row["status"] == 2 && !(bool)row["wasResolved"] && (bool)row["linked"] && (bool)row["useCached"]));
            Pass("cached baseline records actual active configuration before and after", rows.All(row => (string)row["activeConfigurationBefore"] == "Default" && (string)row["activeConfigurationAfter"] == "Default") && (string)baseline["activeConfigurationBefore"] == "Default" && (string)baseline["activeConfigurationAfter"] == "Default");
            Call("ReadScopes", session.Context, model, extension, scopes, "offline_non_cached");
            Pass("original noncached property reads remain after cached baseline", session.PropertyCalls.SequenceEqual(scopes.Select(scope => scope + "/重量/True").Concat(scopes.Select(scope => scope + "/重量/False"))));
            session.ActiveName = "Default"; session.ShiftCachedConfiguration = true;
            baseline = (Dictionary<string, object>)Call("ReadCachedWeightBaseline", session.Context, model, extension, new[] { "Default" }, "offline_config_change");
            rows = ((IEnumerable)baseline["scopes"]).Cast<Dictionary<string, object>>().ToArray();
            Pass("cached getter configuration change is observed without inferred cause", (string)rows[0]["activeConfigurationBefore"] == "Default" && (string)rows[0]["activeConfigurationAfter"] == "DefaultSM-FLAT-PATTERN");
            Call("CloseOwned", session.Context);
        }
        finally { VariableTrial.AllowManagedFixture = false; }
    }
    static void ManagedConfigurationTests(string fixture)
    {
        VariableTrial.AllowManagedFixture = true;
        try
        {
            string target = Path.Combine(fixture, "copy", "target.fixture");
            ManagedSession session = new ManagedSession(); session.Allow(target); ModelDoc2 model = session.Model(target, "target", true); session.Documents.Add(model); Call("EnrollDocuments", session.Context);
            var result = new Dictionary<string, object>(); bool ensured = (bool)Call("EnsureConfiguration", session.Context, model, "Default", result, "offline/already-active");
            Pass("already-active configuration skips ShowConfiguration2", ensured && !(bool)result["showConfigurationCalled"] && session.ShowCalls == 0 && (string)result["activeConfigurationAfterActivation"] == "Default");

            session.ActiveName = "Default"; session.ShowReturn = true; session.ShowAfterName = null;
            result = new Dictionary<string, object>(); ensured = (bool)Call("EnsureConfiguration", session.Context, model, "DefaultSM-FLAT-PATTERN", result, "offline/switch");
            Pass("true configuration switch changes and verifies active identity", ensured && (bool)result["showConfigurationCalled"] && (bool)result["showConfigurationReturned"] && session.ShowCalls == 1 && session.ActiveName == "DefaultSM-FLAT-PATTERN");

            session.ActiveName = "Default"; session.ShowReturn = false; session.ShowAfterName = null;
            result = new Dictionary<string, object>(); ensured = (bool)Call("EnsureConfiguration", session.Context, model, "DefaultSM-FLAT-PATTERN", result, "offline/false-stays-old");
            Pass("false switch that stays old is rejected", !ensured && session.ActiveName == "Default" && ((IList)Get(session.Context, "Errors")).Count > 0);

            session.ActiveName = "Default"; session.ShowReturn = false; session.ShowAfterName = "DefaultSM-FLAT-PATTERN";
            result = new Dictionary<string, object>(); ensured = (bool)Call("EnsureConfiguration", session.Context, model, "DefaultSM-FLAT-PATTERN", result, "offline/false-reaches-target");
            Pass("false switch is rejected even when active state reaches target", !ensured && session.ActiveName == "DefaultSM-FLAT-PATTERN");

            session.ActiveName = "Default"; session.ShowReturn = true; session.ShowAfterName = "Unexpected";
            result = new Dictionary<string, object>(); ensured = (bool)Call("EnsureConfiguration", session.Context, model, "DefaultSM-FLAT-PATTERN", result, "offline/true-wrong-state");
            Pass("true return with wrong active state is rejected", !ensured && (string)result["activeConfigurationAfterActivation"] == "Unexpected");

            session = new ManagedSession(); session.Allow(target); model = session.Model(target, "target", true); session.Documents.Add(model); Call("EnrollDocuments", session.Context); session.Documents.Clear(); session.Documents.Add(session.Model(target, "replacement", true));
            Rejected("configuration identity drift is rejected", delegate { Call("EnsureConfiguration", session.Context, model, "Default", new Dictionary<string, object>(), "offline/identity-drift"); }, "WEIGHT_CONFIGURATION_DOCUMENT_IDENTITY_NOT_LIVE");
            Pass("identity drift never invokes configuration switch", session.ShowCalls == 0);

            session = new ManagedSession(); session.Allow(target); model = session.Model(target, "target", true); session.Documents.Add(model); Call("EnrollDocuments", session.Context); session.MissingConfiguration = "DefaultSM-FLAT-PATTERN";
            Rejected("missing target configuration is rejected", delegate { Call("EnsureConfiguration", session.Context, model, "DefaultSM-FLAT-PATTERN", new Dictionary<string, object>(), "offline/missing"); }, "WEIGHT_TARGET_CONFIGURATION_UNAVAILABLE");
        }
        finally { VariableTrial.AllowManagedFixture = false; }
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new InvalidOperationException("UNIQUE_OFFLINE_TEMP_DIRECTORY_REQUIRED");
            string fixture = Path.GetFullPath(args[0]); Directory.CreateDirectory(fixture);
            Pass("relative path normalizes separators", (string)Call("Relative", "T3\\case1\\part.fixture") == "T3/case1/part.fixture");
            foreach (string value in new[] { "../part", "T3/../part", "T3//part", "T3/./part", "T3/case1/part.", "T3/case1/part ", "T3/case1/a:stream", "D:\\part", "\\\\server\\part" })
                Rejected("reject relative escape " + value, delegate { Call("Relative", value); }, "WEIGHT_RELATIVE_PATH");
            string copy = Path.Combine(fixture, "copy", "T3", "case1"), original = Path.Combine(fixture, "source", "T3", "case1"), other = Path.Combine(fixture, "source", "T4", "case1");
            Directory.CreateDirectory(copy); Directory.CreateDirectory(original); Directory.CreateDirectory(other);
            string expected = Path.Combine(copy, "same.fixture"); File.WriteAllText(expected, "throwaway offline text fixture; not a CAD model");
            File.WriteAllText(Path.Combine(original, "same.fixture"), "throwaway offline text fixture; not a CAD model");
            File.WriteAllText(Path.Combine(other, "same.fixture"), "throwaway offline text fixture; not a CAD model");
            object space = Space(copy, original); var inventory = new HashSet<string>(new[] { expected }, StringComparer.OrdinalIgnoreCase);
            Pass("exact original namespace maps by relative path", (string)Call("MapDependency", space, Path.Combine(original, "same.fixture"), inventory) == expected);
            Pass("already copied dependency stays exact", (string)Call("MapDependency", space, expected, inventory) == expected);
            Rejected("same filename in another origin is rejected", delegate { Call("MapDependency", space, Path.Combine(other, "same.fixture"), inventory); }, "WEIGHT_EXTERNAL_OR_CROSS_NAMESPACE_DEPENDENCY");
            Rejected("unknown dependency is not inferred by basename", delegate { Call("MapDependency", space, Path.Combine(original, "missing.fixture"), inventory); }, "WEIGHT_DEPENDENCY_NOT_IN_EXACT_COPY");
            Rejected("relative dependency is rejected", delegate { Call("MapDependency", space, "same.fixture", inventory); }, "WEIGHT_ABSOLUTE_PATH_REQUIRED");
            Pass("sibling prefix cannot pass containment", !(bool)Call("Under", copy + "-other\\same.fixture", copy));
            var before = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { expected, "A" } };
            Call("SameHashes", before, new Dictionary<string, string>(before, StringComparer.OrdinalIgnoreCase), "same"); Passed++; Console.WriteLine("PASS unchanged inventory is accepted");
            Rejected("changed hash is rejected", delegate { Call("SameHashes", before, new Dictionary<string, string> { { expected, "B" } }, "changed"); }, "WEIGHT_INPUT_HASH_CHANGED");
            Rejected("missing dependency hash is rejected", delegate { Call("SameHashes", before, new Dictionary<string, string>(), "missing"); }, "WEIGHT_INPUT_HASH_CHANGED");
            var extra = new Dictionary<string, string>(before, StringComparer.OrdinalIgnoreCase); extra.Add(expected + ".extra", "A");
            Rejected("additional file is rejected", delegate { Call("SameHashes", before, extra, "extra"); }, "WEIGHT_INPUT_HASH_CHANGED");
            Pass("milligram converts to kg", (double)Call("KilogramsPerUnit", 1) == 0.000001);
            Pass("gram converts to kg", (double)Call("KilogramsPerUnit", 2) == 0.001);
            Pass("kilogram converts to kg", (double)Call("KilogramsPerUnit", 3) == 1.0);
            Pass("pound converts to kg", (double)Call("KilogramsPerUnit", 4) == 0.45359237);
            Pass("unknown mass unit remains null", Call("KilogramsPerUnit", -1) == null);
            ManagedOwnershipTests(fixture);
            ManagedPropertyTests(fixture);
            ManagedConfigurationTests(fixture);
            var report = (Dictionary<string, object>)WeightReadback.Run(null, fixture, new Dictionary<string, object>());
            Pass("missing manifest fails before native runtime", !(bool)report["success"] && !(bool)report["readbackComplete"] && report.ContainsKey("fatalError"));
            Pass("diagnostic cannot approve production", !(bool)report["production_release"] && !(bool)report["fullPlanPass"] && !(bool)report["acceptancePass"] && !(bool)report["stageAcceptance"]);
            Console.WriteLine("OFFLINE_TESTS_PASS " + Passed); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
