using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

namespace SolidWorks.Interop.sldworks
{
    internal interface ISldWorks
    {
        object GetOpenDocumentByName(string path);
        object OpenDoc6(string path, int type, int options, string configuration, ref int errors, ref int warnings);
        object GetDocuments();
        bool CloseAllDocuments(bool includeUnsaved);
    }

    internal sealed class FakeSw : ISldWorks
    {
        internal ModelDoc2 Loaded, Opened;
        internal int Errors, Warnings, LookupCalls, OpenCalls, OpenType, OpenOptions;
        internal string LookupPath, OpenPath, Configuration;
        internal ModelDoc2[] Documents = new ModelDoc2[0];
        internal int DocumentQueries, CloseCalls;
        internal bool CloseResult = true, ClearOnClose = true, IncludeUnsaved;
        internal Exception CloseException;
        public object GetDocuments() { DocumentQueries++; return Documents; }
        public bool CloseAllDocuments(bool includeUnsaved)
        {
            CloseCalls++; IncludeUnsaved = includeUnsaved;
            if (CloseException != null) throw CloseException;
            if (ClearOnClose) { Documents = new ModelDoc2[0]; Loaded = null; }
            return CloseResult;
        }
        public object GetOpenDocumentByName(string path) { LookupCalls++; LookupPath = path; return Loaded; }
        public object OpenDoc6(string path, int type, int options, string configuration, ref int errors, ref int warnings)
        {
            OpenCalls++; OpenPath = path; OpenType = type; OpenOptions = options; Configuration = configuration;
            errors = Errors; warnings = Warnings; return Opened;
        }
    }

    internal class ModelDoc2
    {
        internal string PathName;
        internal bool ReadOnly, RebuildResult = true, SaveResult = true;
        internal bool? ReadOnlyAfterRebuild;
        internal int ReadOnlyCalls, RebuildCalls, SaveCalls, SaveErrors, SaveWarnings, SaveOptions;
        internal bool RebuildTopOnly;
        internal Exception SaveException;
        internal Feature First;
        internal readonly List<string> Calls = new List<string>();
        internal string GetPathName() { return PathName; }
        internal bool IsOpenedReadOnly() { ReadOnlyCalls++; Calls.Add("readonly"); return ReadOnly; }
        internal bool ForceRebuild3(bool topOnly)
        {
            RebuildCalls++; Calls.Add("rebuild"); RebuildTopOnly = topOnly;
            if (ReadOnlyAfterRebuild.HasValue) ReadOnly = ReadOnlyAfterRebuild.Value;
            return RebuildResult;
        }
        internal object FirstFeature() { return First; }
        internal bool Save3(int options, ref int errors, ref int warnings)
        {
            SaveCalls++; Calls.Add("save"); SaveOptions = options;
            if (SaveException != null) throw SaveException;
            errors = SaveErrors; warnings = SaveWarnings; return SaveResult;
        }
    }

    internal sealed class AssemblyDoc : ModelDoc2
    {
        internal object[] Components = new object[0];
        internal object GetComponents(bool topOnly) { return Components; }
    }

    internal sealed class Component2
    {
        internal ModelDoc2 Document;
        internal object GetModelDoc2() { return Document; }
    }

    internal sealed class Feature
    {
        internal string Name = "test-feature";
        internal int Error;
        internal bool Suppressed = false;
        internal Feature Child, Next = null, NextChild = null;
        internal bool IsSuppressed() { return Suppressed; }
        internal int GetErrorCode() { return Error; }
        internal string GetTypeName2() { return "test-feature-type"; }
        internal object GetFirstSubFeature() { return Child; }
        internal object GetNextFeature() { return Next; }
        internal object GetNextSubFeature() { return NextChild; }
    }
}

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        /*__STATE_FIELDS__*/

        /*__OPEN__*/

        /*__SAVE__*/

        /*__CLOSE_DEPENDENCIES__*/

        internal static void CloseDependenciesForTests(ISldWorks sw, string boundary)
        {
            CloseReadOnlyInspectionDocuments(sw, boundary);
        }

        internal static void ResetForTests(bool inspection)
        {
            ReadOnlyInspection = inspection;
            DocumentStates.Clear();
        }
    }

    internal static class ParametricContext
    {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        internal static string AttemptDirectory, Phase;
        internal static readonly List<string> MutationPaths = new List<string>();
        internal static readonly List<CapturedWrite> Writes = new List<CapturedWrite>();

        internal sealed class CapturedWrite { internal string Path, Json; }

        internal static void Reset()
        {
            AttemptDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "synthetic-document-gate-root");
            Phase = "document-state-tests";
            MutationPaths.Clear(); Writes.Clear();
        }

        internal static void AssertMutation(string path)
        {
            CheckPath(path, AttemptDirectory);
            MutationPaths.Add(path);
        }

        internal static void Write(string path, object value)
        {
            AssertMutation(path);
            Writes.Add(new CapturedWrite { Path = path, Json = Json.Serialize(value) });
        }

        /*__REQUIRE__*/

        /*__CONTEXT_PATH__*/
    }

    internal sealed class CaseResult
    {
        public string name, status, expectedFailure, observedFailure, error;
        public readonly List<string> checks = new List<string>();
    }

    internal static class DocumentStateTests
    {
        private static readonly List<CaseResult> Results = new List<CaseResult>();
        private static CaseResult Current;
        private static int Passed, Failed;

        private static string DocumentPath { get { return Path.Combine(ParametricContext.AttemptDirectory, "native_cad", "working_pack", "test-part.SLDPRT"); } }
        private static ModelDoc2 Model(bool readOnly) { return new ModelDoc2 { PathName = DocumentPath, ReadOnly = readOnly }; }
        private static FakeSw Sw(bool readOnly) { return new FakeSw { Opened = Model(readOnly) }; }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("TEST_ASSERTION_FAILED " + description);
            Current.checks.Add(description);
        }

        private static void Gate(Action action, string prefix)
        {
            Current.expectedFailure = prefix;
            try { action(); }
            catch (InvalidOperationException error)
            {
                Current.observedFailure = error.Message;
                Check(error.Message.StartsWith(prefix + " ", StringComparison.Ordinal) || error.Message == prefix, "failed at " + prefix);
                return;
            }
            throw new InvalidOperationException("TEST_EXPECTED_GATE_NOT_RAISED " + prefix);
        }

        private static void Run(string name, bool inspection, Action action)
        {
            Current = new CaseResult { name = name };
            Results.Add(Current);
            ParametricContext.Reset(); NativeParametricAssembly.ResetForTests(inspection);
            try { action(); Current.status = "PASS"; Passed++; }
            catch (Exception error) { Current.status = "FAIL"; Current.error = error.ToString(); Failed++; }
        }

        private static void State(bool requested, bool? loaded, bool? actual, int errors, int warnings, bool inspection)
        {
            Check(ParametricContext.Writes.Count == 1, "one document-state evidence write");
            Check(ParametricContext.Writes[0].Path == Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".document-state.json"), "evidence destination remains phase-bound");
            object[] rows = (object[])ParametricContext.Json.DeserializeObject(ParametricContext.Writes[0].Json);
            Check(rows.Length == 1, "one open state recorded");
            Dictionary<string, object> state = (Dictionary<string, object>)rows[0];
            Check((string)state["operation"] == "open" && (string)state["path"] == DocumentPath, "evidence records operation and requested path");
            Check((bool)state["requestedReadOnly"] == requested && object.Equals(state["loadedReadOnly"], loaded) && object.Equals(state["actualReadOnly"], actual), "evidence distinguishes requested, loaded and actual modes");
            Check((int)state["errors"] == errors && (int)state["warnings"] == warnings && (bool)state["isolatedReadOnlyInspection"] == inspection, "evidence records result codes and isolation mode");
        }

        private static void SaveFailure(string name, bool result, int errors, int warnings)
        {
            Run(name, false, delegate {
                ModelDoc2 model = Model(false); model.SaveResult = result; model.SaveErrors = errors; model.SaveWarnings = warnings;
                Gate(delegate { NativeParametricAssembly.Save(model); }, "SAVE_FAILED");
                Check(model.RebuildCalls == 1 && model.SaveCalls == 1, "save failure follows one rebuild and one Save3 call");
                Check(Current.observedFailure.EndsWith(errors + "/" + warnings, StringComparison.Ordinal), "failure preserves native save errors and warnings");
            });
        }

        private static void Boundary(string boundary, ModelDoc2[] before, bool closed, int afterCount)
        {
            Check(ParametricContext.Writes.Count == 1, "one dependency-boundary evidence write");
            Check(ParametricContext.Writes[0].Path == Path.Combine(ParametricContext.AttemptDirectory, "evidence", ParametricContext.Phase + ".document-state.json"), "dependency evidence uses the current phase destination");
            object[] rows = (object[])ParametricContext.Json.DeserializeObject(ParametricContext.Writes[0].Json);
            Check(rows.Length == 1, "one dependency boundary recorded");
            Dictionary<string, object> row = (Dictionary<string, object>)rows[0];
            Check((string)row["operation"] == "readonly_dependency_boundary" && (string)row["boundary"] == boundary && (bool)row["isolatedReadOnlyInspection"], "boundary name and strict mode recorded");
            Check((bool)row["closeSucceeded"] == closed && (int)row["afterDocumentCount"] == afterCount, "actual close result and remaining-document count recorded");
            object[] documents = (object[])row["before"];
            Check(documents.Length == before.Length, "all pre-close documents recorded");
            for (int index = 0; index < before.Length; index++) {
                Dictionary<string, object> document = (Dictionary<string, object>)documents[index];
                Check((string)document["path"] == before[index].PathName && (bool)document["readOnly"] == before[index].ReadOnly, "pre-close document path and actual mode recorded at index " + index);
            }
        }

        private static void DependencyCloseTests()
        {
            Run("dependency_cleanup_clears_loaded_writable_reference_before_fresh_readonly_open", true, delegate {
                ModelDoc2 dependency = Model(false), readonlyDocument = Model(true), fresh = Model(true);
                readonlyDocument.PathName = Path.Combine(ParametricContext.AttemptDirectory, "other.SLDPRT");
                ModelDoc2[] before = new[] { dependency, readonlyDocument };
                FakeSw sw = new FakeSw { Loaded = dependency, Opened = fresh, Documents = before };
                NativeParametricAssembly.CloseDependenciesForTests(sw, "before-independent-part");
                Check(sw.CloseCalls == 1 && sw.IncludeUnsaved && sw.DocumentQueries == 2 && sw.Documents.Length == 0, "cleanup closes all dependencies once and verifies an empty document set");
                Boundary("before-independent-part", before, true, 0);
                Check(object.ReferenceEquals(NativeParametricAssembly.Open(sw, DocumentPath, true), fresh) && !object.ReferenceEquals(fresh, dependency), "fresh readonly model replaces the previous writable reference");
                Check(sw.Loaded == null && !dependency.ReadOnly && fresh.ReadOnly, "cleanup removes the cached document instead of changing its readonly flag");
                object[] rows = (object[])ParametricContext.Json.DeserializeObject(ParametricContext.Writes[1].Json);
                Dictionary<string, object> opened = (Dictionary<string, object>)rows[1];
                Check(rows.Length == 2 && opened["loadedReadOnly"] == null && (bool)opened["actualReadOnly"] && (bool)opened["requestedReadOnly"], "reopen evidence records no loaded reuse and actual readonly success");
                Check(dependency.SaveCalls == 0 && readonlyDocument.SaveCalls == 0 && fresh.SaveCalls == 0 && dependency.RebuildCalls == 0 && readonlyDocument.RebuildCalls == 0, "dependency cleanup never rebuilds or saves the existing models");
            });
            Run("dependency_cleanup_true_with_remaining_document_stops_before_reopen", true, delegate {
                ModelDoc2 dependency = Model(false); ModelDoc2[] before = new[] { dependency };
                FakeSw sw = new FakeSw { Documents = before, ClearOnClose = false, Opened = Model(true) };
                Gate(delegate { NativeParametricAssembly.CloseDependenciesForTests(sw, "remaining-document"); NativeParametricAssembly.Open(sw, DocumentPath, true); }, "READONLY_DEPENDENCY_CLOSE_INCOMPLETE");
                Check(sw.CloseCalls == 1 && sw.OpenCalls == 0 && dependency.SaveCalls == 0, "remaining document prevents all subsequent native opens without saving");
                Boundary("remaining-document", before, true, 1);
            });
            Run("dependency_cleanup_false_even_with_zero_remaining_stops_before_reopen", true, delegate {
                ModelDoc2 dependency = Model(false); ModelDoc2[] before = new[] { dependency };
                FakeSw sw = new FakeSw { Documents = before, CloseResult = false, Opened = Model(true) };
                Gate(delegate { NativeParametricAssembly.CloseDependenciesForTests(sw, "false-close"); NativeParametricAssembly.Open(sw, DocumentPath, true); }, "READONLY_DEPENDENCY_CLOSE_INCOMPLETE");
                Check(sw.Documents.Length == 0 && sw.OpenCalls == 0 && dependency.SaveCalls == 0, "a false native close result cannot be hidden by an empty document set");
                Boundary("false-close", before, false, 0);
            });
            Run("dependency_cleanup_false_with_remaining_document_stops_before_reopen", true, delegate {
                ModelDoc2 dependency = Model(false); ModelDoc2[] before = new[] { dependency };
                FakeSw sw = new FakeSw { Documents = before, CloseResult = false, ClearOnClose = false, Opened = Model(true) };
                Gate(delegate { NativeParametricAssembly.CloseDependenciesForTests(sw, "false-and-remaining"); NativeParametricAssembly.Open(sw, DocumentPath, true); }, "READONLY_DEPENDENCY_CLOSE_INCOMPLETE");
                Check(sw.CloseCalls == 1 && sw.OpenCalls == 0 && dependency.SaveCalls == 0, "false close plus retained documents prevents further work");
                Boundary("false-and-remaining", before, false, 1);
            });
            Run("dependency_cleanup_outside_strict_inspection_refuses_before_any_native_call", false, delegate {
                ModelDoc2 dependency = Model(false); FakeSw sw = new FakeSw { Documents = new[] { dependency } };
                Gate(delegate { NativeParametricAssembly.CloseDependenciesForTests(sw, "write-phase"); }, "DEPENDENCY_CLOSE_REQUIRES_READONLY_INSPECTION");
                Check(sw.DocumentQueries == 0 && sw.CloseCalls == 0 && dependency.ReadOnlyCalls == 0 && dependency.SaveCalls == 0 && ParametricContext.Writes.Count == 0, "write-phase rejection prevents enumeration, close and evidence mutation");
            });
            Run("dependency_cleanup_external_document_path_refuses_before_close", true, delegate {
                ModelDoc2 safe = Model(false), external = Model(false);
                external.PathName = Path.Combine(ParametricContext.AttemptDirectory, "..", "outside.SLDPRT");
                FakeSw sw = new FakeSw { Documents = new[] { safe, external } };
                Gate(delegate { NativeParametricAssembly.CloseDependenciesForTests(sw, "external-document"); }, "PARAMETRIC_PATH_ESCAPE");
                Check(sw.DocumentQueries == 1 && sw.CloseCalls == 0 && sw.Documents.Length == 2, "every document path is guarded before the first close");
                Check(safe.SaveCalls == 0 && external.SaveCalls == 0 && ParametricContext.Writes.Count == 0, "out-of-scope model is preserved without save or successful boundary evidence");
            });
            Run("dependency_cleanup_empty_array_skips_native_close", true, delegate {
                FakeSw sw = new FakeSw();
                NativeParametricAssembly.CloseDependenciesForTests(sw, "empty-array");
                Check(sw.DocumentQueries == 2 && sw.CloseCalls == 0 && sw.OpenCalls == 0, "zero documents need no close or reopen");
                Boundary("empty-array", new ModelDoc2[0], true, 0);
            });
            Run("dependency_cleanup_null_document_collection_skips_native_close", true, delegate {
                FakeSw sw = new FakeSw { Documents = null };
                NativeParametricAssembly.CloseDependenciesForTests(sw, "empty-null");
                Check(sw.DocumentQueries == 2 && sw.CloseCalls == 0, "native null collection is handled as no documents");
                Boundary("empty-null", new ModelDoc2[0], true, 0);
            });
            Run("dependency_cleanup_native_close_exception_propagates_without_reopen", true, delegate {
                ModelDoc2 dependency = Model(false); FakeSw sw = new FakeSw { Documents = new[] { dependency }, CloseException = new InvalidOperationException("TEST_CLOSE_EXCEPTION"), Opened = Model(true) };
                Gate(delegate { NativeParametricAssembly.CloseDependenciesForTests(sw, "close-exception"); NativeParametricAssembly.Open(sw, DocumentPath, true); }, "TEST_CLOSE_EXCEPTION");
                Check(sw.CloseCalls == 1 && sw.OpenCalls == 0 && dependency.SaveCalls == 0, "native close exception remains a failure without reopen or save");
            });
            Run("dependency_cleanup_success_does_not_relax_actual_readonly_reopen_gate", true, delegate {
                ModelDoc2 dependency = Model(false); FakeSw sw = new FakeSw { Loaded = dependency, Documents = new[] { dependency }, Opened = Model(false) };
                NativeParametricAssembly.CloseDependenciesForTests(sw, "before-bad-reopen");
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, true); }, "READONLY_INSPECTION_ACTUAL_WRITABLE");
                Check(sw.CloseCalls == 1 && sw.OpenCalls == 1 && sw.Opened.SaveCalls == 0 && dependency.SaveCalls == 0, "error-zero writable native result still fails after successful dependency cleanup");
            });
        }

        private static int Main()
        {
            Run("writable_open_new_document", false, delegate {
                FakeSw sw = Sw(false);
                Check(object.ReferenceEquals(NativeParametricAssembly.Open(sw, DocumentPath, false), sw.Opened), "returns actual opened document");
                Check(sw.LookupCalls == 1 && sw.OpenCalls == 1 && sw.OpenType == 1 && sw.OpenOptions == 1 && sw.Configuration == "", "uses part type, silent writable open and one loaded-document query");
                Check(sw.LookupPath == DocumentPath && sw.OpenPath == DocumentPath, "queries and opens the exact requested path");
                State(false, null, false, 0, 0, false);
            });
            Run("writable_open_reuses_loaded_writable_document", false, delegate {
                FakeSw sw = Sw(false); sw.Loaded = sw.Opened;
                Check(object.ReferenceEquals(NativeParametricAssembly.Open(sw, DocumentPath, false), sw.Opened), "loaded writable document is accepted");
                State(false, false, false, 0, 0, false);
            });
            Run("writable_open_rejects_loaded_readonly_before_OpenDoc6", false, delegate {
                FakeSw sw = Sw(false); sw.Loaded = Model(true);
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "WRITABLE_OPEN_REUSES_READONLY_DOCUMENT");
                Check(sw.LookupCalls == 1 && sw.OpenCalls == 0, "loaded readonly rejection prevents OpenDoc6");
                Check(ParametricContext.Writes.Count == 0 && sw.Opened.ReadOnlyCalls == 0, "rejection occurs before new-document inspection and evidence write");
            });
            Run("errors_zero_but_actual_readonly_rejects_writable_open", false, delegate {
                FakeSw sw = Sw(true);
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "WRITABLE_OPEN_ACTUAL_READONLY");
                Check(sw.OpenCalls == 1 && sw.Opened.ReadOnlyCalls == 1, "queries actual mode after error-zero OpenDoc6");
                State(false, null, true, 0, 0, false);
            });
            Run("writable_open_readonly_result_with_warning128_still_rejected", false, delegate {
                FakeSw sw = Sw(true); sw.Warnings = 128;
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "WRITABLE_OPEN_ACTUAL_READONLY");
                State(false, null, true, 0, 128, false);
            });
            Run("isolated_inspection_blocks_writable_open_before_any_native_call", true, delegate {
                FakeSw sw = Sw(false);
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "WRITABLE_OPEN_DURING_READONLY_INSPECTION");
                Check(sw.LookupCalls == 0 && sw.OpenCalls == 0 && ParametricContext.Writes.Count == 0, "isolation gate runs before native calls and evidence mutation");
            });
            Run("isolated_inspection_requires_actual_readonly_with_no_warning", true, delegate {
                FakeSw sw = Sw(false);
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, true); }, "READONLY_INSPECTION_ACTUAL_WRITABLE");
                Check(sw.OpenOptions == 3, "readonly bit is requested");
                State(true, null, false, 0, 0, true);
            });
            Run("isolated_inspection_accepts_actual_readonly", true, delegate {
                FakeSw sw = Sw(true);
                Check(object.ReferenceEquals(NativeParametricAssembly.Open(sw, DocumentPath, true), sw.Opened), "isolated readonly open succeeds");
                State(true, null, true, 0, 0, true);
            });
            Run("isolated_inspection_accepts_actual_readonly_warning128", true, delegate {
                FakeSw sw = Sw(true); sw.Warnings = 128;
                NativeParametricAssembly.Open(sw, DocumentPath, true);
                State(true, null, true, 0, 128, true);
            });
            Run("warning128_rejects_readonly_request_with_actual_writable_document", false, delegate {
                FakeSw sw = Sw(false); sw.Warnings = 128;
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, true); }, "OPEN_MODE_WARNING_MISMATCH");
                State(true, null, false, 0, 128, false);
            });
            Run("warning128_accepts_matching_actual_writable_document", false, delegate {
                FakeSw sw = Sw(false); sw.Warnings = 128;
                NativeParametricAssembly.Open(sw, DocumentPath, false);
                State(false, null, false, 0, 128, false);
            });
            Run("warning128_accepts_matching_loaded_readonly_document", false, delegate {
                FakeSw sw = Sw(true); sw.Loaded = sw.Opened; sw.Warnings = 128;
                NativeParametricAssembly.Open(sw, DocumentPath, true);
                State(true, true, true, 0, 128, false);
            });
            Run("mixed_session_readonly_request_records_actual_writable_reuse", false, delegate {
                FakeSw sw = Sw(false); sw.Loaded = sw.Opened;
                Check(object.ReferenceEquals(NativeParametricAssembly.Open(sw, DocumentPath, true), sw.Opened), "existing mixed-session reuse remains accepted without warning");
                Check(sw.OpenOptions == 3, "requested readonly bit remains distinct from actual mode");
                State(true, false, false, 0, 0, false);
            });
            Run("identity_warning1_rejected_even_when_errors_and_path_are_valid", false, delegate {
                FakeSw sw = Sw(false); sw.Warnings = 1;
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "OPEN_DOCUMENT_IDENTITY_MISMATCH");
                State(false, null, false, 0, 1, false);
            });
            Run("identity_warning1_not_masked_by_warning128", true, delegate {
                FakeSw sw = Sw(true); sw.Warnings = 129;
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, true); }, "OPEN_DOCUMENT_IDENTITY_MISMATCH");
                State(true, null, true, 0, 129, true);
            });
            Run("nonzero_open_errors_preserve_failure", false, delegate {
                FakeSw sw = Sw(false); sw.Errors = 2;
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "OPEN_FAILED");
                State(false, null, false, 2, 0, false);
            });
            Run("null_open_result_preserves_failure", false, delegate {
                FakeSw sw = new FakeSw();
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "OPEN_FAILED");
                State(false, null, null, 0, 0, false);
            });
            Run("different_actual_document_path_rejected", false, delegate {
                FakeSw sw = Sw(false); sw.Opened.PathName = Path.Combine(ParametricContext.AttemptDirectory, "other-part.SLDPRT");
                Gate(delegate { NativeParametricAssembly.Open(sw, DocumentPath, false); }, "OPEN_PATH_MISMATCH");
                State(false, null, false, 0, 0, false);
            });
            Run("equivalent_normalized_case_insensitive_actual_path_accepted", false, delegate {
                FakeSw sw = Sw(false); sw.Opened.PathName = Path.Combine(Path.GetDirectoryName(DocumentPath), ".", Path.GetFileName(DocumentPath)).ToUpperInvariant();
                NativeParametricAssembly.Open(sw, DocumentPath, false);
                State(false, null, false, 0, 0, false);
            });
            Run("requested_path_escape_blocked_before_native_calls", false, delegate {
                FakeSw sw = Sw(false);
                Gate(delegate { NativeParametricAssembly.Open(sw, Path.Combine(ParametricContext.AttemptDirectory, "..", "outside.SLDPRT"), false); }, "PARAMETRIC_PATH_ESCAPE");
                Check(sw.LookupCalls == 0 && sw.OpenCalls == 0, "path guard runs before document access");
            });
            Run("assembly_extension_selects_assembly_type", false, delegate {
                string path = Path.ChangeExtension(DocumentPath, ".sLdAsM"); FakeSw sw = Sw(false); sw.Opened.PathName = path;
                NativeParametricAssembly.Open(sw, path, false);
                Check(sw.OpenType == 2 && sw.OpenPath == path, "case-insensitive assembly extension uses type 2");
            });
            Run("save_writable_rebuilds_then_saves", false, delegate {
                ModelDoc2 model = Model(false); NativeParametricAssembly.Save(model);
                Check(ParametricContext.MutationPaths.Count == 1 && ParametricContext.MutationPaths[0] == DocumentPath, "mutation authorization boundary is called for actual document path");
                Check(model.RebuildCalls == 1 && !model.RebuildTopOnly && model.SaveCalls == 1 && model.SaveOptions == 1, "full rebuild and silent save each execute once");
                Check(model.ReadOnlyCalls == 2, "actual readonly state checked before and after rebuild");
                Check(string.Join(",", model.Calls) == "readonly,rebuild,readonly,save", "mode checks bracket rebuild and precede Save3");
            });
            Run("save_actual_readonly_rejected_before_mutation_rebuild_or_save", false, delegate {
                ModelDoc2 model = Model(true);
                Gate(delegate { NativeParametricAssembly.Save(model); }, "SAVE_REQUIRES_WRITABLE_DOCUMENT");
                Check(ParametricContext.MutationPaths.Count == 0 && model.RebuildCalls == 0 && model.SaveCalls == 0, "readonly rejection has no mutation, rebuild or save");
            });
            Run("isolated_inspection_blocks_save_of_actually_writable_document", true, delegate {
                ModelDoc2 model = Model(false);
                Gate(delegate { NativeParametricAssembly.Save(model); }, "SAVE_REQUIRES_WRITABLE_DOCUMENT");
                Check(ParametricContext.MutationPaths.Count == 0 && model.ReadOnlyCalls == 0 && model.RebuildCalls == 0 && model.SaveCalls == 0, "isolation blocks save before document mutation");
            });
            Run("save_rebuild_changed_to_readonly_rejected_before_Save3", false, delegate {
                ModelDoc2 model = Model(false); model.ReadOnlyAfterRebuild = true;
                Gate(delegate { NativeParametricAssembly.Save(model); }, "SAVE_REBUILD_CHANGED_READONLY_STATE");
                Check(model.RebuildCalls == 1 && model.ReadOnlyCalls == 2 && model.SaveCalls == 0, "post-rebuild actual mode prevents Save3");
            });
            Run("failed_rebuild_preserves_diagnostics_and_prevents_Save3", false, delegate {
                ModelDoc2 child = Model(false); child.First = new Feature { Name = "component-error", Error = 3 };
                AssemblyDoc model = new AssemblyDoc { PathName = DocumentPath, RebuildResult = false, First = new Feature { Name = "root-error", Error = 1, Child = new Feature { Name = "child-error", Error = 2 } }, Components = new object[] { new Component2 { Document = child }, new Component2 { Document = child } } };
                TextWriter original = Console.Out; StringWriter diagnostic = new StringWriter();
                try { Console.SetOut(diagnostic); Gate(delegate { NativeParametricAssembly.Save(model); }, "REBUILD_FAILED"); }
                finally { Console.SetOut(original); }
                Check(model.RebuildCalls == 1 && model.SaveCalls == 0, "failed rebuild does not call Save3");
                string text = diagnostic.ToString();
                Check(text.Contains("REBUILD_FAILURE_FEATURES") && text.Contains("root-error") && text.Contains("child-error") && text.Contains("component-error"), "production failure branch retains root, child and component diagnostics");
            });
            SaveFailure("Save3_false_remains_failure_even_with_zero_codes", false, 0, 0);
            SaveFailure("Save3_true_with_error_remains_failure", true, 4, 0);
            SaveFailure("Save3_true_with_warning_remains_failure", true, 0, 8);
            Run("Save3_exception_propagates_without_success", false, delegate {
                ModelDoc2 model = Model(false); model.SaveException = new InvalidOperationException("TEST_SAVE3_EXCEPTION");
                Gate(delegate { NativeParametricAssembly.Save(model); }, "TEST_SAVE3_EXCEPTION");
                Check(model.RebuildCalls == 1 && model.SaveCalls == 1, "native save exception is preserved after one Save3 attempt");
            });
            Run("save_path_escape_rejected_before_rebuild_or_Save3", false, delegate {
                ModelDoc2 model = Model(false); model.PathName = Path.Combine(ParametricContext.AttemptDirectory, "..", "outside.SLDPRT");
                Gate(delegate { NativeParametricAssembly.Save(model); }, "PARAMETRIC_PATH_ESCAPE");
                Check(model.RebuildCalls == 0 && model.SaveCalls == 0, "actual document path is guarded before mutation");
            });
            DependencyCloseTests();
            Console.WriteLine("DOCUMENT_GATE_TEST_RESULT " + ParametricContext.Json.Serialize(new { passed = Passed, failed = Failed, cases = Results }));
            return Failed == 0 ? 0 : 1;
        }
    }
}
