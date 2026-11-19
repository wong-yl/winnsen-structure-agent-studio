using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

// Throwaway, isolated native variable trial. This unit never saves its input CAD files.
internal static partial class TrialAssembly
{
    const string TrialRoot = @"D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112";
    const double MmTolerance = .001;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly double[] Identity = {1,0,0,0,1,0,0,0,1};
    static readonly double[] FlipXZ = {-1,0,0,0,1,0,0,0,-1};
    static readonly double[] FlipYZ = {1,0,0,0,-1,0,0,0,-1};
    static readonly double[] BottomLatch = {-1,0,0,0,0,-1,0,-1,0};
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 120 };

    static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    static void ReleaseItems(object value) { if (value is IEnumerable && !(value is string)) foreach (object item in (IEnumerable)value) Release(item); Release(value); }
    static object[] Items(object value) { return value == null ? new object[0] : ((IEnumerable)value).Cast<object>().ToArray(); }
    static double[] Numbers(object value) { return Items(value).Select(v => Convert.ToDouble(v, Invariant)).ToArray(); }
    static Dictionary<string, object> Map(object value) { var map = value as Dictionary<string, object>; Need(map != null, "LAYOUT_OBJECT_REQUIRED"); return map; }
    static double Number(Dictionary<string, object> value, string name) { double result = Convert.ToDouble(value[name], Invariant); Need(!Double.IsNaN(result) && !Double.IsInfinity(result), "FINITE_NUMBER_REQUIRED " + name); return result; }
    static bool Near(double a, double b) { return Math.Abs(a-b) <= MmTolerance; }
    static string Token(double value) { return value.ToString("R", Invariant).Replace('.', 'p').Replace('-', 'm').Replace('+', '_'); }
    static string Hash(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    static string Within(string path, string root)
    {
        string full = Path.GetFullPath(path), parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        Need(full.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "ASSEMBLY_SCOPE_VIOLATION " + full);
        for (string item=full; !String.IsNullOrEmpty(item); item=Path.GetDirectoryName(item))
            if (File.Exists(item) || Directory.Exists(item)) Need((File.GetAttributes(item)&FileAttributes.ReparsePoint)==0, "ASSEMBLY_REPARSE_PATH " + item);
        return full;
    }

    sealed class Placement
    {
        internal string Role, Path, Side, Kind, ActualName, OriginalName, ReopenedName;
        internal int Index;
        internal double[] Transform;
        internal Placement(string role, string path, string side, int index, string kind, double[] rotation, double x, double y, double z)
        {
            Role=role; Path=path; Side=side; Index=index; Kind=kind;
            Transform=rotation.Concat(new[]{x/1000,y/1000,z/1000,1.0,0,0,0}).ToArray();
        }
    }
    sealed class WorldBody
    {
        internal Body2 Body;
        internal string Component, Kind, Side, Name, Path;
        internal int DoorIndex;
        internal double[] Box;
        internal List<Cylinder> Cylinders = new List<Cylinder>();
        internal List<NativePlane> Planes = new List<NativePlane>();
    }
    sealed class Cylinder
    {
        internal string Identity, SourceFaceIdentity;
        internal bool ReusedTopology;
        internal int NativeFaceId, BoundaryEdgeCount, SeamEdgeCount;
        internal bool Reversed, CompleteFiniteFace;
        internal double[] Data, Box, UvBounds, SurfaceNormal, FaceNormal, AxialIntervalMm;
        internal double AreaMm2, RadialNormalDot;
        internal readonly List<BoundaryCircle> Circles = new List<BoundaryCircle>();
        internal readonly List<string> Errors = new List<string>();
        internal double[] PointMm { get { return Data.Take(3).Select(v=>v*1000).ToArray(); } }
        internal double[] Axis { get { return Unit(Data.Skip(3).Take(3).ToArray()); } }
        internal bool InnerWall { get { return FaceNormal!=null && RadialNormalDot<-.999999; } }
        internal object Evidence() { return new { faceIdentity=Identity,nativeFaceId=ReusedTopology?(int?)null:NativeFaceId,sourceFaceIdentity=SourceFaceIdentity,sourceNativeFaceId=ReusedTopology?(int?)NativeFaceId:null,identityScope=ReusedTopology?"derived instance identity; native face ID belongs to source transformed-body readback only":"transformed-body readback only; not a persistent CAD reference",independentNativeTopologyReadbackForThisInstance=!ReusedTopology,axisPointMm=PointMm,axisDirection=Axis,radiusMm=Data[6]*1000,faceInSurfaceSense=Reversed,surfaceNormalAtBoundary=SurfaceNormal,outwardFaceNormalAtBoundary=FaceNormal,radialNormalDot=FaceNormal==null?(double?)null:RadialNormalDot,wallClassification=InnerWall?"internal_cylindrical_wall":FaceNormal!=null&&RadialNormalDot>.999999?"external_cylindrical_wall":"unverified",completeFiniteCylinder=CompleteFiniteFace,axialIntervalFromBoundaryCircleCentresMm=AxialIntervalMm,boundaryEdges=BoundaryEdgeCount,seamEdges=SeamEdgeCount,boundaryCircles=Circles.Select(v=>v.Evidence()).ToArray(),nativeFaceAreaMm2=AreaMm2,uvBounds=UvBounds,approximateFaceBoxMm=Box,faceBoxCandidateOnly=true,errors=Errors }; }
    }
    sealed class BoundaryCircle
    {
        internal string Identity, FaceIdentity, SourceEdgeIdentity, SourceFaceIdentity;
        internal bool ReusedTopology;
        internal int NativeEdgeId, CurveTag;
        internal bool Sense, ClosedCurve, PeriodicCurve, Complete;
        internal double[] Data, StartMm, EndMm, Parameters;
        internal double EdgeLengthMm;
        internal double[] CenterMm { get { return Data.Take(3).Select(v=>v*1000).ToArray(); } }
        internal double[] Normal { get { return Unit(Data.Skip(3).Take(3).ToArray()); } }
        internal object Evidence() { return new { edgeIdentity=Identity,faceIdentity=FaceIdentity,nativeEdgeId=ReusedTopology?(int?)null:NativeEdgeId,curveTag=ReusedTopology?(int?)null:CurveTag,sourceEdgeIdentity=SourceEdgeIdentity,sourceFaceIdentity=SourceFaceIdentity,sourceNativeEdgeId=ReusedTopology?(int?)NativeEdgeId:null,sourceCurveTag=ReusedTopology?(int?)CurveTag:null,identityScope=ReusedTopology?"derived instance identity; native edge ID and curve tag belong to source transformed-body readback only":"transformed-body edge readback only; not a persistent CAD reference",independentNativeTopologyReadbackForThisInstance=!ReusedTopology,circleParams=Data,centreMm=CenterMm,normal=Normal,radiusMm=Data[6]*1000,curveAndEdgeSameSense=Sense,edgeParameterRange=Parameters,parameterSpanRadians=Math.Abs(Parameters[1]-Parameters[0]),underlyingCurveClosed=ClosedCurve,underlyingCurvePeriodic=PeriodicCurve,startPointMm=StartMm,endPointMm=EndMm,endpointDistanceMm=Length(Subtract(StartMm,EndMm)),edgeLengthMm=EdgeLengthMm,circumferenceErrorMm=EdgeLengthMm-2*Math.PI*Data[6]*1000,completeCircle=Complete,completeCircleCriterion="closed periodic native circle, one 2pi edge period, coincident endpoints and native edge length equal to circumference; 0.001 mm readback tolerance" }; }
    }
    sealed class NativePlane
    {
        internal string Identity, SourceFaceIdentity;
        internal bool ReusedTopology;
        internal int NativeFaceId;
        internal bool Reversed;
        internal double[] Data, Box;
        internal double[] PointMm { get { return Data.Skip(3).Take(3).Select(v=>v*1000).ToArray(); } }
        internal double[] Normal { get { return Unit(Data.Take(3).Select(v=>Reversed?-v:v).ToArray()); } }
        internal object Evidence() { return new {faceIdentity=Identity,nativeFaceId=ReusedTopology?(int?)null:NativeFaceId,sourceFaceIdentity=SourceFaceIdentity,sourceNativeFaceId=ReusedTopology?(int?)NativeFaceId:null,identityScope=ReusedTopology?"derived instance identity; native face ID belongs to source transformed-body readback only":"transformed-body readback only; not a persistent CAD reference",independentNativeTopologyReadbackForThisInstance=!ReusedTopology,planeParams=Data,rootPointMm=PointMm,surfaceNormal=Data.Take(3).ToArray(),outwardFaceNormal=Normal,faceInSurfaceSense=Reversed,approximateFaceBoxMm=Box,faceBoxCandidateOnly=true}; }
    }

    sealed class TopologyTemplate
    {
        internal object Source;
        internal WorldBody World;
        internal string Role, Path, Sha256, Configuration;
        internal double[] Transform;
        internal int FaceCount, EdgeCount;
        internal bool Disabled;
    }
    sealed class MetadataCacheEntry
    {
        internal object Model;
        internal string Role, Path, Referenced, ActiveBefore, ActiveAfterRead, ActiveAfterRestore, MaterialName, MaterialDatabase;
        internal string[] Configurations;
        internal List<object> ConfigurationMaterials, CustomPropertyScopes, ConfigurationRebuilds;
    }
    sealed class MeasurementPerformance
    {
        readonly System.Diagnostics.Stopwatch Clock=System.Diagnostics.Stopwatch.StartNew();
        readonly Dictionary<string,double> PhaseMilliseconds=new Dictionary<string,double>(StringComparer.Ordinal);
        readonly Dictionary<string,int> FallbackReasons=new Dictionary<string,int>(StringComparer.Ordinal);
        internal readonly List<object> BoundsCounterexamples=new List<object>();
        internal int NativeSurfaceScans, TopologyCacheHits, TopologyCacheEntries, NormalBodies;
        internal double Now { get { return Clock.Elapsed.TotalMilliseconds; } }
        internal void Add(string phase,double started) { double previous; PhaseMilliseconds.TryGetValue(phase,out previous); PhaseMilliseconds[phase]=previous+Now-started; }
        internal void Fallback(string reason) { int previous; FallbackReasons.TryGetValue(reason,out previous); FallbackReasons[reason]=previous+1; }
        internal object Evidence() { return new {elapsedMilliseconds=Now,phaseMilliseconds=PhaseMilliseconds,nativeSurfaceScans=NativeSurfaceScans,topologyCacheHits=TopologyCacheHits,topologyCacheEntries=TopologyCacheEntries,normalBodiesConfirmed=NormalBodies,cacheFallbackReasons=FallbackReasons,nativeBoundsCounterexamples=BoundsCounterexamples,cacheScope="one ReopenAndMeasure invocation only",cachePolicy="same actual file SHA256, referenced configuration, bit-identical rotation and scale, confirmed normal body, native Body2 face/edge counts, component PartDoc GetModelDoc2 ISldWorks.IsSame and retained IUnknown identity; native six-direction body bounds corroborate translation only",timingsCoverCompletedOperationsOnly=true}; }
        internal void Write(BuildContext c) { c.Evidence["measurementPerformance"]=Evidence(); double started=Now; c.Write(); Add("evidence_write",started); }
    }
    static double? CadMemoryMb(ISldWorks sw)
    {
        if (sw == null) return null;
        try { using (var process = System.Diagnostics.Process.GetProcessById(VariableTrial.SwGetProcessID(sw, "TrialAssembly.CadMemoryMb"))) return Math.Round(process.PrivateMemorySize64 / 1048576.0, 1); }
        catch (TimeoutException) { throw; }
        catch { return null; }
    }
    static void ReclaimComWrappers()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    sealed class BuildContext
    {
        internal ISldWorks Sw;
        internal string Cad, Output, EvidencePath, AssemblyPath;
        internal Dictionary<string, object> Layout;
        internal Dictionary<string, object> Evidence;
        internal Dictionary<string, string> InputHashes;
        internal readonly List<object> Events = new List<object>();
        internal readonly List<object> Parts = new List<object>();
        internal readonly List<Placement> Placements = new List<Placement>();
        internal readonly Dictionary<string, string> Hardware = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> OpenedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> ReusedOpenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal double Width, Height, Depth, DoorWidth;
        internal bool ColdSession;
        internal string CurrentStage;
        internal int? CurrentComponentIndex;
        internal string CurrentComponentRole;
        internal string CurrentComponentPath;
        internal string CurrentInsertStep;
        internal Action<Dictionary<string, object>> PreviousComTimeoutObserver;

        internal void Stage(string stage)
        {
            CurrentStage = stage;
            CurrentComponentIndex = null;
            CurrentComponentRole = null;
            CurrentComponentPath = null;
            CurrentInsertStep = null;
        }
        internal void AttachComTimeoutObserver()
        {
            PreviousComTimeoutObserver = VariableTrial.ComTimeoutObserver;
            VariableTrial.ComTimeoutObserver = delegate(Dictionary<string, object> payload)
            {
                try
                {
                    if (Evidence != null)
                    {
                        Evidence["comTimedOut"] = true;
                        Evidence["comTimeoutMethod"] = payload.ContainsKey("method") ? payload["method"] : null;
                        Evidence["comTimeoutContext"] = payload.ContainsKey("context") ? payload["context"] : null;
                        Evidence["comTimeoutMs"] = payload.ContainsKey("timeoutMs") ? payload["timeoutMs"] : null;
                        Evidence["comTimeoutElapsedMs"] = payload.ContainsKey("elapsedMs") ? payload["elapsedMs"] : null;
                        Evidence["comTimeoutWaitedMs"] = payload.ContainsKey("waitedMs") ? payload["waitedMs"] : null;
                        Evidence["comTimeoutStage"] = CurrentStage;
                        Evidence["comTimeoutComponentIndex"] = CurrentComponentIndex;
                        Evidence["comTimeoutComponentRole"] = CurrentComponentRole;
                        Evidence["comTimeoutComponentPath"] = CurrentComponentPath;
                        Evidence["comTimeoutInsertStep"] = CurrentInsertStep;
                    }
                    var timeoutEvent = new Dictionary<string, object>(payload, StringComparer.Ordinal);
                    timeoutEvent["inspection"] = "com_call_timeout";
                    timeoutEvent["stage"] = CurrentStage;
                    timeoutEvent["componentIndex"] = CurrentComponentIndex;
                    timeoutEvent["componentRole"] = CurrentComponentRole;
                    timeoutEvent["componentPath"] = CurrentComponentPath;
                    timeoutEvent["insertStep"] = CurrentInsertStep;
                    Event(timeoutEvent, true);
                }
                catch { }
                if (PreviousComTimeoutObserver != null)
                {
                    try { PreviousComTimeoutObserver(payload); }
                    catch { }
                }
            };
        }
        internal void DetachComTimeoutObserver()
        {
            VariableTrial.ComTimeoutObserver = PreviousComTimeoutObserver;
            PreviousComTimeoutObserver = null;
        }
        internal void Write()
        {
            if (EvidencePath != null) File.WriteAllText(EvidencePath, Json.Serialize(Evidence), new UTF8Encoding(false));
        }
        internal void Event(object value, bool persist = true) { Events.Add(value); if (persist) Write(); }
        internal void Scope()
        {
            foreach (object value in VariableTrial.SwGetDocuments(Sw, "BuildContext.Scope"))
            {
                var model=value as ModelDoc2;
                try { string docPath = model == null ? null : VariableTrial.ModelGetPathName(model, "BuildContext.Scope"); Need(model != null && !String.IsNullOrEmpty(docPath), "UNSAVED_OR_UNKNOWN_DOCUMENT"); Within(docPath,Cad); }
                finally { Release(value); }
            }
        }
        internal ModelDoc2 Open(string path, bool readOnly)
        {
            VariableTrial.RuntimeOwnership(); Scope();
            path=Within(path,Cad); bool previouslyOpened=OpenedPaths.Contains(path); int errors=0,warnings=0;
            bool isAsm = Path.GetExtension(path).Equals(".SLDASM",StringComparison.OrdinalIgnoreCase);
            int openTimeoutMs = isAsm ? VariableTrial.HeavyAssemblyComTimeoutMs : VariableTrial.IoRebuildComTimeoutMs;
            var model=VariableTrial.SwOpenDoc6(Sw,path,isAsm?2:1,1|(readOnly?2:0),"",out errors,out warnings,"BuildContext.Open",openTimeoutMs);
            bool alreadyOpen=warnings==(int)swFileLoadWarning_e.swFileLoadWarning_AlreadyOpen;
            bool? actualReadOnly = model==null?(bool?)null:VariableTrial.ModelIsOpenedReadOnly(model,"BuildContext.Open");
            Event(new { inspection="assembly_document_open",path=path,readOnly=readOnly,errors=errors,warnings=warnings,previouslyOpenedInContext=previouslyOpened,reusedAlreadyOpen=alreadyOpen,actualReadOnly=actualReadOnly });
            Need(model!=null && errors==0 && (warnings==0 || warnings==(int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen || alreadyOpen&&previouslyOpened&&readOnly), "ASSEMBLY_OPEN_FAILED " + path + " " + errors + "/" + warnings);
            Need(String.Equals(Path.GetFullPath(VariableTrial.ModelGetPathName(model,"BuildContext.Open")),path,StringComparison.OrdinalIgnoreCase),"ASSEMBLY_OPEN_PATH_MISMATCH");
            Need(actualReadOnly==readOnly,"ASSEMBLY_OPEN_MODE_MISMATCH " + path); Scope(); OpenedPaths.Add(path); if(alreadyOpen)ReusedOpenPaths.Add(path); return model;
        }
        internal void Close(ModelDoc2 model)
        {
            if(model==null)return;
            try
            {
                string path=Within(VariableTrial.ModelGetPathName(model,"BuildContext.Close",true),Cad);
                bool reused=ReusedOpenPaths.Remove(path);
                bool dirty=VariableTrial.ModelGetSaveFlag(model,"BuildContext.Close",true);
                Event(new {inspection="assembly_document_close",path=path,reusedAlreadyOpen=reused,closeDocument=!reused,dirty=dirty});
                if(!reused)VariableTrial.SwCloseDoc(Sw,VariableTrial.ModelGetTitle(model,"BuildContext.Close",true),"BuildContext.Close",true);
            }
            finally { Release(model); }
        }
        internal void CloseOutputDocuments()
        {
            int guard=0;
            while(true)
            {
                object[] documents=VariableTrial.SwGetDocuments(Sw,"BuildContext.CloseOutputDocuments",true);
                if(documents.Length==0) { VariableTrial.SwCloseAllDocuments(Sw,true,"BuildContext.CloseOutputDocuments",true); ReusedOpenPaths.Clear(); VariableTrial.ClearOwned(); return; }
                Need(guard++<200,"ASSEMBLY_DOCUMENT_CLOSE_INCOMPLETE"); string title=null;
                try
                {
                    foreach(object value in documents) { var model=value as ModelDoc2; Need(model!=null,"UNKNOWN_LOADED_DOCUMENT"); Within(VariableTrial.ModelGetPathName(model,"BuildContext.CloseOutputDocuments",true),Output); if(title==null)title=VariableTrial.ModelGetTitle(model,"BuildContext.CloseOutputDocuments",true); }
                    VariableTrial.SwCloseDoc(Sw,title,"BuildContext.CloseOutputDocuments",true);
                }
                finally { foreach(object value in documents)Release(value); }
            }
        }
        internal void Save(ModelDoc2 model)
        {
            VariableTrial.RuntimeOwnership(); Scope();
            string path=VariableTrial.ModelGetPathName(model,"BuildContext.Save");
            Within(path,Output); Need(!VariableTrial.ModelIsOpenedReadOnly(model,"BuildContext.Save"),"ASSEMBLY_SAVE_READONLY");
            int errors=0,warnings=0; bool saved=VariableTrial.ModelSave3(model,1,out errors,out warnings,"BuildContext.Save");
            Event(new { inspection="assembly_document_save",path=path,saved=saved,errors=errors,warnings=warnings });
            Need(saved&&errors==0&&warnings==0&&!VariableTrial.ModelGetSaveFlag(model,"BuildContext.Save"),"ASSEMBLY_SAVE_FAILED " + path);
        }
        internal void SaveNew(ModelDoc2 model,string path,bool copy)
        {
            VariableTrial.RuntimeOwnership();
            Within(path,Output); Need(!File.Exists(path),"PRESERVE_ASSEMBLY_OUTPUT " + path); VariableTrial.ModelClearSelection2(model,true,"BuildContext.SaveNew");
            int errors=0,warnings=0;
            bool saved=VariableTrial.ModelSaveAs(model,path,0,1|(copy?(int)swSaveAsOptions_e.swSaveAsOptions_Copy:0),null,out errors,out warnings,"BuildContext.SaveNew");
            Event(new { inspection="assembly_document_save_as",source=VariableTrial.ModelGetPathName(model,"BuildContext.SaveNew"),path=path,copy=copy,saved=saved,errors=errors,warnings=warnings });
            Need(saved&&errors==0&&warnings==0&&File.Exists(path),"ASSEMBLY_SAVE_AS_FAILED " + path); Scope();
        }
        internal string Source(string name)
        {
            string[] matches=Directory.GetFiles(Cad).Where(p=>Path.GetFileName(p).Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
            Need(matches.Length==1,"ASSEMBLY_SOURCE_MISSING_OR_AMBIGUOUS " + name); return matches[0];
        }
        internal string NativeCopy(string source,string leaf)
        {
            VariableTrial.RuntimeOwnership(); source=Within(source,Cad);
            string target=Within(Path.Combine(Output,"parts",leaf),Output),contextFolder=Within(Path.Combine(Output,"structure"),Output);
            Need(InputHashes.ContainsKey(source)&&File.Exists(source),"ASSEMBLY_COPY_SOURCE_REQUIRED " + source);
            Need(Path.GetExtension(source).Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase)&&Path.GetExtension(target).Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase),"ASSEMBLY_COPY_PART_REQUIRED");
            string before=Hash(source); Need(before==InputHashes[source],"ASSEMBLY_COPY_SOURCE_CHANGED " + source);
            string[] dependencies=Dependencies(this,source);
            var references=dependencies.ToDictionary(path=>path,path=>Within(Path.Combine(contextFolder,Path.GetFileName(path)),contextFolder),StringComparer.OrdinalIgnoreCase);
            foreach(var item in references)Need(InputHashes.ContainsKey(item.Key)&&File.Exists(item.Value),"ASSEMBLY_COPY_CONTEXT_MISSING " + item.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(target)); Need(!File.Exists(target),"PRESERVE_ASSEMBLY_COPY " + target);
            File.Copy(source,target,false); string byteCopyHash=Hash(target); Need(byteCopyHash==before,"ASSEMBLY_COPY_HASH_MISMATCH " + target);
            foreach(var item in references)
            {
                VariableTrial.RuntimeOwnership(); Need(VariableTrial.SwReplaceReferencedDocument(Sw,target,item.Key,item.Value,"BuildContext.NativeCopy"),"ASSEMBLY_COPY_REWIRE_FAILED " + target + " -> " + item.Key);
            }
            string[] actual=Dependencies(this,target); foreach(string dependency in actual)Within(dependency,contextFolder);
            Need(new HashSet<string>(actual,StringComparer.OrdinalIgnoreCase).SetEquals(references.Values),"ASSEMBLY_COPY_REFERENCE_SET_MISMATCH " + target);
            Need(Hash(source)==before,"ASSEMBLY_INPUT_MUTATED " + source);
            Event(new {inspection="assembly_part_byte_copy",source=source,path=target,sourceSha256=before,byteCopySha256=byteCopyHash,rewiredSha256=Hash(target),dependencyEdges=references.Select(item=>new {source=item.Key,target=item.Value}).ToArray(),sourceOpenedOrSaved=false,copyRebuildRequired=true});
            return target;
        }
    }

    public static object Build(ISldWorks sw,string cadDirectory,string outputDirectory,Dictionary<string,object> layout)
    {
        return Build(sw,cadDirectory,outputDirectory,layout,false);
    }

    public static object Build(ISldWorks sw,string cadDirectory,string outputDirectory,Dictionary<string,object> layout,bool allowWidthEnvelope)
    {
        var c=new BuildContext { Sw=sw,Cad=Path.GetFullPath(cadDirectory),Output=Path.GetFullPath(outputDirectory),Layout=layout };
        c.Evidence=new Dictionary<string,object> {
            {"schema","16029-independent-variable-minimum-assembly/v1"},{"envelopeMode",allowWidthEnvelope?"parametric_width":"pilot"},{"production_release",false},{"success",false},{"acceptancePass",false},
            {"events",c.Events},{"parts",c.Parts},{"layout",layout},
            {"unverified",new[]{"未验证：缺 [新SOLIDWORKS进程中的冷启动复核]","未验证：缺 [机械锁开合、完整门转动行程和工厂实物验收]","未验证：缺 [本次实际干涉的工程逐项批准；没有继承其他装配的干涉豁免]"}},
            {"ruleSources",new[]{
                new { path="workers/native_model_requests/parametric_v1/repairs/readonly-open-20261111-r4-rightowner1/source/NativeParametricDoor.cs",lines="3070-3179",purpose="door stiffener length and door hardware translations"},
                new { path="workers/native_model_requests/parametric_v1/repairs/readonly-open-20261111-r4-rightowner1/source/NativeParametricDoorPlacement.cs",lines="974-1044",purpose="V35 native corrected hand/orientations and dedicated top latch"},
                new { path="workers/native_model_requests/parametric_v1/repairs/readonly-open-20261111-r4-rightowner1/source/NativeParametricAssembly.cs",lines="580-615",purpose="door global X and per-column shelf/crossbar locations"},
                new { path="workers/native_model_requests/parametric_v1/repairs/readonly-open-20261111-r4-rightowner1/source/NativeParametricWidth.cs",lines="1695-1721,1790-1814",purpose="existing master width interfaces and native derived X bounds"},
                new { path="workers/native_model_requests/parametric_v1/repairs/readonly-open-20261111-r4-rightowner1/source/NativeParametricEnvelope.cs",lines="723-726",purpose="native height-grid translation"}
            }}
        };
        var memoryProfile=new Dictionary<string,object>(StringComparer.Ordinal);
        c.Evidence["memoryProfileMb"]=memoryProfile;
        try
        {
            c.AttachComTimeoutObserver();
            VariableTrial.RuntimeOwnership();
            Need(sw!=null && layout!=null,"ASSEMBLY_ARGUMENT_REQUIRED"); Within(c.Cad,TrialRoot);
            Within(c.Output,Path.Combine(c.Cad,"assemblies"));
            Need(Path.GetDirectoryName(c.Output).Equals(Path.Combine(c.Cad,"assemblies"),StringComparison.OrdinalIgnoreCase),"ASSEMBLY_CASE_DIRECTORY_REQUIRED");
            Need(!Directory.Exists(c.Output)&&!File.Exists(c.Output),"PRESERVE_EXISTING_ASSEMBLY_CASE " + c.Output);
            Need(VariableTrial.SwGetDocuments(sw,"TrialAssembly.Build").Length==0,"ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
            Directory.CreateDirectory(c.Output); c.EvidencePath=Path.Combine(c.Output,"assembly-evidence.json");
            c.AssemblyPath=Path.Combine(c.Output,"16029_variable_minimum.SLDASM");
            c.Evidence["assemblyPath"]=c.AssemblyPath; c.Evidence["outputDirectory"]=c.Output;
            var cabinet=Map(layout["cabinet"]); c.Width=Number(cabinet,"widthMm"); c.Height=Number(cabinet,"heightMm"); c.Depth=Number(cabinet,"depthMm"); c.DoorWidth=Number(layout,"doorLeafWidthMm");
            ValidateCabinetEnvelope(c.Width,c.Height,c.Depth,c.DoorWidth,allowWidthEnvelope);
            ValidateLayout(c);
            c.InputHashes=Directory.GetFiles(c.Cad).Where(p=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(p).ToUpperInvariant())).ToDictionary(p=>p,Hash,StringComparer.OrdinalIgnoreCase);
            Need(c.InputHashes.Count==60,"ASSEMBLY_EXPECTS_COMPLETE_60_FILE_INPUT"); double? beforeMem=CadMemoryMb(c.Sw); c.Evidence["cadPrivateMemoryBeforeMb"]=beforeMem; memoryProfile["beforeBuild"]=beforeMem; c.Write();
            c.Stage("PrepareStructure");
            var structure=PrepareStructure(c);
            memoryProfile["afterPrepareStructure"]=CadMemoryMb(c.Sw);
            c.Stage("PrepareHardware");
            PrepareHardware(c);
            memoryProfile["afterPrepareHardware"]=CadMemoryMb(c.Sw);
            c.Stage("PreparePlacements");
            PreparePlacements(c,structure);
            memoryProfile["afterPreparePlacements"]=CadMemoryMb(c.Sw);
            c.Stage("BuildNativeAssembly");
            BuildNativeAssembly(c);
            memoryProfile["afterBuildNativeAssembly"]=CadMemoryMb(c.Sw);
            c.Stage("ReopenAndMeasure");
            ReopenAndMeasure(c);
            memoryProfile["afterReopenAndMeasure"]=CadMemoryMb(c.Sw);
            c.Stage("Completed");
            c.Evidence["success"]=true;
        }
        catch(Exception error) { c.Evidence["error"]=error.ToString(); if(VariableTrial.ComTimedOut) c.Evidence["comTimedOut"]=true; }
        finally
        {
            try { c.CloseOutputDocuments(); ReclaimComWrappers(); } catch(Exception error) { c.Evidence["cleanupError"]=error.ToString(); c.Evidence["success"]=false; }
            c.DetachComTimeoutObserver();
            double? afterMem=CadMemoryMb(c.Sw); c.Evidence["cadPrivateMemoryAfterMb"]=afterMem; memoryProfile["afterCleanup"]=afterMem;
            if(c.InputHashes!=null)
            {
                var changed=c.InputHashes.Where(item=>!File.Exists(item.Key)||Hash(item.Key)!=item.Value).Select(item=>item.Key).ToArray();
                c.Evidence["inputFilesUnchanged"]=changed.Length==0; c.Evidence["changedInputFiles"]=changed;
                if(changed.Length!=0)c.Evidence["success"]=false;
            }
            c.Evidence["production_release"]=false; c.Evidence["acceptancePass"]=false; c.Write();
        }
        return c.Evidence;
    }
    public static object ReopenExisting(ISldWorks sw,string cadDirectory,string outputDirectory)
    {
        VariableTrial.RuntimeOwnership();Need(sw!=null,"ASSEMBLY_ARGUMENT_REQUIRED");
        string cad=Within(cadDirectory,TrialRoot),folder=Within(outputDirectory,Path.Combine(cad,"assemblies"));
        Need(Path.GetDirectoryName(folder).Equals(Path.Combine(cad,"assemblies"),StringComparison.OrdinalIgnoreCase),"ASSEMBLY_CASE_DIRECTORY_REQUIRED");
        string sourceEvidence=Path.Combine(folder,"assembly-evidence.json");
        var prior=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(sourceEvidence,Encoding.UTF8));
        var c=new BuildContext {Sw=sw,Cad=cad,Output=folder,Layout=Map(prior["layout"]),ColdSession=true};
        c.EvidencePath=Path.Combine(folder,"relocation-evidence.json");Need(!File.Exists(c.EvidencePath),"PRESERVE_RELOCATION_EVIDENCE");
        c.AssemblyPath=Path.Combine(folder,"16029_variable_minimum.SLDASM");
        string sourceHash=Hash(sourceEvidence);
        c.Evidence=new Dictionary<string,object>{{"schema","16029-variable-relocation/v1"},{"production_release",false},{"acceptancePass",false},{"stageAcceptance",false},{"diagnosticOnly",true},{"success",false},{"layout",c.Layout},{"events",c.Events},{"parts",c.Parts},{"sourceEvidencePath",sourceEvidence},{"sourceEvidenceSha256",sourceHash},{"outputDirectory",folder},{"assemblyPath",c.AssemblyPath}};
        try
        {
            Need(VariableTrial.SwGetDocuments(sw,"TrialAssembly.ReopenExisting").Length==0,"ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
            Need(Convert.ToString(prior["schema"])=="16029-independent-variable-minimum-assembly/v1"&&Convert.ToBoolean(prior["success"])&&Convert.ToBoolean(prior["nativeReadbackPass"])&&Convert.ToBoolean(prior["componentIdentityReadbackPass"]),"RELOCATED_SOURCE_ASSEMBLY_WAS_NOT_GENERATED");
            Need(Convert.ToBoolean(prior["inputFilesUnchanged"])&&Items(prior["changedInputFiles"]).Length==0,"RELOCATED_SOURCE_INPUTS_CHANGED");
            Need(!Convert.ToBoolean(prior["acceptancePass"])&&!Convert.ToBoolean(prior["production_release"]),"RELOCATED_SOURCE_ACCEPTANCE_CHANGED");
            string oldRoot=Within(Convert.ToString(prior["outputDirectory"]),TrialRoot),oldAssembly=Path.GetFullPath(Convert.ToString(prior["assemblyPath"]));
            Need(!oldRoot.Equals(folder,StringComparison.OrdinalIgnoreCase),"NEW_RELOCATION_COPY_REQUIRED");
            Need(oldAssembly.Equals(Path.Combine(oldRoot,Path.GetFileName(c.AssemblyPath)),StringComparison.OrdinalIgnoreCase),"RELOCATION_SOURCE_ASSEMBLY_PATH_MISMATCH");
            c.Evidence["sourceOutputDirectory"]=oldRoot;c.Evidence["sourceAssemblyPath"]=oldAssembly;c.Evidence["referenceRemappingExpected"]=true;
            var cabinet=Map(c.Layout["cabinet"]);c.Width=Number(cabinet,"widthMm");c.Height=Number(cabinet,"heightMm");c.Depth=Number(cabinet,"depthMm");c.DoorWidth=Number(c.Layout,"doorLeafWidthMm");ValidateLayout(c);
            object[] manifest=Items(prior["expectedManifest"]),sourceInstances=Items(prior["instances"]);var remapping=new List<object>();c.Evidence["referenceRemapping"]=remapping;
            int expected=(int)Number(Map(prior["minimumScope"]),"expectedComponentCount");
            Need(expected>0&&manifest.Length==expected&&sourceInstances.Length==expected,"RELOCATION_SOURCE_MANIFEST_COUNT_MISMATCH");
            foreach(object raw in sourceInstances)
            {
                var item=Map(raw);string role=Convert.ToString(item["identity"]),old=Within(Convert.ToString(item["path"]),oldRoot);
                double[] transform=Numbers(item["transform"]);Need(transform.Length>=13&&transform.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)),"RELOCATION_TRANSFORM_INVALID");
                var declared=manifest.Select(Map).Where(value=>Convert.ToString(value["identity"])==role).ToArray();
                Need(declared.Length==1&&Path.GetFullPath(Convert.ToString(declared[0]["path"])).Equals(old,StringComparison.OrdinalIgnoreCase)&&SameTransform(Numbers(declared[0]["transform"]),transform),"RELOCATION_SOURCE_MANIFEST_IDENTITY_MISMATCH "+role);
                foreach(string key in new[]{"side","index","kind"})Need(Convert.ToString(item[key],Invariant)==Convert.ToString(declared[0][key],Invariant),"RELOCATION_SOURCE_MANIFEST_FIELD_MISMATCH "+role+"/"+key);
                string path=Within(Path.Combine(folder,old.Substring(oldRoot.Length+1)),folder);Need(File.Exists(path),"RELOCATION_PART_MISSING "+path);
                var placement=new Placement(role,path,Convert.ToString(item["side"]),(int)Number(item,"index"),Convert.ToString(item["kind"]),transform.Take(9).ToArray(),transform[9]*1000,transform[10]*1000,transform[11]*1000);
                Need(SameTransform(placement.Transform,transform),"RELOCATION_SOURCE_TRANSFORM_CHANGED");placement.ActualName=Convert.ToString(item["component"]);c.Placements.Add(placement);
                remapping.Add(new {identity=role,sourcePath=old,path=path});
            }
            Need(c.Placements.Select(value=>value.Role).Distinct(StringComparer.Ordinal).Count()==expected,"RELOCATION_DUPLICATE_IDENTITY");
            for(int i=0;i<c.Placements.Count;i++)for(int j=i+1;j<c.Placements.Count;j++)Need(!c.Placements[i].Path.Equals(c.Placements[j].Path,StringComparison.OrdinalIgnoreCase)||!SameTransform(c.Placements[i].Transform,c.Placements[j].Transform),"RELOCATION_AMBIGUOUS_PATH_TRANSFORM");
            c.Evidence["expectedManifest"]=c.Placements.Select(value=>new {identity=value.Role,path=value.Path,side=value.Side,index=value.Index,kind=value.Kind,transform=value.Transform}).ToArray();c.Evidence["minimumScope"]=prior["minimumScope"];
            c.Evidence["sourceCaseSuccess"]=prior["success"];c.Evidence["sourceAssemblySha256"]=prior["assemblySha256"];c.Evidence["assemblySha256"]=Hash(c.AssemblyPath);
            c.InputHashes=Directory.GetFiles(c.Cad,"*",SearchOption.AllDirectories).Where(path=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(path).ToUpperInvariant())).ToDictionary(path=>path,Hash,StringComparer.OrdinalIgnoreCase);
            c.Write();ReopenAndMeasure(c);c.Evidence["success"]=true;
        }
        catch(Exception error){c.Evidence["error"]=error.ToString();}
        finally
        {
            try{c.CloseOutputDocuments();}catch(Exception error){c.Evidence["cleanupError"]=error.ToString();c.Evidence["success"]=false;}
            if(c.InputHashes!=null)
            {
                var after=Directory.GetFiles(c.Cad,"*",SearchOption.AllDirectories).Where(path=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(path).ToUpperInvariant())).ToDictionary(path=>path,Hash,StringComparer.OrdinalIgnoreCase);
                string[] paths=c.InputHashes.Keys.Union(after.Keys,StringComparer.OrdinalIgnoreCase).OrderBy(path=>path,StringComparer.OrdinalIgnoreCase).ToArray();
                string[] changed=paths.Where(path=>!c.InputHashes.ContainsKey(path)||!after.ContainsKey(path)||c.InputHashes[path]!=after[path]).ToArray();
                c.Evidence["readOnlyInputFiles"]=paths.Select(path=>new {path=path,sha256Before=c.InputHashes.ContainsKey(path)?c.InputHashes[path]:null,sha256After=after.ContainsKey(path)?after[path]:null}).ToArray();
                c.Evidence["inputFilesUnchanged"]=changed.Length==0;c.Evidence["changedInputFiles"]=changed;if(changed.Length!=0)c.Evidence["success"]=false;
            }
            c.Evidence["sourceEvidenceUnchanged"]=File.Exists(sourceEvidence)&&Hash(sourceEvidence)==sourceHash;if(!Convert.ToBoolean(c.Evidence["sourceEvidenceUnchanged"]))c.Evidence["success"]=false;
            c.Evidence["stageAcceptance"]=false;c.Evidence["acceptancePass"]=false;c.Evidence["production_release"]=false;c.Write();
        }
        return c.Evidence;
    }

    public static object MeasureBuiltCase(ISldWorks sw,string cadDirectory,string outputDirectory)
    {
        VariableTrial.RuntimeOwnership(); Need(sw!=null,"ASSEMBLY_ARGUMENT_REQUIRED");
        string cad=Within(cadDirectory,TrialRoot),folder=Within(outputDirectory,Path.Combine(cad,"assemblies"));
        Need(Path.GetDirectoryName(folder).Equals(Path.Combine(cad,"assemblies"),StringComparison.OrdinalIgnoreCase),"ASSEMBLY_CASE_DIRECTORY_REQUIRED");
        string sourceEvidence=Path.Combine(folder,"assembly-evidence.json");
        var prior=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(sourceEvidence,Encoding.UTF8));
        var c=new BuildContext {Sw=sw,Cad=cad,Output=folder,Layout=Map(prior["layout"]),ColdSession=true};
        c.EvidencePath=Path.Combine(folder,"measurement-evidence.json"); Need(!File.Exists(c.EvidencePath),"PRESERVE_MEASUREMENT_EVIDENCE");
        c.AssemblyPath=Path.Combine(folder,"16029_variable_minimum.SLDASM");
        string sourceHash=Hash(sourceEvidence);
        c.Evidence=new Dictionary<string,object>{{"schema","16029-built-assembly-measurement/v1"},{"production_release",false},{"acceptancePass",false},{"stageAcceptance",false},{"diagnosticOnly",true},{"success",false},{"layout",c.Layout},{"events",c.Events},{"parts",c.Parts},{"sourceEvidencePath",sourceEvidence},{"sourceEvidenceSha256",sourceHash},{"assemblyPath",c.AssemblyPath},{"outputDirectory",folder}};
        try
        {
            Need(VariableTrial.SwGetDocuments(sw,"TrialAssembly.MeasureBuiltCase").Length==0,"ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
            string oldRoot=Within(Convert.ToString(prior["outputDirectory"]),TrialRoot),oldAssembly=Path.GetFullPath(Convert.ToString(prior["assemblyPath"]));
            Need(!oldRoot.Equals(folder,StringComparison.OrdinalIgnoreCase),"NEW_ASSEMBLY_MEASUREMENT_COPY_REQUIRED");
            Need(oldAssembly.Equals(Path.Combine(oldRoot,Path.GetFileName(c.AssemblyPath)),StringComparison.OrdinalIgnoreCase),"MEASUREMENT_SOURCE_ASSEMBLY_PATH_MISMATCH");
            Need(prior.ContainsKey("inputFilesUnchanged")&&Convert.ToBoolean(prior["inputFilesUnchanged"])&&Items(prior["changedInputFiles"]).Length==0,"MEASUREMENT_SOURCE_INPUTS_CHANGED");
            var cabinet=Map(c.Layout["cabinet"]);c.Width=Number(cabinet,"widthMm");c.Height=Number(cabinet,"heightMm");c.Depth=Number(cabinet,"depthMm");c.DoorWidth=Number(c.Layout,"doorLeafWidthMm");
            bool allowWidthEnvelope=prior.ContainsKey("envelopeMode")&&Convert.ToString(prior["envelopeMode"])=="parametric_width";
            ValidateCabinetEnvelope(c.Width,c.Height,c.Depth,c.DoorWidth,allowWidthEnvelope); ValidateLayout(c);
            object[] manifest=Items(prior["expectedManifest"]),sourceEvents=Items(prior["events"]);
            int boundaries=Items(Map(c.Layout["boundaries"])["L"]).Length+Items(Map(c.Layout["boundaries"])["R"]).Length,expected=8+11*(int)Number(c.Layout,"doorCount")+2*boundaries;
            Need(manifest.Length==expected,"MEASUREMENT_SOURCE_MANIFEST_COUNT_MISMATCH");
            var built=sourceEvents.Select((value,index)=>new {item=Map(value),index=index}).Where(value=>Convert.ToString(value.item["inspection"])=="minimum_assembly_built").ToArray();
            Need(built.Length==1&&Convert.ToString(built[0].item["path"]).Equals(oldAssembly,StringComparison.OrdinalIgnoreCase)&&(int)Number(built[0].item,"componentCount")==expected,"MEASUREMENT_SOURCE_BUILD_INCOMPLETE");
            bool saved=sourceEvents.Take(built[0].index).Select(Map).Any(item=>Convert.ToString(item["inspection"])=="assembly_document_save"&&Convert.ToString(item["path"]).Equals(oldAssembly,StringComparison.OrdinalIgnoreCase)&&Convert.ToBoolean(item["saved"])&&(int)Number(item,"errors")==0&&(int)Number(item,"warnings")==0);
            Need(saved&&File.Exists(c.AssemblyPath),"MEASUREMENT_SOURCE_ZERO_WARNING_SAVE_REQUIRED");
            var renamed=sourceEvents.Select(Map).Where(item=>Convert.ToString(item["inspection"])=="assembly_component_rename_readback").ToArray(); Need(renamed.Length==expected,"MEASUREMENT_SOURCE_RENAME_COUNT_MISMATCH");
            foreach(object raw in manifest)
            {
                var item=Map(raw); string role=Convert.ToString(item["identity"]),old=Path.GetFullPath(Convert.ToString(item["path"]));
                Need(old.StartsWith(oldRoot+"\\",StringComparison.OrdinalIgnoreCase),"MEASUREMENT_SOURCE_PART_PATH_MISMATCH");
                string path=Within(Path.Combine(folder,old.Substring(oldRoot.Length+1)),folder); Need(File.Exists(path),"MEASUREMENT_PART_MISSING "+path);
                double[] transform=Numbers(item["transform"]); Need(transform.Length>=13&&transform.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)),"MEASUREMENT_SOURCE_TRANSFORM_INVALID");
                var placement=new Placement(role,path,Convert.ToString(item["side"]),(int)Number(item,"index"),Convert.ToString(item["kind"]),transform.Take(9).ToArray(),transform[9]*1000,transform[10]*1000,transform[11]*1000);
                Need(SameTransform(placement.Transform,transform),"MEASUREMENT_SOURCE_TRANSFORM_CHANGED");
                var names=renamed.Where(value=>Convert.ToString(value["requested"])==role&&Convert.ToString(value["path"]).Equals(old,StringComparison.OrdinalIgnoreCase)).ToArray();
                Need(names.Length==1&&Convert.ToBoolean(names[0]["nameMatches"])&&Convert.ToBoolean(names[0]["identityUnique"])&&Convert.ToBoolean(names[0]["selectedComponentIdentity"]),"MEASUREMENT_SOURCE_RENAME_IDENTITY_INVALID "+role);
                placement.OriginalName=Convert.ToString(names[0]["before"]);placement.ActualName=Convert.ToString(names[0]["actual"]);c.Placements.Add(placement);
            }
            Need(c.Placements.Select(value=>value.Role).Distinct(StringComparer.Ordinal).Count()==expected,"MEASUREMENT_SOURCE_DUPLICATE_IDENTITY");
            for(int i=0;i<c.Placements.Count;i++)for(int j=i+1;j<c.Placements.Count;j++)Need(!c.Placements[i].Path.Equals(c.Placements[j].Path,StringComparison.OrdinalIgnoreCase)||!SameTransform(c.Placements[i].Transform,c.Placements[j].Transform),"MEASUREMENT_SOURCE_AMBIGUOUS_PATH_TRANSFORM");
            c.Evidence["expectedManifest"]=c.Placements.Select(value=>new {identity=value.Role,path=value.Path,side=value.Side,index=value.Index,kind=value.Kind,transform=value.Transform}).ToArray();
            c.Evidence["minimumScope"]=prior["minimumScope"];c.Evidence["sourceCaseSuccess"]=prior["success"];c.Evidence["sourceAssemblySha256"]=prior["assemblySha256"];c.Evidence["sourceError"]=prior.ContainsKey("error")?prior["error"]:null;
            c.Evidence["assemblySha256"]=Hash(c.AssemblyPath);c.Evidence["referenceRemappingExpected"]=true;
            c.InputHashes=Directory.GetFiles(c.Cad,"*",SearchOption.AllDirectories).Where(path=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(path).ToUpperInvariant())).ToDictionary(path=>path,Hash,StringComparer.OrdinalIgnoreCase);
            c.Write(); ReopenAndMeasure(c); c.Evidence["success"]=true;
        }
        catch(Exception error) {c.Evidence["error"]=error.ToString();}
        finally
        {
            try {c.CloseOutputDocuments();}catch(Exception error){c.Evidence["cleanupError"]=error.ToString();c.Evidence["success"]=false;}
            if(c.InputHashes!=null)
            {
                string[] changed=c.InputHashes.Where(item=>!File.Exists(item.Key)||Hash(item.Key)!=item.Value).Select(item=>item.Key).ToArray();
                c.Evidence["inputFilesUnchanged"]=changed.Length==0;c.Evidence["changedInputFiles"]=changed;if(changed.Length!=0)c.Evidence["success"]=false;
            }
            c.Evidence["sourceEvidenceUnchanged"]=File.Exists(sourceEvidence)&&Hash(sourceEvidence)==sourceHash;
            if(!Convert.ToBoolean(c.Evidence["sourceEvidenceUnchanged"]))c.Evidence["success"]=false;
            c.Evidence["stageAcceptance"]=false;c.Evidence["acceptancePass"]=false;c.Evidence["production_release"]=false;c.Write();
        }
        return c.Evidence;
    }

    static void ValidateLayout(BuildContext c)
    {
        var rows=Map(c.Layout["rowsByColumn"]); var boundaries=Map(c.Layout["boundaries"]); int total=0;
        var counts=Items(c.Layout["columnDoorCounts"]); Need(counts.Length==2,"ASSEMBLY_COLUMN_COUNTS_REQUIRED");
        foreach(string side in new[]{"L","R"})
        {
            object[] column=Items(rows[side]); Need(column.Length==Convert.ToInt32(counts[side=="L"?0:1],Invariant),"ASSEMBLY_COLUMN_COUNT_MISMATCH");
            Need(Items(boundaries[side]).Length==column.Length-1,"ASSEMBLY_BOUNDARY_COUNT_MISMATCH"); double bottom=32;
            for(int i=0;i<column.Length;i++)
            {
                var row=Map(column[i]); double height=Number(row,"doorHeightMm");
                Need(Convert.ToString(row["side"])==side&&Number(row,"index")==i+1&&height>=100&&Near(Number(row,"bottomYmm"),bottom)&&Near(Number(row,"topYmm"),bottom+height)&&Near(Number(row,"centerYmm"),bottom+height/2),"ASSEMBLY_ROW_GEOMETRY_MISMATCH");
                if(i<column.Length-1)
                {
                    var boundary=Map(Items(boundaries[side])[i]); double gap=bottom+height+3.5;
                    Need(Number(boundary,"index")==i+1&&Near(Number(boundary,"doorGapCenterYmm"),gap)&&Near(Number(boundary,"shelfCenterYmm"),gap-1)&&Near(Number(boundary,"crossbarCenterYmm"),gap-6),"ASSEMBLY_BOUNDARY_GEOMETRY_MISMATCH");
                }
                bottom+=height+7;
            }
            Need(Near(bottom-32,c.Height-87),"ASSEMBLY_LAYOUT_CLOSURE_FAILED"); total+=column.Length;
        }
        Need(total==Number(c.Layout,"doorCount"),"ASSEMBLY_DOOR_COUNT_MISMATCH");
    }

    static string[] Dependencies(BuildContext c,string path)
    {
        object[] values=VariableTrial.SwGetDocumentDependencies2(c.Sw,path,false,false,false,"TrialAssembly.Dependencies"); Need(values.Length%2==0,"ASSEMBLY_DEPENDENCY_ARRAY_INVALID " + path);
        var result=new List<string>();
        for(int i=1;i<values.Length;i+=2) { string dependency=Convert.ToString(values[i],Invariant); Within(dependency,c.Cad); Need(File.Exists(dependency),"ASSEMBLY_DEPENDENCY_MISSING " + dependency); result.Add(Path.GetFullPath(dependency)); }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    static Dictionary<string,string> PrepareStructure(BuildContext c)
    {
        string[] targets={"门框 左.sldprt","门框 右.sldprt","门框 上.sldprt","门框 下.sldprt","门框 竖隔板L.sldprt","门框 竖隔板R.SLDPRT","箱体竖隔板L.sldprt","箱体竖隔板R_parametric.SLDPRT","门框 横隔板.sldprt","门框 横隔板R.SLDPRT","箱体横层板L.sldprt","箱体横层板R.SLDPRT"};
        var ordered=new List<string>(); var visiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase); var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dependencies=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase); var stack=new List<string>(); var contextCycles=new List<object>();
        Action<string> visit=null;
        visit=path=>{
            path=Within(path,c.Cad); Need(c.InputHashes.ContainsKey(path)&&File.Exists(path),"ASSEMBLY_STRUCTURE_INPUT_REQUIRED " + path);
            string extension=Path.GetExtension(path); Need(extension.Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase)||extension.Equals(".SLDASM",StringComparison.OrdinalIgnoreCase),"ASSEMBLY_STRUCTURE_CONTEXT_TYPE_UNSUPPORTED " + path);
            if(visited.Contains(path))return;
            if(visiting.Contains(path))
            {
                string[] cycle=stack.Skip(stack.FindIndex(item=>item.Equals(path,StringComparison.OrdinalIgnoreCase))).Concat(new[]{path}).ToArray();
                Need(cycle.Any(item=>Path.GetExtension(item).Equals(".SLDASM",StringComparison.OrdinalIgnoreCase)),"ASSEMBLY_STRUCTURE_PART_DEPENDENCY_CYCLE " + path);
                contextCycles.Add(new {from=stack.Last(),to=path,paths=cycle,classification="assembly-context back reference; all members copied before native rewiring"}); return;
            }
            VariableTrial.RuntimeOwnership(); visiting.Add(path); stack.Add(path); dependencies.Add(path,Dependencies(c,path));
            foreach(string dependency in dependencies[path])visit(dependency);
            stack.RemoveAt(stack.Count-1); visiting.Remove(path); visited.Add(path); ordered.Add(path); Need(ordered.Count<=c.InputHashes.Count,"ASSEMBLY_STRUCTURE_CLOSURE_EXCEEDS_INPUT");
        };
        foreach(string name in targets)visit(c.Source(name));
        var refreshOrder=new List<string>(); var refreshVisiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase); var refreshVisited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Action<string> refresh=null;
        refresh=path=>{
            if(refreshVisited.Contains(path))return; Need(refreshVisiting.Add(path),"ASSEMBLY_STRUCTURE_PART_DEPENDENCY_CYCLE " + path);
            foreach(string dependency in dependencies[path])if(Path.GetExtension(dependency).Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase))refresh(dependency);
            refreshVisiting.Remove(path); refreshVisited.Add(path); refreshOrder.Add(path); Need(refreshOrder.Count<=24,"ASSEMBLY_STRUCTURE_REFRESH_EXCEEDS_MINIMUM");
        };
        foreach(string name in targets)refresh(c.Source(name));
        string folder=Within(Path.Combine(c.Output,"structure"),c.Output); Directory.CreateDirectory(folder);
        var copies=ordered.ToDictionary(path=>path,path=>Within(Path.Combine(folder,Path.GetFileName(path)),c.Output),StringComparer.OrdinalIgnoreCase);
        Need(copies.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()==copies.Count,"ASSEMBLY_STRUCTURE_FILENAME_COLLISION");
        foreach(var item in copies)
        {
            VariableTrial.RuntimeOwnership(); Need(!File.Exists(item.Value),"PRESERVE_STRUCTURE_COPY"); Need(Hash(item.Key)==c.InputHashes[item.Key],"ASSEMBLY_STRUCTURE_SOURCE_CHANGED " + item.Key);
            File.Copy(item.Key,item.Value,false); Need(Hash(item.Value)==c.InputHashes[item.Key],"ASSEMBLY_STRUCTURE_COPY_HASH_MISMATCH " + item.Value);
        }
        foreach(var item in copies)
        {
            VariableTrial.RuntimeOwnership();
            foreach(string old in dependencies[item.Key])
            {
                Need(copies.ContainsKey(old),"ASSEMBLY_STRUCTURE_CLOSURE_INCOMPLETE");
                Need(VariableTrial.SwReplaceReferencedDocument(c.Sw,item.Value,old,copies[old],"PrepareStructure"),"ASSEMBLY_STRUCTURE_REWIRE_FAILED " + item.Value + " -> " + old);
            }
        }
        foreach(var item in copies)
        {
            string[] actual=Dependencies(c,item.Value); foreach(string dependency in actual)Within(dependency,folder);
            Need(new HashSet<string>(actual,StringComparer.OrdinalIgnoreCase).SetEquals(dependencies[item.Key].Select(path=>copies[path])),"ASSEMBLY_STRUCTURE_REWIRED_DEPENDENCY_SET_MISMATCH " + item.Value);
        }
        c.Event(new { inspection="minimum_structure_dependency_closure",copyMethod="verified byte copy of complete part/assembly context before native ReplaceReferencedDocument; retains native SplitBody/MirrorStock context",files=copies.Select(item=>new {source=item.Key,path=item.Value,sourceSha256=c.InputHashes[item.Key],byteCopySha256=c.InputHashes[item.Key],rewiredSha256=Hash(item.Value),preparedAsStructuralPart=refreshVisited.Contains(item.Key)}).ToArray(),dependencyEdges=dependencies.SelectMany(item=>item.Value.Select(path=>new {source=item.Key,dependency=path,copiedSource=copies[item.Key],copiedDependency=copies[path]})).ToArray(),assemblyContextCycles=contextCycles.ToArray(),partRefreshOrder=refreshOrder.Select(path=>copies[path]).ToArray(),copiedContextAssemblyCount=ordered.Count(path=>Path.GetExtension(path).Equals(".SLDASM",StringComparison.OrdinalIgnoreCase)),insertedTargetPartCount=targets.Length,referenceModelsInserted=false,contextAssembliesExplicitlyRebuilt=false });
        string master=copies[c.Source("标准寄存柜 模型.SLDPRT")];
        var partRecordIndexByPath=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var partRecordMetaByPath=new Dictionary<string,Tuple<string,string,List<object>,object,object>>(StringComparer.OrdinalIgnoreCase);
        ModelDoc2 sharedMaster=null; string sharedMasterHash=null;
        try
        {
        foreach(string source in refreshOrder)
        {
            string path=copies[source]; ModelDoc2 model=null;
            try
            {
                string fileName=Path.GetFileName(path);
                if(fileName.Equals("门框 下.sldprt",StringComparison.OrdinalIgnoreCase))
                {
                    if(sharedMaster==null) { sharedMasterHash=Hash(master); sharedMaster=c.Open(master,true); }
                    c.Event(new {inspection="locked_bottom_frame_exact_source_loaded",path=master,sourceSha256=sharedMasterHash,actualReadOnly=sharedMaster.IsOpenedReadOnly(),geometry=PartGeometry(sharedMaster),sourceRule="NativeParametricWidth.RunDerivedEdit; verified isolated A/B probe 20261113"});
                }
                model=c.Open(path,false);
                var before=PartGeometry(model); double[] beforeBox=BodyBounds(model); var changes=new List<object>();
                c.Event(new {inspection="structural_part_before_refresh",path=path,source=source,geometry=before});
                if(path.Equals(master,StringComparison.OrdinalIgnoreCase))
                {
                    Need(Near(DimensionValue(model,"D2@草图1"),c.Height)&&Near(DimensionValue(model,"D1@凸台-拉伸1"),c.Depth),"ASSEMBLY_STRUCTURE_FIXED_HD_DRIFT");
                    SetDimension(model,"D1@草图1",c.Width,changes);
                    SetDimension(model,"D2@草图130",c.Width-150,changes);
                    SetDimension(model,"D2@草图142",(c.Width-160)/2,changes);
                    SetDimension(model,"D1@草图143",(c.Width-160)/2,changes);
                    SetDimension(model,"D3@草图137",(c.Width-300)/2,changes);
                    c.Event(new {inspection="structural_master_width_interfaces",path=path,source=source,sourceRule="NativeParametricWidth.cs:1695-1721",interfaces=changes});
                }
                var links=RefreshNativeLinks(c,model);
                c.Event(new {inspection="structural_part_native_links",path=path,links=links});
                if(fileName.Equals("门框 竖隔板L.sldprt",StringComparison.OrdinalIgnoreCase))EnsureFrameLinearPatternPitch(c,model,"阵列(线性)1",changes);
                else if(fileName.Equals("门框 左.sldprt",StringComparison.OrdinalIgnoreCase))EnsureFrameLinearPatternPitch(c,model,"阵列(线性)2",changes);
                Need(VariableTrial.ModelForceRebuild3(model,false,"PrepareStructure"),"ASSEMBLY_STRUCTURE_REBUILD_FAILED " + path); CheckFeatureHealth(model);
                double[] afterBox=BodyBounds(model);
                c.Event(new {inspection="structural_part_after_refresh_before_validation",path=path,source=source,beforeBoxMm=beforeBox,afterBoxMm=afterBox,deltaBoxMm=afterBox.Select((value,axis)=>value-beforeBox[axis]).ToArray(),geometry=PartGeometry(model),stageAcceptance=false});
                if(path.Equals(master,StringComparison.OrdinalIgnoreCase))
                {
                    Need(Near(DimensionValue(model,"D6@草图115"),c.DoorWidth)&&Near(DimensionValue(model,"D8@草图76"),c.DoorWidth),"ASSEMBLY_MASTER_WIDTH_READBACK_FAILED");
                    Need(Near(afterBox[3]-afterBox[0],c.Width),"ASSEMBLY_MASTER_ACTUAL_WIDTH_FAILED");
                }
                else
                {
                    double[] expected=ExpectedStructureX(fileName,c.Width);
                    if(expected!=null)Need(Near(afterBox[0],expected[0])&&Near(afterBox[3],expected[1]),"ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED " + path);
                    Need(new[]{1,2,4,5}.All(axis=>Near(beforeBox[axis],afterBox[axis])),"ASSEMBLY_DERIVED_YZ_DRIFT " + path);
                }
                if(fileName.Equals("箱体竖隔板L.sldprt",StringComparison.OrdinalIgnoreCase))RepairPartitionLocks(c,model,"L");
                else if(fileName.Equals("箱体竖隔板R参数源.SLDPRT",StringComparison.OrdinalIgnoreCase))RepairPartitionLocks(c,model,"R");
                else if(fileName.Equals("箱体竖隔板R_parametric.SLDPRT",StringComparison.OrdinalIgnoreCase))
                {
                    VerifyPartitionLockRows(c,model,"R","mirrored_parametric");
                    EnsureSteelMaterial(c,model,"Default");
                    Need(VariableTrial.ModelForceRebuild3(model,false,"PrepareStructure.PostMaterial"),"ASSEMBLY_STRUCTURE_POST_MATERIAL_REBUILD_FAILED " + path); CheckFeatureHealth(model);
                }
                c.Save(model);
                string partRole=targets.Contains(fileName,StringComparer.OrdinalIgnoreCase)?"structural_component":"structural_native_reference";
                partRecordIndexByPath[path]=c.Parts.Count;
                partRecordMetaByPath[path]=Tuple.Create(partRole,source,changes,links,before);
                c.Parts.Add(MakeStructurePartRecord(partRole,path,source,changes,links,before,PartGeometry(model))); c.Write();
                if(path.Equals(master,StringComparison.OrdinalIgnoreCase))
                {
                    c.Close(model); model=null;
                    c.CloseOutputDocuments();
                    sharedMasterHash=Hash(master);
                    sharedMaster=c.Open(master,true);
                }
            }
            finally { if(model!=null)c.Close(model); }
        }
        }
        finally { if(sharedMaster!=null)c.Close(sharedMaster);c.CloseOutputDocuments();if(sharedMasterHash!=null)Need(Hash(master)==sharedMasterHash,"ASSEMBLY_READONLY_DERIVED_SOURCE_CHANGED"); }
        var structure=targets.ToDictionary(name=>name,name=>copies[c.Source(name)],StringComparer.OrdinalIgnoreCase);
        RepairColumnFrameSlots(c,structure,partRecordIndexByPath,partRecordMetaByPath);
        return structure;
    }

    // Throwaway A/B probe: only edits the protected runner's new copy of the failed closure.
    public static object ProbeBottomFrame(ISldWorks sw,string cadDirectory,bool sourceFirst)
    {
        string cad=Within(cadDirectory,TrialRoot),path=Within(Path.Combine(cad,"门框 下.sldprt"),cad),source=Within(Path.Combine(cad,"标准寄存柜 模型.SLDPRT"),cad);
        var c=new BuildContext {Sw=sw,Cad=cad,Output=cad,EvidencePath=Path.Combine(cad,"structure-probe.json")};
        Need(!File.Exists(c.EvidencePath),"PRESERVE_STRUCTURE_PROBE");
        c.Evidence=new Dictionary<string,object>{{"schema","16029-bottom-frame-refresh-probe/v1"},{"sourceFirst",sourceFirst},{"production_release",false},{"stageAcceptance",false},{"success",false},{"events",c.Events},{"sourcePath",source},{"targetPath",path}};
        var hashes=Directory.GetFiles(cad).Where(p=>new[]{".SLDPRT",".SLDASM"}.Contains(Path.GetExtension(p).ToUpperInvariant())).ToDictionary(p=>p,Hash,StringComparer.OrdinalIgnoreCase);
        ModelDoc2 sourceModel=null,model=null; Feature feature=null;
        try
        {
            Need(VariableTrial.SwGetDocuments(sw,"ProbeBottomFrame.Empty").Length==0,"PROBE_EMPTY_SESSION_REQUIRED");
            if(sourceFirst) {sourceModel=c.Open(source,true);c.Event(new {inspection="probe_source_loaded",geometry=PartGeometry(sourceModel)});}
            model=c.Open(path,false); feature=model.FirstFeature() as Feature;
            while(feature!=null&&feature.GetTypeName2()!="SplitBody") {Feature next=feature.GetNextFeature() as Feature;Release(feature);feature=next;}
            Need(feature!=null,"PROBE_SPLIT_BODY_REQUIRED");
            Action<string> capture=stage=>{
                object files,parts,features,types,statuses,entities,components; int option=0;string config="";
                feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out option,out config);
                string[] paths=Items(files).Select(v=>Path.GetFullPath(Convert.ToString(v,Invariant))).ToArray();int[] states=Items(statuses).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                var loaded=new List<object>();foreach(object raw in VariableTrial.SwGetDocuments(sw,"ProbeBottomFrame."+stage)){var doc=(ModelDoc2)raw;try{loaded.Add(new {path=doc.GetPathName(),readOnly=doc.IsOpenedReadOnly(),dirty=doc.GetSaveFlag(),configuration=doc.ConfigurationManager.ActiveConfiguration.Name});}finally{Release(raw);}}
                c.Event(new {inspection="probe_stage",stage=stage,feature=feature.Name,sourcePaths=paths,statuses=states,configOption=option,configName=config,geometry=PartGeometry(model),loadedDocuments=loaded});
                Need(paths.Length>0&&states.Length==paths.Length&&paths.All(p=>p.Equals(source,StringComparison.OrdinalIgnoreCase))&&!states.Any(v=>v==0||v==5),"PROBE_EXACT_LOCAL_LINK_REQUIRED "+stage);
                if(stage=="opened"||stage=="relocked"||stage=="relock_rebuild"||stage=="cold_reopen")Need(states.All(v=>v==1),"PROBE_LOCK_STATE_REQUIRED "+stage);
                if(stage=="unlocked"||stage=="updated"||stage=="rebuilt")Need(states.All(v=>v!=1),"PROBE_UNLOCK_FAILED "+stage);
            };
            capture("opened");double[] before=BodyBounds(model);
            object f,linkedParts,fs,t,s,e,co;int opt=0;string cfg="";feature.ListExternalFileReferences2(out f,out linkedParts,out fs,out t,out s,out e,out co,out opt,out cfg);
            VariableTrial.FeatureUpdateExternalFileReferences(feature,opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesunlockAll,"ProbeBottomFrame");capture("unlocked");
            VariableTrial.FeatureUpdateExternalFileReferences(feature,opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone,"ProbeBottomFrame");capture("updated");
            Need(VariableTrial.ModelForceRebuild3(model,false,"ProbeBottomFrame"),"PROBE_REBUILD_FAILED");capture("rebuilt");
            VariableTrial.FeatureUpdateExternalFileReferences(feature,opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll,"ProbeBottomFrame");capture("relocked");
            Need(VariableTrial.ModelForceRebuild3(model,false,"ProbeBottomFrame.Relock"),"PROBE_RELOCK_REBUILD_FAILED");capture("relock_rebuild");
            double[] after=BodyBounds(model);c.Evidence["beforeBoxMm"]=before;c.Evidence["afterBoxMm"]=after;
            Need(Near(after[0],-448.6)&&Near(after[3],448.6),"PROBE_TARGET_WIDTH_FAILED");
            Need(new[]{1,2,4,5}.All(axis=>Near(before[axis],after[axis])),"PROBE_YZ_DRIFT");CheckFeatureHealth(model);RequireOneBody(model);c.Save(model);
            Release(feature);feature=null;c.Close(model);model=null;if(sourceModel!=null){c.Close(sourceModel);sourceModel=null;}c.CloseOutputDocuments();
            string saved=Hash(path);model=c.Open(path,true);feature=model.FirstFeature() as Feature;
            while(feature!=null&&feature.GetTypeName2()!="SplitBody"){Feature next=feature.GetNextFeature() as Feature;Release(feature);feature=next;}
            Need(feature!=null,"PROBE_REOPEN_SPLIT_BODY_REQUIRED");capture("cold_reopen");CheckFeatureHealth(model);RequireOneBody(model);
            double[] reopened=BodyBounds(model);Need(after.Select((value,axis)=>Near(value,reopened[axis])).All(v=>v),"PROBE_COLD_GEOMETRY_DRIFT");
            c.Evidence["savedSha256"]=saved;c.Evidence["coldReopenBoxMm"]=reopened;c.Evidence["success"]=true;
        }
        catch(Exception error){c.Evidence["error"]=error.ToString();}
        finally
        {
            try{Release(feature);c.Close(model);if(sourceModel!=null)c.Close(sourceModel);c.CloseOutputDocuments();}catch(Exception error){c.Evidence["cleanupError"]=error.ToString();c.Evidence["success"]=false;}
            string[] changed=hashes.Where(item=>!item.Key.Equals(path,StringComparison.OrdinalIgnoreCase)&&Hash(item.Key)!=item.Value).Select(item=>item.Key).ToArray();
            c.Evidence["nonTargetFilesUnchanged"]=changed.Length==0;c.Evidence["changedNonTargetFiles"]=changed;if(changed.Length!=0)c.Evidence["success"]=false;
            c.Evidence["production_release"]=false;c.Evidence["stageAcceptance"]=false;c.Write();
        }
        return c.Evidence;
    }

    static double[] ExpectedStructureX(string name,double width)
    {
        switch(name.ToLowerInvariant())
        {
            case "门框 左.sldprt": return new[]{-width/2,-width/2+20};
            case "门框 右.sldprt": return new[]{width/2-20,width/2};
            case "门框 上.sldprt": case "门框 下.sldprt": return new[]{-width/2+1.4,width/2-1.4};
            case "门框 横隔板.sldprt": return new[]{-width/2+18.5,-36.5};
            case "门框 横隔板r.sldprt": return new[]{36.5,width/2-18.5};
            case "箱体横层板l.sldprt": return new[]{-width/2+2.3,-64.5};
            case "箱体横层板r.sldprt": return new[]{64.5,width/2-2.3};
            default:return null;
        }
    }

    static object RefreshNativeLinks(BuildContext c,ModelDoc2 model)
    {
        var rows=new List<object>(); Feature feature=model.FirstFeature() as Feature; int guard=0;
        while(feature!=null)
        {
            Need(guard++<10000,"ASSEMBLY_LINK_TRAVERSAL_INCOMPLETE"); Feature next=null;
            try
            {
                next=feature.GetNextFeature() as Feature; string type=feature.GetTypeName2();
                if(type=="SplitBody"||type=="MirrorStock"||type=="MirrorPart")
                {
                    object files=null,parts=null,features=null,types=null,statuses=null,entities=null,components=null; int configOption=0; string configName="";
                    string[] fileRows; int[] before;
                    try
                    {
                        feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out configOption,out configName);
                        fileRows=Items(files).Select(v=>Convert.ToString(v,Invariant)).ToArray(); before=Items(statuses).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                    }
                    finally { ReleaseItems(files); ReleaseItems(parts); ReleaseItems(features); ReleaseItems(types); ReleaseItems(statuses); ReleaseItems(entities); ReleaseItems(components); }
                    if(fileRows.Length>0)
                    {
                        foreach(string file in fileRows)Within(file,c.Output);
                        Need(!before.Any(v=>v==0||v==5),"ASSEMBLY_BROKEN_DERIVED_REFERENCE " + model.GetPathName());
                        bool locked=before.Any(v=>v==1);
                        Need(!locked||before.All(v=>v==1),"ASSEMBLY_MIXED_DERIVED_LOCK_STATE");
                        var stages=new List<object>();
                        Action<string,bool> capture=(stage,expectLocked)=>{
                            object sf=null,sp=null,sft=null,st=null,ss=null,se=null,sc=null;int so=0;string sn="";
                            string[] paths; int[] states;
                            try
                            {
                                feature.ListExternalFileReferences2(out sf,out sp,out sft,out st,out ss,out se,out sc,out so,out sn);
                                paths=Items(sf).Select(v=>Path.GetFullPath(Convert.ToString(v,Invariant))).ToArray();states=Items(ss).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                            }
                            finally { ReleaseItems(sf); ReleaseItems(sp); ReleaseItems(sft); ReleaseItems(st); ReleaseItems(ss); ReleaseItems(se); ReleaseItems(sc); }
                            var row=new {stage=stage,sourcePaths=paths,statuses=states,geometry=PartGeometry(model)};stages.Add(row);c.Event(new {inspection="derived_link_refresh_stage",path=model.GetPathName(),feature=feature.Name,state=row},false);
                            Need(states.Length==before.Length&&paths.Length==fileRows.Length&&!states.Any(v=>v==0||v==5)&&new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase).SetEquals(fileRows.Select(v=>Path.GetFullPath(v))),"ASSEMBLY_DERIVED_INTERMEDIATE_REFERENCE_FAILED "+stage);
                            if(locked)Need(expectLocked?states.All(v=>v==1):states.All(v=>v!=1),"ASSEMBLY_DERIVED_INTERMEDIATE_LOCK_FAILED "+stage);
                        };
                        if(locked){VariableTrial.FeatureUpdateExternalFileReferences(feature,configOption,configName??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesunlockAll,"RefreshNativeLinks");capture("unlocked",false);}
                        VariableTrial.FeatureUpdateExternalFileReferences(feature,configOption,configName??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone,"RefreshNativeLinks");
                        capture("updated",false);
                        Need(VariableTrial.ModelForceRebuild3(model,false,"RefreshNativeLinks"),"ASSEMBLY_DERIVED_REFRESH_FAILED " + VariableTrial.ModelGetPathName(model,"RefreshNativeLinks"));
                        capture("rebuilt",false);
                        if(locked){VariableTrial.FeatureUpdateExternalFileReferences(feature,configOption,configName??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll,"RefreshNativeLinks");capture("relocked",true);Need(VariableTrial.ModelForceRebuild3(model,false,"RefreshNativeLinks.Relock"),"ASSEMBLY_DERIVED_RELOCK_REBUILD_FAILED");capture("relock_rebuilt",true);}
                        object af=null,ap=null,aft=null,at=null,ast=null,ae=null,ac=null;
                        string[] afterFiles; int[] after;
                        try
                        {
                            feature.ListExternalFileReferences2(out af,out ap,out aft,out at,out ast,out ae,out ac,out configOption,out configName);
                            afterFiles=Items(af).Select(v=>Convert.ToString(v,Invariant)).ToArray(); after=Items(ast).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                        }
                        finally { ReleaseItems(af); ReleaseItems(ap); ReleaseItems(aft); ReleaseItems(at); ReleaseItems(ast); ReleaseItems(ae); ReleaseItems(ac); }
                        Need(after.Length==before.Length&&!after.Any(v=>v==0||v==5)&&(!locked||after.All(v=>v==1)),"ASSEMBLY_DERIVED_REFERENCE_STATUS_FAILED");
                        rows.Add(new {feature=feature.Name,type=type,sourcePaths=afterFiles,statusesBefore=before,statusesAfter=after,wasLocked=locked,lockStateRestored=!locked||after.All(v=>v==1),stages=stages,refreshed=true});
                    }
                }
            }
            finally { Release(feature); } feature=next;
        }
        return rows;
    }

    static double DimensionValue(ModelDoc2 model,string name)
    {
        var dimension=model.Parameter(name) as Dimension; Need(dimension!=null,"ASSEMBLY_DIMENSION_MISSING " + name);
        try { return dimension.SystemValue*1000; } finally { Release(dimension); }
    }
    static double DimensionRawValue(ModelDoc2 model,string name)
    {
        // Dimensionless parameters such as linear pattern instance counts already store their unscaled numeric value in SystemValue.
        var dimension=model.Parameter(name) as Dimension; Need(dimension!=null,"ASSEMBLY_DIMENSION_MISSING " + name);
        try { return dimension.SystemValue; } finally { Release(dimension); }
    }
    static void SetDimension(ModelDoc2 model,string name,double value,List<object> evidence)
    {
        VariableTrial.RuntimeOwnership();
        var dimension=model.Parameter(name) as Dimension; Need(dimension!=null&&dimension.DrivenState==2,"ASSEMBLY_DRIVING_DIMENSION_MISSING " + name);
        try { double before=dimension.SystemValue*1000; dimension.SystemValue=value/1000; Need(Near(dimension.SystemValue*1000,value),"ASSEMBLY_DIMENSION_WRITE_FAILED " + name); evidence.Add(new {name=name,beforeMm=before,targetMm=value,readbackMm=dimension.SystemValue*1000}); }
        finally { Release(dimension); }
    }
    static object SetDoorVariables(ModelDoc2 model,double width,double height)
    {
        EquationMgr eq=model.GetEquationMgr() as EquationMgr; Need(eq!=null&&!eq.LinkToFile,"ASSEMBLY_INTERNAL_DOOR_EQUATIONS_REQUIRED"); var rows=new List<object>();
        try
        {
            foreach(var change in new[]{new {name="DOOR_W",value=width},new {name="DOOR_H",value=height}})
            {
                VariableTrial.RuntimeOwnership();
                var matches=Enumerable.Range(0,eq.GetCount()).Where(i=>eq.Equation[i].TrimStart().StartsWith("\""+change.name+"\"",StringComparison.Ordinal)).ToArray(); Need(matches.Length==1,"ASSEMBLY_DOOR_VARIABLE_AMBIGUOUS " + change.name);
                int index=matches[0]; string before=eq.Equation[index];
                int changed=eq.SetEquationAndConfigurationOption(index,"\""+change.name+"\" = "+change.value.ToString("R",Invariant)+"mm",(int)swInConfigurationOpts_e.swAllConfiguration,null);
                Need(changed==index,"ASSEMBLY_DOOR_VARIABLE_WRITE_FAILED " + change.name); rows.Add(new {name=change.name,index=index,before=before,after=eq.Equation[index],targetMm=change.value});
            }
            eq.EvaluateAll();
            for(int i=0;i<eq.GetCount();i++) { double value=eq.Value[i]; int status=eq.Status; Need(status==i&&!Double.IsNaN(value)&&!Double.IsInfinity(value),"ASSEMBLY_DOOR_EQUATION_FAILED " + i); }
            Need(!eq.LinkToFile,"ASSEMBLY_EXTERNAL_EQUATIONS_FORBIDDEN");
        }
        finally { Release(eq); }
        Need(VariableTrial.ModelForceRebuild3(model,false,"SetDoorVariables"),"ASSEMBLY_DOOR_VARIABLE_REBUILD_FAILED");
        Need(Near(DimensionValue(model,"D2@草图1"),width)&&Near(DimensionValue(model,"D1@草图1"),height),"ASSEMBLY_DOOR_VARIABLE_DIMENSION_READBACK_FAILED");
        double[] box=BodyBounds(model); Need(Near(box[3]-box[0],width)&&Near(box[4]-box[1],height)&&Near(box[5]-box[2],15),"ASSEMBLY_DOOR_VARIABLE_GEOMETRY_READBACK_FAILED"); return rows;
    }

    static void PrepareHardware(BuildContext c)
    {
        foreach(string stem in new[]{"hinge_latch_plate","u_hook_pad","plastic_bushing","door_hinge_pin","circlip","mechanical_lock_tongue","top_latch_left","top_latch_right"})
        {
            string source=c.Source(stem+"_W437_H471p25.SLDPRT"),path=c.NativeCopy(source,"trial_"+stem+".SLDPRT"); ModelDoc2 model=c.Open(path,false);
            try
            {
                CheckFeatureHealth(model,c,"hardware_byte_copy_before_rebuild",false);
                bool rebuilt=VariableTrial.ModelForceRebuild3(model,false,"PrepareHardware"); c.Event(new {inspection="assembly_hardware_rebuild",path=path,source=source,rebuilt=rebuilt});
                CheckFeatureHealth(model,c,"hardware_byte_copy_after_rebuild"); Need(rebuilt,"ASSEMBLY_HARDWARE_REBUILD_FAILED " + stem);
                var geometry=PartGeometry(model); RequireOneBody(model); c.Save(model); c.Hardware[stem]=path; c.Parts.Add(new {role=stem,path=path,source=source,sha256=Hash(path),geometry=geometry}); c.Write();
            }
            finally { c.Close(model); }
        }
    }

    static void PreparePlacements(BuildContext c,Dictionary<string,string> structure)
    {
        foreach(string name in new[]{"门框 左.sldprt","门框 右.sldprt","门框 上.sldprt","门框 下.sldprt","门框 竖隔板L.sldprt","门框 竖隔板R.SLDPRT","箱体竖隔板L.sldprt","箱体竖隔板R_parametric.SLDPRT"})
            c.Placements.Add(new Placement("structure_"+Token(c.Width)+"_"+(c.Placements.Count+1),structure[name],"",0,name.StartsWith("箱体")?"vertical_partition":"door_frame",Identity,0,0,0));
        var variants=new Dictionary<string,string[]>(StringComparer.Ordinal); var rowMap=Map(c.Layout["rowsByColumn"]); var boundaryMap=Map(c.Layout["boundaries"]);
        foreach(string side in new[]{"L","R"})
        {
            double hand=side=="L"?1:-1;
            foreach(object raw in Items(rowMap[side]))
            {
                var row=Map(raw); double height=Number(row,"doorHeightMm"),center=Number(row,"centerYmm"); int index=(int)Number(row,"index"); string key=side+"_W"+Token(c.DoorWidth)+"_H"+Token(height);
                string[] paths;
                if(!variants.TryGetValue(key,out paths))
                {
                    paths=new[]{c.NativeCopy(c.Source("door_panel_"+(side=="L"?"left":"right")+"_W437_H471p25.SLDPRT"),key+"_panel.SLDPRT"),c.NativeCopy(c.Source("door_stiffener_W437_H471p25.SLDPRT"),key+"_stiffener.SLDPRT")};
                    for(int partIndex=0;partIndex<paths.Length;partIndex++)
                    {
                        ModelDoc2 model=c.Open(paths[partIndex],false);
                        try
                        {
                            object edits;
                            if(partIndex==0)edits=SetDoorVariables(model,c.DoorWidth,height);
                            else { var changes=new List<object>(); SetDimension(model,"D2@草图1",height-10.5,changes); Need(VariableTrial.ModelForceRebuild3(model,false,"PreparePlacements"),"ASSEMBLY_STIFFENER_REBUILD_FAILED"); double[] box=BodyBounds(model); Need(Near(box[4]-box[1],height-10.5),"ASSEMBLY_STIFFENER_ACTUAL_HEIGHT_FAILED"); edits=changes; }
                            CheckFeatureHealth(model); c.Save(model); c.Parts.Add(new {role=partIndex==0?"door_panel":"door_stiffener",hand=side,widthMm=c.DoorWidth,heightMm=height,path=paths[partIndex],sha256=Hash(paths[partIndex]),edits=edits,geometry=PartGeometry(model)}); c.Write();
                        }
                        finally { c.Close(model); }
                    }
                    variants[key]=paths;
                }
                double globalX=-hand*(c.Width/4+8.5),hingeX=-(c.DoorWidth/2-10)*hand,lockX=(c.DoorWidth/2-15)*hand,half=height/2,latchY=half-29.0;
                string prefix="door_"+side+"_"+index.ToString("D2",Invariant)+"_";
                Action<string,string,double[],double,double,double> add=(kind,path,rotation,x,y,z)=>c.Placements.Add(new Placement(prefix+kind,path,side,index,kind,rotation,globalX+x,center+y,z));
                add("panel",paths[0],Identity,0,0,0);
                add("stiffener",paths[1],FlipXZ,0,0,-.8);
                add("hinge_latch_bottom",c.Hardware["hinge_latch_plate"],BottomLatch,hingeX,-latchY,-.8);
                add("hinge_latch_top",c.Hardware[side=="L"?"top_latch_left":"top_latch_right"],Identity,(side=="L"?-1:1)*(c.DoorWidth-307)/2,(height-298.4)/2,0);
                add("hook_pad",c.Hardware["u_hook_pad"],side=="L"?FlipXZ:FlipYZ,lockX,0,-10.5);
                add("bushing_top",c.Hardware["plastic_bushing"],FlipYZ,hingeX,half+1.5,-7);
                add("bushing_bottom",c.Hardware["plastic_bushing"],Identity,hingeX,-half-1.5,-7);
                add("hinge_pin",c.Hardware["door_hinge_pin"],Identity,hingeX,-half-9.7,-7);
                add("circlip_top",c.Hardware["circlip"],Identity,hingeX,half-29.7,-7.7236);
                add("circlip_bottom",c.Hardware["circlip"],Identity,hingeX,-half+29.4,-7.7236);
                add("mechanical_lock_tongue",c.Hardware["mechanical_lock_tongue"],FlipXZ,lockX,0,-11.3);
            }
            foreach(object raw in Items(boundaryMap[side]))
            {
                var boundary=Map(raw); int index=(int)Number(boundary,"index");
                double gridTranslation=c.Height-1917+152.5-(c.Height-87)/12;
                c.Placements.Add(new Placement("crossbar_"+side+"_"+index,structure[side=="L"?"门框 横隔板.sldprt":"门框 横隔板R.SLDPRT"],side,index,"crossbar",Identity,0,Number(boundary,"crossbarCenterYmm")-1700-gridTranslation,0));
                c.Placements.Add(new Placement("shelf_"+side+"_"+index,structure[side=="L"?"箱体横层板L.sldprt":"箱体横层板R.SLDPRT"],side,index,"shelf",Identity,0,Number(boundary,"shelfCenterYmm")-1705-gridTranslation,0));
            }
        }
        int boundaryCount=Items(boundaryMap["L"]).Length+Items(boundaryMap["R"]).Length;
        Need(c.Placements.Count==8+11*(int)Number(c.Layout,"doorCount")+2*boundaryCount,"ASSEMBLY_MINIMUM_MANIFEST_COUNT_FAILED");
        Need(c.Placements.Select(p=>p.Role).Distinct(StringComparer.Ordinal).Count()==c.Placements.Count,"ASSEMBLY_DUPLICATE_COMPONENT_IDENTITY");
        c.Evidence["expectedManifest"]=c.Placements.Select(p=>new {identity=p.Role,path=p.Path,side=p.Side,index=p.Index,kind=p.Kind,transform=p.Transform}).ToArray();
        c.Evidence["minimumScope"]=new {fixedStructurePartCount=8,partsPerDoor=11,boundaryCount=boundaryCount,crossbarCount=boundaryCount,shelfCount=boundaryCount,expectedComponentCount=c.Placements.Count,referenceMasterInserted=false,shelfReinforcementAndCabinetShellIncluded=false}; c.Write();
    }

    static void PreloadPartsInvisibly(BuildContext c, List<ModelDoc2> openedParts, string context)
    {
        // Hide MDI part windows/thumbnails during bulk preloading so SolidWorks stays well under Windows GDI/rm_gdi limits.
        VariableTrial.SwDocumentVisible(c.Sw, false, (int)swDocumentTypes_e.swDocPART, context + ".HidePreload");
        bool preloadFailed = false;
        try
        {
            foreach (string uniquePath in c.Placements.Select(p => p.Path).Distinct(StringComparer.OrdinalIgnoreCase))
                openedParts.Add(c.Open(uniquePath, true));
        }
        catch
        {
            preloadFailed = true;
            throw;
        }
        finally
        {
            if (!VariableTrial.ComTimedOut)
            {
                try { VariableTrial.SwDocumentVisible(c.Sw, true, (int)swDocumentTypes_e.swDocPART, context + ".RestoreVisible", true); }
                catch { if (!preloadFailed) throw; }
            }
        }
    }

    static void BuildNativeAssembly(BuildContext c)
    {
        c.CloseOutputDocuments(); string template=VariableTrial.SwGetUserPreferenceStringValue(c.Sw,(int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly,"BuildNativeAssembly");
        Need(!String.IsNullOrWhiteSpace(template)&&File.Exists(template),"ASSEMBLY_NATIVE_TEMPLATE_MISSING");
        VariableTrial.RuntimeOwnership();
        ModelDoc2 model=VariableTrial.SwNewDocument(c.Sw,template,0,0,0,"BuildNativeAssembly");
        Need(model!=null,"ASSEMBLY_NEW_DOCUMENT_FAILED"); VariableTrial.RegisterOwned(model);
        MathUtility math=null;
        var openedParts=new List<ModelDoc2>();
        try
        {
            var assembly=(AssemblyDoc)model;
            Need(VariableTrial.AssemblyGetComponents(assembly,true,"BuildNativeAssembly").Length==0,"ASSEMBLY_TEMPLATE_CONTAINS_COMPONENTS"); c.SaveNew(model,c.AssemblyPath,false); math=VariableTrial.SwGetMathUtility(c.Sw,"BuildNativeAssembly"); Need(math!=null,"ASSEMBLY_MATH_UTILITY_MISSING");
            PreloadPartsInvisibly(c, openedParts, "BuildNativeAssembly");
            int activateErrors=0; VariableTrial.SwActivateDoc3(c.Sw,VariableTrial.ModelGetTitle(model,"BuildNativeAssembly"),false,0,out activateErrors,"BuildNativeAssembly"); Need(activateErrors==0,"ASSEMBLY_ACTIVATE_FAILED");
            for(int i=0;i<c.Placements.Count;i++)
            {
                Placement placement=c.Placements[i];
                bool persistEvent=(i+1)%20==0||i+1==c.Placements.Count;
                c.CurrentComponentIndex=i; c.CurrentComponentRole=placement.Role; c.CurrentComponentPath=placement.Path; c.CurrentInsertStep="pre_activate";
                VariableTrial.RuntimeOwnership();
                Component2 component=null; MathTransform transform=null;
                try
                {
                    int errors=0; ModelDoc2 checkActive=VariableTrial.SwActiveDoc(c.Sw,"BuildNativeAssembly");
                    string modelTitle=VariableTrial.ModelGetTitle(model,"BuildNativeAssembly");
                    try { if(checkActive==null||!SameNativeObject(checkActive,model))VariableTrial.SwActivateDoc3(c.Sw,modelTitle,false,0,out errors,"BuildNativeAssembly"); }
                    finally { Release(checkActive); }
                    Need(errors==0,"ASSEMBLY_ACTIVATE_FAILED");
                    c.CurrentInsertStep="add_component";
                    component=VariableTrial.AssemblyAddComponent5(assembly,placement.Path,(int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,"",false,"",placement.Transform[9],placement.Transform[10],placement.Transform[11],"BuildNativeAssembly");
                    Need(component!=null,"ASSEMBLY_COMPONENT_ADD_FAILED " + placement.Role); VariableTrial.ModelClearSelection2(model,true,"BuildNativeAssembly"); Need(VariableTrial.ComponentSelect4(component,false,null,false,"BuildNativeAssembly"),"ASSEMBLY_COMPONENT_SELECT_FAILED");
                    c.CurrentInsertStep="unfix";
                    if(VariableTrial.ComponentIsFixed(component,"BuildNativeAssembly"))VariableTrial.AssemblyUnfixComponent(assembly,"BuildNativeAssembly");
                    string beforeName=VariableTrial.ComponentGetName2(component,"BuildNativeAssembly");placement.OriginalName=beforeName; bool prior=VariableTrial.SwGetUserPreferenceToggle(c.Sw,(int)swUserPreferenceToggle_e.swExtRefUpdateCompNames,"BuildNativeAssembly");
                    bool renameInnerFailed=false;
                    try
                    {
                        c.CurrentInsertStep="set_pref_false";
                        VariableTrial.SwSetUserPreferenceToggle(c.Sw,(int)swUserPreferenceToggle_e.swExtRefUpdateCompNames,false,"BuildNativeAssembly");
                        ModelDoc2 active=null; SelectionMgr selection=null; object selected=null;
                        bool activeAssembly=false, selectReturned=false, selectedIdentity=false, updateNames=false;
                        int selectedCount=-1; string activeDocPath=null;
                        try
                        {
                            errors=0; active=VariableTrial.SwActiveDoc(c.Sw,"BuildNativeAssembly.Rename");
                            if(active==null||!SameNativeObject(active,model))
                            {
                                Release(active); active=null;
                                VariableTrial.SwActivateDoc3(c.Sw,modelTitle,false,0,out errors,"BuildNativeAssembly.Rename");
                                active=VariableTrial.SwActiveDoc(c.Sw,"BuildNativeAssembly.Rename");
                            }
                            activeAssembly=active!=null&&SameNativeObject(active,model);
                            activeDocPath=active==null?null:VariableTrial.ModelGetPathName(active,"BuildNativeAssembly.Rename");
                            VariableTrial.ModelClearSelection2(model,true,"BuildNativeAssembly.Rename"); selectReturned=VariableTrial.ComponentSelect4(component,false,null,false,"BuildNativeAssembly.Rename");
                            selection=model.SelectionManager as SelectionMgr; selectedCount=selection==null?-1:selection.GetSelectedObjectCount2(-1);
                            if(selectedCount==1)selected=selection.GetSelectedObject6(1,-1);
                            selectedIdentity=selected!=null&&SameNativeObject(selected,component);
                            updateNames=VariableTrial.SwGetUserPreferenceToggle(c.Sw,(int)swUserPreferenceToggle_e.swExtRefUpdateCompNames,"BuildNativeAssembly.Rename");
                        }
                        finally { Release(selected); Release(selection); Release(active); }
                        c.Event(new {inspection="assembly_component_rename_selection",requested=placement.Role,before=beforeName,path=placement.Path,activationErrors=errors,activeDocument=activeDocPath,activeAssemblyIdentity=activeAssembly,selectReturned=selectReturned,selectedCount=selectedCount,selectedComponentIdentity=selectedIdentity,updateComponentNames=updateNames},persistEvent);
                        Need(errors==0&&activeAssembly&&selectReturned&&selectedCount==1&&selectedIdentity&&!updateNames,"ASSEMBLY_COMPONENT_RENAME_CONTEXT_FAILED " + placement.Role);
                        c.CurrentInsertStep="rename_set";
                        VariableTrial.ComponentSetName2(component,placement.Role,"BuildNativeAssembly"); string actualName=VariableTrial.ComponentGetName2(component,"BuildNativeAssembly");
                        bool nameMatches=actualName==placement.Role||actualName.StartsWith(placement.Role+"-",StringComparison.Ordinal);
                        bool unique=c.Placements.All(item=>item.ActualName==null||item.ActualName!=actualName);
                        string compPath=VariableTrial.ComponentGetPathName(component,"BuildNativeAssembly");
                        c.Event(new {inspection="assembly_component_rename_readback",requested=placement.Role,before=beforeName,actual=actualName,path=compPath,nameMatches=nameMatches,identityUnique=unique,selectedComponentIdentity=selectedIdentity},persistEvent);
                        Need(nameMatches&&unique&&compPath.Equals(placement.Path,StringComparison.OrdinalIgnoreCase),"ASSEMBLY_COMPONENT_RENAME_FAILED " + placement.Role);
                        placement.ActualName=actualName;
                    }
                    catch { renameInnerFailed=true; throw; }
                    finally
                    {
                        if(!VariableTrial.ComTimedOut)
                        {
                            try { VariableTrial.SwSetUserPreferenceToggle(c.Sw,(int)swUserPreferenceToggle_e.swExtRefUpdateCompNames,prior,"BuildNativeAssembly.RestorePref",true); }
                            catch { if(!renameInnerFailed) throw; }
                        }
                    }
                    c.CurrentInsertStep="transform";
                    transform=math.CreateTransform(placement.Transform) as MathTransform; Need(transform!=null&&VariableTrial.ComponentSetTransformAndSolve2(component,transform,"BuildNativeAssembly"),"ASSEMBLY_TRANSFORM_FAILED " + placement.Role);
                    double[] actual=Transform(component); Need(SameTransform(actual,placement.Transform),"ASSEMBLY_TRANSFORM_READBACK_FAILED " + placement.Role);
                    c.CurrentInsertStep="fix";
                    VariableTrial.ModelClearSelection2(model,true,"BuildNativeAssembly"); Need(VariableTrial.ComponentSelect4(component,false,null,false,"BuildNativeAssembly"),"ASSEMBLY_FIX_SELECTION_FAILED"); VariableTrial.AssemblyFixComponent(assembly,"BuildNativeAssembly"); Need(VariableTrial.ComponentIsFixed(component,"BuildNativeAssembly"),"ASSEMBLY_FIXED_PLACEMENT_FAILED"); VariableTrial.ModelClearSelection2(model,true,"BuildNativeAssembly");
                }
                finally { Release(transform); Release(component); }
            }
            c.CurrentInsertStep="rebuild";
            Need(VariableTrial.ModelForceRebuild3(model,false,"BuildNativeAssembly",VariableTrial.HeavyAssemblyComTimeoutMs),"ASSEMBLY_NATIVE_REBUILD_FAILED"); CheckFeatureHealth(model); c.Save(model);
            c.Evidence["assemblySha256"]=Hash(c.AssemblyPath); c.Event(new {inspection="minimum_assembly_built",path=c.AssemblyPath,componentCount=c.Placements.Count,placementMode="fixed native components at explicit transforms; no kinematic-mate claim"});
        }
        finally { Release(math); c.Close(model); foreach(ModelDoc2 part in openedParts)Release(part); c.CloseOutputDocuments(); }
    }

    static double[] Transform(Component2 component)
    {
        MathTransform transform=component.Transform2;
        try { double[] values=transform==null?null:Numbers(transform.ArrayData); Need(values!=null&&values.Length>=13,"ASSEMBLY_ACTUAL_TRANSFORM_MISSING"); return values; }
        finally { Release(transform); }
    }
    static bool SameTransform(double[] a,double[] b) { return a!=null&&a.Length>=13&&Enumerable.Range(0,13).All(i=>Math.Abs(a[i]-b[i])<1e-8); }

    static bool SameRotationScale(double[] a,double[] b)
    {
        return a!=null&&b!=null&&a.Length>=13&&b.Length>=13&&Enumerable.Range(0,9).Concat(new[]{12}).All(i=>BitConverter.DoubleToInt64Bits(a[i])==BitConverter.DoubleToInt64Bits(b[i]));
    }
    static bool CacheTransformAvailable(double[] value) { return value!=null&&value.Length>=13&&value.Take(13).All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v))&&value[12]>0; }
    static object RetainTopologySource(object source)
    {
        IntPtr pointer=IntPtr.Zero;
        try { pointer=Marshal.GetIUnknownForObject(source); return Marshal.GetUniqueObjectForIUnknown(pointer); }
        finally { if(pointer!=IntPtr.Zero)Marshal.Release(pointer); }
    }
    static double[] TranslatePointData(double[] value,int pointOffset,double[] deltaMm)
    {
        if(value==null)return null; double[] result=(double[])value.Clone();
        for(int axis=0;axis<3;axis++)result[pointOffset+axis]+=deltaMm[axis]/1000; return result;
    }
    static double[] TranslatePointMm(double[] value,double[] deltaMm)
    {
        if(value==null)return null; return value.Select((v,i)=>v+deltaMm[i]).ToArray();
    }
    static double[] TranslateBox(double[] box,double[] deltaMm)
    {
        if(box==null)return null; return box.Select((v,i)=>v+deltaMm[i%3]).ToArray();
    }
    static double[] CloneNumbers(double[] value) { return value==null?null:(double[])value.Clone(); }
    static string TranslatedIdentity(string identity,WorldBody source,WorldBody target)
    {
        string prefix=source.Component+"/body:"+source.Name;
        Need(identity.StartsWith(prefix+"/",StringComparison.Ordinal),"ASSEMBLY_CACHE_SOURCE_IDENTITY_PREFIX_INVALID");
        return target.Component+"/body:"+target.Name+identity.Substring(prefix.Length);
    }
    static void CopyTranslatedTopology(TopologyTemplate template,WorldBody target,double[] deltaMm)
    {
        WorldBody source=template.World;
        foreach(Cylinder item in source.Cylinders)
        {
            var cylinder=new Cylinder {Identity=TranslatedIdentity(item.Identity,source,target),SourceFaceIdentity=item.Identity,ReusedTopology=true,NativeFaceId=item.NativeFaceId,BoundaryEdgeCount=item.BoundaryEdgeCount,SeamEdgeCount=item.SeamEdgeCount,Reversed=item.Reversed,CompleteFiniteFace=item.CompleteFiniteFace,Data=TranslatePointData(item.Data,0,deltaMm),Box=TranslateBox(item.Box,deltaMm),UvBounds=CloneNumbers(item.UvBounds),SurfaceNormal=CloneNumbers(item.SurfaceNormal),FaceNormal=CloneNumbers(item.FaceNormal),AxialIntervalMm=CloneNumbers(item.AxialIntervalMm),AreaMm2=item.AreaMm2,RadialNormalDot=item.RadialNormalDot};
            foreach(BoundaryCircle circle in item.Circles)cylinder.Circles.Add(new BoundaryCircle {Identity=TranslatedIdentity(circle.Identity,source,target),FaceIdentity=cylinder.Identity,SourceEdgeIdentity=circle.Identity,SourceFaceIdentity=item.Identity,ReusedTopology=true,NativeEdgeId=circle.NativeEdgeId,CurveTag=circle.CurveTag,Sense=circle.Sense,ClosedCurve=circle.ClosedCurve,PeriodicCurve=circle.PeriodicCurve,Complete=circle.Complete,Data=TranslatePointData(circle.Data,0,deltaMm),StartMm=TranslatePointMm(circle.StartMm,deltaMm),EndMm=TranslatePointMm(circle.EndMm,deltaMm),Parameters=CloneNumbers(circle.Parameters),EdgeLengthMm=circle.EdgeLengthMm});
            cylinder.Errors.AddRange(item.Errors); target.Cylinders.Add(cylinder);
        }
        foreach(NativePlane item in source.Planes)target.Planes.Add(new NativePlane {Identity=TranslatedIdentity(item.Identity,source,target),SourceFaceIdentity=item.Identity,ReusedTopology=true,NativeFaceId=item.NativeFaceId,Reversed=item.Reversed,Data=TranslatePointData(item.Data,3,deltaMm),Box=TranslateBox(item.Box,deltaMm)});
    }

    static string MetadataActiveConfiguration(ModelDoc2 model)
    {
        ConfigurationManager manager=model.ConfigurationManager;Configuration configuration=null;
        try {configuration=manager.ActiveConfiguration;Need(configuration!=null,"ASSEMBLY_METADATA_ACTIVE_CONFIGURATION_MISSING");return configuration.Name;}
        finally {Release(configuration);Release(manager);}
    }

    internal static bool ShouldAllowDerivedFlatPatternRebuildFailure(string configuration,string referencedConfiguration,bool rebuilt,string materialName,int customPropertyCount)
    {
        if(rebuilt)return true;
        if(String.IsNullOrEmpty(configuration)||String.Equals(configuration,referencedConfiguration,StringComparison.Ordinal))return false;
        if(!configuration.EndsWith("SM-FLAT-PATTERN",StringComparison.OrdinalIgnoreCase))return false;
        // Legacy factory sheet-metal parts can carry a non-referenced flat-pattern config whose pre-existing folded cut fails to rebuild when flattened, while holding no properties or material.
        return String.IsNullOrEmpty(materialName)&&customPropertyCount==0;
    }

    static bool EnsureMetadataConfiguration(ModelDoc2 model,string configuration,string role)
    {
        // Activate and rebuild each configuration before reading material/custom properties so derived values are never stale.
        if(MetadataActiveConfiguration(model)!=configuration)
        {
            Need(VariableTrial.ModelShowConfiguration2(model,configuration,"EnsureMetadataConfiguration"),"ASSEMBLY_METADATA_CONFIGURATION_ACTIVATE_FAILED "+role+"/"+configuration);
            Need(MetadataActiveConfiguration(model)==configuration,"ASSEMBLY_METADATA_CONFIGURATION_ACTIVATE_READBACK_FAILED "+role+"/"+configuration);
        }
        return VariableTrial.ModelForceRebuild3(model,false,"EnsureMetadataConfiguration");
    }

    static object ReadCustomPropertyScope(ModelDocExtension extension,string configuration,string role,out int propertyCount)
    {
        CustomPropertyManager manager=extension.CustomPropertyManager[configuration];
        try
        {
            Need(manager!=null,"ASSEMBLY_METADATA_PROPERTY_MANAGER_MISSING "+role+"/"+configuration);
            string[] names=Items(manager.GetNames()).Select(Convert.ToString).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            Need(names.All(value=>!String.IsNullOrEmpty(value))&&names.Distinct(StringComparer.Ordinal).Count()==names.Length,"ASSEMBLY_METADATA_PROPERTY_NAMES_INVALID "+role+"/"+configuration);
            var properties=new List<object>();
            foreach(string name in names)
            {
                string raw,resolved;bool wasResolved,linked;int status=manager.Get6(name,false,out raw,out resolved,out wasResolved,out linked);
                Need(status==0||status==2,"ASSEMBLY_METADATA_NAMED_PROPERTY_NOT_PRESENT "+role+"/"+configuration+"/"+name);
                properties.Add(new {name=name,value=raw,resolved=resolved,status=status,wasResolved=wasResolved,linked=linked});
            }
            propertyCount=properties.Count;
            return new {configuration=configuration,propertyCount=propertyCount,properties=properties};
        }
        finally {Release(manager);}
    }

    internal static bool CanReuseTopologySourceIdentity(bool docSameIUnknown,int docEquality,int candidateFaces,int sourceFaces,int candidateEdges,int sourceEdges)
    {
        return docSameIUnknown&&docEquality==(int)swObjectEquality.swObjectSame&&candidateFaces>0&&candidateFaces==sourceFaces&&candidateEdges==sourceEdges;
    }
    internal static Dictionary<string,object> CloneMetadataRecord(Dictionary<string,object> template,string identity,string sourceIdentity)
    {
        var copy=new Dictionary<string,object>(template,StringComparer.Ordinal);
        copy["identity"]=identity;
        copy["reusedDocumentMetadata"]=true;
        copy["metadataSourceIdentity"]=sourceIdentity;
        return copy;
    }

    static Dictionary<string,object> ReadDocumentMetadataCore(BuildContext c,Component2 component,Placement placement,Dictionary<string,MetadataCacheEntry> cache)
    {
        ModelDoc2 model=component.GetModelDoc2() as ModelDoc2;ModelDocExtension extension=null;string activeBefore=null,activeAfterRead=null,activeAfterRestore=null,referenced=component.ReferencedConfiguration;
        var result=new Dictionary<string,object>(StringComparer.Ordinal);
        try
        {
            Need(model!=null&&model is PartDoc,"ASSEMBLY_METADATA_PART_DOCUMENT_REQUIRED "+placement.Role);
            string path=Within(model.GetPathName(),c.Output);Need(path.Equals(Path.GetFullPath(placement.Path),StringComparison.OrdinalIgnoreCase),"ASSEMBLY_METADATA_DOCUMENT_PATH_MISMATCH "+placement.Role);
            activeBefore=MetadataActiveConfiguration(model);
            string cacheKey=path.ToUpperInvariant()+"\n"+referenced+"\n"+activeBefore;
            MetadataCacheEntry cached;
            if(cache!=null&&cache.TryGetValue(cacheKey,out cached)&&SameNativeObject(cached.Model,model)&&c.Sw.IsSame(cached.Model,model)==(int)swObjectEquality.swObjectSame&&cached.ActiveBefore==activeBefore&&cached.Referenced==referenced)
            {
                Need(component.ReferencedConfiguration==referenced,"ASSEMBLY_METADATA_REFERENCED_CONFIGURATION_CHANGED "+placement.Role);
                var template=new Dictionary<string,object>(StringComparer.Ordinal)
                {
                    {"identity",cached.Role},{"path",cached.Path},{"configurations",cached.Configurations},{"referencedConfiguration",cached.Referenced},
                    {"materialName",cached.MaterialName},{"materialDatabase",cached.MaterialDatabase},{"configurationMaterials",cached.ConfigurationMaterials},
                    {"customPropertyScopes",cached.CustomPropertyScopes},{"configurationRebuilds",cached.ConfigurationRebuilds},
                    {"activeConfigurationBefore",cached.ActiveBefore},{"activeConfigurationAfterPropertyRead",cached.ActiveAfterRead},
                    {"activeConfigurationAfterRestore",cached.ActiveAfterRestore},{"configurationRestored",true},{"referencedConfigurationUnchanged",true},{"readbackComplete",true}
                };
                return CloneMetadataRecord(template,placement.Role,cached.Role);
            }
            string[] configurations=Items(model.GetConfigurationNames()).Select(Convert.ToString).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            Need(configurations.Length>0&&configurations.All(value=>!String.IsNullOrEmpty(value))&&configurations.Distinct(StringComparer.Ordinal).Count()==configurations.Length&&configurations.Contains(referenced),"ASSEMBLY_METADATA_CONFIGURATION_MISSING "+placement.Role);
            string database=null,material=null;var materials=new List<object>();var scopes=new List<object>();var rebuilds=new List<object>();extension=model.Extension;
            int rootPropertyCount;
            scopes.Add(ReadCustomPropertyScope(extension,"",placement.Role,out rootPropertyCount));
            foreach(string configuration in configurations)
            {
                bool rebuilt=EnsureMetadataConfiguration(model,configuration,placement.Role);
                string configurationDatabase,configurationMaterial=((PartDoc)model).GetMaterialPropertyName2(configuration,out configurationDatabase);
                int configurationPropertyCount;
                object scope=ReadCustomPropertyScope(extension,configuration,placement.Role,out configurationPropertyCount);
                string[] featureErrors=new string[0];
                if(!rebuilt)
                {
                    var rows=new List<object>();var failures=new List<string>();
                    ScanFeatureHealth(model,rows,failures);
                    featureErrors=failures.ToArray();
                    Need(ShouldAllowDerivedFlatPatternRebuildFailure(configuration,referenced,rebuilt,configurationMaterial,configurationPropertyCount),
                        "ASSEMBLY_METADATA_CONFIGURATION_REBUILD_FAILED "+placement.Role+"/"+configuration+(featureErrors.Length>0?" ["+String.Join("; ",featureErrors)+"]":""));
                }
                materials.Add(new {configuration=configuration,materialName=configurationMaterial,materialDatabase=configurationDatabase});
                if(configuration==referenced) { material=configurationMaterial;database=configurationDatabase; }
                scopes.Add(scope);
                rebuilds.Add(new {configuration=configuration,rebuilt=rebuilt,diagnosticOnly=!rebuilt,featureErrors=featureErrors});
            }
            result["identity"]=placement.Role;result["path"]=path;result["configurations"]=configurations;result["referencedConfiguration"]=referenced;
            result["materialName"]=material;result["materialDatabase"]=database;result["configurationMaterials"]=materials;result["customPropertyScopes"]=scopes;result["configurationRebuilds"]=rebuilds;
            activeAfterRead=MetadataActiveConfiguration(model);
            if(activeAfterRead!=activeBefore)
            {
                Need(VariableTrial.ModelShowConfiguration2(model,activeBefore,"ReadDocumentMetadataCore.Restore"),"ASSEMBLY_METADATA_CONFIGURATION_RESTORE_FAILED "+placement.Role);
                Need(VariableTrial.ModelForceRebuild3(model,false,"ReadDocumentMetadataCore.Restore"),"ASSEMBLY_METADATA_CONFIGURATION_RESTORE_REBUILD_FAILED "+placement.Role);
            }
            activeAfterRestore=MetadataActiveConfiguration(model);Need(activeAfterRestore==activeBefore,"ASSEMBLY_METADATA_CONFIGURATION_RESTORE_READBACK_FAILED "+placement.Role);
            Need(component.ReferencedConfiguration==referenced,"ASSEMBLY_METADATA_REFERENCED_CONFIGURATION_CHANGED "+placement.Role);
            if(cache!=null&&!cache.ContainsKey(cacheKey))
            {
                object retained=null;
                try
                {
                    retained=RetainTopologySource(model);
                    if(retained!=null&&SameNativeObject(retained,model)&&c.Sw.IsSame(retained,model)==(int)swObjectEquality.swObjectSame)
                    {
                        cache[cacheKey]=new MetadataCacheEntry { Model=retained,Role=placement.Role,Path=path,Referenced=referenced,ActiveBefore=activeBefore,ActiveAfterRead=activeAfterRead,ActiveAfterRestore=activeAfterRestore,MaterialName=material,MaterialDatabase=database,Configurations=configurations,ConfigurationMaterials=materials,CustomPropertyScopes=scopes,ConfigurationRebuilds=rebuilds };
                        retained=null;
                    }
                }
                finally { Release(retained); }
            }
        }
        finally
        {
            Release(extension);Release(model);
        }
        result["activeConfigurationBefore"]=activeBefore;result["activeConfigurationAfterPropertyRead"]=activeAfterRead;result["activeConfigurationAfterRestore"]=activeAfterRestore;
        result["configurationRestored"]=activeBefore==activeAfterRestore;result["referencedConfigurationUnchanged"]=component.ReferencedConfiguration==referenced;result["readbackComplete"]=true;
        return result;
    }

    static object ReadDocumentMetadata(BuildContext c,Component2 component,Placement placement)
    {
        return ReadDocumentMetadataCore(c,component,placement,null);
    }

    static void RequireCompleteInterference(object evidence,int componentCount)
    {
        var result=Map(Json.DeserializeObject(Json.Serialize(evidence)));int expected=componentCount*(componentCount-1)/2;
        Need(Convert.ToBoolean(result["complete"])&&Items(result["errors"]).Length==0,"ASSEMBLY_INTERFERENCE_NATIVE_READBACK_INCOMPLETE");
        Need((int)Number(result,"componentCount")==componentCount&&(int)Number(result,"expectedPairCount")==expected&&(int)Number(result,"enumeratedPairCount")==expected&&(int)Number(result,"measuredPairCount")==expected&&(int)Number(result,"unknownPairCount")==0&&Items(result["pairs"]).Length==expected,"ASSEMBLY_INTERFERENCE_FULL_PAIR_COVERAGE_REQUIRED");
        Need(Items(result["pairs"]).Select(Map).All(value=>Convert.ToBoolean(value["measurementComplete"])&&Convert.ToBoolean(value["nativeDetectionComplete"])),"ASSEMBLY_INTERFERENCE_PAIR_READBACK_INCOMPLETE");
    }

    static void ReopenAndMeasure(BuildContext c)
    {
        var performance=new MeasurementPerformance(); var topologyTemplates=new List<TopologyTemplate>(); var metadataCache=new Dictionary<string,MetadataCacheEntry>(StringComparer.OrdinalIgnoreCase); double phase=performance.Now;
        string hash=Hash(c.AssemblyPath); var worlds=new List<WorldBody>(); ModelDoc2 model=c.Open(c.AssemblyPath,true);
        try
        {
            var assembly=(AssemblyDoc)model; VariableTrial.AssemblyResolveAllLightWeightComponents(assembly,false,"ReopenAndMeasure"); Need(VariableTrial.ModelForceRebuild3(model,false,"ReopenAndMeasure",VariableTrial.HeavyAssemblyComTimeoutMs),"ASSEMBLY_REOPEN_REBUILD_FAILED"); CheckFeatureHealth(model); performance.Add("assembly_reopen_rebuild",phase);
            object[] components=VariableTrial.AssemblyGetComponents(assembly,true,"ReopenAndMeasure"); Need(components.Length==c.Placements.Count,"ASSEMBLY_REOPEN_COMPONENT_COUNT_FAILED actual="+components.Length+" expected="+c.Placements.Count); var instances=new List<object>();c.Evidence["instances"]=instances;c.Evidence["nativeReadbackPass"]=false;
            try
            {
                var matched=new Placement[components.Length];var identities=new List<object>();var identityFailures=new List<string>();var roles=new HashSet<string>(StringComparer.Ordinal);var nativeNames=new HashSet<string>(StringComparer.Ordinal);bool identityPass=true;c.Evidence["componentIdentityReadback"]=identities;c.Evidence["componentIdentityReadbackPass"]=false;phase=performance.Now;
                for(int i=0;i<components.Length;i++)
                {
                    var component=(Component2)components[i];string path=component.GetPathName(),name=component.Name2,fullPath=null,scopeError=null,transformError=null;double[] actual=null;
                    try {fullPath=Within(path,c.Output);}catch(Exception error){scopeError=error.Message;}
                    try {actual=Transform(component);}catch(Exception error){transformError=error.Message;}
                    var candidates=fullPath==null?new Placement[0]:c.Placements.Where(value=>Path.GetFullPath(value.Path).Equals(fullPath,StringComparison.OrdinalIgnoreCase)&&SameTransform(actual,value.Transform)).ToArray();
                    Placement placement=candidates.Length==1?candidates[0]:null;bool roleUnique=placement!=null&&roles.Add(placement.Role),nameUnique=!String.IsNullOrEmpty(name)&&nativeNames.Add(name);
                    bool state=!component.IsSuppressed()&&!component.IsHidden(true)&&component.IsFixed(),pass=placement!=null&&roleUnique&&nameUnique&&state&&scopeError==null&&transformError==null;
                    var identity=new {inspection="assembly_component_identity_readback",identity=placement==null?null:placement.Role,component=name,path=path,transform=actual,matchBasis="full native component path and unchanged 13-value transform tolerance; Name2 is evidence only",candidateCount=candidates.Length,candidateIdentities=candidates.Select(value=>value.Role).ToArray(),identityUnique=roleUnique,nativeNameUnique=nameUnique,statePass=state,scopeError=scopeError,transformError=transformError,componentNameBeforeRename=placement==null?null:placement.OriginalName,componentNameAfterRename=placement==null?null:placement.ActualName,componentNameAfterReopen=name,nameMatchesRenameReadback=placement!=null&&name==placement.ActualName,pass=pass};
                    identities.Add(identity);c.Event(identity,(i+1)%20==0||i+1==components.Length);
                    if(!pass)identityFailures.Add("component="+name+" path="+path+" transform=["+(actual==null?"unavailable":String.Join(",",actual.Select(value=>value.ToString("R",Invariant))))+"] candidateCount="+candidates.Length+" candidateRoles=["+String.Join(",",candidates.Select(value=>value.Role))+"] scopeError="+scopeError+" transformError="+transformError);
                    matched[i]=placement;identityPass=identityPass&&pass;
                }
                performance.Add("component_identity_pass_including_event_writes",phase);
                c.Evidence["componentIdentityReadback"]=identities;c.Evidence["componentIdentityReadbackPass"]=identityPass&&roles.Count==c.Placements.Count&&nativeNames.Count==components.Length;performance.Write(c);
                Need(Convert.ToBoolean(c.Evidence["componentIdentityReadbackPass"]),"ASSEMBLY_REOPEN_PATH_TRANSFORM_IDENTITY_FAILED "+String.Join("; ",identityFailures));
                for(int i=0;i<components.Length;i++)
                {
                    phase=performance.Now;var component=(Component2)components[i];Placement placement=matched[i];string path=component.GetPathName();double[] actual=Transform(component);
                    Need(String.Equals(path,placement.Path,StringComparison.OrdinalIgnoreCase)&&SameTransform(actual,placement.Transform)&&component.IsFixed()&&!component.IsSuppressed()&&!component.IsHidden(true),"ASSEMBLY_REOPEN_COMPONENT_STATE_FAILED component="+component.Name2+" path="+path+" transform=["+String.Join(",",actual.Select(value=>value.ToString("R",Invariant)))+"] candidateCount=1 candidateRoles=["+placement.Role+"]");performance.Add("component_state_and_transform_readback",phase);
                    phase=performance.Now;string sourceHash=Hash(path),configuration=component.ReferencedConfiguration;performance.Add("instance_source_hash_and_configuration",phase);
                    phase=performance.Now;object info; object[] sources=Items(component.GetBodies3(0,out info)); Need(sources.Length==1,"ASSEMBLY_REOPEN_COMPONENT_ONE_BODY_REQUIRED " + placement.Role); var bodyRows=new List<object>();int? bodyInfo=null;string bodyInfoError=null;
                    try {object[] values=Items(info);if(values.Length==sources.Length&&values[0] is int)bodyInfo=(int)values[0];else bodyInfoError="BODY_INFO_NOT_ONE_NATIVE_INT";}
                    catch(Exception error){bodyInfoError=error.Message;}performance.Add("native_component_bodies_and_info",phase);
                    ModelDoc2 sourceDoc=component.GetModelDoc2() as ModelDoc2;
                    try
                    {
                        foreach(object sourceRaw in sources)
                        {
                            var source=(Body2)sourceRaw; Body2 body=null; MathTransform native=null; WorldBody world=null;
                            try
                            {
                                phase=performance.Now;native=component.GetTotalTransform(true);double[] totalTransform=null;string nativeTransformError=null;
                                try {if(native!=null)totalTransform=Numbers(native.ArrayData);}catch(Exception error){nativeTransformError=error.Message;}
                                body=source.Copy() as Body2; Need(body!=null&&native!=null&&body.ApplyTransform(native),"ASSEMBLY_WORLD_BODY_TRANSFORM_FAILED " + placement.Role);
                                world=new WorldBody {Body=body,Component=component.Name2,Kind=placement.Kind,Side=placement.Side,DoorIndex=placement.Index,Name=source.Name,Path=path,Box=Bounds(body)}; body=null; performance.Add("native_world_copy_transform_and_six_direction_bounds",phase);
                                int sourceFaceCount=source.GetFaceCount(),sourceEdgeCount=source.GetEdgeCount();
                                bool normalBody=bodyInfo==(int)swBodyInfo_e.swNormalBody_e,eligible=normalBody&&sourceDoc!=null&&sourceFaceCount>0&&!String.IsNullOrEmpty(configuration)&&CacheTransformAvailable(totalTransform)&&SameTransform(totalTransform,actual),reused=false;
                                if(normalBody)performance.NormalBodies++;string cacheReason=eligible?"no_same_key_template":!normalBody?"normal_body_not_confirmed":sourceDoc==null?"source_document_unavailable":String.IsNullOrEmpty(configuration)?"configuration_unavailable":"native_total_transform_unverified",cacheError=null,cacheStoreError=null;var identityChecks=new List<object>();TopologyTemplate template=null;double[] deltaMm=null;object boundsCheck=null;
                                phase=performance.Now;
                                if(eligible)foreach(TopologyTemplate candidate in topologyTemplates.Where(v=>String.Equals(v.Path,path,StringComparison.OrdinalIgnoreCase)&&v.Sha256==sourceHash&&v.Configuration==configuration&&SameRotationScale(v.Transform,totalTransform)))
                                {
                                    try
                                    {
                                        bool docSameIUnknown=SameNativeObject(candidate.Source,sourceDoc);int equality=c.Sw.IsSame(candidate.Source,sourceDoc);
                                        bool countsMatch=candidate.FaceCount==sourceFaceCount&&candidate.EdgeCount==sourceEdgeCount;
                                        bool sameIUnknown=CanReuseTopologySourceIdentity(docSameIUnknown,equality,candidate.FaceCount,sourceFaceCount,candidate.EdgeCount,sourceEdgeCount);
                                        identityChecks.Add(new {sourceIdentity=candidate.Role,sourceComponent=candidate.World.Component,sameIUnknown=sameIUnknown,documentSameIUnknown=docSameIUnknown,swObjectEquality=equality,faceCountMatch=candidate.FaceCount==sourceFaceCount,edgeCountMatch=candidate.EdgeCount==sourceEdgeCount,disabled=candidate.Disabled});
                                        if(sameIUnknown){template=candidate;break;}cacheReason="native_source_identity_different_or_unverified";
                                    }
                                    catch(Exception error){cacheReason="native_source_identity_check_failed";cacheError=error.Message;}
                                }
                                if(template!=null)
                                {
                                    cacheReason=template.Disabled?"template_disabled_after_reuse_failure":"same_source_translation_candidate";
                                    if(!template.Disabled)
                                    {
                                        deltaMm=Enumerable.Range(0,3).Select(a=>(totalTransform[a+9]-template.Transform[a+9])*1000).ToArray();double[] predictedBounds=TranslateBox(template.World.Box,deltaMm),difference=world.Box.Select((v,a)=>v-predictedBounds[a]).ToArray();bool boundsPass=difference.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)&&Math.Abs(v)<=MmTolerance);
                                        boundsCheck=new {sourceIdentity=template.Role,sourceComponent=template.World.Component,sourceActualSha256=template.Sha256,sourceReferencedConfiguration=template.Configuration,sourceNativeTotalTransform=template.Transform,sourceNormalBodyConfirmed=true,sourceNativeExactBodyBoundsMm=template.World.Box,targetIdentity=placement.Role,targetComponent=world.Component,targetNativeTotalTransform=totalTransform,targetNormalBodyConfirmed=normalBody,targetNativeExactBodyBoundsMm=world.Box,translatedSourceNativeExactBodyBoundsMm=predictedBounds,targetMinusTranslatedSourceBoundsMm=difference,translationMm=deltaMm,toleranceMm=MmTolerance,api="IBody2.GetExtremePoint in six directions for each instance; no approximate face box equivalence",pass=boundsPass};
                                        if(!boundsPass){template.Disabled=true;cacheReason="native_exact_body_bounds_translation_counterexample";performance.BoundsCounterexamples.Add(boundsCheck);}
                                        else try {CopyTranslatedTopology(template,world,deltaMm);reused=true;cacheReason=null;performance.TopologyCacheHits++;}
                                        catch(Exception error){world.Cylinders.Clear();world.Planes.Clear();template.Disabled=true;cacheReason="topology_translation_copy_failed";cacheError=error.Message;}
                                    }
                                }
                                performance.Add("topology_cache_identity_bounds_and_translation",phase);
                                if(!reused)
                                {
                                    performance.Fallback(cacheReason);phase=performance.Now;performance.NativeSurfaceScans++;CaptureNativeSurfaces(world);performance.Add("native_surface_and_boundary_scan",phase);
                                    if(eligible&&template==null)
                                    {
                                        phase=performance.Now;object retained=null;
                                        try
                                        {
                                            retained=RetainTopologySource(sourceDoc);Need(retained!=null&&!Object.ReferenceEquals(retained,sourceDoc)&&SameNativeObject(retained,sourceDoc)&&c.Sw.IsSame(retained,sourceDoc)==(int)swObjectEquality.swObjectSame,"ASSEMBLY_CACHE_RETAINED_SOURCE_IDENTITY_UNVERIFIED");
                                            topologyTemplates.Add(new TopologyTemplate {Source=retained,World=world,Role=placement.Role,Path=path,Sha256=sourceHash,Configuration=configuration,Transform=(double[])totalTransform.Clone(),FaceCount=sourceFaceCount,EdgeCount=sourceEdgeCount});retained=null;performance.TopologyCacheEntries=topologyTemplates.Count;
                                        }
                                        catch(Exception error){cacheStoreError=error.Message;}
                                        finally{Release(retained);}performance.Add("topology_cache_source_retention",phase);
                                    }
                                }
                                Release(world.Body); world.Body=null;
                                phase=performance.Now;
                                bodyRows.Add(new {bodyName=world.Name,boxMm=world.Box,topologyReadback=new {mode=reused?"same_native_source_translation_reuse":"native_transformed_body_scan",independentNativeTopologyReadbackForThisInstance=!reused,normalBodyInfo=bodyInfo,normalBodyConfirmed=normalBody,bodyInfoError=bodyInfoError,cacheEligible=eligible,cacheFallbackReason=cacheReason,cacheError=cacheError,cacheStoreError=cacheStoreError,nativeTotalTransform=totalTransform,nativeTransformError=nativeTransformError,sourceIdentity=reused?template.Role:null,sourceComponent=reused?template.World.Component:null,sourceActualSha256=reused?template.Sha256:null,sourceReferencedConfiguration=reused?template.Configuration:null,sourceNativeTotalTransform=reused?template.Transform:null,sourceNativeObjectIdentityChecks=identityChecks,translationMm=reused?deltaMm:null,nativeSixDirectionBoundsCheck=boundsCheck,nativeFaceAndEdgeIdsReferToSourceReadback=reused},cylinders=world.Cylinders.Select(v=>v.Evidence()).ToArray(),planarFaces=world.Planes.Select(v=>v.Evidence()).ToArray(),zNormalPlanarFaceBoxesMm=world.Planes.Where(v=>Math.Abs(v.Normal[2])>.999999).Select(v=>v.Box).ToArray(),faceBoxesAreApproximateCandidatesOnly=true}); worlds.Add(world); performance.Add("instance_evidence_materialization",phase);
                                if(placement.Kind=="panel")ValidateWorldPanel(c,placement,world.Box);
                                if(placement.Kind=="shelf"||placement.Kind=="crossbar")ValidateWorldBoundary(c,placement,world.Box);
                            }
                            finally { Release(native); Release(body); if(world!=null&&world.Body!=null){Release(world.Body);world.Body=null;} Release(source); }
                        }
                    }
                    finally { Release(sourceDoc); }
                    instances.Add(new {identity=placement.Role,component=component.Name2,componentNameBeforeRename=placement.OriginalName,componentNameAfterRename=placement.ActualName,componentNameAfterReopen=component.Name2,path=path,sha256=sourceHash,side=placement.Side,index=placement.Index,kind=placement.Kind,configuration=configuration,fixedState=component.IsFixed(),transform=actual,transformPass=true,bodies=bodyRows});if((i+1)%15==0||i+1==components.Length)performance.Write(c);
                }
                c.Evidence["instances"]=instances;c.Evidence["nativeReadbackPass"]=true;c.Evidence["nativeTopologyIndependentlyScannedForEveryInstance"]=performance.TopologyCacheHits==0;phase=performance.Now;c.Evidence["hingeInterfaceMeasurements"]=MeasureHingeInterfaces(c,worlds);performance.Add("hinge_interface_measurements",phase);performance.Write(c);
                phase=performance.Now;c.Evidence["interference"]=MeasureInterference(c,model,worlds);performance.Add("full_native_interference",phase);RequireCompleteInterference(c.Evidence["interference"],components.Length);c.Evidence["interferenceClassification"]=ClassifyInterferenceFromEvidence(c.Evidence["interference"],worlds);performance.Write(c);
                phase=performance.Now;var metadata=new List<object>();c.Evidence["documentMetadata"]=metadata;c.Evidence["documentMetadataReadbackPass"]=false;
                for(int i=0;i<components.Length;i++)metadata.Add(ReadDocumentMetadataCore(c,(Component2)components[i],matched[i],metadataCache));
                c.Evidence["documentMetadataReadbackPass"]=metadata.Count==components.Length;performance.Add("document_metadata_readback_and_configuration_restore",phase);
                c.Evidence["reopen"]=new {pass=true,coldSession=c.ColdSession,assemblySha256=hash,componentCount=components.Length,cadPrivateMemoryMb=CadMemoryMb(c.Sw)};performance.Write(c);
            }
            finally {foreach(object component in components)Release(component);}
        }
        finally
        {
            phase=performance.Now;try {foreach(TopologyTemplate template in topologyTemplates)Release(template.Source);foreach(MetadataCacheEntry entry in metadataCache.Values)Release(entry.Model);foreach(WorldBody body in worlds)Release(body.Body);c.Close(model);c.CloseOutputDocuments();ReclaimComWrappers();}
            finally {performance.Add("native_document_and_cache_cleanup",phase);performance.Write(c);}
        }
        Need(Hash(c.AssemblyPath)==hash,"ASSEMBLY_READONLY_REOPEN_CHANGED_FILE");
    }

    static void ValidateWorldPanel(BuildContext c,Placement placement,double[] box)
    {
        var row=Items(Map(c.Layout["rowsByColumn"])[placement.Side]).Select(Map).Single(v=>(int)Number(v,"index")==placement.Index);
        Need(Near(box[1],Number(row,"bottomYmm"))&&Near(box[4],Number(row,"topYmm"))&&Near((box[0]+box[3])/2,placement.Transform[9]*1000)&&Near(box[3]-box[0],c.DoorWidth)&&Near(box[2],-15)&&Near(box[5],0),"ASSEMBLY_PANEL_WORLD_GEOMETRY_FAILED " + placement.Role);
    }
    static void ValidateWorldBoundary(BuildContext c,Placement placement,double[] box)
    {
        var boundary=Items(Map(c.Layout["boundaries"])[placement.Side]).Select(Map).Single(v=>(int)Number(v,"index")==placement.Index);
        string field=placement.Kind=="shelf"?"shelfCenterYmm":"crossbarCenterYmm";
        // The shelf/frame native datum is the nominal plate centre, not the bbox midpoint of its folded returns.
        double grid=c.Height-1917+152.5-(c.Height-87)/12,sourceDatum=(placement.Kind=="shelf"?1705:1700)+grid;
        Need(Near(placement.Transform[10]*1000+sourceDatum,Number(boundary,field)),"ASSEMBLY_BOUNDARY_WORLD_DATUM_FAILED " + placement.Role);
        Need(box[1]<=Number(boundary,field)+30&&box[4]>=Number(boundary,field)-30,"ASSEMBLY_BOUNDARY_ACTUAL_GEOMETRY_DISCONNECTED " + placement.Role);
    }

    static object MeasureHingeInterfaces(BuildContext c,List<WorldBody> worlds)
    {
        var rows=new List<object>();
        foreach(string side in new[]{"L","R"})foreach(var row in Items(Map(c.Layout["rowsByColumn"])[side]).Select(Map))
        {
            int index=(int)Number(row,"index"); double sign=side=="L"?1:-1,centreY=Number(row,"centerYmm"),bottom=Number(row,"bottomYmm"),top=Number(row,"topYmm");
            WorldBody panel=worlds.Single(v=>v.Side==side&&v.DoorIndex==index&&v.Kind=="panel"),stiffener=worlds.Single(v=>v.Side==side&&v.DoorIndex==index&&v.Kind=="stiffener");
            double centreX=(panel.Box[0]+panel.Box[3])/2,axisX=centreX-(c.DoorWidth/2-10)*sign,lockX=centreX+(c.DoorWidth/2-6)*sign;
            double[] hingeAxis={0,1,0},hingePoint={axisX,centreY,-7};
            var hingeFaces=panel.Cylinders.Where(v=>Near(v.Data[6]*1000,5.1)&&AxisDistance(v,hingePoint,hingeAxis)<=MmTolerance).ToArray();
            double[] hingeY={bottom,bottom+.8,top-.8,top}; var hingeLandmarks=new List<object>(); bool hingeSurfacesVerified=hingeFaces.Length==2&&hingeFaces.All(v=>v.InnerWall&&v.CompleteFiniteFace),hingePass=hingeSurfacesVerified;
            foreach(double y in hingeY)
            {
                var matches=hingeFaces.SelectMany(v=>v.Circles).Where(v=>v.Complete&&Length(Subtract(v.CenterMm,new[]{axisX,y,-7}))<=MmTolerance&&Near(v.Data[6]*1000,5.1)&&Parallel(v.Normal,hingeAxis)).ToArray();
                hingePass=hingePass&&matches.Length==1;
                hingeLandmarks.Add(new {expectedCentreMm=new[]{axisX,y,-7},expectedRadiusMm=5.1,matchCount=matches.Length,pass=hingeSurfacesVerified&&matches.Length==1,matchedBoundaryCircles=matches.Select(v=>v.Evidence()).ToArray()});
            }
            var parts=new List<object>(); bool all=true,unambiguous=true;
            foreach(string kind in new[]{"hinge_latch_bottom","hinge_latch_top","bushing_top","bushing_bottom","hinge_pin"})
            {
                WorldBody body=worlds.Single(v=>v.Side==side&&v.DoorIndex==index&&v.Kind==kind);
                var aligned=body.Cylinders.Where(v=>AxisDistance(v,hingePoint,hingeAxis)<=.05).ToArray(); all=all&&aligned.Length>0;
                var complete=aligned.Where(v=>v.CompleteFiniteFace).ToArray(); int unresolved=aligned.Count(v=>!v.CompleteFiniteFace||(!v.InnerWall&&!(v.FaceNormal!=null&&v.RadialNormalDot>.999999))); unambiguous=unambiguous&&complete.Length==1&&unresolved==0;
                parts.Add(new {kind=kind,component=body.Component,candidateCylindersPresent=aligned.Length>0,candidateSearchToleranceMm=.05,completeFiniteCandidateCount=complete.Length,unresolvedCandidateCount=unresolved,uniqueWithinCompleteFiniteCandidates=complete.Length==1,uniqueFiniteCandidate=complete.Length==1&&unresolved==0,candidates=aligned.Select(v=>new {surface=v.Evidence(),radialAxisOffsetMm=AxisDistance(v,hingePoint,hingeAxis),positiveWorldYIntervalMm=WorldAxisInterval(v,hingeAxis),relativeToPanelHoleCylinders=hingeFaces.Select(h=>CylinderRelation(h,v,hingeAxis)).ToArray()}).ToArray(),allCylinderCount=body.Cylinders.Count,mechanicalFitPass=(bool?)null});
            }
            int count=Items(Map(c.Layout["rowsByColumn"])[side]).Length;
            var frameHoles=new List<object>();
            foreach(bool lower in new[]{true,false})
            {
                bool end=lower?index==1:index==count; string leaf=lower?"门框 下.sldprt":"门框 上.sldprt"; int boundary=lower?index-1:index;
                var owners=worlds.Where(v=>end?v.Kind=="door_frame"&&Path.GetFileName(v.Path).Equals(leaf,StringComparison.OrdinalIgnoreCase):(v.Kind=="shelf"||v.Kind=="crossbar")&&v.Side==side&&v.DoorIndex==boundary).ToArray();
                var candidates=new List<object>(); var ownerRows=new List<object>(); int selected=0,unresolvedCandidates=0;
                foreach(WorldBody owner in owners)
                {
                    var aligned=owner.Cylinders.Where(v=>AxisDistance(v,hingePoint,hingeAxis)<=.05).ToArray();
                    var internalCandidates=aligned.Where(v=>v.InnerWall).ToArray(); var finite=internalCandidates.Where(v=>v.CompleteFiniteFace).ToArray(); int unresolved=aligned.Count(v=>!v.CompleteFiniteFace||(!v.InnerWall&&!(v.FaceNormal!=null&&v.RadialNormalDot>.999999))); selected+=finite.Length; unresolvedCandidates+=unresolved;
                    ownerRows.Add(new {owner=HingeOwner(owner),axisCandidateCount=aligned.Length,internalCylinderCandidateCount=internalCandidates.Length,completeFiniteInternalCandidateCount=finite.Length,unresolvedCandidateCount=unresolved,uniqueWithinCompleteFiniteCandidates=finite.Length==1,uniqueFiniteCandidate=finite.Length==1&&unresolved==0,candidates=aligned.Select(v=>new {surface=v.Evidence(),radialAxisOffsetMm=AxisDistance(v,hingePoint,hingeAxis),positiveWorldYIntervalMm=WorldAxisInterval(v,hingeAxis)}).ToArray()});
                    foreach(Cylinder hole in finite)candidates.Add(new {component=owner.Component,path=owner.Path,kind=owner.Kind,side=owner.Side,index=owner.DoorIndex,surface=hole.Evidence(),positiveWorldYIntervalMm=WorldAxisInterval(hole,hingeAxis),relativeToDoorHoles=hingeFaces.Select(h=>CylinderRelation(h,hole,hingeAxis)).ToArray(),relativeToActualHingePinCylinders=worlds.Where(v=>v.Side==side&&Math.Abs(v.DoorIndex-index)<=1&&v.Kind=="hinge_pin").SelectMany(v=>v.Cylinders.Where(p=>p.CompleteFiniteFace&&p.FaceNormal!=null&&p.RadialNormalDot>.999999&&AxisDistance(p,hingePoint,hingeAxis)<=.05).Select(p=>new {owner=HingeOwner(v),relation=CylinderRelation(hole,p,hingeAxis)})).ToArray()});
                }
                frameHoles.Add(new {end=lower?"bottom":"top",requiredOwner=end?leaf:"per-column shelf and crossbar boundary "+boundary,ownerCount=owners.Length,expectedOwnerCount=end?1:2,owners=ownerRows,internalCylinderCandidateCount=selected,unresolvedCandidateCount=unresolvedCandidates,uniqueWithinCompleteFiniteCandidates=owners.Length==(end?1:2)&&selected==1,identityUnambiguous=owners.Length==(end?1:2)&&selected==1&&unresolvedCandidates==0,candidates=candidates,mechanicalFitPass=(bool?)null});
            }
            var contacts=new List<object>();
            foreach(NativePlane a in panel.Planes.Where(v=>Math.Abs(v.Normal[2])>.999999))foreach(NativePlane b in stiffener.Planes.Where(v=>Math.Abs(v.Normal[2])>.999999))
                if(a.Box!=null&&b.Box!=null&&Math.Min(a.Box[3],b.Box[3])-Math.Max(a.Box[0],b.Box[0])>0&&Math.Min(a.Box[4],b.Box[4])-Math.Max(a.Box[1],b.Box[1])>0)
                {
                    double separation=Dot(Subtract(b.PointMm,a.PointMm),a.Normal);
                    if(Math.Abs(separation)<=MmTolerance)contacts.Add(new {panelFace=a.Evidence(),stiffenerFace=b.Evidence(),signedPlaneSeparationMm=separation,faceNormalDot=Dot(a.Normal,b.Normal),approximateBoundsOverlapCandidateOnly=true,trimmedFaceContactPass=(bool?)null});
                }
            double[] lockAxis={0,0,1},lockPoint={lockX,centreY,0};
            var lockHoles=panel.Cylinders.Where(v=>Near(v.Data[6]*1000,2)&&AxisDistance(v,lockPoint,lockAxis)<=MmTolerance).ToArray();
            var lockCircles=lockHoles.SelectMany(v=>v.Circles).Where(v=>v.Complete&&Near(v.CenterMm[0],lockX)&&Near(v.CenterMm[1],centreY)&&Parallel(v.Normal,lockAxis)).ToArray();
            // T1 native left/right circle-edge readback places this folded return hole at Z=-15/-14.2 mm.
            double[] expectedLockZ={-15,-14.2};
            bool lockPass=lockHoles.Length==1&&lockHoles[0].InnerWall&&lockHoles[0].CompleteFiniteFace&&lockCircles.Length==2&&expectedLockZ.All(z=>lockCircles.Count(v=>Near(v.CenterMm[2],z))==1);
            WorldBody[] tongues=worlds.Where(v=>v.Side==side&&v.DoorIndex==index&&v.Kind=="mechanical_lock_tongue").ToArray();
            var lockRelations=tongues.Select(v=>new {component=v.Component,path=v.Path,exactBodyExtremeBoundsMm=v.Box,cylinders=v.Cylinders.Select(t=>new {surface=t.Evidence(),relativeToLockHoleCylinders=lockHoles.Select(h=>CylinderRelation(h,t,lockAxis)).ToArray(),boundaryCircleCentresRelativeToLockCircles=t.Circles.SelectMany(tedge=>lockCircles.Select(hedge=>new {tongueEdgeIdentity=tedge.Identity,lockHoleEdgeIdentity=hedge.Identity,centreDeltaMm=Subtract(tedge.CenterMm,hedge.CenterMm),radiusDifferenceMm=hedge.Data[6]*1000-tedge.Data[6]*1000,normalDot=Dot(tedge.Normal,hedge.Normal)})).ToArray()}).ToArray(),planeOffsetsFromLockBoundaryCircles=v.Planes.Select(p=>new {plane=p.Evidence(),offsets=lockCircles.Select(h=>new {lockHoleEdgeIdentity=h.Identity,signedDistanceToTongueInfinitePlaneMm=Dot(Subtract(h.CenterMm,p.PointMm),p.Normal)}).ToArray()}).ToArray(),lockClosurePass=(bool?)null}).ToArray();
            rows.Add(new {side=side,index=index,expectedHingeAxisPointMm=hingePoint,expectedAxisDirection=hingeAxis,reference="verify.mjs checkLandmarks: R5.1, X=door centreX-sign*(W/2-10), Z=-7, Y=bottom/bottom+.8/top-.8/top; actual transformed native circles checked independently",panelHingeBoundaryPass=hingePass,panelHingeBoundaryChecks=hingeLandmarks,panelHingeCylinderFaces=hingeFaces.Select(v=>v.Evidence()).ToArray(),candidateCylindersPresent=all,hardwareCandidateIdentityUnambiguous=unambiguous,coaxialMeasurementsPass=(bool?)null,parts=parts,frameHoleInterfaces=frameHoles,adjacentDoorHingeSupportChains=MeasureHingeSupportChains(c,worlds,side,index,hingePoint,hingeAxis),stiffenerPanelCoplanarCandidates=contacts,lockInterface=new {expectedLockCentreXyMm=new[]{lockX,centreY},expectedLockBoundaryZmm=expectedLockZ,lockBoundaryReference="T1-matrix native circleEdges, both hands: Z=-15/-14.2 mm",expectedRadiusMm=2,panelLockBoundaryPass=lockPass,lockHoleFaces=lockHoles.Select(v=>v.Evidence()).ToArray(),actualLockBoundaryCircles=lockCircles.Select(v=>v.Evidence()).ToArray(),tongueComponentCount=tongues.Length,tongueGeometryRelations=lockRelations,lockClosurePass=(bool?)null},mechanicalFitPass=(bool?)null,acceptancePass=false,unverified="未验证：缺 [选定且唯一的有效孔/轴/锁舌配合面、轴径间隙和有符号轴向搭接工程阈值、有限平面接触区域与门锁转动全程的工程判定]"});
        }
        return rows;
    }

    static object HingeOwner(WorldBody body)
    {
        return new {component=body.Component,path=body.Path,side=body.Side,index=body.DoorIndex,kind=body.Kind};
    }
    static object MeasureHingeSupportChains(BuildContext c,List<WorldBody> worlds,string side,int index,double[] point,double[] axis)
    {
        string[] kinds={"hinge_pin","bushing_top","bushing_bottom","hinge_latch_top","hinge_latch_bottom"};
        var nearby=worlds.Where(v=>v.Side==side&&Math.Abs(v.DoorIndex-index)<=1&&kinds.Contains(v.Kind)).ToArray();
        var alignedPins=nearby.Where(v=>v.Kind=="hinge_pin").SelectMany(owner=>owner.Cylinders.Where(v=>AxisDistance(v,point,axis)<=.05).Select(surface=>new {owner=owner,surface=surface})).ToArray();
        var pins=alignedPins.Where(v=>v.surface.CompleteFiniteFace&&v.surface.FaceNormal!=null&&v.surface.RadialNormalDot>.999999).ToArray(); int unresolvedPinCandidates=alignedPins.Count(v=>!v.surface.CompleteFiniteFace||(!v.surface.InnerWall&&!(v.surface.FaceNormal!=null&&v.surface.RadialNormalDot>.999999)));
        Func<Cylinder,Cylinder,bool> intervalsOverlap=(a,b)=>{ double[] first=WorldAxisInterval(a,axis),second=WorldAxisInterval(b,axis); return first!=null&&second!=null&&Math.Min(first[1],second[1])-Math.Max(first[0],second[0])>0; };
        var owners=nearby.Select(owner=>{
            var aligned=owner.Cylinders.Where(v=>AxisDistance(v,point,axis)<=.05).ToArray(); var finite=aligned.Where(v=>v.CompleteFiniteFace).ToArray(); int unresolved=aligned.Count(v=>!v.CompleteFiniteFace||(!v.InnerWall&&!(v.FaceNormal!=null&&v.RadialNormalDot>.999999)));
            return new {owner=HingeOwner(owner),axisCandidateCount=aligned.Length,completeFiniteCandidateCount=finite.Length,completeFiniteInternalCandidateCount=finite.Count(v=>v.InnerWall),completeFiniteExternalCandidateCount=finite.Count(v=>v.FaceNormal!=null&&v.RadialNormalDot>.999999),unresolvedCandidateCount=unresolved,uniqueWithinCompleteFiniteCandidates=finite.Length==1,uniqueFiniteCandidate=finite.Length==1&&unresolved==0,candidates=aligned.Select(v=>new {surface=v.Evidence(),radialAxisOffsetMm=AxisDistance(v,point,axis),positiveWorldYIntervalMm=WorldAxisInterval(v,axis)}).ToArray()};
        }).ToArray();
        var chains=new List<object>(); int count=Items(Map(c.Layout["rowsByColumn"])[side]).Length;
        foreach(WorldBody bushing in nearby.Where(v=>v.DoorIndex==index&&(v.Kind=="bushing_top"||v.Kind=="bushing_bottom")))
        {
            bool lower=bushing.Kind=="bushing_bottom",end=lower?index==1:index==count; int boundary=lower?index-1:index; string leaf=lower?"门框 下.sldprt":"门框 上.sldprt";
            var localSupports=worlds.Where(v=>end?v.Kind=="door_frame"&&Path.GetFileName(v.Path).Equals(leaf,StringComparison.OrdinalIgnoreCase):(v.Kind=="shelf"||v.Kind=="crossbar")&&v.Side==side&&v.DoorIndex==boundary).Concat(nearby.Where(v=>v.Kind=="hinge_latch_top"||v.Kind=="hinge_latch_bottom")).ToArray();
            var alignedHoles=bushing.Cylinders.Where(v=>AxisDistance(v,point,axis)<=.05).ToArray(); var holes=alignedHoles.Where(v=>v.InnerWall&&v.CompleteFiniteFace).ToArray(); int unresolvedBushingCandidates=alignedHoles.Count(v=>!v.CompleteFiniteFace||(!v.InnerWall&&!(v.FaceNormal!=null&&v.RadialNormalDot>.999999)));
            var pinCandidates=new List<object>();
            foreach(Cylinder hole in holes)foreach(var pin in pins)
            {
                var supportRows=localSupports.Select(owner=>{
                    var aligned=owner.Cylinders.Where(v=>AxisDistance(v,point,axis)<=.05).ToArray(); var internalCandidates=aligned.Where(v=>v.InnerWall).ToArray(); var finite=internalCandidates.Where(v=>v.CompleteFiniteFace).ToArray(); int unresolved=aligned.Count(v=>!v.CompleteFiniteFace||(!v.InnerWall&&!(v.FaceNormal!=null&&v.RadialNormalDot>.999999)));
                    return new {owner=HingeOwner(owner),axisCandidateCount=aligned.Length,internalCylinderCandidateCount=internalCandidates.Length,completeFiniteInternalCandidateCount=finite.Length,unresolvedCandidateCount=unresolved,uniqueWithinCompleteFiniteCandidates=finite.Length==1,uniqueFiniteCandidate=finite.Length==1&&unresolved==0,candidates=finite.Select(v=>new {surface=v.Evidence(),positiveWorldYIntervalMm=WorldAxisInterval(v,axis),relativeToBushingHole=CylinderRelation(hole,v,axis),relativeToActualPin=CylinderRelation(v,pin.surface,axis),positiveAxialOverlapWithActualPin=intervalsOverlap(v,pin.surface),mechanicalFitPass=(bool?)null}).ToArray()};
                }).ToArray();
                pinCandidates.Add(new {bushingHole=hole.Evidence(),actualPinOwner=HingeOwner(pin.owner),actualPinSurface=pin.surface.Evidence(),sameDoorIndex=pin.owner.DoorIndex==index,bushingToActualPin=CylinderRelation(hole,pin.surface,axis),positiveAxialOverlap=intervalsOverlap(hole,pin.surface),localSupportOwners=supportRows,mechanicalFitPass=(bool?)null});
            }
            int overlapping=holes.Sum(hole=>pins.Count(pin=>intervalsOverlap(hole,pin.surface)));
            chains.Add(new {bushingOwner=HingeOwner(bushing),bushingAxisCandidateCount=alignedHoles.Length,completeFiniteInternalCandidateCount=holes.Length,unresolvedBushingCandidateCount=unresolvedBushingCandidates,uniqueBushingHoleWithinCompleteFiniteCandidates=holes.Length==1,uniqueBushingHoleCandidate=holes.Length==1&&unresolvedBushingCandidates==0,actualPinAxisCandidateCount=alignedPins.Length,actualPinFiniteCandidateCount=pins.Length,unresolvedPinCandidateCount=unresolvedPinCandidates,unresolvedCandidateCount=unresolvedBushingCandidates+unresolvedPinCandidates,spatiallyOverlappingPinCandidateCount=overlapping,uniqueWithinCompleteFiniteCandidates=holes.Length==1&&overlapping==1,uniqueSpatiallyOverlappingPinCandidate=holes.Length==1&&overlapping==1&&unresolvedBushingCandidates==0&&unresolvedPinCandidates==0,pinCandidates=pinCandidates,mechanicalFitPass=(bool?)null});
        }
        return new {candidateSide=side,currentDoorIndex=index,requestedDoorIndices=new[]{index-1,index,index+1}.Where(v=>v>=1&&v<=count).ToArray(),candidateSearchToleranceMm=.05,actualPinAxisCandidateCount=alignedPins.Length,actualPinFiniteCandidateCount=pins.Length,unresolvedPinCandidateCount=unresolvedPinCandidates,indexDoesNotEstablishFit=true,matchBasis="actual complete finite cylinders, radial axis distance and signed world-Y interval overlap; unresolved aligned cylinders prevent global uniqueness; engineering clearances remain undefined",owners=owners,bushingPinSupportChains=chains,mechanicalFitPass=(bool?)null};
    }

    static double Dot(double[] a,double[] b) { return a.Zip(b,(x,y)=>x*y).Sum(); }
    static double[] Subtract(double[] a,double[] b) { return a.Zip(b,(x,y)=>x-y).ToArray(); }
    static double Length(double[] value) { return Math.Sqrt(Dot(value,value)); }
    static double[] Unit(double[] value) { double length=Length(value); Need(value.Length==3&&length>0&&!Double.IsNaN(length)&&!Double.IsInfinity(length),"ASSEMBLY_NATIVE_DIRECTION_INVALID"); return value.Select(v=>v/length).ToArray(); }
    static bool Parallel(double[] a,double[] b) { return Math.Abs(Dot(a,b))>.999999; }
    static double AxisDistance(Cylinder value,double[] point,double[] direction)
    {
        if(!Parallel(value.Axis,direction))return Double.MaxValue;
        double[] delta=Subtract(value.PointMm,point); double projection=Dot(delta,direction);
        return Length(delta.Select((v,i)=>v-projection*direction[i]).ToArray());
    }
    static double[] WorldAxisInterval(Cylinder value,double[] direction) { return value.CompleteFiniteFace&&Parallel(value.Axis,direction)?new[]{value.Circles.Min(v=>Dot(v.CenterMm,direction)),value.Circles.Max(v=>Dot(v.CenterMm,direction))}:null; }
    static object CylinderRelation(Cylinder hole,Cylinder candidate,double[] direction)
    {
        bool parallel=Parallel(hole.Axis,candidate.Axis); double[] a=WorldAxisInterval(hole,direction),b=WorldAxisInterval(candidate,direction);
        return new {holeFaceIdentity=hole.Identity,candidateFaceIdentity=candidate.Identity,axesParallel=parallel,absoluteAxisDot=Math.Abs(Dot(hole.Axis,candidate.Axis)),radialAxisDistanceMm=parallel?(double?)AxisDistance(candidate,hole.PointMm,hole.Axis):null,holeRadiusMinusCandidateRadiusMm=hole.Data[6]*1000-candidate.Data[6]*1000,holeRadiusMinusExternalShaftRadiusMm=hole.InnerWall&&candidate.FaceNormal!=null&&candidate.RadialNormalDot>.999999?(double?)(hole.Data[6]*1000-candidate.Data[6]*1000):null,referenceAxisDirection=direction,holeAxialIntervalMm=a,candidateAxialIntervalMm=b,signedAxialOverlapMm=a!=null&&b!=null?(double?)(Math.Min(a[1],b[1])-Math.Max(a[0],b[0])):null,overlapDefinition="positive=interval overlap; negative=axial separation; not a fit acceptance threshold",holeInternalWallVerified=hole.InnerWall,candidateWallClassification=candidate.InnerWall?"internal_cylindrical_wall":candidate.FaceNormal!=null&&candidate.RadialNormalDot>.999999?"external_cylindrical_wall":"unverified",mechanicalFitPass=(bool?)null};
    }

    static object MeasureInterference(BuildContext c,ModelDoc2 model,List<WorldBody> worlds)
    {
        var collisions=new List<Dictionary<string,object>>(); var pairs=new List<object>(); var failures=new List<string>(); InterferenceDetectionMgr manager=null; bool complete=false; int declared=-1;
        var names=new HashSet<string>(worlds.Select(v=>v.Component),StringComparer.Ordinal); int measuredPairCount=0;
        try
        {
            VariableTrial.ModelClearSelection2(model,true,"MeasureInterference"); manager=((AssemblyDoc)model).InterferenceDetectionManager; Need(manager!=null,"ASSEMBLY_INTERFERENCE_MANAGER_MISSING");
            manager.TreatCoincidenceAsInterference=false; manager.TreatSubAssembliesAsComponents=false; manager.IncludeMultibodyPartInterferences=true; manager.IgnoreHiddenBodies=false; manager.ShowIgnoredInterferences=true; manager.UseTransform=false; manager.CreateFastenersFolder=false;
            object[] results=VariableTrial.InterferenceGetInterferences(manager,"MeasureInterference"); declared=VariableTrial.InterferenceGetInterferenceCount(manager,"MeasureInterference"); Need(declared==results.Length,"ASSEMBLY_INTERFERENCE_RESULT_COUNT_MISMATCH");
            foreach(object raw in results)
            {
                var interference=raw as Interference; Body2 body=null;
                try
                {
                    Need(interference!=null,"ASSEMBLY_INTERFERENCE_RESULT_UNREADABLE"); object[] components=Items(interference.Components); string[] participants=components.Cast<Component2>().Select(v=>v.Name2).ToArray();
                    try
                    {
                        Need(participants.Length==2&&participants.All(names.Contains),"ASSEMBLY_INTERFERENCE_PARTICIPANTS_UNRESOLVED");
                        double volume=interference.Volume*1e9; Need(!Double.IsNaN(volume)&&!Double.IsInfinity(volume)&&volume>=0,"ASSEMBLY_INTERFERENCE_VOLUME_MISSING");
                        body=interference.GetInterferenceBody() as Body2; double[] box=body==null?null:Bounds(body);
                        bool possible=interference.IsPossibleInterference; bool measured=!possible&&body!=null;
                        collisions.Add(new Dictionary<string,object> {{"componentA",participants[0]},{"componentB",participants[1]},{"volumeMm3",volume},{"intersectionBoundsMm",box},{"possibleInterference",possible},{"nativeIgnored",interference.Ignore},{"measurementComplete",measured},{"approved",false},{"pass",false},{"disposition",measured?"unapproved_native_interference":"unknown_native_interference"}});
                    }
                    finally { foreach(object component in components)Release(component); }
                }
                finally { Release(body); Release(interference); }
            }
            complete=true;
        }
        catch(Exception error) { failures.Add(error.ToString()); }
        finally { if(manager!=null) { try { VariableTrial.InterferenceDone(manager,"MeasureInterference"); } catch(Exception error) { failures.Add(error.ToString()); complete=false; } Release(manager); } }
        string[] componentsList=names.OrderBy(v=>v,StringComparer.Ordinal).ToArray();
        for(int i=0;i<componentsList.Length;i++)for(int j=i+1;j<componentsList.Length;j++)
        {
            string a=componentsList[i],b=componentsList[j];
            var matches=collisions.Where(v=>(Convert.ToString(v["componentA"])==a&&Convert.ToString(v["componentB"])==b)||(Convert.ToString(v["componentA"])==b&&Convert.ToString(v["componentB"])==a)).ToArray();
            var bodiesA=worlds.Where(v=>v.Component==a).ToArray(); var bodiesB=worlds.Where(v=>v.Component==b).ToArray();
            bool disjoint=bodiesA.All(x=>bodiesB.All(y=>Enumerable.Range(0,3).Any(axis=>x.Box[axis+3]<y.Box[axis]-MmTolerance||y.Box[axis+3]<x.Box[axis]-MmTolerance)));
            bool measured=complete&&matches.All(v=>Convert.ToBoolean(v["measurementComplete"],Invariant));
            bool pass=matches.Length==0&&(complete||disjoint),measurementComplete=measured||disjoint; if(measurementComplete)measuredPairCount++;
            pairs.Add(new {componentA=a,componentB=b,nativeDetectionComplete=complete,disjointExactBounds=disjoint,interferenceCount=matches.Length,volumeMm3=matches.Sum(v=>Convert.ToDouble(v["volumeMm3"],Invariant)),intersectionBoundsMm=matches.Select(v=>v["intersectionBoundsMm"]).ToArray(),measurementComplete=measurementComplete,pass=pass,status=matches.Length>0?"unapproved_interference":pass?"no_interference":"unverified"});
        }
        return new {api="IInterferenceDetectionMgr.GetInterferences / IInterference.Volume / IInterference.GetInterferenceBody",complete=complete,declaredNativeInterferenceCount=declared,componentCount=names.Count,expectedPairCount=names.Count*(names.Count-1)/2,enumeratedPairCount=pairs.Count,measuredPairCount=measuredPairCount,unknownPairCount=pairs.Count-measuredPairCount,pass=complete&&collisions.Count==0,coincidentContactIncluded=false,excludedComponentPairs=new object[0],pairs=pairs,collisions=collisions,errors=failures,approvalPolicy="No pair/type/size exemption is inherited. Every reported overlap remains unapproved."};
    }

    static double[] Bounds(Body2 body)
    {
        double[] box=new double[6];
        for(int axis=0;axis<3;axis++)
        {
            double x,y,z; Need(body.GetExtremePoint(axis==0?-1:0,axis==1?-1:0,axis==2?-1:0,out x,out y,out z),"ASSEMBLY_BODY_EXTREME_MISSING"); box[axis]=new[]{x,y,z}[axis]*1000;
            Need(body.GetExtremePoint(axis==0?1:0,axis==1?1:0,axis==2?1:0,out x,out y,out z),"ASSEMBLY_BODY_EXTREME_MISSING"); box[axis+3]=new[]{x,y,z}[axis]*1000;
            Need(!Double.IsNaN(box[axis])&&!Double.IsInfinity(box[axis])&&!Double.IsNaN(box[axis+3])&&!Double.IsInfinity(box[axis+3]),"ASSEMBLY_BODY_BOUNDS_INVALID");
        }
        return box;
    }
    static double[] BodyBounds(ModelDoc2 model)
    {
        object[] bodies=Items(((PartDoc)model).GetBodies2(0,false)); Need(bodies.Length>0,"ASSEMBLY_PART_HAS_NO_SOLIDS");
        double[] result={Double.MaxValue,Double.MaxValue,Double.MaxValue,Double.MinValue,Double.MinValue,Double.MinValue};
        foreach(object raw in bodies) { var body=(Body2)raw; try { double[] box=Bounds(body); for(int a=0;a<3;a++){result[a]=Math.Min(result[a],box[a]);result[a+3]=Math.Max(result[a+3],box[a+3]);} } finally { Release(body); } } return result;
    }
    static void RequireOneBody(ModelDoc2 model)
    {
        object[] bodies=Items(((PartDoc)model).GetBodies2(0,false));
        try { Need(bodies.Length==1,"ASSEMBLY_HARDWARE_ONE_BODY_REQUIRED"); }
        finally { foreach(object body in bodies)Release(body); }
    }
    static object PartGeometry(ModelDoc2 model)
    {
        var bodies=new List<object>();
        foreach(object raw in Items(((PartDoc)model).GetBodies2(0,false)))
        {
            var body=(Body2)raw;
            try { double[] mass=Numbers(body.GetMassProperties(1)); Need(mass.Length>3&&mass[3]>0,"ASSEMBLY_NATIVE_BODY_VOLUME_MISSING"); bodies.Add(new {name=body.Name,boxMm=Bounds(body),sheetMetal=body.IsSheetMetal(),volumeMm3=mass[3]*1e9}); }
            finally { Release(body); }
        }
        Need(bodies.Count>0,"ASSEMBLY_NATIVE_PART_BODY_MISSING"); return new {solidBodyCount=bodies.Count,bodies=bodies};
    }
    static void CaptureNativeSurfaces(WorldBody world)
    {
        int faceIndex=0;
        foreach(object raw in Items(world.Body.GetFaces()))
        {
            var face=(Face2)raw; Surface surface=null;
            try
            {
                string identity=world.Component+"/body:"+world.Name+"/face:"+(++faceIndex).ToString(Invariant);
                surface=face.GetSurface() as Surface; Need(surface!=null,"ASSEMBLY_NATIVE_SURFACE_MISSING");
                if(surface.IsCylinder())
                {
                    double[] box=CandidateFaceBox(face),parameters=Numbers(surface.CylinderParams); Need(parameters.Length==7&&parameters.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v))&&parameters[6]>0,"ASSEMBLY_CYLINDER_PARAMETERS_MISSING");
                    var cylinder=new Cylinder {Identity=identity,NativeFaceId=face.GetFaceId(),Data=parameters,Box=box,Reversed=face.FaceInSurfaceSense(),UvBounds=Numbers(face.GetUVBounds()),AreaMm2=face.GetArea()*1e6};
                    Need(cylinder.UvBounds.Length==4&&cylinder.UvBounds.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v))&&!Double.IsNaN(cylinder.AreaMm2)&&!Double.IsInfinity(cylinder.AreaMm2)&&cylinder.AreaMm2>0,"ASSEMBLY_CYLINDER_FACE_EXTENT_INVALID");
                    int edgeIndex=0;
                    foreach(object rawEdge in Items(face.GetEdges()))
                    {
                        var edge=(Edge)rawEdge; Curve curve=null;
                        try
                        {
                            cylinder.BoundaryEdgeCount++; edgeIndex++; curve=edge.GetCurve() as Curve; Need(curve!=null,"ASSEMBLY_FACE_EDGE_CURVE_MISSING");
                            if(curve.IsCircle())cylinder.Circles.Add(CaptureBoundaryCircle(edge,curve,identity,edgeIndex));
                            else if(curve.IsLine()&&IsSeamEdge(edge,face))cylinder.SeamEdgeCount++;
                            else cylinder.Errors.Add("NON_CIRCULAR_NON_SEAM_BOUNDARY "+edgeIndex);
                        }
                        catch(Exception error) { cylinder.Errors.Add("BOUNDARY_MEASUREMENT_FAILED "+edgeIndex+" "+error.Message); }
                        finally { Release(curve); Release(edge); }
                    }
                    if(cylinder.Circles.Count>0)
                    {
                        double[] point=cylinder.Circles[0].StartMm,delta=Subtract(point,cylinder.PointMm); double projection=Dot(delta,cylinder.Axis);
                        double[] radial=Unit(delta.Select((v,i)=>v-projection*cylinder.Axis[i]).ToArray());
                        cylinder.SurfaceNormal=Unit(Numbers(surface.EvaluateAtPoint(point[0]/1000,point[1]/1000,point[2]/1000)).Take(3).ToArray());
                        cylinder.FaceNormal=cylinder.SurfaceNormal.Select(v=>cylinder.Reversed?-v:v).ToArray(); cylinder.RadialNormalDot=Dot(cylinder.FaceNormal,radial);
                    }
                    bool complete=cylinder.Errors.Count==0&&cylinder.Circles.Count==2&&cylinder.Circles.All(v=>v.Complete&&Parallel(v.Normal,cylinder.Axis)&&Near(v.Data[6]*1000,cylinder.Data[6]*1000)&&AxisDistance(cylinder,v.CenterMm,v.Normal)<=MmTolerance);
                    if(complete)
                    {
                        double[] positions=cylinder.Circles.Select(v=>Dot(Subtract(v.CenterMm,cylinder.PointMm),cylinder.Axis)).OrderBy(v=>v).ToArray();
                        // IFace2.GetArea is approximate; this is conservative corroboration of the actual boundary-circle topology.
                        double expectedArea=2*Math.PI*cylinder.Data[6]*1000*(positions[1]-positions[0]);
                        complete=positions[1]-positions[0]>MmTolerance&&Near(cylinder.AreaMm2/(2*Math.PI*cylinder.Data[6]*1000),positions[1]-positions[0]);
                        if(!complete)cylinder.Errors.Add("FINITE_CYLINDER_AREA_OR_AXIAL_EXTENT_UNVERIFIED expectedAreaMm2="+expectedArea.ToString("R",Invariant));
                        else cylinder.AxialIntervalMm=positions;
                    }
                    cylinder.CompleteFiniteFace=complete; world.Cylinders.Add(cylinder);
                }
                if(surface.IsPlane())
                {
                    double[] box=CandidateFaceBox(face),plane=Numbers(surface.PlaneParams); Need(plane.Length==6&&plane.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)),"ASSEMBLY_PLANE_PARAMETERS_MISSING");
                    world.Planes.Add(new NativePlane {Identity=identity,NativeFaceId=face.GetFaceId(),Data=plane,Box=box,Reversed=face.FaceInSurfaceSense()});
                }
            }
            finally { Release(surface); Release(face); }
        }
    }
    static double[] CandidateFaceBox(Face2 face)
    {
        double[] box=Numbers(face.GetBox()).Select(v=>v*1000).ToArray();
        return box.Length==6&&box.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v))?box:null;
    }
    static BoundaryCircle CaptureBoundaryCircle(Edge edge,Curve curve,string faceIdentity,int index)
    {
        CurveParamData range=null;
        try
        {
            // Official SDK: GetCurve precedes GetCurveParams3; reversed sense uses swapped, negated U values for native length evaluation.
            range=edge.GetCurveParams3(); Need(range!=null,"ASSEMBLY_CIRCLE_EDGE_RANGE_MISSING");
            double[] circle=Numbers(curve.CircleParams),start=Numbers(range.StartPoint),end=Numbers(range.EndPoint);
            Need(circle.Length==7&&start.Length==3&&end.Length==3&&circle.Concat(start).Concat(end).All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v))&&circle[6]>0,"ASSEMBLY_CIRCLE_PARAMETERS_MISSING");
            double uMin=range.UMinValue,uMax=range.UMaxValue,curveMin,curveMax; bool closed,periodic;
            bool endParameters=curve.GetEndParams(out curveMin,out curveMax,out closed,out periodic);
            double length=curve.GetLength3(range.Sense?uMin:-uMax,range.Sense?uMax:-uMin)*1000;
            Need(new[]{uMin,uMax,curveMin,curveMax,length}.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v))&&uMax>uMin&&length>0,"ASSEMBLY_CIRCLE_EDGE_RANGE_INVALID");
            var result=new BoundaryCircle {Identity=faceIdentity+"/edge:"+index.ToString(Invariant),FaceIdentity=faceIdentity,NativeEdgeId=edge.GetID(),CurveTag=range.CurveTag,Data=circle,StartMm=start.Select(v=>v*1000).ToArray(),EndMm=end.Select(v=>v*1000).ToArray(),Parameters=new[]{uMin,uMax},Sense=range.Sense,ClosedCurve=endParameters&&closed,PeriodicCurve=endParameters&&periodic,EdgeLengthMm=length};
            result.Complete=result.ClosedCurve&&result.PeriodicCurve&&Math.Abs(uMax-uMin-2*Math.PI)<=1e-7&&Length(Subtract(result.StartMm,result.EndMm))<=MmTolerance&&Math.Abs(length-2*Math.PI*circle[6]*1000)<=MmTolerance;
            return result;
        }
        finally { Release(range); }
    }
    static bool IsSeamEdge(Edge edge,Face2 face)
    {
        object[] adjacent=Items(edge.GetTwoAdjacentFaces2());
        try { return adjacent.Length==2&&adjacent.All(v=>SameNativeObject(v,face)); }
        finally { foreach(object value in adjacent)Release(value); }
    }
    static bool SameNativeObject(object a,object b)
    {
        if(a==null||b==null)return false; IntPtr left=IntPtr.Zero,right=IntPtr.Zero;
        try { left=Marshal.GetIUnknownForObject(a); right=Marshal.GetIUnknownForObject(b); return left==right; }
        finally { if(left!=IntPtr.Zero)Marshal.Release(left); if(right!=IntPtr.Zero)Marshal.Release(right); }
    }
    static void ScanFeatureHealth(ModelDoc2 model,List<object> rows,List<string> failures)
    {
        var seen=new HashSet<int>(); int visited=0; Action<Feature,int,bool> scan=null;
        scan=(feature,depth,top)=>{
            Need(depth<32,"ASSEMBLY_FEATURE_DEPTH_LIMIT");
            while(feature!=null)
            {
                Need(visited++<20000,"ASSEMBLY_FEATURE_TRAVERSAL_LIMIT"); Feature next=null;
                try
                {
                    next=top?feature.GetNextFeature() as Feature:feature.GetNextSubFeature() as Feature;
                    if(seen.Add(feature.GetID()))
                    {
                        bool warning=false; int code=feature.GetErrorCode2(out warning); bool suppressed=feature.IsSuppressed();
                        rows.Add(new {id=feature.GetID(),name=feature.Name,type=feature.GetTypeName2(),depth=depth,suppressed=suppressed,errorCode=code,warning=warning});
                        if(!suppressed&&(code!=0||warning))failures.Add(feature.Name+" "+feature.GetTypeName2()+" "+code+"/"+warning);
                        scan(feature.GetFirstSubFeature() as Feature,depth+1,false);
                    }
                }
                finally { Release(feature); } feature=next;
            }
        };
        scan(model.FirstFeature() as Feature,0,true);
    }

    static void CheckFeatureHealth(ModelDoc2 model,BuildContext context=null,string phase=null,bool requireHealthy=true)
    {
        var rows=new List<object>(); var failures=new List<string>();
        try
        {
            ScanFeatureHealth(model,rows,failures);
            if(context!=null)context.Event(new {inspection="assembly_native_feature_health",path=model.GetPathName(),phase=phase,traversalComplete=true,featureCount=rows.Count,healthy=failures.Count==0,features=rows.ToArray(),errors=failures.ToArray(),requiredForAcceptance=requireHealthy});
        }
        catch(Exception error)
        {
            if(context!=null)context.Event(new {inspection="assembly_native_feature_health",path=model.GetPathName(),phase=phase,traversalComplete=false,featureCount=rows.Count,healthy=false,features=rows.ToArray(),errors=failures.ToArray(),error=error.ToString(),requiredForAcceptance=requireHealthy});
            throw;
        }
        Need(!requireHealthy||failures.Count==0,"ASSEMBLY_NATIVE_FEATURE_ERROR " + model.GetPathName()+" "+String.Join("; ",failures.ToArray()));
    }

    internal static void ValidateCabinetEnvelope(double widthMm,double heightMm,double depthMm,double doorWidthMm,bool allowWidthEnvelope)
    {
        bool fixedGeometry=Near(heightMm,2000)&&Near(depthMm,500)&&Near(doorWidthMm,(widthMm-126)/2);
        if(!allowWidthEnvelope)
        {
            Need((Near(widthMm,1000)||Near(widthMm,900))&&fixedGeometry,"ASSEMBLY_TRIAL_MATRIX_RANGE_REQUIRED");
            return;
        }
        // Parametric width mode holds H=2000/D=500 fixed while opening W to the T1/T2 door-width envelope [700, 1200].
        Need(widthMm>=700-MmTolerance&&widthMm<=1200+MmTolerance&&fixedGeometry,"ASSEMBLY_TRIAL_WIDTH_ENVELOPE_RANGE_REQUIRED");
    }

    internal static double[] ComputeMissingSlotCenters(IEnumerable<double> existingCentersMm,IEnumerable<double> expectedCentersMm,double toleranceMm=0.01)
    {
        Need(existingCentersMm!=null&&expectedCentersMm!=null,"FRAME_SLOT_INPUT_REQUIRED");
        double[] existing=existingCentersMm.ToArray();
        double[] expected=expectedCentersMm.ToArray();
        Need(existing.Concat(expected).All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)),"FRAME_SLOT_FINITE_NUMBER_REQUIRED");
        var missing=new List<double>();
        foreach(double target in expected.OrderBy(v=>v))
        {
            if(!existing.Any(v=>Math.Abs(v-target)<=toleranceMm)&&!missing.Any(v=>Math.Abs(v-target)<=toleranceMm))missing.Add(target);
        }
        return missing.ToArray();
    }

    internal static double[] ComputeLockSeedMove(IEnumerable<double> activeRowsMm,double currentSeedSlotRowYmm,double currentD2Mm,out double targetSeedRowYmm,out double targetD2Mm)
    {
        Need(activeRowsMm!=null,"ASSEMBLY_LOCK_ROWS_REQUIRED");
        double[] rows=activeRowsMm.OrderBy(v=>v).ToArray();
        Need(rows.Length>0&&rows.Concat(new[]{currentSeedSlotRowYmm,currentD2Mm}).All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)),"ASSEMBLY_LOCK_SEED_INPUT_INVALID");
        targetSeedRowYmm=rows.Where(row=>row<=currentSeedSlotRowYmm+0.01).DefaultIfEmpty(rows[0]).Last();
        double seedY=targetSeedRowYmm;
        targetD2Mm=currentD2Mm+(currentSeedSlotRowYmm-seedY);
        Need(targetD2Mm>0,"ASSEMBLY_LOCK_SEED_TARGET_DIMENSION_INVALID");
        return rows.Where(row=>Math.Abs(row-seedY)>0.001).ToArray();
    }

    static string InferKindFromComponentName(string componentName)
    {
        if(String.IsNullOrEmpty(componentName))return "other";
        if(componentName.IndexOf("_panel-",StringComparison.OrdinalIgnoreCase)>=0)return "panel";
        if(componentName.StartsWith("trial_hinge_latch_plate-",StringComparison.OrdinalIgnoreCase))return "hinge_latch_bottom";
        if(componentName.StartsWith("trial_top_latch_left-",StringComparison.OrdinalIgnoreCase)||componentName.StartsWith("trial_top_latch_right-",StringComparison.OrdinalIgnoreCase))return "hinge_latch_top";
        if(componentName.StartsWith("trial_circlip-",StringComparison.OrdinalIgnoreCase))return "circlip";
        if(componentName.StartsWith("trial_door_hinge_pin-",StringComparison.OrdinalIgnoreCase))return "hinge_pin";
        return "other";
    }

    static string ResolveComponentKind(string componentName,IDictionary<string,string> kindByComponent)
    {
        string kind;
        if(kindByComponent!=null&&!String.IsNullOrEmpty(componentName)&&kindByComponent.TryGetValue(componentName,out kind)&&!String.IsNullOrEmpty(kind))return kind;
        return InferKindFromComponentName(componentName);
    }

    static bool IsUnorderedKindPair(string kindA,string kindB,Func<string,bool> first,Func<string,bool> second)
    {
        return (first(kindA)&&second(kindB))||(first(kindB)&&second(kindA));
    }

    internal static object ClassifyInterferenceCollisions(IEnumerable<Dictionary<string,object>> collisions,IDictionary<string,string> kindByComponent)
    {
        const double V35LatchFilletMaxVolumeMm3=0.05;
        const double V35CirclipGrooveMaxVolumeMm3=1.2;
        var classified=new List<object>();
        int v35LatchCount=0,v35CirclipCount=0,structuralCount=0;
        if(collisions!=null)
        {
            foreach(var item in collisions)
            {
                string compA=Convert.ToString(item["componentA"],Invariant),compB=Convert.ToString(item["componentB"],Invariant);
                double volume=Number(item,"volumeMm3");
                string kindA=ResolveComponentKind(compA,kindByComponent),kindB=ResolveComponentKind(compB,kindByComponent);
                string category;
                if(IsUnorderedKindPair(kindA,kindB,k=>k=="panel",k=>k=="hinge_latch_bottom"||k=="hinge_latch_top")&&volume<=V35LatchFilletMaxVolumeMm3)
                {
                    category="v35_door_hinge_latch_bend_fillet";v35LatchCount++;
                }
                else if(IsUnorderedKindPair(kindA,kindB,k=>k=="circlip"||k=="circlip_top"||k=="circlip_bottom",k=>k=="hinge_pin")&&volume<=V35CirclipGrooveMaxVolumeMm3)
                {
                    category="v35_circlip_hinge_pin_groove_mismatch";v35CirclipCount++;
                }
                else
                {
                    category="structural_collision";structuralCount++;
                }
                classified.Add(new {componentA=compA,componentB=compB,kindA=kindA,kindB=kindB,volumeMm3=volume,category=category,intersectionBoundsMm=item.ContainsKey("intersectionBoundsMm")?item["intersectionBoundsMm"]:null});
            }
        }
        return new {policy="Diagnostic classification only; does NOT approve or waive any reported SolidWorks interference.",totalCollisionCount=classified.Count,baselineV35CollisionCount=v35LatchCount+v35CirclipCount,v35DoorHingeLatchBendFilletCount=v35LatchCount,v35CirclipHingePinGrooveMismatchCount=v35CirclipCount,structuralCollisionCount=structuralCount,structuralCollisionsPass=structuralCount==0,classifiedCollisions=classified.ToArray()};
    }

    static object ClassifyInterferenceFromEvidence(object interferenceEvidence,List<WorldBody> worlds)
    {
        var map=Map(Json.DeserializeObject(Json.Serialize(interferenceEvidence)));
        var kindByComponent=worlds.ToDictionary(w=>w.Component,w=>w.Kind,StringComparer.Ordinal);
        var collisions=Items(map["collisions"]).Select(Map).ToArray();
        return ClassifyInterferenceCollisions(collisions,kindByComponent);
    }

    static object MakeStructurePartRecord(string role,string path,string source,List<object> changes,object links,object before,object after)
    {
        return new {role=role,path=path,source=source,sha256=Hash(path),widthInterfaces=changes,nativeLinks=links,before=before,after=after,geometryPass=true};
    }

    static void UpdateStructurePartRecord(BuildContext c,ModelDoc2 model,string path,Dictionary<string,int> indexByPath,Dictionary<string,Tuple<string,string,List<object>,object,object>> metaByPath)
    {
        int idx;Tuple<string,string,List<object>,object,object> meta;
        if(!indexByPath.TryGetValue(path,out idx)||!metaByPath.TryGetValue(path,out meta))return;
        c.Parts[idx]=MakeStructurePartRecord(meta.Item1,path,meta.Item2,meta.Item3,meta.Item4,meta.Item5,PartGeometry(model));
        c.Write();
    }

    static Feature FindFeatureByName(ModelDoc2 model,string name)
    {
        Feature feature=model.FirstFeature() as Feature;
        while(feature!=null)
        {
            if(String.Equals(feature.Name,name,StringComparison.Ordinal))return feature;
            Feature next=feature.GetNextFeature() as Feature;
            Release(feature);
            feature=next;
        }
        return null;
    }

    static string[] ListTopFeatureNames(ModelDoc2 model)
    {
        var names=new List<string>();
        Feature feature=model.FirstFeature() as Feature;
        while(feature!=null)
        {
            Feature next=null;
            try
            {
                names.Add(feature.Name);
                next=feature.GetNextFeature() as Feature;
            }
            finally { Release(feature); }
            feature=next;
        }
        return names.ToArray();
    }

    static int NextAvailablePatternIndex(IEnumerable<string> featureNames,string prefix)
    {
        int max=0;
        foreach(string name in featureNames)
        {
            if(!name.StartsWith(prefix,StringComparison.Ordinal))continue;
            int parsed;
            if(Int32.TryParse(name.Substring(prefix.Length),NumberStyles.Integer,Invariant,out parsed)&&parsed>max)max=parsed;
        }
        return max+1;
    }

    static void SetFeatureSuppression(ModelDoc2 model,string name,bool suppress,string context)
    {
        Feature feature=FindFeatureByName(model,name);
        try
        {
            Need(feature!=null,"ASSEMBLY_FEATURE_MISSING "+context+"/"+name);
            if(feature.IsSuppressed()==suppress)return;
            int action=suppress?(int)swFeatureSuppressionAction_e.swSuppressFeature:(int)swFeatureSuppressionAction_e.swUnSuppressFeature;
            Need(feature.SetSuppression2(action,(int)swInConfigurationOpts_e.swThisConfiguration,null),"ASSEMBLY_FEATURE_SUPPRESSION_FAILED "+context+"/"+name);
        }
        finally { Release(feature); }
    }

    static void ActivatePartForEdit(BuildContext c,ModelDoc2 model)
    {
        VariableTrial.SwDocumentVisible(c.Sw,true,(int)swDocumentTypes_e.swDocPART,"ActivatePartForEdit");
        int activateError=0;
        VariableTrial.SwActivateDoc3(c.Sw,VariableTrial.ModelGetTitle(model,"ActivatePartForEdit"),false,0,out activateError,"ActivatePartForEdit");
    }

    static void EnsureSteelMaterial(BuildContext c,ModelDoc2 model,string configuration)
    {
        var part=(PartDoc)model;
        string beforeDatabase,beforeMaterial=part.GetMaterialPropertyName2(configuration,out beforeDatabase);
        if(!String.IsNullOrEmpty(beforeMaterial))return;
        string activeConfig=model.ConfigurationManager.ActiveConfiguration.Name;
        if(!String.Equals(activeConfig,configuration,StringComparison.Ordinal))
            Need(VariableTrial.ModelShowConfiguration2(model,configuration,"EnsureSteelMaterial"),"ASSEMBLY_MATERIAL_CONFIGURATION_FAILED "+VariableTrial.ModelGetPathName(model,"EnsureSteelMaterial")+"/"+configuration);
        string[] installedDbs=VariableTrial.SwGetMaterialDatabases(c.Sw,"EnsureSteelMaterial").Select(v=>Convert.ToString(v,Invariant)).Where(s=>!String.IsNullOrEmpty(s)).ToArray();
        var candidates=new List<Tuple<string,string>> { Tuple.Create("云绅材料库","冷轧钢板") };
        // Fall back to SolidWorks' built-in 7870 kg/m^3 cold-rolled carbon steel when the custom factory library is not installed on this workstation.
        foreach(string db in installedDbs.Where(d=>d.IndexOf("solidworks materials",StringComparison.OrdinalIgnoreCase)>=0)
            .Concat(new[]{"solidworks materials","SOLIDWORKS Materials",""}))
        {
            candidates.Add(Tuple.Create(db,"AISI 1020 钢，冷轧"));
            candidates.Add(Tuple.Create(db,"AISI 1020 Steel, Cold Rolled"));
        }
        string afterDatabase="",afterMaterial="";
        foreach(var candidate in candidates)
        {
            part.SetMaterialPropertyName2(configuration,candidate.Item1,candidate.Item2);
            afterMaterial=part.GetMaterialPropertyName2(configuration,out afterDatabase);
            if(!String.IsNullOrEmpty(afterMaterial))break;
        }
        Need(!String.IsNullOrEmpty(afterMaterial),"ASSEMBLY_MATERIAL_ASSIGN_FAILED "+model.GetPathName()+"/"+configuration+" db="+afterDatabase+" mat="+afterMaterial+" installed=["+String.Join(";",installedDbs)+"]");
        double densityKgPerM3=model.Extension.GetUserPreferenceDouble((int)swUserPreferenceDoubleValue_e.swMaterialPropertyDensity,(int)swUserPreferenceOption_e.swDetailingNoOptionSpecified);
        if(!String.Equals(model.ConfigurationManager.ActiveConfiguration.Name,activeConfig,StringComparison.Ordinal))
            VariableTrial.ModelShowConfiguration2(model,activeConfig,"EnsureSteelMaterial.Restore");
        c.Event(new {inspection="structural_part_material_repaired",path=model.GetPathName(),configuration=configuration,beforeMaterial=beforeMaterial,beforeDatabase=beforeDatabase,afterMaterial=afterMaterial,afterDatabase=afterDatabase,densityKgPerM3=densityKgPerM3,fallbackApplied=afterMaterial!="冷轧钢板",installedDatabases=installedDbs});
    }

    static void EnsureFrameLinearPatternPitch(BuildContext c,ModelDoc2 model,string patternName,List<object> changes)
    {
        double unit=(c.Height-87.0)/12.0;
        double count=DimensionRawValue(model,"D1@"+patternName);
        Need(Near(count,11),"ASSEMBLY_FRAME_PATTERN_COUNT_DRIFT "+model.GetPathName()+"/"+patternName+" actual="+count.ToString("R",Invariant));
        double before=DimensionValue(model,"D3@"+patternName);
        if(!Near(before,unit))SetDimension(model,"D3@"+patternName,unit,changes);
        c.Event(new {inspection="frame_native_pattern_driver",path=model.GetPathName(),pattern=patternName,beforePitchMm=before,targetPitchMm=unit,readbackPitchMm=DimensionValue(model,"D3@"+patternName),instanceCount=count});
    }

    static void LockMirrorExternalReferences(BuildContext c,ModelDoc2 model)
    {
        var names=new List<string>();
        Feature scan=model.FirstFeature() as Feature;
        while(scan!=null)
        {
            Feature next=null;
            try
            {
                if(scan.GetTypeName2()=="MirrorStock")names.Add(scan.Name);
                next=scan.GetNextFeature() as Feature;
            }
            finally { Release(scan); }
            scan=next;
        }
        Need(names.Count>0,"ASSEMBLY_RIGHT_FRAME_MIRROR_MISSING "+model.GetPathName());
        foreach(string name in names)
        {
            Feature feature=FindFeatureByName(model,name);
            try
            {
                object files=null,parts=null,features=null,types=null,statuses=null,entities=null,components=null;int opt=0;string cfg="";
                string[] paths; int[] beforeStates;
                try
                {
                    feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out opt,out cfg);
                    paths=Items(files).Select(v=>Within(Convert.ToString(v,Invariant),c.Output)).ToArray();
                    beforeStates=Items(statuses).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                }
                finally { ReleaseItems(files); ReleaseItems(parts); ReleaseItems(features); ReleaseItems(types); ReleaseItems(statuses); ReleaseItems(entities); ReleaseItems(components); }
                Need(paths.Length>0&&beforeStates.Length==paths.Length,"ASSEMBLY_RIGHT_FRAME_MIRROR_LINKS_EMPTY "+model.GetPathName()+"/"+name);
                if(!beforeStates.All(v=>v==1))
                {
                    VariableTrial.FeatureUpdateExternalFileReferences(feature,opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll,"LockMirrorExternalReferences");
                    Need(VariableTrial.ModelForceRebuild3(model,false,"LockMirrorExternalReferences"),"ASSEMBLY_RIGHT_FRAME_LOCK_REBUILD_FAILED "+VariableTrial.ModelGetPathName(model,"LockMirrorExternalReferences")+"/"+name);
                }
                object lf=null,lp=null,lft=null,lt=null,ls=null,le=null,lc=null;
                int[] lockedStates;
                try
                {
                    feature.ListExternalFileReferences2(out lf,out lp,out lft,out lt,out ls,out le,out lc,out opt,out cfg);
                    lockedStates=Items(ls).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                }
                finally { ReleaseItems(lf); ReleaseItems(lp); ReleaseItems(lft); ReleaseItems(lt); ReleaseItems(ls); ReleaseItems(le); ReleaseItems(lc); }
                Need(lockedStates.Length==paths.Length&&lockedStates.All(v=>v==1),"ASSEMBLY_RIGHT_FRAME_LOCK_FAILED "+model.GetPathName()+"/"+name);
                c.Event(new {inspection="frame_mirror_reference_locked",path=model.GetPathName(),feature=name,sourcePaths=paths,statuses=lockedStates});
            }
            finally { Release(feature); }
        }
    }

    static List<double[]> FrameSlotWallBoxesMm(ModelDoc2 model)
    {
        var walls=new List<double[]>();
        foreach(Body2 body in Items(((PartDoc)model).GetBodies2(0,false)))
        {
            try
            {
                foreach(Face2 face in Items(body.GetFaces()))
                {
                    Surface surface=null;
                    try
                    {
                        surface=face.GetSurface() as Surface;
                        if(surface==null||!surface.IsPlane())continue;
                        double[] box=Numbers(face.GetBox());
                        if(Math.Abs(box[2]*1000+16.7)<0.01&&Math.Abs(box[5]*1000+16.7)<0.01&&Math.Abs((box[4]-box[1])*1000-3.6)<0.01)
                            walls.Add(box.Select(v=>v*1000).ToArray());
                    }
                    finally { Release(surface);Release(face); }
                }
            }
            finally { Release(body); }
        }
        return walls;
    }

    static double[] VerifyFrameGridAndSlots(BuildContext c,ModelDoc2 model,string stage,double[] expectedDoorGapCenters)
    {
        Need(VariableTrial.ModelForceRebuild3(model,false,"VerifyFrameGridAndSlots"),"ASSEMBLY_FRAME_REBUILD_FAILED "+VariableTrial.ModelGetPathName(model,"VerifyFrameGridAndSlots")+"/"+stage);
        CheckFeatureHealth(model);
        double unit=(c.Height-87.0)/12.0;
        double[] grid=Enumerable.Range(1,11).Select(i=>28.5+i*unit).ToArray();
        List<double[]> walls=FrameSlotWallBoxesMm(model);
        double[] centers=walls.Select(b=>(b[1]+b[4])/2.0).OrderBy(y=>y).ToArray();
        c.Event(new {inspection="frame_native_slot_grid",path=model.GetPathName(),stage=stage,unitPitchMm=unit,expectedGridCentersMm=grid,measuredCentersMm=centers,expectedDoorGapCentersMm=expectedDoorGapCenters});
        Need(walls.All(b=>Math.Abs(b[3]-b[0]-1.2)<0.01),"ASSEMBLY_FRAME_SLOT_WALL_THICKNESS_INVALID "+model.GetPathName()+"/"+stage);
        Need(grid.All(y=>centers.Count(x=>Math.Abs(x-y)<0.01)>=1),"ASSEMBLY_FRAME_GRID_SLOT_MISSING "+model.GetPathName()+"/"+stage);
        if(expectedDoorGapCenters!=null)Need(expectedDoorGapCenters.All(y=>centers.Any(x=>Math.Abs(x-y)<0.01)),"ASSEMBLY_FRAME_DOOR_GAP_SLOT_MISSING "+model.GetPathName()+"/"+stage);
        return centers;
    }

    static void CutFrameMissingSlots(BuildContext c,ModelDoc2 model,string path,string side,bool central,double[] missing)
    {
        ActivatePartForEdit(c,model);
        double x=(side=="L"?-1.0:1.0)*(central?36.8:c.Width/2.0-20.0);
        Face2 selected=null;double area=0;
        foreach(Body2 body in Items(((PartDoc)model).GetBodies2(0,false)))
        {
            try
            {
                foreach(Face2 face in Items(body.GetFaces()))
                {
                    Surface surface=null;bool keep=false;
                    try
                    {
                        surface=face.GetSurface() as Surface;
                        if(surface!=null&&surface.IsPlane())
                        {
                            double[] plane=Numbers(surface.PlaneParams);
                            double faceArea=face.GetArea();
                            if(Math.Abs(Math.Abs(plane[0])-1.0)<1e-6&&Math.Abs(plane[3]*1000-x)<0.01&&faceArea>area)
                            {
                                Release(selected);selected=face;area=faceArea;keep=true;
                            }
                        }
                    }
                    finally { Release(surface);if(!keep)Release(face); }
                }
            }
            finally { Release(body); }
        }
        try
        {
            VariableTrial.ModelClearSelection2(model,true,"CutFrameMissingSlots");
            Need(selected!=null&&((Entity)selected).Select4(false,null),"ASSEMBLY_FRAME_SLOT_FACE_MISSING "+path);
        }
        finally { Release(selected); }
        MathUtility math=null;Sketch sketch=null;MathTransform transform=null;Feature cut=null;
        try
        {
            model.SketchManager.InsertSketch(true);
            sketch=model.SketchManager.ActiveSketch;
            Need(sketch!=null,"ASSEMBLY_FRAME_SLOT_SKETCH_CREATE_FAILED "+path);
            math=VariableTrial.SwGetMathUtility(c.Sw,"CutFrameMissingSlots");
            transform=sketch.ModelToSketchTransform;
            foreach(double y in missing)
            {
                MathPoint a=null,b=null,ta=null,tb=null;
                try
                {
                    a=(MathPoint)math.CreatePoint(new[]{x/1000.0,(y-1.8)/1000.0,-.019});
                    b=(MathPoint)math.CreatePoint(new[]{x/1000.0,(y+1.8)/1000.0,-.0167});
                    ta=(MathPoint)a.MultiplyTransform(transform);tb=(MathPoint)b.MultiplyTransform(transform);
                    double[] p=Numbers(ta.ArrayData),q=Numbers(tb.ArrayData);
                    model.SketchManager.AddToDB=true;
                    try
                    {
                        double xmin=Math.Min(p[0],q[0]),xmax=Math.Max(p[0],q[0]),ymin=Math.Min(p[1],q[1]),ymax=Math.Max(p[1],q[1]);
                        Need(model.SketchManager.CreateLine(xmin,ymin,0,xmax,ymin,0)!=null&&
                             model.SketchManager.CreateLine(xmax,ymin,0,xmax,ymax,0)!=null&&
                             model.SketchManager.CreateLine(xmax,ymax,0,xmin,ymax,0)!=null&&
                             model.SketchManager.CreateLine(xmin,ymax,0,xmin,ymin,0)!=null,"ASSEMBLY_FRAME_SLOT_RECTANGLE_FAILED "+path);
                    }
                    finally { model.SketchManager.AddToDB=false; }
                }
                finally { Release(tb);Release(ta);Release(b);Release(a); }
            }
            model.SketchManager.InsertSketch(true);
            cut=model.FeatureManager.FeatureCut3(true,false,false,0,0,.003,.003,false,false,false,false,0,0,false,false,false,false,true,true,true,false,false,false,0,0,false);
            Need(cut!=null,"ASSEMBLY_FRAME_SLOT_NATIVE_CUT_FAILED "+path);
            string[] topFeatures=ListTopFeatureNames(model);
            string baseName="PARAMETRIC_CROSSBAR_SLOTS_"+side;
            cut.Name=topFeatures.Contains(baseName,StringComparer.Ordinal)?baseName+"_"+NextAvailablePatternIndex(topFeatures,baseName+"_").ToString(Invariant):baseName;
            c.Event(new {inspection="frame_missing_crossbar_slots_cut",path=path,side=side,central=central,flangeXmm=x,cutFeature=cut.Name,addedCentersMm=missing});
        }
        finally { Release(cut);Release(transform);Release(math);Release(sketch);if(!VariableTrial.ComTimedOut)try{VariableTrial.ModelClearSelection2(model,true,"CutFrameMissingSlots");}catch{} }
    }

    static void RepairColumnFrameSlots(BuildContext c,Dictionary<string,string> structure,Dictionary<string,int> indexByPath,Dictionary<string,Tuple<string,string,List<object>,object,object>> metaByPath)
    {
        var boundaries=Map(c.Layout["boundaries"]);
        // Process column R first and lock its MirrorStock references before any custom cuts are added to L.
        foreach(string side in new[]{"R","L"})
        {
            double[] expectedDoorGaps=Items(boundaries[side]).Select(Map).Select(row=>Number(row,"doorGapCenterYmm")).ToArray();
            foreach(bool central in new[]{false,true})
            {
                string key=central?"门框 竖隔板"+side+(side=="L"?".sldprt":".SLDPRT"):"门框 "+(side=="L"?"左":"右")+".sldprt";
                string path=structure[key];
                ModelDoc2 model=null;
                try
                {
                    model=c.Open(path,false);
                    double[] beforeBox=BodyBounds(model);
                    bool changed=false;
                    if(side=="R") { LockMirrorExternalReferences(c,model); changed=true; }
                    double[] existing=VerifyFrameGridAndSlots(c,model,"native-driven",null);
                    double[] missing=ComputeMissingSlotCenters(existing,expectedDoorGaps,0.01);
                    if(missing.Length>0)
                    {
                        CutFrameMissingSlots(c,model,path,side,central,missing);
                        changed=true;
                    }
                    double[] measured=VerifyFrameGridAndSlots(c,model,"after-slot-repair",expectedDoorGaps);
                    double[] afterBox=BodyBounds(model);
                    Need(beforeBox.Zip(afterBox,(a,b)=>Near(a,b)).All(v=>v),"ASSEMBLY_FRAME_ENVELOPE_CHANGED "+path);
                    c.Event(new {inspection="frame_slot_repair_verified",path=path,side=side,central=central,existingCentersMm=existing,requestedCentersMm=expectedDoorGaps,addedCentersMm=missing,measuredCentersMm=measured});
                    if(changed)
                    {
                        c.Save(model);
                        UpdateStructurePartRecord(c,model,path,indexByPath,metaByPath);
                    }
                }
                finally { c.Close(model);c.CloseOutputDocuments(); }
            }
        }
    }

    static double[] ExpectedPartitionLockRows(BuildContext c,string side)
    {
        return Items(Map(c.Layout["rowsByColumn"])[side]).Select(Map).Select(row=>Number(row,"centerYmm")).OrderBy(v=>v).ToArray();
    }

    static double[] PartitionLockHoleSlotRows(ModelDoc2 model)
    {
        var holeYs=new List<double>();
        foreach(Body2 body in Items(((PartDoc)model).GetBodies2(0,false)))
        {
            try
            {
                foreach(Face2 face in Items(body.GetFaces()))
                {
                    Surface surface=null;
                    try
                    {
                        surface=face.GetSurface() as Surface;
                        if(surface==null||!surface.IsCylinder())continue;
                        double[] cyl=Numbers(surface.CylinderParams);
                        double radiusMm=cyl[6]*1000.0,axisZ=Math.Abs(cyl[5]),xMm=Math.Abs(cyl[0]*1000.0),yMm=cyl[1]*1000.0;
                        if(Math.Abs(radiusMm-2.5)<0.05&&axisZ>0.9999&&Math.Abs(xMm-45.0)<1.5)
                        {
                            if(!holeYs.Any(existing=>Math.Abs(existing-yMm)<0.05))holeYs.Add(yMm);
                        }
                    }
                    finally { Release(surface);Release(face); }
                }
            }
            finally { Release(body); }
        }
        return holeYs.Select(y=>y+60.0).OrderBy(v=>v).ToArray();
    }

    static bool LockRowsMatch(double[] actual,double[] expected)
    {
        return actual.Length==expected.Length&&expected.All(row=>actual.Count(v=>Math.Abs(v-row)<=0.05)==1);
    }

    static void VerifyPartitionLockRows(BuildContext c,ModelDoc2 model,string side,string stage)
    {
        double[] expected=ExpectedPartitionLockRows(c,side);
        double[] actual=PartitionLockHoleSlotRows(model);
        c.Event(new {inspection="partition_lock_rows_readback",path=model.GetPathName(),side=side,stage=stage,expectedSlotRowsMm=expected,actualSlotRowsMm=actual});
        Need(LockRowsMatch(actual,expected),"ASSEMBLY_PARTITION_LOCK_ROWS_MISMATCH "+side+"/"+stage+" path="+model.GetPathName()+" actual=["+String.Join(",",actual.Select(v=>v.ToString("R",Invariant)))+"] expected=["+String.Join(",",expected.Select(v=>v.ToString("R",Invariant)))+"]");
    }

    static void CreateLockRowPattern(ModelDoc2 model,double spacingMm,bool reverse,string patternName)
    {
        Feature source=FindFeatureByName(model,"阵列(线性)2");
        Need(source!=null,"ASSEMBLY_LOCK_SOURCE_PATTERN_MISSING 阵列(线性)2");
        LinearPatternFeatureData sourceData=source.GetDefinition() as LinearPatternFeatureData;
        Need(sourceData!=null&&sourceData.AccessSelections(model,null),"ASSEMBLY_LOCK_SOURCE_PATTERN_UNAVAILABLE");
        object axis=null;Array seeds=null;SelectionMgr selection=null;SelectData axisData=null;FeatureManager manager=null;LinearPatternFeatureData data=null;Feature created=null;
        try
        {
            axis=sourceData.D1Axis;
            seeds=sourceData.PatternFeatureArray as Array;
            var seedFeatures=new List<Feature>();
            var seedNames=new List<string>();
            if(seeds!=null)
            {
                foreach(object raw in seeds)
                {
                    var seed=raw as Feature;
                    if(seed!=null) { seedFeatures.Add(seed); seedNames.Add(seed.Name); }
                }
            }
            Need(axis!=null&&seedFeatures.Count==2&&seedNames.Contains("切除-拉伸3")&&seedNames.Contains("切除-拉伸5"),"ASSEMBLY_LOCK_SEED_CONTRACT_MISMATCH");
            VariableTrial.ModelClearSelection2(model,true,"CreateLockRowPattern");
            selection=model.SelectionManager as SelectionMgr;
            axisData=selection==null?null:selection.CreateSelectData() as SelectData;
            var axisEntity=axis as Entity;
            if(axisData!=null)axisData.Mark=1;
            bool axisSelected=axisEntity!=null&&axisData!=null&&axisEntity.Select4(false,axisData);
            int seedsSelected=0;
            foreach(Feature seed in seedFeatures)if(seed.Select2(true,4))seedsSelected++;
            Need(axisSelected&&seedsSelected==2,"ASSEMBLY_LOCK_PATTERN_SELECTION_FAILED");
            manager=model.FeatureManager;
            data=manager.CreateDefinition((int)swFeatureNameID_e.swFmLPattern) as LinearPatternFeatureData;
            Need(data!=null,"ASSEMBLY_LOCK_PATTERN_DEFINITION_FAILED");
            data.BodyPattern=false;
            data.D1EndCondition=(int)swPatternEndCondition_e.swPatternEndCondition_SpacingAndInstances;
            data.D1Spacing=spacingMm/1000.0;
            data.D1TotalInstances=2;
            data.D1ReverseDirection=reverse;
            data.D2EndCondition=(int)swPatternEndCondition_e.swPatternEndCondition_SpacingAndInstances;
            data.D2Spacing=Math.Max(sourceData.D2Spacing,0.001);
            data.D2TotalInstances=1;
            data.D2ReverseDirection=sourceData.D2ReverseDirection;
            data.D2PatternSeedOnly=sourceData.D2PatternSeedOnly;
            data.GeometryPattern=true;
            data.VarySketch=sourceData.VarySketch;
            data.PropagateVisualProperty=sourceData.PropagateVisualProperty;
            created=manager.CreateFeature(data) as Feature;
            Need(created!=null,"ASSEMBLY_LOCK_PATTERN_CREATE_FAILED "+patternName);
            created.Name=patternName;
            Need(VariableTrial.ModelForceRebuild3(model,false,"CreateLockRowPattern"),"ASSEMBLY_LOCK_PATTERN_REBUILD_FAILED "+patternName);
            if(reverse)
            {
                string[] names=ListTopFeatureNames(model);
                int fillIndex=Array.IndexOf(names,"拉伸-薄壁4");
                int createdIndex=Array.IndexOf(names,created.Name);
                Need(fillIndex>=0&&createdIndex>=0,"ASSEMBLY_LOCK_PATTERN_ANCHOR_MISSING "+patternName);
                if(createdIndex<fillIndex)
                {
                    bool reordered=model.Extension.ReorderFeature(created.Name,"拉伸-薄壁4",(int)swMoveLocation_e.swMoveAfter);
                    Need(reordered&&VariableTrial.ModelForceRebuild3(model,false,"CreateLockRowPattern.Reorder"),"ASSEMBLY_LOCK_PATTERN_REORDER_FAILED "+patternName);
                }
            }
            VariableTrial.ModelClearSelection2(model,true,"CreateLockRowPattern");
        }
        finally
        {
            Release(created);Release(data);Release(manager);Release(axisData);Release(selection);
            if(seeds!=null)foreach(object raw in seeds)Release(raw);
            Release(axis);
            try { sourceData.ReleaseSelectionAccess(); } catch {}
            Release(sourceData);Release(source);
        }
    }

    static string[] SuppressPartitionLockPatterns(ModelDoc2 model,string side)
    {
        string[] legacyPatterns={"阵列(线性)2","V34_锁孔补充_Y1248.5","V34_锁孔补充_Y638.5","V35_L_锁孔_Y486"};
        string[] allFeatures=ListTopFeatureNames(model);
        string[] priorParametricPatterns=allFeatures.Where(n=>n.StartsWith("PARAMETRIC_LOCK_",StringComparison.Ordinal)).ToArray();
        foreach(string name in legacyPatterns.Concat(priorParametricPatterns))SetFeatureSuppression(model,name,true,side);
        return allFeatures;
    }

    static void RepairPartitionLocks(BuildContext c,ModelDoc2 model,string side)
    {
        double[] targetRows=ExpectedPartitionLockRows(c,side);
        double[] currentRows=PartitionLockHoleSlotRows(model);
        if(LockRowsMatch(currentRows,targetRows))
        {
            c.Event(new {inspection="partition_lock_holes_verified",path=model.GetPathName(),side=side,repaired=false,slotRowsMm=currentRows});
            return;
        }
        double[] beforeBox=BodyBounds(model);
        string[] allFeatures=SuppressPartitionLockPatterns(model,side);
        Need(VariableTrial.ModelForceRebuild3(model,false,"RepairPartitionLocks.Seed"),"ASSEMBLY_LOCK_SEED_REBUILD_FAILED "+side);
        CheckFeatureHealth(model);
        double[] seedRows=PartitionLockHoleSlotRows(model);
        Need(seedRows.Length==1,"ASSEMBLY_LOCK_SINGLE_SEED_REQUIRED "+side+" count="+seedRows.Length);
        double seedRowY=seedRows[0];
        ActivatePartForEdit(c,model);
        Feature slotSketch=FindFeatureByName(model,"草图6");
        double targetSeedRowY=0,beforeD2Mm=0,targetD2Mm=0;double[] remainingRows;
        try
        {
            Need(slotSketch!=null,"ASSEMBLY_LOCK_SKETCH_MISSING 草图6");
            VariableTrial.ModelClearSelection2(model,true,"RepairPartitionLocks");
            Need(slotSketch.Select2(false,0),"ASSEMBLY_LOCK_SKETCH_SELECT_FAILED "+side);
            model.EditSketch();
            try
            {
                Dimension verticalDatum=model.Parameter("D2@草图6") as Dimension;
                try
                {
                    Need(verticalDatum!=null&&verticalDatum.DrivenState==2,"ASSEMBLY_LOCK_VERTICAL_DIM_MISSING "+side);
                    beforeD2Mm=verticalDatum.SystemValue*1000.0;
                    remainingRows=ComputeLockSeedMove(targetRows,seedRowY,beforeD2Mm,out targetSeedRowY,out targetD2Mm);
                    if(Math.Abs(beforeD2Mm-targetD2Mm)>0.001)verticalDatum.SystemValue=targetD2Mm/1000.0;
                }
                finally { Release(verticalDatum); }
            }
            finally { model.SketchManager.InsertSketch(true); }
        }
        finally { Release(slotSketch); }
        Need(VariableTrial.ModelForceRebuild3(model,false,"RepairPartitionLocks.Move"),"ASSEMBLY_LOCK_SEED_MOVE_REBUILD_FAILED "+side);
        CheckFeatureHealth(model);
        Need(LockRowsMatch(PartitionLockHoleSlotRows(model),new[]{targetSeedRowY}),"ASSEMBLY_LOCK_SEED_MOVE_MISMATCH "+side);
        int nextIndex=NextAvailablePatternIndex(allFeatures,"PARAMETRIC_LOCK_ROW_");
        var createdPatterns=new List<string>();
        foreach(double row in remainingRows)
        {
            string patternName="PARAMETRIC_LOCK_ROW_"+(nextIndex++).ToString(Invariant);
            CreateLockRowPattern(model,Math.Abs(targetSeedRowY-row),row>targetSeedRowY,patternName);
            createdPatterns.Add(patternName);
        }
        CheckFeatureHealth(model);
        VerifyPartitionLockRows(c,model,side,"after-repair");
        double[] afterBox=BodyBounds(model);
        Need(beforeBox.Zip(afterBox,(a,b)=>Near(a,b)).All(v=>v),"ASSEMBLY_LOCK_ENVELOPE_CHANGED "+side);
        c.Event(new {inspection="partition_lock_holes_repaired",path=model.GetPathName(),side=side,repaired=true,seedRowBeforeMm=seedRowY,targetSeedRowMm=targetSeedRowY,d2BeforeMm=beforeD2Mm,d2TargetMm=targetD2Mm,createdPatterns=createdPatterns.ToArray(),targetRowsMm=targetRows});
    }

    static void FullCloseAllDocuments(BuildContext c)
    {
        c.CloseOutputDocuments();
        object[] remaining = VariableTrial.SwGetDocuments(c.Sw, "FullCloseAllDocuments", true);
        foreach (object raw in remaining)
        {
            var doc = raw as ModelDoc2;
            try
            {
                if (doc != null)
                {
                    string title = VariableTrial.ModelGetTitle(doc, "FullCloseAllDocuments", true);
                    if (!string.IsNullOrEmpty(title)) VariableTrial.SwCloseDoc(c.Sw, title, "FullCloseAllDocuments", true);
                }
            }
            finally { Release(doc); }
        }
    }

    internal static class FullCabinetAssembly
    {
        static readonly double[] FullIdentity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        static readonly double[] FullFlipXZ = { -1, 0, 0, 0, 1, 0, 0, 0, -1 };
        static readonly double[] FullFlipYZ = { 1, 0, 0, 0, -1, 0, 0, 0, -1 };
        static readonly double[] FullFlipXY = { -1, 0, 0, 0, -1, 0, 0, 0, 1 };
        static readonly double[] FullRotX90 = { 1, 0, 0, 0, 0, 1, 0, -1, 0 };
        static readonly double[] FullRotY90 = { 0, 0, 1, 0, 1, 0, -1, 0, 0 };
        static readonly double[] FullBottomLatch = { -1, 0, 0, 0, 0, -1, 0, -1, 0 };

        static readonly string[] FullStructureSeedNames = new[]
        {
            "标准寄存柜 模型.SLDPRT",
            "底座 模型.sldprt",
            "上盖 模型.sldprt",
            "箱体竖隔板R参数源.SLDPRT",
            "底座外框.sldprt",
            "底座底板.sldprt",
            "底座加强筋.sldprt",
            "螺母M12.SLDPRT",
            "调整脚 M12X60(模型).SLDPRT",
            "上盖壳体底板.sldprt",
            "上盖壳体前侧板.sldprt",
            "上盖壳体后侧板.sldprt",
            "上盖壳体左侧板.sldprt",
            "上盖壳体右侧板.SLDPRT",
            "箱体左侧板.sldprt",
            "箱体右侧板.sldprt",
            "箱体竖隔板L.sldprt",
            "箱体竖隔板R_parametric.SLDPRT",
            "箱体侧板加强筋1.sldprt",
            "箱体侧板加强筋2.sldprt",
            "箱体竖隔板加强件.sldprt",
            "箱体横层板L.sldprt",
            "箱体横层板R.SLDPRT",
            "箱体横层板加强筋.SLDPRT",
            "锁控维护条_源钣金.SLDPRT",
            "维护门锁支架_原生.SLDPRT",
            "门框 左.sldprt",
            "门框 右.sldprt",
            "门框 上.sldprt",
            "门框 下.sldprt",
            "门框 竖隔板L.sldprt",
            "门框 竖隔板R.SLDPRT",
            "门框 横隔板.sldprt",
            "门框 横隔板R.SLDPRT",
        };

        public static object FullBuild(ISldWorks sw, string cadDirectory, string outputDirectory, Dictionary<string, object> layout)
        {
            return FullBuild(sw, cadDirectory, outputDirectory, layout, null);
        }

        public static object FullBuild(ISldWorks sw, string cadDirectory, string outputDirectory, Dictionary<string, object> layout, string envelopePhase)
        {
            VariableTrial.RuntimeOwnership();
            Need(sw != null && layout != null, "FULL_CABINET_ARGUMENT_REQUIRED");
            Need(envelopePhase == null || envelopePhase == "prepare" || envelopePhase == "build_and_measure", "FULL_ENVELOPE_PHASE_INVALID");
            bool doPrepare = envelopePhase == null || envelopePhase == "prepare";
            bool doBuild = envelopePhase == null || envelopePhase == "build_and_measure";
            string cad = Within(cadDirectory, TrialRoot);
            string folder = Within(outputDirectory, Path.Combine(cad, "assemblies"));
            var c = new BuildContext
            {
                Sw = sw,
                Cad = cad,
                Output = folder,
                Layout = layout,
                ColdSession = true,
                EvidencePath = Path.Combine(folder, "assembly-evidence.json"),
                AssemblyPath = Path.Combine(folder, "16029_variable_full_cabinet.SLDASM"),
            };
            if (doPrepare)
            {
                Directory.CreateDirectory(folder);
                Need(!File.Exists(c.EvidencePath), "PRESERVE_FULL_CABINET_EVIDENCE");
            }
            else
            {
                Need(Directory.Exists(folder), "FULL_PREPARED_CASE_FOLDER_MISSING");
                Need(File.Exists(c.EvidencePath), "FULL_PREPARED_EVIDENCE_MISSING");
            }
            Need(!File.Exists(c.AssemblyPath), "PRESERVE_FULL_CABINET_MODEL");
            var cabinet = Map(c.Layout["cabinet"]);
            c.Width = Number(cabinet, "widthMm");
            c.Height = Number(cabinet, "heightMm");
            c.Depth = Number(cabinet, "depthMm");
            c.DoorWidth = Number(c.Layout, "doorLeafWidthMm");
            c.Evidence = new Dictionary<string, object>
            {
                { "schema", "16029-full-cabinet-assembly/v1" },
                { "envelopeMode", "parametric_envelope" },
                { "envelopePhase", envelopePhase ?? "single_session" },
                { "fullCabinetScope", true },
                { "minimumScope", false },
                { "production_release", false },
                { "acceptancePass", false },
                { "stageAcceptance", false },
                { "diagnosticOnly", true },
                { "prepared", !doPrepare },
                { "assemblyBuilt", false },
                { "success", false },
                { "layout", c.Layout },
                { "events", c.Events },
                { "parts", c.Parts },
                { "hardware", c.Hardware },
                { "assemblyPath", c.AssemblyPath },
                { "outputDirectory", folder },
            };
            try
            {
                c.AttachComTimeoutObserver();
                c.Stage("EmptySessionCheck");
                Need(VariableTrial.SwGetDocuments(sw, "FullBuild.EmptySession").Length == 0, "ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
                c.InputHashes = Directory.GetFiles(c.Cad, "*", SearchOption.AllDirectories)
                    .Where(path => !path.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase) && !path.Equals(folder, StringComparison.OrdinalIgnoreCase))
                    .Where(path => new[] { ".SLDPRT", ".SLDASM", ".SLDDRW" }.Contains(Path.GetExtension(path).ToUpperInvariant()))
                    .ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
                if (doPrepare)
                {
                    c.Write();
                    FullPrepareCase(c);
                    c.Evidence["prepared"] = true;
                    c.Write();
                }
                else
                {
                    FullLoadPreparedCase(c);
                    c.Write();
                }
                if (doBuild)
                {
                    c.Stage("FullApplyProductionMarks");
                    c.Evidence["productionMarks"] = ApplyAssemblyProductionMarks(c);
                    c.Write();
                    c.Stage("FullBuildNativeAssembly");
                    FullBuildNativeAssembly(c);
                    c.Stage("FullCloseBeforeReopen");
                    FullCloseAllDocuments(c);
                    c.Stage("FullReopenAndMeasure");
                    FullReopenAndMeasure(c);
                    c.Scope();
                    c.Stage("Completed");
                    c.Evidence["assemblyBuilt"] = true;
                    c.Evidence["success"] = true;
                }
                else
                {
                    c.Scope();
                    c.Stage("Prepared");
                }
            }
            catch (Exception error)
            {
                c.Evidence["error"] = error.ToString();
                if (VariableTrial.ComTimedOut) c.Evidence["comTimedOut"] = true;
                c.Event(new { inspection = "full_cabinet_failure", error = error.ToString(), stage = c.CurrentStage, componentIndex = c.CurrentComponentIndex, componentRole = c.CurrentComponentRole, insertStep = c.CurrentInsertStep, comTimedOut = VariableTrial.ComTimedOut });
            }
            finally
            {
                try
                {
                    c.Stage("Cleanup");
                    FullCloseAllDocuments(c);
                }
                catch (Exception error)
                {
                    c.Evidence["cleanupError"] = error.ToString();
                    c.Evidence["prepared"] = false;
                    c.Evidence["success"] = false;
                }
                c.DetachComTimeoutObserver();
                if (c.InputHashes != null)
                {
                    var after = Directory.GetFiles(c.Cad, "*", SearchOption.AllDirectories)
                        .Where(path => !path.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase) && !path.Equals(folder, StringComparison.OrdinalIgnoreCase))
                        .Where(path => new[] { ".SLDPRT", ".SLDASM", ".SLDDRW" }.Contains(Path.GetExtension(path).ToUpperInvariant()))
                        .ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
                    string[] paths = c.InputHashes.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                    string[] changed = paths.Where(path => !c.InputHashes.ContainsKey(path) || !after.ContainsKey(path) || c.InputHashes[path] != after[path]).ToArray();
                    c.Evidence["inputFilesUnchanged"] = changed.Length == 0;
                    c.Evidence["changedInputFiles"] = changed;
                    if (changed.Length != 0)
                    {
                        c.Evidence["prepared"] = false;
                        c.Evidence["success"] = false;
                    }
                }
                c.Evidence["stageAcceptance"] = false;
                c.Evidence["acceptancePass"] = false;
                c.Evidence["production_release"] = false;
                c.Write();
            }
            return c.Evidence;
        }

        static void FullPrepareCase(BuildContext c)
        {
            c.Stage("FullValidateLayout");
            FullValidateLayout(c);
            double[] gussetBox;
            c.Stage("FullPrepareStructure");
            Dictionary<string, string> structure = FullPrepareStructure(c, out gussetBox);
            c.Stage("PrepareHardware");
            PrepareHardware(c);
            c.Stage("FullEnsureHardwareMaterials");
            FullEnsureHardwareMaterials(c);
            c.Stage("FullPreparePlacements");
            FullPreparePlacements(c, structure, gussetBox);
            c.Stage("FullClosePrepareDocuments");
            FullCloseAllDocuments(c);
        }

        static void FullLoadPreparedCase(BuildContext c)
        {
            c.Stage("FullLoadPreparedCase");
            var saved = Map(Json.DeserializeObject(File.ReadAllText(c.EvidencePath, Encoding.UTF8)));
            Need(saved.ContainsKey("schema") && Convert.ToString(saved["schema"], Invariant) == "16029-full-cabinet-assembly/v1", "FULL_PREPARED_SCHEMA_INVALID");
            Need(saved.ContainsKey("prepared") && Convert.ToBoolean(saved["prepared"], Invariant), "FULL_CASE_NOT_PREPARED");
            Need(saved.ContainsKey("layout") && Json.Serialize(saved["layout"]) == Json.Serialize(c.Layout), "FULL_PREPARED_LAYOUT_MISMATCH");
            if (saved.ContainsKey("hardware") && saved["hardware"] != null)
            {
                foreach (var kv in Map(saved["hardware"]))
                {
                    string hwPath = Within(Convert.ToString(kv.Value, Invariant), c.Output);
                    Need(File.Exists(hwPath), "FULL_PREPARED_HARDWARE_MISSING " + hwPath);
                    c.Hardware[kv.Key] = hwPath;
                }
            }
            if (saved.ContainsKey("parts") && saved["parts"] != null)
                c.Parts.AddRange(Items(saved["parts"]));
            if (saved.ContainsKey("events") && saved["events"] != null)
                c.Events.AddRange(Items(saved["events"]));
            if (saved.ContainsKey("fullScopeSummary") && saved["fullScopeSummary"] != null)
                c.Evidence["fullScopeSummary"] = saved["fullScopeSummary"];
            object[] manifest = saved.ContainsKey("expectedManifest") ? Items(saved["expectedManifest"]) : new object[0];
            Need(manifest.Length > 0, "FULL_PREPARED_MANIFEST_EMPTY");
            var seenRoles = new HashSet<string>(StringComparer.Ordinal);
            foreach (object rawItem in manifest)
            {
                var item = Map(rawItem);
                string role = Convert.ToString(item["identity"], Invariant);
                Need(!string.IsNullOrEmpty(role) && seenRoles.Add(role), "FULL_PREPARED_MANIFEST_DUPLICATE_ROLE " + role);
                string partPath = Within(Convert.ToString(item["path"], Invariant), c.Output);
                Need(File.Exists(partPath), "FULL_PREPARED_PART_MISSING " + partPath);
                string side = Convert.ToString(item["side"] ?? "", Invariant);
                int index = Convert.ToInt32(item["index"], Invariant);
                string kind = Convert.ToString(item["kind"], Invariant);
                double[] xform = Numbers(item["transform"]);
                Need(xform.Length >= 13, "FULL_PREPARED_TRANSFORM_INVALID " + role);
                var p = new Placement(role, partPath, side, index, kind, FullIdentity, 0, 0, 0);
                p.Transform = xform;
                c.Placements.Add(p);
            }
            c.Evidence["expectedManifest"] = manifest;
            c.Event(new { inspection = "full_prepared_case_loaded", placementCount = c.Placements.Count, utc = DateTime.UtcNow.ToString("o") });
        }

        static void FullValidateLayout(BuildContext c)
        {
            Need(c.Width >= 700.0 - 1e-6 && c.Width <= 1200.0 + 1e-6, "FULL_ENVELOPE_WIDTH_OUT_OF_RANGE");
            Need(c.Height >= 1700.0 - 1e-6 && c.Height <= 2200.0 + 1e-6, "FULL_ENVELOPE_HEIGHT_OUT_OF_RANGE");
            Need(c.Depth >= 250.0 - 1e-6 && c.Depth <= 650.0 + 1e-6, "FULL_ENVELOPE_DEPTH_OUT_OF_RANGE");
            Need(Near(c.DoorWidth, (c.Width - 126.0) / 2.0), "FULL_ENVELOPE_DOOR_WIDTH_MISMATCH");
            ValidateLayout(c);
        }

        static double FullGridOffset(double heightMm)
        {
            return heightMm - 1917.0 + 152.5 - (heightMm - 87.0) / 12.0;
        }

        static void FullDepthStiffenerPlanes(double depthMm, out double zFront, out double zBack)
        {
            if (depthMm < 450.0 - 1e-6)
            {
                double basePitch = Math.Min(250.0, depthMm - 130.0);
                double baseFront = (depthMm - 3.0 - basePitch) / 2.0;
                zFront = Math.Max(85.0, baseFront);
                zBack = baseFront + basePitch;
            }
            else
            {
                zFront = 148.5;
                zBack = 398.5;
            }
        }

        static Feature FullFindFeature(ModelDoc2 model, string name)
        {
            Feature direct = FindFeatureByName(model, name);
            if (direct != null) return direct;
            Feature feat = model.FirstFeature() as Feature;
            int guard = 0;
            while (feat != null && guard++ < 10000)
            {
                Feature found = FullFindSubFeature(feat, name);
                if (found != null) return found;
                Feature next = feat.GetNextFeature() as Feature;
                Release(feat);
                feat = next;
            }
            if (feat != null) Release(feat);
            return null;
        }

        static Feature FullFindSubFeature(Feature parent, string name)
        {
            Feature sub = parent.GetFirstSubFeature() as Feature;
            int guard = 0;
            while (sub != null && guard++ < 2000)
            {
                if (string.Equals(sub.Name, name, StringComparison.OrdinalIgnoreCase)) return sub;
                Feature nested = FullFindSubFeature(sub, name);
                if (nested != null)
                {
                    Release(sub);
                    return nested;
                }
                Feature next = sub.GetNextSubFeature() as Feature;
                Release(sub);
                sub = next;
            }
            if (sub != null) Release(sub);
            return null;
        }

        static string FullActiveConfigName(ModelDoc2 model)
        {
            object rawConfigs = model.GetConfigurationNames();
            string[] names = Items(rawConfigs).Select(v => Convert.ToString(v, Invariant)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            string exact = names.FirstOrDefault(n => string.Equals(n, "Default", StringComparison.OrdinalIgnoreCase));
            return !string.IsNullOrEmpty(exact) ? exact : (names.FirstOrDefault() ?? "");
        }

        static void FullEnsureMaterial(BuildContext c, ModelDoc2 model)
        {
            string cfg = FullActiveConfigName(model);
            EnsureSteelMaterial(c, model, cfg);
        }

        static void FullEnsureHardwareMaterials(BuildContext c)
        {
            foreach (string hwPath in c.Hardware.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ModelDoc2 model = c.Open(hwPath, false);
                try
                {
                    var part = model as PartDoc;
                    if (part != null)
                    {
                        string db;
                        string cfg = FullActiveConfigName(model);
                        string mat = part.GetMaterialPropertyName2("", out db);
                        if (string.IsNullOrWhiteSpace(mat) && !string.IsNullOrEmpty(cfg))
                            mat = part.GetMaterialPropertyName2(cfg, out db);
                        if (string.IsNullOrWhiteSpace(mat))
                        {
                            FullEnsureMaterial(c, model);
                            VariableTrial.ModelForceRebuild3(model, false, "FullEnsureHardwareMaterials");
                            c.Save(model);
                            string repairedHash = Hash(hwPath);
                            object repairedGeometry = PartGeometry(model);
                            for (int pi = 0; pi < c.Parts.Count; pi++)
                            {
                                var pm = Map(Json.DeserializeObject(Json.Serialize(c.Parts[pi])));
                                if (pm.ContainsKey("path") && string.Equals(Convert.ToString(pm["path"], Invariant), hwPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    pm["sha256"] = repairedHash;
                                    pm["geometry"] = repairedGeometry;
                                    pm["materialRepaired"] = true;
                                    c.Parts[pi] = pm;
                                }
                            }
                            c.Write();
                        }
                    }
                }
                finally { c.Close(model); }
            }
        }

        static void FullEditSketchAction(BuildContext c, ModelDoc2 model, string sketchName, Action<Sketch, MathUtility, MathTransform, MathTransform> action)
        {
            Feature feat = FullFindFeature(model, sketchName);
            Need(feat != null, "FULL_SKETCH_MISSING " + sketchName + " in " + model.GetPathName());
            MathUtility math = null;
            Sketch sk = null;
            MathTransform m2s = null;
            MathTransform s2m = null;
            try
            {
                VariableTrial.ModelClearSelection2(model, true, "FullEditSketchAction");
                Need(feat.Select2(false, 0), "FULL_SKETCH_SELECT_FAILED " + sketchName);
                model.EditSketch();
                sk = model.SketchManager.ActiveSketch;
                Need(sk != null, "FULL_SKETCH_EDIT_FAILED " + sketchName);
                bool oldAdd = model.SketchManager.AddToDB;
                bool oldDisp = model.SketchManager.DisplayWhenAdded;
                try
                {
                    model.SketchManager.AddToDB = true;
                    model.SketchManager.DisplayWhenAdded = false;
                    math = VariableTrial.SwGetMathUtility(c.Sw, "FullEditSketchAction");
                    Need(math != null, "FULL_MATH_UTILITY_MISSING");
                    m2s = sk.ModelToSketchTransform;
                    s2m = m2s.Inverse() as MathTransform;
                    action(sk, math, m2s, s2m);
                }
                finally
                {
                    model.SketchManager.AddToDB = oldAdd;
                    model.SketchManager.DisplayWhenAdded = oldDisp;
                    model.InsertSketch2(true);
                    if (!VariableTrial.ComTimedOut)
                    {
                        try { VariableTrial.ModelClearSelection2(model, true, "FullEditSketchAction.Finally"); }
                        catch { }
                    }
                }
            }
            finally
            {
                Release(s2m);
                Release(m2s);
                Release(sk);
                Release(math);
                Release(feat);
            }
        }

        static void FullDeleteSketchExternalRelations(Sketch sk)
        {
            SketchRelationManager mgr = sk.RelationManager;
            try
            {
                object[] rels = Items(mgr.GetRelations((int)swSketchRelationFilterType_e.swExternal));
                foreach (object raw in rels)
                {
                    var rel = raw as SketchRelation;
                    try { if (rel != null) mgr.DeleteRelation(rel); }
                    finally { Release(rel); }
                }
            }
            finally { Release(mgr); }
        }

        static double[] FullToWorld(MathUtility math, MathTransform s2m, double sx, double sy, double sz)
        {
            MathPoint pt = math.CreatePoint(new[] { sx, sy, sz }) as MathPoint;
            MathPoint wpt = null;
            try
            {
                wpt = pt.MultiplyTransform(s2m) as MathPoint;
                return Numbers(wpt.ArrayData);
            }
            finally
            {
                Release(wpt);
                Release(pt);
            }
        }

        static double[] FullToSketch(MathUtility math, MathTransform m2s, double wx, double wy, double wz)
        {
            MathPoint pt = math.CreatePoint(new[] { wx, wy, wz }) as MathPoint;
            MathPoint spt = null;
            try
            {
                spt = pt.MultiplyTransform(m2s) as MathPoint;
                return Numbers(spt.ArrayData);
            }
            finally
            {
                Release(spt);
                Release(pt);
            }
        }

        static void FullMovePointWorld(SketchPoint pt, MathUtility math, MathTransform m2s, double wx, double wy, double wz)
        {
            double[] s = FullToSketch(math, m2s, wx, wy, wz);
            pt.SetCoords(s[0], s[1], s[2]);
        }

        static void FullAddUniquePoint(List<SketchPoint> list, SketchPoint pt)
        {
            if (pt == null) return;
            foreach (SketchPoint existing in list)
            {
                if (SameNativeObject(existing, pt))
                {
                    Release(pt);
                    return;
                }
            }
            list.Add(pt);
        }

        static List<SketchPoint> FullGetSegmentSketchPoints(Sketch sk)
        {
            var pts = new List<SketchPoint>();
            foreach (object raw in Items(sk.GetSketchSegments()))
            {
                var seg = raw as SketchSegment;
                try
                {
                    if (seg == null) continue;
                    int segType = seg.GetType();
                    if (segType == (int)swSketchSegments_e.swSketchLINE)
                    {
                        var ln = seg as SketchLine;
                        if (ln != null)
                        {
                            FullAddUniquePoint(pts, ln.GetStartPoint2() as SketchPoint);
                            FullAddUniquePoint(pts, ln.GetEndPoint2() as SketchPoint);
                        }
                    }
                    else if (segType == (int)swSketchSegments_e.swSketchARC)
                    {
                        var arc = seg as SketchArc;
                        if (arc != null)
                        {
                            FullAddUniquePoint(pts, arc.GetCenterPoint2() as SketchPoint);
                            FullAddUniquePoint(pts, arc.GetStartPoint2() as SketchPoint);
                            FullAddUniquePoint(pts, arc.GetEndPoint2() as SketchPoint);
                        }
                    }
                    else if (segType == (int)swSketchSegments_e.swSketchELLIPSE)
                    {
                        var ell = seg as SketchEllipse;
                        if (ell != null)
                        {
                            FullAddUniquePoint(pts, ell.GetCenterPoint2() as SketchPoint);
                            FullAddUniquePoint(pts, ell.GetStartPoint2() as SketchPoint);
                            FullAddUniquePoint(pts, ell.GetEndPoint2() as SketchPoint);
                        }
                    }
                    else if (segType == (int)swSketchSegments_e.swSketchSPLINE)
                    {
                        var sp = seg as SketchSpline;
                        if (sp != null)
                        {
                            foreach (object pRaw in Items(sp.GetPoints2())) FullAddUniquePoint(pts, pRaw as SketchPoint);
                        }
                    }
                }
                finally { Release(seg); }
            }
            return pts;
        }

        static void FullTranslateSketchWorld(BuildContext c, ModelDoc2 model, string sketchName, double dwx, double dwy, double dwz, bool detachExternal)
        {
            FullEditSketchAction(c, model, sketchName, delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
            {
                if (detachExternal) FullDeleteSketchExternalRelations(sk);
                if (Math.Abs(dwx) < 1e-9 && Math.Abs(dwy) < 1e-9 && Math.Abs(dwz) < 1e-9) return;
                List<SketchPoint> pts = FullGetSegmentSketchPoints(sk);
                bool prevDb = model.SketchManager.AddToDB;
                bool prevDisp = model.SketchManager.DisplayWhenAdded;
                try
                {
                    var targets = new List<double[]>(pts.Count);
                    foreach (SketchPoint pt in pts)
                    {
                        double[] w = FullToWorld(math, s2m, pt.X, pt.Y, pt.Z);
                        targets.Add(new[] { w[0] + dwx, w[1] + dwy, w[2] + dwz });
                    }
                    model.SketchManager.AddToDB = true;
                    model.SketchManager.DisplayWhenAdded = false;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        double[] t = targets[i];
                        double[] wNow = FullToWorld(math, s2m, pts[i].X, pts[i].Y, pts[i].Z);
                        if (Math.Abs(wNow[0] - t[0]) > 1e-7 || Math.Abs(wNow[1] - t[1]) > 1e-7 || Math.Abs(wNow[2] - t[2]) > 1e-7)
                            FullMovePointWorld(pts[i], math, m2s, t[0], t[1], t[2]);
                    }
                }
                finally
                {
                    model.SketchManager.AddToDB = prevDb;
                    model.SketchManager.DisplayWhenAdded = prevDisp;
                    foreach (SketchPoint pt in pts) Release(pt);
                }
            });
        }

        static void FullRepairMasterSketch126(BuildContext c, ModelDoc2 model, double depthMm)
        {
            double targetY = (depthMm - 97.0) / 1000.0;
            Action<bool> moveIfNeeded = delegate(bool requireExactAfter)
            {
                bool moved = false;
                FullEditSketchAction(c, model, "草图126", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
                {
                    object[] segs = Items(sk.GetSketchSegments());
                    var rearArcs = new List<SketchArc>();
                    var ptsToMove = new List<SketchPoint>();
                    try
                    {
                        foreach (object raw in segs)
                        {
                            var seg = raw as SketchSegment;
                            if (seg == null) continue;
                            if (seg.GetType() == (int)swSketchSegments_e.swSketchARC)
                            {
                                var arc = seg as SketchArc;
                                SketchPoint ctr = arc == null ? null : arc.GetCenterPoint2() as SketchPoint;
                                if (ctr != null)
                                {
                                    double yMm = ctr.Y * 1000.0;
                                    bool isSeedRear = Math.Abs(yMm - 403.0) < 0.05 || Math.Abs(yMm - 453.0) < 0.05;
                                    bool isTarget = Math.Abs(yMm - (depthMm - 97.0)) < 0.05;
                                    if (isSeedRear || (!isTarget && Math.Abs(yMm - 80.0) > 2.0 && Math.Abs(yMm - 265.0) > 2.0 && Math.Abs(yMm - 450.0) > 2.0))
                                    {
                                        rearArcs.Add(arc);
                                        FullAddUniquePoint(ptsToMove, ctr);
                                        ctr = null;
                                        FullAddUniquePoint(ptsToMove, arc.GetStartPoint2() as SketchPoint);
                                        FullAddUniquePoint(ptsToMove, arc.GetEndPoint2() as SketchPoint);
                                    }
                                    if (ctr != null) Release(ctr);
                                }
                            }
                            if (!rearArcs.Contains(seg as SketchArc)) Release(seg);
                        }
                        if (rearArcs.Count > 0)
                        {
                            SketchRelationManager mgr = sk.RelationManager;
                            try
                            {
                                object[] allRels = Items(mgr.GetRelations((int)swSketchRelationFilterType_e.swAll));
                                foreach (object rRaw in allRels)
                                {
                                    var rel = rRaw as SketchRelation;
                                    try
                                    {
                                        if (rel == null) continue;
                                        int t = rel.GetRelationType();
                                        bool isTypeToStrip = t == 9 || t == 12 || t == (int)swConstraintType_e.swConstraintType_FIXED;
                                        object[] ents = Items(rel.GetEntities());
                                        bool touchesRear = false;
                                        foreach (object e in ents)
                                        {
                                            foreach (var a in rearArcs) if (SameNativeObject(e, a)) touchesRear = true;
                                            foreach (var p in ptsToMove) if (SameNativeObject(e, p)) touchesRear = true;
                                            Release(e);
                                        }
                                        if (touchesRear && (isTypeToStrip || t == (int)swConstraintType_e.swConstraintType_SNAPANGLE))
                                            mgr.DeleteRelation(rel);
                                    }
                                    finally { Release(rel); }
                                }
                                object[] extRels = Items(mgr.GetRelations((int)swSketchRelationFilterType_e.swExternal));
                                foreach (object rRaw in extRels)
                                {
                                    var rel = rRaw as SketchRelation;
                                    try { if (rel != null) mgr.DeleteRelation(rel); }
                                    finally { Release(rel); }
                                }
                            }
                            finally { Release(mgr); }

                            foreach (var arc in rearArcs)
                            {
                                SketchPoint ctr = arc.GetCenterPoint2() as SketchPoint;
                                SketchPoint st = arc.GetStartPoint2() as SketchPoint;
                                SketchPoint en = arc.GetEndPoint2() as SketchPoint;
                                try
                                {
                                    if (ctr != null && Math.Abs(ctr.Y - targetY) > 1e-6)
                                    {
                                        double dy = targetY - ctr.Y;
                                        ctr.SetCoords(ctr.X, ctr.Y + dy, ctr.Z);
                                        if (st != null && !SameNativeObject(st, ctr)) st.SetCoords(st.X, st.Y + dy, st.Z);
                                        if (en != null && !SameNativeObject(en, ctr) && (st == null || !SameNativeObject(en, st))) en.SetCoords(en.X, en.Y + dy, en.Z);
                                        moved = true;
                                    }
                                }
                                finally { Release(en); Release(st); Release(ctr); }
                            }
                        }
                    }
                    finally
                    {
                        foreach (var p in ptsToMove) Release(p);
                        foreach (var a in rearArcs) Release(a);
                    }
                });
                if (requireExactAfter && moved)
                    Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareMasterPart.Sketch126"), "FULL_MASTER_SKETCH126_REBUILD_FAILED");
            };

            moveIfNeeded(false);
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareMasterPart"), "FULL_MASTER_REBUILD_FAILED");
            moveIfNeeded(true);

            Feature feat = FullFindFeature(model, "草图126");
            try
            {
                Sketch sk = feat.GetSpecificFeature2() as Sketch;
                try
                {
                    int verifiedRear = 0;
                    foreach (object raw in Items(sk.GetSketchSegments()))
                    {
                        var arc = raw as SketchArc;
                        try
                        {
                            if (arc == null) continue;
                            SketchPoint ctr = arc.GetCenterPoint2() as SketchPoint;
                            try
                            {
                                if (ctr != null && Math.Abs(ctr.Y * 1000.0 - (depthMm - 97.0)) < 0.1)
                                    verifiedRear++;
                            }
                            finally { Release(ctr); }
                        }
                        finally { Release(raw); }
                    }
                    Need(verifiedRear >= 2, "FULL_MASTER_SKETCH126_REAR_ARCS_VERIFY_FAILED count=" + verifiedRear.ToString(Invariant));
                }
                finally { Release(sk); }
            }
            finally { Release(feat); }
        }

        static void FullPrepareMasterPart(BuildContext c, ModelDoc2 model, double widthMm, double heightMm, double depthMm, double zFront, double zBack, double gridH, double grid2000, List<object> changes)
        {
            SetDimension(model, "D1@草图1", widthMm, changes);
            SetDimension(model, "D2@草图130", widthMm - 150.0, changes);
            SetDimension(model, "D2@草图142", (widthMm - 160.0) / 2.0, changes);
            SetDimension(model, "D1@草图143", (widthMm - 160.0) / 2.0, changes);
            SetDimension(model, "D3@草图137", (widthMm - 300.0) / 2.0, changes);
            SetDimension(model, "D2@草图1", heightMm, changes);
            SetDimension(model, "D1@草图76", (heightMm - 87.0) / 12.0 - 7.0, changes);
            SetDimension(model, "D3@阵列(线性)6", (heightMm - 87.0) / 12.0, changes);
            SetDimension(model, "D1@凸台-拉伸1", depthMm, changes);
            SetDimension(model, "D4@草图130", depthMm - 124.0, changes);
            SetDimension(model, "D3@草图84", depthMm - 100.0, changes);
            double thirdDepth = (depthMm - 100.0) / 3.0;
            SetDimension(model, "D6@草图120", thirdDepth, changes);
            SetDimension(model, "D7@草图120", thirdDepth, changes);
            SetDimension(model, "D8@草图120", thirdDepth, changes);
            SetDimension(model, "D3@草图122", thirdDepth, changes);
            SetDimension(model, "D4@草图122", thirdDepth, changes);
            SetDimension(model, "D5@草图122", thirdDepth, changes);
            SetDimension(model, "D2@草图126", (depthMm - 130.0) / 2.0, changes);

            if (depthMm < 450.0 - 1e-6)
            {
                double targetTopY = (1836.4 + heightMm - 1917.0) / 1000.0;
                double targetCenterX = zFront / 1000.0;
                FullEditSketchAction(c, model, "草图100", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
                {
                    FullDeleteSketchExternalRelations(sk);
                    List<SketchPoint> pts = FullGetSegmentSketchPoints(sk);
                    try
                    {
                        Need(pts.Count > 0, "FULL_MASTER_SKETCH100_EMPTY");
                        double minX = Double.MaxValue, maxX = Double.MinValue;
                        foreach (SketchPoint pt in pts)
                        {
                            if (pt.X < minX) minX = pt.X;
                            if (pt.X > maxX) maxX = pt.X;
                        }
                        double dx = targetCenterX - 0.5 * (minX + maxX);
                        // Moving one point translates the rigidly constrained rectangle once; moving every point re-applies dx N times.
                        if (Math.Abs(dx) > 1e-6)
                            pts[0].SetCoords(pts[0].X + dx, pts[0].Y, pts[0].Z);

                        double minX2 = Double.MaxValue, maxX2 = Double.MinValue, maxWy = Double.MinValue;
                        foreach (SketchPoint pt in pts)
                        {
                            if (pt.X < minX2) minX2 = pt.X;
                            if (pt.X > maxX2) maxX2 = pt.X;
                            double[] w = FullToWorld(math, s2m, pt.X, pt.Y, pt.Z);
                            if (w[1] > maxWy) maxWy = w[1];
                        }
                        Need(Math.Abs(0.5 * (minX2 + maxX2) - targetCenterX) <= 1e-4, "FULL_MASTER_SKETCH100_X_ALIGN_FAILED");
                        Need(Math.Abs(maxWy - targetTopY) <= 1e-4, "FULL_MASTER_SKETCH100_Y_ALIGN_FAILED");
                    }
                    finally { foreach (SketchPoint pt in pts) Release(pt); }
                });
                SetDimension(model, "D3@阵列(线性)7", zBack - zFront, changes);
            }

            // Sketch126 already scales D2 with depthMm and preserves Seg[6..7] on its native edge references; detaching external relations collapses Seg[1]/Seg[4] onto Seg[6]/Seg[7].
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareMasterPart"), "FULL_MASTER_REBUILD_FAILED");
        }

        static void FullPrepareBasePlate(BuildContext c, ModelDoc2 model, double widthMm, double depthMm, List<object> changes)
        {
            FullEditSketchAction(c, model, "草图9", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
            {
                foreach (object raw in Items(sk.GetSketchBlockInstances()))
                {
                    var inst = raw as SketchBlockInstance;
                    try
                    {
                        if (inst == null) continue;
                        MathPoint pos = inst.InstancePosition;
                        try
                        {
                            double[] s = Numbers(pos.ArrayData);
                            double[] w = FullToWorld(math, s2m, s[0], s[1], s[2]);
                            double targetWx = (w[0] < 0 ? -1.0 : 1.0) * (widthMm - 150.0) / 2000.0;
                            double targetWz = Math.Abs(Math.Abs(w[2] * 1000.0) - 74.5) < 5.0 ? w[2] : -(depthMm - 49.5) / 1000.0;
                            double[] newS = FullToSketch(math, m2s, targetWx, w[1], targetWz);
                            MathPoint np = math.CreatePoint(new[] { newS[0], newS[1], newS[2] }) as MathPoint;
                            try { inst.InstancePosition = np; }
                            finally { Release(np); }
                        }
                        finally { Release(pos); }
                    }
                    finally { Release(inst); }
                }
            });
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareBasePlate"), "FULL_BASE_PLATE_REBUILD_FAILED");

            double slotGapMm = Double.MaxValue;
            Feature fKo = FullFindFeature(model, "FeedbackKnockoutFront");
            Feature fSlot = FullFindFeature(model, "FeedbackSlotEnds");
            try
            {
                if (fKo != null && fSlot != null)
                {
                    double koMaxZ = Double.MinValue;
                    double slotMinZ = Double.MaxValue;
                    foreach (object raw in Items(fKo.GetFaces()))
                    {
                        var face = raw as Face2;
                        try { if (face != null) { double[] b = Numbers(face.GetBox()); koMaxZ = Math.Max(koMaxZ, b[5] * 1000.0); } }
                        finally { Release(face); }
                    }
                    foreach (object raw in Items(fSlot.GetFaces()))
                    {
                        var face = raw as Face2;
                        try { if (face != null) { double[] b = Numbers(face.GetBox()); slotMinZ = Math.Min(slotMinZ, b[2] * 1000.0); } }
                        finally { Release(face); }
                    }
                    if (koMaxZ > Double.MinValue && slotMinZ < Double.MaxValue)
                    {
                        slotGapMm = slotMinZ - koMaxZ;
                        if (slotGapMm < 3.0)
                        {
                            double curD2 = DimensionValue(model, "D2@草图6");
                            double newD2 = Math.Max(20.0, curD2 - (3.0 - slotGapMm));
                            SetDimension(model, "D2@草图6", newD2, changes);
                            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareBasePlate.SlotShorten"), "FULL_BASE_PLATE_SLOT_SHORTEN_REBUILD_FAILED");
                        }
                    }
                }
            }
            finally
            {
                Release(fSlot);
                Release(fKo);
            }
            c.Event(new { inspection = "full_base_plate_slot_check", path = model.GetPathName(), slotGapMm = slotGapMm });
        }

        static void FullPrepareBaseStiffener(BuildContext c, ModelDoc2 model, double widthMm, List<object> changes)
        {
            double targetAbsX = widthMm / 2.0 - 35.0;
            bool hasLeft = false, hasRight = false;
            Feature fCut2 = FullFindFeature(model, "切除-拉伸2");
            try
            {
                if (fCut2 != null)
                {
                    foreach (object raw in Items(fCut2.GetFaces()))
                    {
                        var face = raw as Face2;
                        Surface surf = null;
                        try
                        {
                            if (face == null) continue;
                            surf = face.GetSurface() as Surface;
                            if (surf != null && surf.IsCylinder())
                            {
                                double[] p = Numbers(surf.CylinderParams);
                                double rMm = p[6] * 1000.0;
                                double xMm = p[0] * 1000.0;
                                if (Math.Abs(rMm - 7.5) < 0.25)
                                {
                                    if (Math.Abs(xMm + targetAbsX) < 0.5) hasLeft = true;
                                    if (Math.Abs(xMm - targetAbsX) < 0.5) hasRight = true;
                                }
                            }
                        }
                        finally { Release(surf); Release(face); }
                    }
                }
            }
            finally { Release(fCut2); }

            string customCutName = "切除-拉伸_调整脚Ø15_X335_V33";
            if (hasLeft && hasRight)
            {
                if (FindFeatureByName(model, customCutName) != null)
                    SetFeatureSuppression(model, customCutName, true, "full_base_stiffener_native_holes_exist");
            }
            else
            {
                if (FindFeatureByName(model, "草图_调整脚Ø15_X335_V33") != null)
                {
                    FullEditSketchAction(c, model, "草图_调整脚Ø15_X335_V33", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
                    {
                        foreach (object raw in Items(sk.GetSketchSegments()))
                        {
                            var arc = raw as SketchArc;
                            try
                            {
                                if (arc == null || Math.Abs(arc.GetRadius() * 1000.0 - 7.5) >= 0.5) continue;
                                SketchPoint ctr = arc.GetCenterPoint2() as SketchPoint;
                                SketchPoint st = arc.GetStartPoint2() as SketchPoint;
                                SketchPoint en = arc.GetEndPoint2() as SketchPoint;
                                try
                                {
                                    if (ctr == null) continue;
                                    double[] wc = FullToWorld(math, s2m, ctr.X, ctr.Y, ctr.Z);
                                    double targetWx = (wc[0] < 0 ? -targetAbsX : targetAbsX) / 1000.0;
                                    double dwx = targetWx - wc[0];
                                    FullMovePointWorld(ctr, math, m2s, wc[0] + dwx, wc[1], wc[2]);
                                    if (st != null && !SameNativeObject(st, ctr))
                                    {
                                        double[] ws = FullToWorld(math, s2m, st.X, st.Y, st.Z);
                                        FullMovePointWorld(st, math, m2s, ws[0] + dwx, ws[1], ws[2]);
                                    }
                                    if (en != null && !SameNativeObject(en, ctr) && (st == null || !SameNativeObject(en, st)))
                                    {
                                        double[] we = FullToWorld(math, s2m, en.X, en.Y, en.Z);
                                        FullMovePointWorld(en, math, m2s, we[0] + dwx, we[1], we[2]);
                                    }
                                }
                                finally { Release(en); Release(st); Release(ctr); }
                            }
                            finally { Release(arc); }
                        }
                    });
                }
                if (FindFeatureByName(model, customCutName) != null)
                    SetFeatureSuppression(model, customCutName, false, "full_base_stiffener_custom_foot_holes");
            }
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareBaseStiffener"), "FULL_BASE_STIFFENER_REBUILD_FAILED");
            changes.Add(new { feature = customCutName, nativeHasFootHoles = hasLeft && hasRight, suppressed = hasLeft && hasRight, targetAbsX = targetAbsX });
        }

        static void FullPrepareTopBase(BuildContext c, ModelDoc2 model, double heightMm, double depthMm, List<object> changes)
        {
            foreach (string skName in new[] { "草图2", "草图7", "草图8" })
            {
                if (FullFindFeature(model, skName) != null)
                    FullEditSketchAction(c, model, skName, delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m) { FullDeleteSketchExternalRelations(sk); });
            }
            if (Math.Abs(heightMm - 2000.0) > 1e-4)
                FullTranslateSketchWorld(c, model, "草图8", 0.0, (heightMm - 2000.0) / 1000.0, 0.0, false);
            if (depthMm < 450.0 - 1e-6)
            {
                SetDimension(model, "D2@草图2", Math.Min(190.0, depthMm - 170.0), changes);
                SetDimension(model, "D4@草图7", Math.Min(160.0, depthMm - 170.0), changes);
            }
            FullEditSketchAction(c, model, "草图7", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
            {
                foreach (object raw in Items(sk.GetSketchSegments()))
                {
                    var arc = raw as SketchArc;
                    try
                    {
                        if (arc == null) continue;
                        SketchPoint ctr = arc.GetCenterPoint2() as SketchPoint;
                        SketchPoint st = arc.GetStartPoint2() as SketchPoint;
                        SketchPoint en = arc.GetEndPoint2() as SketchPoint;
                        try
                        {
                            if (ctr == null) continue;
                            double[] wc = FullToWorld(math, s2m, ctr.X, ctr.Y, ctr.Z);
                            double absZmm = Math.Abs(wc[2] * 1000.0);
                            double? newAbsZ = null;
                            if (Math.Abs(absZmm - 450.0) < 1.5 || Math.Abs(absZmm - 500.0) < 1.5) newAbsZ = depthMm - 50.0;
                            else if (Math.Abs(absZmm - 265.0) < 1.5 || Math.Abs(absZmm - 290.0) < 1.5) newAbsZ = (depthMm + 30.0) / 2.0;
                            else if (Math.Abs(absZmm - 403.0) < 1.5 || Math.Abs(absZmm - 453.0) < 1.5) newAbsZ = depthMm - 97.0;
                            if (newAbsZ.HasValue)
                            {
                                double dwz = (-newAbsZ.Value / 1000.0) - wc[2];
                                if (Math.Abs(dwz) > 1e-7)
                                {
                                    FullMovePointWorld(ctr, math, m2s, wc[0], wc[1], wc[2] + dwz);
                                    if (st != null && !SameNativeObject(st, ctr))
                                    {
                                        double[] ws = FullToWorld(math, s2m, st.X, st.Y, st.Z);
                                        FullMovePointWorld(st, math, m2s, ws[0], ws[1], ws[2] + dwz);
                                    }
                                    if (en != null && !SameNativeObject(en, ctr) && (st == null || !SameNativeObject(en, st)))
                                    {
                                        double[] we = FullToWorld(math, s2m, en.X, en.Y, en.Z);
                                        FullMovePointWorld(en, math, m2s, we[0], we[1], we[2] + dwz);
                                    }
                                }
                            }
                        }
                        finally { Release(en); Release(st); Release(ctr); }
                    }
                    finally { Release(arc); }
                }
                if (Math.Abs(depthMm - 500.0) > 1e-4)
                {
                        var notchPts = new List<SketchPoint>();
                        var notchTargets = new List<double[]>();
                        double dNotchZ = (500.0 - depthMm) / 1000.0;
                        foreach (object raw in Items(sk.GetSketchSegments()))
                        {
                            var seg = raw as SketchSegment;
                            var line = raw as SketchLine;
                            try
                            {
                                if (line == null || seg == null || seg.ConstructionGeometry) continue;
                                SketchPoint p1 = line.GetStartPoint2() as SketchPoint;
                                SketchPoint p2 = line.GetEndPoint2() as SketchPoint;
                                foreach (SketchPoint cand in new[] { p1, p2 })
                                {
                                    if (cand == null) continue;
                                    bool dup = false;
                                    foreach (SketchPoint existing in notchPts)
                                    {
                                        if (SameNativeObject(existing, cand)) { dup = true; break; }
                                    }
                                    if (dup) { Release(cand); continue; }
                                    double[] w = FullToWorld(math, s2m, cand.X, cand.Y, cand.Z);
                                    if (w[2] * 1000.0 < -400.0)
                                    {
                                        notchPts.Add(cand);
                                        notchTargets.Add(new[] { w[0], w[1], w[2] + dNotchZ });
                                    }
                                    else Release(cand);
                                }
                            }
                            finally { Release(raw); }
                        }
                        bool prevDb = model.SketchManager.AddToDB;
                        bool prevDisp = model.SketchManager.DisplayWhenAdded;
                        try
                        {
                            model.SketchManager.AddToDB = true;
                            model.SketchManager.DisplayWhenAdded = false;
                            for (int i = 0; i < notchPts.Count; i++)
                            {
                                double[] t = notchTargets[i];
                                double[] wNow = FullToWorld(math, s2m, notchPts[i].X, notchPts[i].Y, notchPts[i].Z);
                                if (Math.Abs(wNow[2] - t[2]) > 1e-7)
                                    FullMovePointWorld(notchPts[i], math, m2s, t[0], t[1], t[2]);
                            }
                        }
                        finally
                        {
                            model.SketchManager.AddToDB = prevDb;
                            model.SketchManager.DisplayWhenAdded = prevDisp;
                            foreach (SketchPoint pt in notchPts) Release(pt);
                        }
                }
            });
            RefreshNativeLinks(c, model);
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareTopBase"), "FULL_TOP_BASE_REBUILD_FAILED");
        }

        static void FullPrepareTopBack(BuildContext c, ModelDoc2 model, double widthMm, List<object> changes)
        {
            string cancelHoles = "反馈_取消后侧两孔";
            if (FindFeatureByName(model, cancelHoles) != null)
            {
                bool suppress = widthMm <= 894.5;
                SetFeatureSuppression(model, cancelHoles, suppress, "full_top_back_width_rule");
                changes.Add(new { feature = cancelHoles, suppressed = suppress, widthMm = widthMm });
            }
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareTopBack"), "FULL_TOP_BACK_REBUILD_FAILED");
        }

        static void FullSelectFaceAtY(ModelDoc2 model, double targetYmm)
        {
            VariableTrial.ModelClearSelection2(model, true, "FullSelectFaceAtY");
            foreach (object bRaw in Items(((PartDoc)model).GetBodies2(0, false)))
            {
                var body = bRaw as Body2;
                try
                {
                    if (body == null) continue;
                    foreach (object fRaw in Items(body.GetFaces()))
                    {
                        var face = fRaw as Face2;
                        Surface surf = null;
                        try
                        {
                            if (face == null) continue;
                            surf = face.GetSurface() as Surface;
                            if (surf != null && surf.IsPlane())
                            {
                                double[] p = Numbers(surf.PlaneParams);
                                double[] b = Numbers(face.GetBox());
                                double yMinMm = b[1] * 1000.0, yMaxMm = b[4] * 1000.0;
                                if (Math.Abs(p[1]) > 0.99 && Math.Abs(yMinMm - targetYmm) < 0.1 && Math.Abs(yMaxMm - targetYmm) < 0.1)
                                {
                                    var ent = face as Entity;
                                    Need(ent != null && ent.Select4(false, null), "FULL_SELECT_TOP_FACE_FAILED " + targetYmm.ToString(Invariant));
                                    return;
                                }
                            }
                        }
                        finally { Release(surf); Release(face); }
                    }
                }
                finally { Release(body); }
            }
            Need(false, "FULL_TOP_FACE_NOT_FOUND " + targetYmm.ToString(Invariant));
        }

        static void FullDeleteFeatureIfExists(ModelDoc2 model, string name)
        {
            Feature f = FullFindFeature(model, name);
            if (f == null) return;
            try
            {
                model.ClearSelection2(true);
                Need(f.Select2(false, 0), "FULL_DELETE_SELECT_FAILED " + name);
                Need(model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed), "FULL_DELETE_FEATURE_FAILED " + name);
            }
            finally { Release(f); }
        }

        static void FullRebuildShallowTopSide(BuildContext c, ModelDoc2 model, double widthMm, double heightMm, double depthMm, List<object> changes)
        {
            if (depthMm >= 300.0 - 1e-6)
                SetDimension(model, "D9@草图3", depthMm - 280.0, changes);

            var sketch4Blocks = new List<Tuple<SketchBlockDefinition, double[], double, double>>();
            try
            {
                FullEditSketchAction(c, model, "草图4", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
                {
                    foreach (object raw in Items(sk.GetSketchBlockInstances()))
                    {
                        var inst = raw as SketchBlockInstance;
                        if (inst == null) continue;
                        MathPoint pos = inst.InstancePosition;
                        SketchBlockDefinition def = inst.Definition;
                        try
                        {
                            double[] s = Numbers(pos.ArrayData);
                            double[] w = FullToWorld(math, s2m, s[0], s[1], s[2]);
                            sketch4Blocks.Add(Tuple.Create(def, w, inst.Scale2, inst.Angle));
                        }
                        finally { Release(pos); Release(inst); }
                    }
                });
                Need(sketch4Blocks.Count >= 3, "FULL_SHALLOW_TOP_SIDE_BLOCKS_MISSING");
                var sorted4 = sketch4Blocks.OrderByDescending(v => v.Item2[2]).ToList();
                double[] zsMm = depthMm < 300.0 - 1e-6
                    ? new[] { -94.5, -194.5 }
                    : new[] { -94.5, -194.5, -(depthMm - 85.5) };
                double targetWx = -(widthMm / 2.0 - 19.5) / 1000.0;
                double targetWy = (heightMm - 0.8) / 1000.0;

                FullDeleteFeatureIfExists(model, "凸台-拉伸2");
                FullDeleteFeatureIfExists(model, "草图4");

                // Boss-extrude press-nut blocks on underside face Y=H-0.8, preserving 草图3 / 切除-拉伸2 pre-holes
                FullSelectFaceAtY(model, heightMm - 0.8);
                model.SketchManager.InsertSketch(true);
                MathUtility mathBoss = VariableTrial.SwGetMathUtility(c.Sw, "FullRebuildShallowTopSide.Boss");
                Sketch skBoss = model.SketchManager.ActiveSketch;
                MathTransform m2sBoss = skBoss.ModelToSketchTransform;
                try
                {
                    for (int i = 0; i < zsMm.Length; i++)
                    {
                        double[] s = FullToSketch(mathBoss, m2sBoss, targetWx, targetWy, zsMm[i] / 1000.0);
                        MathPoint mp = mathBoss.CreatePoint(new[] { s[0], s[1], s[2] }) as MathPoint;
                        SketchBlockInstance placed = null;
                        try
                        {
                            placed = model.SketchManager.InsertSketchBlockInstance(sorted4[i].Item1, mp, sorted4[i].Item3, sorted4[i].Item4);
                            Need(placed != null, "FULL_SHALLOW_TOP_INSERT_BOSS_BLOCK_FAILED");
                        }
                        finally { Release(placed); Release(mp); }
                    }
                }
                finally { Release(m2sBoss); Release(skBoss); Release(mathBoss); }
                model.InsertSketch2(true);
                Feature bossFeat = model.FeatureManager.FeatureExtrusion3(true, false, false, (int)swEndConditions_e.swEndCondBlind, 0, 0.004, 0.0, false, false, false, false, 0, 0, false, false, false, false, true, true, true, 0, 0, false);
                Need(bossFeat != null, "FULL_SHALLOW_TOP_BOSS_FAILED");
                Release(bossFeat);
                Need(VariableTrial.ModelForceRebuild3(model, false, "FullRebuildShallowTopSide"), "FULL_SHALLOW_TOP_SIDE_REBUILD_FAILED");
                changes.Add(new { inspection = "rebuild_shallow_top_side", widthMm = widthMm, depthMm = depthMm, zsMm = zsMm });
            }
            finally
            {
                foreach (var item in sketch4Blocks) Release(item.Item1);
            }
        }

        static double[] FullMeasureSketchWorldBox(BuildContext c, ModelDoc2 model, string sketchName, bool solidOnly)
        {
            var box = new[] { Double.MaxValue, Double.MaxValue, Double.MaxValue, Double.MinValue, Double.MinValue, Double.MinValue };
            int count = 0;
            FullEditSketchAction(c, model, sketchName, delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
            {
                foreach (object raw in Items(sk.GetSketchSegments()))
                {
                    var seg = raw as SketchSegment;
                    var line = raw as SketchLine;
                    var arc = raw as SketchArc;
                    try
                    {
                        if (seg == null || (solidOnly && seg.ConstructionGeometry)) continue;
                        var segPts = new List<SketchPoint>();
                        if (line != null)
                        {
                            segPts.Add(line.GetStartPoint2() as SketchPoint);
                            segPts.Add(line.GetEndPoint2() as SketchPoint);
                        }
                        else if (arc != null)
                        {
                            segPts.Add(arc.GetStartPoint2() as SketchPoint);
                            segPts.Add(arc.GetEndPoint2() as SketchPoint);
                            segPts.Add(arc.GetCenterPoint2() as SketchPoint);
                        }
                        try
                        {
                            foreach (SketchPoint pt in segPts)
                            {
                                if (pt == null) continue;
                                double[] w = FullToWorld(math, s2m, pt.X, pt.Y, pt.Z);
                                for (int a = 0; a < 3; a++)
                                {
                                    double mm = w[a] * 1000.0;
                                    if (mm < box[a]) box[a] = mm;
                                    if (mm > box[a + 3]) box[a + 3] = mm;
                                }
                                count++;
                            }
                        }
                        finally { foreach (SketchPoint pt in segPts) Release(pt); }
                    }
                    finally { Release(raw); }
                }
            });
            Need(count > 0, "FULL_SKETCH_EMPTY " + sketchName + " in " + model.GetPathName());
            return box;
        }

        static double FullPrepareSidePanel(BuildContext c, ModelDoc2 model, bool isRight, double heightMm, double depthMm, double targetSk19MidZ, List<object> changes)
        {
            if (isRight)
            {
                FullEditSketchAction(c, model, "草图18", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m) { FullDeleteSketchExternalRelations(sk); });
                SetDimension(model, "D3@草图18", (heightMm - 177.0) / 2.0, changes);
            }
            else
            {
                double[] bx18 = FullMeasureSketchWorldBox(c, model, "草图18", false);
                double targetMinY18 = 1879.0 + (heightMm - 2000.0);
                double dy18 = (targetMinY18 - bx18[1]) / 1000.0;
                if (Math.Abs(dy18) > 1e-5)
                    FullTranslateSketchWorld(c, model, "草图18", 0.0, dy18, 0.0, true);
            }

            double[] bx19 = FullMeasureSketchWorldBox(c, model, "草图19", false);
            double curMidZ19 = 0.5 * (bx19[2] + bx19[5]);
            double targetMaxY19 = 1870.69 + (heightMm - 2000.0);
            double dy19 = (targetMaxY19 - bx19[4]) / 1000.0;
            // Left panel 草图19 tracks depth natively via external refs; right panel has no external refs and aligns to left's Z.
            double dz19 = isRight ? (targetSk19MidZ - curMidZ19) / 1000.0 : 0.0;
            if (!isRight && depthMm < 450.0 - 1e-6 && (bx19[2] < -(depthMm - 19.5) || bx19[5] > -19.5))
                dz19 = (-(depthMm + 17.5) / 2.0 - curMidZ19) / 1000.0;
            if (Math.Abs(dy19) > 1e-5 || Math.Abs(dz19) > 1.5e-3)
                FullTranslateSketchWorld(c, model, "草图19", 0.0, dy19, dz19, true);

            SetDimension(model, "D3@阵列(线性)1", (heightMm - 177.0) / 6.0, changes);
            RefreshNativeLinks(c, model);
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareSidePanel"), "FULL_SIDE_PANEL_REBUILD_FAILED " + (isRight ? "R" : "L"));
            return curMidZ19 + dz19 * 1000.0;
        }

        static void FullPreparePartitionSourceOrL(BuildContext c, ModelDoc2 model, bool isLeft, double heightMm, double depthMm, double zFront, double zBack, double gridH, double grid2000, double targetSk21MidZ, List<object> changes)
        {
            SetDimension(model, "D4@草图19", depthMm < 450.0 - 1e-6 ? 240.0 + (depthMm - 550.0) / 2.0 : 240.0, changes);
            SetDimension(model, "D5@草图19", heightMm - 1557.0, changes);

            double[] bx21 = FullMeasureSketchWorldBox(c, model, "草图21", false);
            double targetMaxY21 = 1870.69 + (heightMm - 2000.0);
            double dy21 = (targetMaxY21 - bx21[4]) / 1000.0;
            double dz21 = (targetSk21MidZ - 0.5 * (bx21[2] + bx21[5])) / 1000.0;
            if (Math.Abs(dy21) > 1e-5 || Math.Abs(dz21) > 1.5e-3)
                FullTranslateSketchWorld(c, model, "草图21", 0.0, dy21, dz21, true);

            if (isLeft && depthMm < 450.0 - 1e-6)
            {
                double[] bx9 = FullMeasureSketchWorldBox(c, model, "草图9", false);
                double zMid = 0.5 * (bx9[2] + bx9[5]);
                double targetGridY = 1779.08 + (gridH - grid2000);
                double dGridYm = Math.Abs(bx9[1] - targetGridY) > 1.5 ? (targetGridY - bx9[1]) / 1000.0 : 0.0;
                FullEditSketchAction(c, model, "草图9", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
                {
                    FullDeleteSketchExternalRelations(sk);
                    List<SketchPoint> pts = FullGetSegmentSketchPoints(sk);
                    bool prevDb = model.SketchManager.AddToDB;
                    bool prevDisp = model.SketchManager.DisplayWhenAdded;
                    try
                    {
                        var targets = new List<double[]>(pts.Count);
                        foreach (SketchPoint pt in pts)
                        {
                            double[] w = FullToWorld(math, s2m, pt.X, pt.Y, pt.Z);
                            double wz = (w[2] * 1000.0 > zMid ? -(zFront + 15.0) : -(zBack - 15.0)) / 1000.0;
                            targets.Add(new[] { w[0], w[1] + dGridYm, wz });
                        }
                        model.SketchManager.AddToDB = true;
                        model.SketchManager.DisplayWhenAdded = false;
                        for (int i = 0; i < pts.Count; i++)
                        {
                            double[] t = targets[i];
                            double[] wNow = FullToWorld(math, s2m, pts[i].X, pts[i].Y, pts[i].Z);
                            if (Math.Abs(wNow[1] - t[1]) > 1e-7 || Math.Abs(wNow[2] - t[2]) > 1e-7)
                                FullMovePointWorld(pts[i], math, m2s, t[0], t[1], t[2]);
                        }
                    }
                    finally
                    {
                        model.SketchManager.AddToDB = prevDb;
                        model.SketchManager.DisplayWhenAdded = prevDisp;
                        foreach (SketchPoint pt in pts) Release(pt);
                    }
                });
            }

            // On resized builds, 草图10 external relations become over-constrained against the master cut.
            Feature sk10 = FullFindFeature(model, "草图10");
            if (sk10 != null)
            {
                bool sk10Warn = false;
                int sk10Err = sk10.GetErrorCode2(out sk10Warn);
                Release(sk10);
                if (sk10Err != 0 || sk10Warn || depthMm < 450.0 - 1e-6)
                    FullEditSketchAction(c, model, "草图10", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m) { FullDeleteSketchExternalRelations(sk); });
            }

            string lockSide = isLeft ? "L" : "R";
            if (!LockRowsMatch(PartitionLockHoleSlotRows(model), ExpectedPartitionLockRows(c, lockSide)))
                SuppressPartitionLockPatterns(model, lockSide);

            if (isLeft)
                RefreshNativeLinks(c, model);
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPreparePartitionSourceOrL"), "FULL_PARTITION_REBUILD_FAILED " + (isLeft ? "L" : "R_SRC"));
            RepairPartitionLocks(c, model, lockSide);
        }

        static void FullPrepareShelfL(BuildContext c, ModelDoc2 model, double zFront, double zBack, List<object> changes)
        {
            SetDimension(model, "D3@阵列(线性)1", zBack - zFront, changes);
            RefreshNativeLinks(c, model);
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareShelfL"), "FULL_SHELF_L_LINK_REBUILD_FAILED");
        }

        static double[] FullPrepareSideStiffener1(BuildContext c, ModelDoc2 model, double heightMm, double zFront, double gridH, double grid2000, List<object> changes)
        {
            SetDimension(model, "D3@阵列(线性)1", (heightMm - 87.0) / 12.0, changes);
            double dGridYm = gridH - grid2000;

            Feature sk8 = FullFindFeature(model, "草图8");
            if (sk8 != null)
            {
                Release(sk8);
                double[] bx8 = FullMeasureSketchWorldBox(c, model, "草图8", false);
                double dy8 = (1779.0833333 + dGridYm - bx8[4]) / 1000.0;
                double dz8 = (-zFront - 0.5 * (bx8[2] + bx8[5])) / 1000.0;
                if (Math.Abs(dy8) > 1e-5 || Math.Abs(dz8) > 1e-5)
                {
                    FullEditSketchAction(c, model, "草图8", delegate(Sketch sk, MathUtility math, MathTransform m2s, MathTransform s2m)
                    {
                        SketchRelationManager rm = sk.RelationManager;
                        if (rm != null) { try { rm.DeleteAllRelations(); } finally { Release(rm); } }
                        object[] rawPts = Items(sk.GetSketchPoints2());
                        var pts = new List<SketchPoint>();
                        foreach (object rp in rawPts)
                        {
                            var pt = rp as SketchPoint;
                            if (pt != null && pt.Type != -1) pts.Add(pt);
                            else Release(rp);
                        }
                        bool prevDb = model.SketchManager.AddToDB;
                        bool prevDisp = model.SketchManager.DisplayWhenAdded;
                        try
                        {
                            var targets = new List<double[]>(pts.Count);
                            foreach (SketchPoint pt in pts)
                            {
                                double[] w = FullToWorld(math, s2m, pt.X, pt.Y, pt.Z);
                                targets.Add(new[] { w[0], w[1] + dy8, w[2] + dz8 });
                            }
                            model.SketchManager.AddToDB = true;
                            model.SketchManager.DisplayWhenAdded = false;
                            for (int i = 0; i < pts.Count; i++)
                            {
                                double[] t = targets[i];
                                double[] wNow = FullToWorld(math, s2m, pts[i].X, pts[i].Y, pts[i].Z);
                                if (Math.Abs(wNow[1] - t[1]) > 1e-7 || Math.Abs(wNow[2] - t[2]) > 1e-7)
                                    FullMovePointWorld(pts[i], math, m2s, t[0], t[1], t[2]);
                            }
                        }
                        finally
                        {
                            model.SketchManager.AddToDB = prevDb;
                            model.SketchManager.DisplayWhenAdded = prevDisp;
                            foreach (SketchPoint pt in pts) Release(pt);
                        }
                    });
                }
            }
            Feature sk9 = FullFindFeature(model, "草图9");
            if (sk9 != null)
            {
                Release(sk9);
                double[] bx9 = FullMeasureSketchWorldBox(c, model, "草图9", true);
                double dy9 = (1762.0833333 + dGridYm - bx9[1]) / 1000.0;
                double dz9 = (-zFront - 0.5 * (bx9[2] + bx9[5])) / 1000.0;
                if (Math.Abs(dy9) > 1e-5 || Math.Abs(dz9) > 1e-5)
                    FullTranslateSketchWorld(c, model, "草图9", 0.0, dy9, dz9, true);
            }
            Feature sk10 = FullFindFeature(model, "草图10");
            if (sk10 != null)
            {
                Release(sk10);
                double[] bx10 = FullMeasureSketchWorldBox(c, model, "草图10", false);
                double dz10 = (-zFront - bx10[5]) / 1000.0;
                if (Math.Abs(dz10) > 1e-5)
                    FullTranslateSketchWorld(c, model, "草图10", 0.0, 0.0, dz10, true);
            }

            RefreshNativeLinks(c, model);
            Need(VariableTrial.ModelForceRebuild3(model, false, "FullPrepareSideStiffener1"), "FULL_SIDE_STIFFENER1_REBUILD_FAILED");
            return BodyBounds(model);
        }

        static Dictionary<string, string> FullPrepareStructure(BuildContext c, out double[] gussetBox)
        {
            double W = c.Width, H = c.Height, D = c.Depth;
            double zFront, zBack;
            FullDepthStiffenerPlanes(D, out zFront, out zBack);
            double gridH = FullGridOffset(H), grid2000 = FullGridOffset(2000.0);

            string structDir = Within(Path.Combine(c.Output, "structure"), c.Output);
            Directory.CreateDirectory(structDir);
            var structure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sourceByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var depsBySource = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var closureQueue = new Queue<string>();
            foreach (string fileName in FullStructureSeedNames)
            {
                string src = c.Source(fileName);
                if (!copies.ContainsKey(src))
                {
                    string dst = Within(Path.Combine(structDir, Path.GetFileName(src)), structDir);
                    copies[src] = dst;
                    closureQueue.Enqueue(src);
                }
            }
            while (closureQueue.Count > 0)
            {
                string curSrc = closureQueue.Dequeue();
                string[] deps = Dependencies(c, curSrc);
                depsBySource[curSrc] = deps;
                foreach (string dep in deps)
                {
                    if (!copies.ContainsKey(dep))
                    {
                        string depDst = Within(Path.Combine(structDir, Path.GetFileName(dep)), structDir);
                        Need(!copies.Values.Any(v => v.Equals(depDst, StringComparison.OrdinalIgnoreCase)), "FULL_STRUCTURE_FILENAME_COLLISION " + depDst);
                        copies[dep] = depDst;
                        closureQueue.Enqueue(dep);
                    }
                }
            }
            foreach (var item in copies)
            {
                VariableTrial.RuntimeOwnership();
                Need(File.Exists(item.Key), "FULL_STRUCTURE_SOURCE_MISSING " + item.Key);
                Need(!File.Exists(item.Value), "PRESERVE_FULL_STRUCTURE_PART " + item.Value);
                File.Copy(item.Key, item.Value, false);
                string leafName = Path.GetFileName(item.Key);
                if (!structure.ContainsKey(leafName)) structure[leafName] = item.Value;
                sourceByPath[item.Value] = item.Key;
            }
            foreach (var item in copies)
            {
                VariableTrial.RuntimeOwnership();
                foreach (string dep in depsBySource[item.Key])
                {
                    Need(copies.ContainsKey(dep), "FULL_STRUCTURE_CLOSURE_INCOMPLETE " + dep);
                    Need(VariableTrial.SwReplaceReferencedDocument(c.Sw, item.Value, dep, copies[dep], "FullPrepareStructure"), "FULL_REPOINT_FAILED " + Path.GetFileName(item.Key) + " -> " + Path.GetFileName(dep));
                }
            }
            c.Event(new { inspection = "full_structure_dependency_closure", copiedFileCount = copies.Count, seedPartCount = FullStructureSeedNames.Length });

            var indexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var metaByPath = new Dictionary<string, Tuple<string, string, List<object>, object, object>>(StringComparer.OrdinalIgnoreCase);

            Action<string, string, Action<ModelDoc2, List<object>>> processPart = delegate(string fileName, string role, Action<ModelDoc2, List<object>> editAction)
            {
                string path = structure[fileName];
                var changes = new List<object>();
                ModelDoc2 model = c.Open(path, false);
                try
                {
                    CheckFeatureHealth(model, c, "full_before_" + role, false);
                    object beforeGeom = PartGeometry(model);
                    if (editAction != null)
                    {
                        try { editAction(model, changes); }
                        catch
                        {
                            try { CheckFeatureHealth(model, c, "full_error_diag_" + role, false); }
                            catch { }
                            throw;
                        }
                    }
                    CheckFeatureHealth(model, c, "full_after_" + role, true);
                    FullEnsureMaterial(c, model);
                    c.Save(model);
                    int idx = c.Parts.Count;
                    c.Parts.Add(MakeStructurePartRecord(role, path, sourceByPath[path], changes, null, beforeGeom, PartGeometry(model)));
                    indexByPath[path] = idx;
                    metaByPath[path] = Tuple.Create(role, sourceByPath[path], changes, (object)null, beforeGeom);
                    c.Write();
                }
                finally { c.Close(model); c.CloseOutputDocuments(); }
            };

            // 1. Cabinet Master Skeleton
            processPart("标准寄存柜 模型.SLDPRT", "master_cabinet", delegate(ModelDoc2 m, List<object> ch)
            {
                FullPrepareMasterPart(c, m, W, H, D, zFront, zBack, gridH, grid2000, ch);
            });

            // 2. Base Skeleton & Base Parts
            processPart("底座 模型.sldprt", "master_base", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.master_base"), "FULL_BASE_MASTER_REBUILD_FAILED");
            });
            processPart("底座外框.sldprt", "base_frame", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.base_frame"), "FULL_BASE_FRAME_REBUILD_FAILED");
            });
            processPart("底座底板.sldprt", "base_plate", delegate(ModelDoc2 m, List<object> ch)
            {
                // Move 草图9 knockouts inside the resized W x D plate before RefreshNativeLinks rebuilds 切除-拉伸5.
                FullPrepareBasePlate(c, m, W, D, ch);
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.base_plate"), "FULL_BASE_PLATE_LINK_REBUILD_FAILED");
            });
            processPart("底座加强筋.sldprt", "base_stiffener", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.base_stiffener"), "FULL_BASE_STIFFENER_LINK_REBUILD_FAILED");
                FullPrepareBaseStiffener(c, m, W, ch);
            });
            foreach (string stdName in new[] { "螺母M12.SLDPRT", "调整脚 M12X60(模型).SLDPRT" })
            {
                processPart(stdName, stdName.StartsWith("螺母") ? "base_nut" : "base_foot", delegate(ModelDoc2 m, List<object> ch)
                {
                    Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure." + stdName), "FULL_BASE_HARDWARE_REBUILD_FAILED " + stdName);
                });
            }

            // 3. Top Skeleton & Top Parts
            processPart("上盖 模型.sldprt", "master_top", delegate(ModelDoc2 m, List<object> ch)
            {
                if (D < 450.0 - 1e-6)
                    SetDimension(m, "D1@草图7", Math.Min(300.0, D - 100.0), ch);
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.master_top"), "FULL_TOP_MASTER_REBUILD_FAILED");
            });
            processPart("上盖壳体底板.sldprt", "top_plate", delegate(ModelDoc2 m, List<object> ch) { FullPrepareTopBase(c, m, H, D, ch); });
            processPart("上盖壳体前侧板.sldprt", "top_front", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.top_front"), "FULL_TOP_FRONT_REBUILD_FAILED"); });
            processPart("上盖壳体后侧板.sldprt", "top_back", delegate(ModelDoc2 m, List<object> ch)
            {
                FullPrepareTopBack(c, m, W, ch);
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.top_back"), "FULL_TOP_BACK_REBUILD_FAILED");
            });
            processPart("上盖壳体左侧板.sldprt", "top_left", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.top_left"), "FULL_TOP_LEFT_LINK_REBUILD_FAILED");
                if (Math.Abs(W - 1000.0) > 1e-3 || Math.Abs(D - 500.0) > 1e-3) FullRebuildShallowTopSide(c, m, W, H, D, ch);
            });
            processPart("上盖壳体右侧板.SLDPRT", "top_right", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.top_right"), "FULL_TOP_RIGHT_REBUILD_FAILED"); });

            // 4. Side Panels, Partitions, Shelves, Stiffeners, Maintenance
            double sk19MidZ = -273.02;
            processPart("箱体左侧板.sldprt", "side_panel_left", delegate(ModelDoc2 m, List<object> ch) { sk19MidZ = FullPrepareSidePanel(c, m, false, H, D, sk19MidZ, ch); });
            processPart("箱体右侧板.sldprt", "side_panel_right", delegate(ModelDoc2 m, List<object> ch) { FullPrepareSidePanel(c, m, true, H, D, sk19MidZ, ch); });
            processPart("箱体竖隔板L.sldprt", "partition_left", delegate(ModelDoc2 m, List<object> ch) { FullPreparePartitionSourceOrL(c, m, true, H, D, zFront, zBack, gridH, grid2000, sk19MidZ, ch); });
            processPart("箱体竖隔板R参数源.SLDPRT", "partition_right_source", delegate(ModelDoc2 m, List<object> ch) { FullPreparePartitionSourceOrL(c, m, false, H, D, zFront, zBack, gridH, grid2000, sk19MidZ, ch); });
            processPart("箱体竖隔板R_parametric.SLDPRT", "partition_right", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); LockMirrorExternalReferences(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.partition_right"), "FULL_PARTITION_R_REBUILD_FAILED"); });
            processPart("箱体横层板L.sldprt", "shelf_left", delegate(ModelDoc2 m, List<object> ch)
            {
                FullPrepareShelfL(c, m, zFront, zBack, ch);
                ch.Add(ApplyShelfLocatorHoleRemoval(c, m, 8, false));
            });
            processPart("箱体横层板R.SLDPRT", "shelf_right", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.shelf_right"), "FULL_SHELF_R_REBUILD_FAILED");
                ch.Add(ApplyShelfLocatorHoleRemoval(c, m, 8, true));
            });
            processPart("箱体横层板加强筋.SLDPRT", "shelf_stiffener", delegate(ModelDoc2 m, List<object> ch)
            {
                SetDimension(m, "D2@草图1", (W - 142.0) / 2.0, ch);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.shelf_stiffener"), "FULL_SHELF_STIFFENER_REBUILD_FAILED");
            });

            double[] localGussetBox = null;
            processPart("箱体侧板加强筋1.sldprt", "side_stiffener1", delegate(ModelDoc2 m, List<object> ch)
            {
                localGussetBox = FullPrepareSideStiffener1(c, m, H, zFront, gridH, grid2000, ch);
            });
            Need(localGussetBox != null && localGussetBox.Length == 6, "FULL_SIDE_STIFFENER1_BOUNDS_MISSING");
            gussetBox = localGussetBox;

            processPart("箱体侧板加强筋2.sldprt", "side_stiffener2", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.side_stiffener2"), "FULL_SIDE_STIFFENER2_REBUILD_FAILED"); });
            processPart("箱体竖隔板加强件.sldprt", "partition_brace", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.partition_brace"), "FULL_PARTITION_BRACE_REBUILD_FAILED"); });
            processPart("锁控维护条_源钣金.SLDPRT", "maint_lock_bar", delegate(ModelDoc2 m, List<object> ch)
            {
                SetDimension(m, "D2@草图1", H - 92.0, ch);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.maint_lock_bar"), "FULL_MAINT_LOCK_BAR_REBUILD_FAILED");
            });
            processPart("维护门锁支架_原生.SLDPRT", "maint_lock_bracket", delegate(ModelDoc2 m, List<object> ch) { Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.maint_lock_bracket"), "FULL_MAINT_BRACKET_REBUILD_FAILED"); });

            // 5. Door Frame (8 parts) + Column Frame Slot Repair
            processPart("门框 左.sldprt", "frame_left", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                EnsureFrameLinearPatternPitch(c, m, "阵列(线性)2", ch);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.frame_left"), "FULL_FRAME_LEFT_REBUILD_FAILED");
            });
            processPart("门框 右.sldprt", "frame_right", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.frame_right"), "FULL_FRAME_RIGHT_REBUILD_FAILED"); });
            processPart("门框 上.sldprt", "frame_top", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.frame_top"), "FULL_FRAME_TOP_REBUILD_FAILED"); });
            processPart("门框 下.sldprt", "frame_bottom", delegate(ModelDoc2 m, List<object> ch)
            {
                ModelDoc2 sharedMaster = c.Open(structure["标准寄存柜 模型.SLDPRT"], true);
                try
                {
                    RefreshNativeLinks(c, m);
                    Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.frame_bottom"), "FULL_FRAME_BOTTOM_REBUILD_FAILED");
                }
                finally { c.Close(sharedMaster); }
            });
            processPart("门框 竖隔板L.sldprt", "frame_partition_left", delegate(ModelDoc2 m, List<object> ch)
            {
                RefreshNativeLinks(c, m);
                EnsureFrameLinearPatternPitch(c, m, "阵列(线性)1", ch);
                Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.frame_partition_left"), "FULL_FRAME_PART_L_REBUILD_FAILED");
            });
            processPart("门框 竖隔板R.SLDPRT", "frame_partition_right", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.frame_partition_right"), "FULL_FRAME_PART_R_REBUILD_FAILED"); });
            processPart("门框 横隔板.sldprt", "crossbar_left", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.crossbar_left"), "FULL_CROSSBAR_L_REBUILD_FAILED"); });
            processPart("门框 横隔板R.SLDPRT", "crossbar_right", delegate(ModelDoc2 m, List<object> ch) { RefreshNativeLinks(c, m); Need(VariableTrial.ModelForceRebuild3(m, false, "FullPrepareStructure.crossbar_right"), "FULL_CROSSBAR_R_REBUILD_FAILED"); });

            RepairColumnFrameSlots(c, structure, indexByPath, metaByPath);
            return structure;
        }

        static void FullPreparePlacements(BuildContext c, Dictionary<string, string> structure, double[] gussetBox)
        {
            double W = c.Width, H = c.Height, D = c.Depth;
            double zFront, zBack;
            FullDepthStiffenerPlanes(D, out zFront, out zBack);
            double gridH = FullGridOffset(H);
            double bxC = (gussetBox[0] + gussetBox[3]) / 2.0;
            double bzC = (gussetBox[2] + gussetBox[5]) / 2.0;
            double footAbsX = W / 2.0 - 35.0;
            double rearFootZ = -(D - 49.5);

            Action<string, string, string, double[], double, double, double> addStruct = delegate(string role, string fileKey, string kind, double[] rot, double x, double y, double z)
            {
                c.Placements.Add(new Placement(role, structure[fileKey], "", 0, kind, rot, x, y, z));
            };

            // Base (12)
            addStruct("base_frame", "底座外框.sldprt", "base_frame", FullIdentity, 0, 0, 0);
            addStruct("base_plate", "底座底板.sldprt", "base_plate", FullIdentity, 0, 0, 0);
            addStruct("base_stiffener_1", "底座加强筋.sldprt", "base_stiffener", FullIdentity, 0, 0, 0);
            addStruct("base_stiffener_2", "底座加强筋.sldprt", "base_stiffener", FullFlipXZ, 0, 0, -(D + 25.0));
            addStruct("base_nut_fl", "螺母M12.SLDPRT", "base_nut", FullRotX90, -footAbsX, 2.0, -74.5);
            addStruct("base_nut_fr", "螺母M12.SLDPRT", "base_nut", FullRotX90, footAbsX, 2.0, -74.5);
            addStruct("base_nut_rl", "螺母M12.SLDPRT", "base_nut", FullRotX90, -footAbsX, 2.0, rearFootZ);
            addStruct("base_nut_rr", "螺母M12.SLDPRT", "base_nut", FullRotX90, footAbsX, 2.0, rearFootZ);
            addStruct("base_foot_fl", "调整脚 M12X60(模型).SLDPRT", "base_foot", FullIdentity, -footAbsX, -60.0, -74.5);
            addStruct("base_foot_fr", "调整脚 M12X60(模型).SLDPRT", "base_foot", FullIdentity, footAbsX, -60.0, -74.5);
            addStruct("base_foot_rl", "调整脚 M12X60(模型).SLDPRT", "base_foot", FullIdentity, -footAbsX, -60.0, rearFootZ);
            addStruct("base_foot_rr", "调整脚 M12X60(模型).SLDPRT", "base_foot", FullIdentity, footAbsX, -60.0, rearFootZ);

            // Top (5)
            addStruct("top_plate", "上盖壳体底板.sldprt", "top_plate", FullIdentity, 0, 0, 0);
            addStruct("top_front", "上盖壳体前侧板.sldprt", "top_front", FullIdentity, 0, 0, 0);
            addStruct("top_back", "上盖壳体后侧板.sldprt", "top_back", FullIdentity, 0, 0, 0);
            addStruct("top_left", "上盖壳体左侧板.sldprt", "top_left", FullIdentity, 0, 0, 0);
            addStruct("top_right", "上盖壳体右侧板.SLDPRT", "top_right", FullIdentity, 0, 0, 0);

            // Body & Fixed Door Frame (10)
            addStruct("side_panel_left", "箱体左侧板.sldprt", "side_panel", FullIdentity, 0, 0, 0);
            addStruct("side_panel_right", "箱体右侧板.sldprt", "side_panel", FullIdentity, 0, 0, 0);
            addStruct("partition_left", "箱体竖隔板L.sldprt", "vertical_partition", FullIdentity, 0, 0, 0);
            addStruct("partition_right", "箱体竖隔板R_parametric.SLDPRT", "vertical_partition", FullIdentity, 0, 0, 0);
            addStruct("frame_left", "门框 左.sldprt", "door_frame", FullIdentity, 0, 0, 0);
            addStruct("frame_right", "门框 右.sldprt", "door_frame", FullIdentity, 0, 0, 0);
            addStruct("frame_top", "门框 上.sldprt", "door_frame", FullIdentity, 0, 0, 0);
            addStruct("frame_bottom", "门框 下.sldprt", "door_frame", FullIdentity, 0, 0, 0);
            addStruct("frame_partition_left", "门框 竖隔板L.sldprt", "door_frame", FullIdentity, 0, 0, 0);
            addStruct("frame_partition_right", "门框 竖隔板R.SLDPRT", "door_frame", FullIdentity, 0, 0, 0);

            // Side / Partition Stiffeners & Maintenance (16)
            addStruct("side_stiffener1_L_1", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullIdentity, 0, 0, -zFront - bzC);
            addStruct("side_stiffener1_L_2", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullIdentity, 0, 0, -zBack - bzC);
            addStruct("side_stiffener1_R_1", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullFlipXZ, 0, 0, -zFront + bzC);
            addStruct("side_stiffener1_R_2", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullFlipXZ, 0, 0, -zBack + bzC);
            addStruct("side_stiffener1_L_rear", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullRotY90, -(W / 4.0 + 31.1) + bzC, 0, -(D - 3.4) - bxC);
            addStruct("side_stiffener1_R_rear", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullRotY90, (W / 4.0 + 31.1) + bzC, 0, -(D - 3.4) - bxC);
            addStruct("partition_stiffener1_R_1", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullIdentity, 65.6 - bxC, 0, -zFront - bzC);
            addStruct("partition_stiffener1_R_2", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullIdentity, 65.6 - bxC, 0, -zBack - bzC);
            addStruct("partition_stiffener1_L_1", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullFlipXZ, -65.6 + bxC, 0, -zFront + bzC);
            addStruct("partition_stiffener1_L_2", "箱体侧板加强筋1.sldprt", "side_stiffener1", FullFlipXZ, -65.6 + bxC, 0, -zBack + bzC);
            addStruct("side_stiffener2_L", "箱体侧板加强筋2.sldprt", "side_stiffener2", FullIdentity, 0, 0, 0);
            addStruct("side_stiffener2_R", "箱体侧板加强筋2.sldprt", "side_stiffener2", FullFlipXY, 0, H - 50.0, 0);
            addStruct("partition_brace_1", "箱体竖隔板加强件.sldprt", "partition_brace", FullIdentity, 0, 0, 0);
            addStruct("partition_brace_2", "箱体竖隔板加强件.sldprt", "partition_brace", FullIdentity, 0, 915.0, 0);
            addStruct("maint_lock_bar", "锁控维护条_源钣金.SLDPRT", "maint_lock_bar", FullIdentity, 0, H / 2.0 - 15.5, -16.0);
            addStruct("maint_lock_bracket", "维护门锁支架_原生.SLDPRT", "maint_lock_bracket", FullIdentity, 0, H - 165.5, -0.8);

            // Doors, Hardware & Boundaries
            var variants = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var rowMap = Map(c.Layout["rowsByColumn"]);
            var boundaryMap = Map(c.Layout["boundaries"]);
            foreach (string side in new[] { "L", "R" })
            {
                double hand = side == "L" ? 1.0 : -1.0;
                foreach (object raw in Items(rowMap[side]))
                {
                    var row = Map(raw);
                    double height = Number(row, "doorHeightMm");
                    double center = Number(row, "centerYmm");
                    int index = (int)Number(row, "index");
                    string key = side + "_W" + Token(c.DoorWidth) + "_H" + Token(height);
                    string[] paths;
                    if (!variants.TryGetValue(key, out paths))
                    {
                        paths = new[]
                        {
                            c.NativeCopy(c.Source("door_panel_" + (side == "L" ? "left" : "right") + "_W437_H471p25.SLDPRT"), key + "_panel.SLDPRT"),
                            c.NativeCopy(c.Source("door_stiffener_W437_H471p25.SLDPRT"), key + "_stiffener.SLDPRT"),
                        };
                        for (int partIndex = 0; partIndex < paths.Length; partIndex++)
                        {
                            ModelDoc2 model = c.Open(paths[partIndex], false);
                            try
                            {
                                object edits;
                                if (partIndex == 0)
                                {
                                    object varEdits = SetDoorVariables(model, c.DoorWidth, height);
                                    object craftEdits = ApplyDoorProductionCraft(c, model, side, c.DoorWidth);
                                    edits = new { variables = varEdits, productionCraft = craftEdits };
                                }
                                else
                                {
                                    var changes = new List<object>();
                                    SetDimension(model, "D2@草图1", height - 10.5, changes);
                                    Need(VariableTrial.ModelForceRebuild3(model, false, "FullPreparePlacements"), "ASSEMBLY_STIFFENER_REBUILD_FAILED");
                                    double[] box = BodyBounds(model);
                                    Need(Near(box[4] - box[1], height - 10.5), "ASSEMBLY_STIFFENER_ACTUAL_HEIGHT_FAILED");
                                    edits = changes;
                                }
                                FullEnsureMaterial(c, model);
                                CheckFeatureHealth(model);
                                c.Save(model);
                                c.Parts.Add(new { role = partIndex == 0 ? "door_panel" : "door_stiffener", hand = side, widthMm = c.DoorWidth, heightMm = height, path = paths[partIndex], sha256 = Hash(paths[partIndex]), edits = edits, geometry = PartGeometry(model) });
                                c.Write();
                            }
                            finally { c.Close(model); }
                        }
                        variants[key] = paths;
                    }
                    double globalX = -hand * (W / 4.0 + 8.5);
                    double hingeX = -(c.DoorWidth / 2.0 - 10.0) * hand;
                    double lockX = (c.DoorWidth / 2.0 - 15.0) * hand;
                    double half = height / 2.0;
                    double latchY = half - 29.0;
                    string prefix = "door_" + side + "_" + index.ToString("D2", Invariant) + "_";
                    Action<string, string, double[], double, double, double> addDoor = delegate(string kind, string path, double[] rotation, double x, double y, double z)
                    {
                        c.Placements.Add(new Placement(prefix + kind, path, side, index, kind, rotation, globalX + x, center + y, z));
                    };
                    addDoor("panel", paths[0], FullIdentity, 0, 0, 0);
                    addDoor("stiffener", paths[1], FullFlipXZ, 0, 0, -0.8);
                    addDoor("hinge_latch_bottom", c.Hardware["hinge_latch_plate"], FullBottomLatch, hingeX, -latchY, -0.8);
                    addDoor("hinge_latch_top", c.Hardware[side == "L" ? "top_latch_left" : "top_latch_right"], FullIdentity, (side == "L" ? -1.0 : 1.0) * (c.DoorWidth - 307.0) / 2.0, (height - 298.4) / 2.0, 0);
                    addDoor("hook_pad", c.Hardware["u_hook_pad"], side == "L" ? FullFlipXZ : FullFlipYZ, lockX, 0, -10.5);
                    addDoor("bushing_top", c.Hardware["plastic_bushing"], FullFlipYZ, hingeX, half + 1.5, -7.0);
                    addDoor("bushing_bottom", c.Hardware["plastic_bushing"], FullIdentity, hingeX, -half - 1.5, -7.0);
                    addDoor("hinge_pin", c.Hardware["door_hinge_pin"], FullIdentity, hingeX, -half - 9.7, -7.0);
                    addDoor("circlip_top", c.Hardware["circlip"], FullIdentity, hingeX, half - 29.7, -7.7236);
                    addDoor("circlip_bottom", c.Hardware["circlip"], FullIdentity, hingeX, -half + 29.4, -7.7236);
                    addDoor("mechanical_lock_tongue", c.Hardware["mechanical_lock_tongue"], FullFlipXZ, lockX, 0, -11.3);
                }
                double stiffenerX = side == "L" ? -(W / 4.0 + 31.1) : (W / 4.0 + 31.1);
                foreach (object raw in Items(boundaryMap[side]))
                {
                    var boundary = Map(raw);
                    int index = (int)Number(boundary, "index");
                    double crossbarY = Number(boundary, "crossbarCenterYmm");
                    double shelfY = Number(boundary, "shelfCenterYmm");
                    c.Placements.Add(new Placement("crossbar_" + side + "_" + index, structure[side == "L" ? "门框 横隔板.sldprt" : "门框 横隔板R.SLDPRT"], side, index, "crossbar", FullIdentity, 0, crossbarY - 1700.0 - gridH, 0));
                    c.Placements.Add(new Placement("shelf_" + side + "_" + index, structure[side == "L" ? "箱体横层板L.sldprt" : "箱体横层板R.SLDPRT"], side, index, "shelf", FullIdentity, 0, shelfY - 1705.0 - gridH, 0));
                    c.Placements.Add(new Placement("shelf_stiffener_" + side + "_" + index + "_front", structure["箱体横层板加强筋.SLDPRT"], side, index, "shelf_stiffener", FullIdentity, stiffenerX, shelfY + 0.7, -zFront));
                    c.Placements.Add(new Placement("shelf_stiffener_" + side + "_" + index + "_back", structure["箱体横层板加强筋.SLDPRT"], side, index, "shelf_stiffener", FullIdentity, stiffenerX, shelfY + 0.7, -zBack));
                }
            }

            int boundaryCount = Items(boundaryMap["L"]).Length + Items(boundaryMap["R"]).Length;
            int doorCount = (int)Number(c.Layout, "doorCount");
            int expected = 43 + 11 * doorCount + 4 * boundaryCount;
            Need(c.Placements.Count == expected, "FULL_MANIFEST_COUNT_MISMATCH actual=" + c.Placements.Count + " expected=" + expected);
            Need(c.Placements.Select(p => p.Role).Distinct(StringComparer.Ordinal).Count() == c.Placements.Count, "FULL_DUPLICATE_COMPONENT_IDENTITY");
            c.Evidence["expectedManifest"] = c.Placements.Select(p => new { identity = p.Role, path = p.Path, side = p.Side, index = p.Index, kind = p.Kind, transform = p.Transform }).ToArray();
            c.Evidence["fullScopeSummary"] = new
            {
                fixedStructurePartCount = 43,
                partsPerDoor = 11,
                boundaryCount = boundaryCount,
                partsPerBoundary = 4,
                expectedComponentCount = c.Placements.Count,
                referenceMasterInserted = false,
                shelfReinforcementAndCabinetShellIncluded = true,
            };
            c.Write();
        }

        static void FullBuildNativeAssembly(BuildContext c)
        {
            c.CloseOutputDocuments();
            string template = VariableTrial.SwGetUserPreferenceStringValue(c.Sw, (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly, "FullBuildNativeAssembly");
            Need(!string.IsNullOrWhiteSpace(template) && File.Exists(template), "ASSEMBLY_NATIVE_TEMPLATE_MISSING");
            VariableTrial.RuntimeOwnership();
            ModelDoc2 model = VariableTrial.SwNewDocument(c.Sw, template, 0, 0, 0, "FullBuildNativeAssembly");
            Need(model != null, "ASSEMBLY_NEW_DOCUMENT_FAILED");
            VariableTrial.RegisterOwned(model);
            MathUtility math = null;
            var openedParts = new List<ModelDoc2>();
            Exception primaryError = null;
            try
            {
                var assembly = (AssemblyDoc)model;
                Need(VariableTrial.AssemblyGetComponents(assembly, true, "FullBuildNativeAssembly").Length == 0, "ASSEMBLY_TEMPLATE_CONTAINS_COMPONENTS");
                c.SaveNew(model, c.AssemblyPath, false);
                math = VariableTrial.SwGetMathUtility(c.Sw, "FullBuildNativeAssembly");
                Need(math != null, "ASSEMBLY_MATH_UTILITY_MISSING");
                PreloadPartsInvisibly(c, openedParts, "FullBuildNativeAssembly");
                int activateErrors = 0;
                string modelTitle = VariableTrial.ModelGetTitle(model, "FullBuildNativeAssembly");
                VariableTrial.SwActivateDoc3(c.Sw, modelTitle, false, 0, out activateErrors, "FullBuildNativeAssembly");
                Need(activateErrors == 0, "ASSEMBLY_ACTIVATE_FAILED");
                c.Event(new { inspection = "full_cabinet_assembly_pre_insert", path = c.AssemblyPath, componentCount = c.Placements.Count, uniquePartCount = openedParts.Count, utc = DateTime.UtcNow.ToString("o") }, true);

                for (int i = 0; i < c.Placements.Count; i++)
                {
                    Placement placement = c.Placements[i];
                    bool persistEvent = i < 12 || (i + 1) % 10 == 0 || i + 1 == c.Placements.Count;
                    bool logStep = i == 0;
                    Action<string> markStep = delegate(string step)
                    {
                        c.CurrentComponentIndex = i;
                        c.CurrentComponentRole = placement.Role;
                        c.CurrentComponentPath = placement.Path;
                        c.CurrentInsertStep = step;
                        if (logStep)
                            c.Event(new { inspection = "full_component_insert_step", index = i, role = placement.Role, step = step, utc = DateTime.UtcNow.ToString("o") }, true);
                    };
                    VariableTrial.RuntimeOwnership();
                    Component2 component = null;
                    MathTransform transform = null;
                    try
                    {
                        markStep("activate");
                        int errors = 0;
                        ModelDoc2 checkActive = VariableTrial.SwActiveDoc(c.Sw, "FullBuildNativeAssembly");
                        try
                        {
                            if (checkActive == null || !SameNativeObject(checkActive, model))
                                VariableTrial.SwActivateDoc3(c.Sw, modelTitle, false, 0, out errors, "FullBuildNativeAssembly");
                        }
                        finally { Release(checkActive); }
                        Need(errors == 0, "ASSEMBLY_ACTIVATE_FAILED");

                        markStep("add_component");
                        component = VariableTrial.AssemblyAddComponent5(assembly, placement.Path, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig, "", false, "", placement.Transform[9], placement.Transform[10], placement.Transform[11], "FullBuildNativeAssembly");
                        Need(component != null, "ASSEMBLY_COMPONENT_ADD_FAILED " + placement.Role);
                        VariableTrial.ModelClearSelection2(model, true, "FullBuildNativeAssembly");
                        Need(VariableTrial.ComponentSelect4(component, false, null, false, "FullBuildNativeAssembly"), "ASSEMBLY_COMPONENT_SELECT_FAILED");
                        markStep("pre_unfix");
                        if (VariableTrial.ComponentIsFixed(component, "FullBuildNativeAssembly")) VariableTrial.AssemblyUnfixComponent(assembly, "FullBuildNativeAssembly");
                        string beforeName = VariableTrial.ComponentGetName2(component, "FullBuildNativeAssembly");
                        placement.OriginalName = beforeName;
                        markStep("pre_set_pref_false");
                        bool prior = VariableTrial.SwGetUserPreferenceToggle(c.Sw, (int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, "FullBuildNativeAssembly");
                        bool renameInnerFailed = false;
                        try
                        {
                            VariableTrial.SwSetUserPreferenceToggle(c.Sw, (int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, false, "FullBuildNativeAssembly");
                            ModelDoc2 active = VariableTrial.SwActiveDoc(c.Sw, "FullBuildNativeAssembly.Rename");
                            try
                            {
                                if (active == null || !SameNativeObject(active, model))
                                {
                                    Release(active);
                                    active = null;
                                    errors = 0;
                                    VariableTrial.SwActivateDoc3(c.Sw, modelTitle, false, 0, out errors, "FullBuildNativeAssembly.Rename");
                                }
                            }
                            finally { Release(active); }
                            VariableTrial.ModelClearSelection2(model, true, "FullBuildNativeAssembly.Rename");
                            Need(VariableTrial.ComponentSelect4(component, false, null, false, "FullBuildNativeAssembly.Rename"), "ASSEMBLY_RENAME_SELECT_FAILED " + placement.Role);
                            markStep("pre_rename");
                            VariableTrial.ComponentSetName2(component, placement.Role, "FullBuildNativeAssembly");
                            string actualName = VariableTrial.ComponentGetName2(component, "FullBuildNativeAssembly");
                            bool nameMatches = actualName == placement.Role || actualName.StartsWith(placement.Role + "-", StringComparison.Ordinal);
                            bool unique = c.Placements.All(item => item.ActualName == null || item.ActualName != actualName);
                            string compPath = VariableTrial.ComponentGetPathName(component, "FullBuildNativeAssembly");
                            c.Event(new { inspection = "full_component_rename_readback", requested = placement.Role, before = beforeName, actual = actualName, path = compPath, nameMatches = nameMatches, identityUnique = unique }, persistEvent);
                            Need(nameMatches && unique && compPath.Equals(placement.Path, StringComparison.OrdinalIgnoreCase), "ASSEMBLY_COMPONENT_RENAME_FAILED " + placement.Role);
                            placement.ActualName = actualName;
                        }
                        catch { renameInnerFailed = true; throw; }
                        finally
                        {
                            if (!VariableTrial.ComTimedOut)
                            {
                                try { VariableTrial.SwSetUserPreferenceToggle(c.Sw, (int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, prior, "FullBuildNativeAssembly.RestorePref", true); }
                                catch { if (!renameInnerFailed) throw; }
                            }
                        }

                        markStep("pre_transform");
                        transform = math.CreateTransform(placement.Transform) as MathTransform;
                        Need(transform != null && VariableTrial.ComponentSetTransformAndSolve2(component, transform, "FullBuildNativeAssembly"), "ASSEMBLY_TRANSFORM_FAILED " + placement.Role);
                        double[] actual = Transform(component);
                        Need(SameTransform(actual, placement.Transform), "ASSEMBLY_TRANSFORM_READBACK_FAILED " + placement.Role);
                        markStep("pre_fix");
                        VariableTrial.ModelClearSelection2(model, true, "FullBuildNativeAssembly");
                        Need(VariableTrial.ComponentSelect4(component, false, null, false, "FullBuildNativeAssembly"), "ASSEMBLY_FIX_SELECTION_FAILED");
                        VariableTrial.AssemblyFixComponent(assembly, "FullBuildNativeAssembly");
                        Need(VariableTrial.ComponentIsFixed(component, "FullBuildNativeAssembly"), "ASSEMBLY_FIXED_PLACEMENT_FAILED");
                        VariableTrial.ModelClearSelection2(model, true, "FullBuildNativeAssembly");
                        c.Event(new { inspection = "full_component_inserted", index = i, total = c.Placements.Count, role = placement.Role, actualName = placement.ActualName, utc = DateTime.UtcNow.ToString("o") }, persistEvent);
                    }
                    catch (Exception err)
                    {
                        throw new InvalidOperationException("FULL_INSERT_FAILED idx=" + i + " role=" + placement.Role + ": " + err.Message, err);
                    }
                    finally
                    {
                        Release(transform);
                        Release(component);
                    }
                }

                c.Stage("FullBuildNativeAssembly.RebuildAndSave");
                Need(VariableTrial.ModelForceRebuild3(model, false, "FullBuildNativeAssembly", VariableTrial.HeavyAssemblyComTimeoutMs), "FULL_ASSEMBLY_NATIVE_REBUILD_FAILED");
                CheckFeatureHealth(model);
                c.Save(model);
                c.Evidence["assemblySha256"] = Hash(c.AssemblyPath);
                c.Event(new { inspection = "full_cabinet_assembly_built", path = c.AssemblyPath, componentCount = c.Placements.Count, placementMode = "fixed native components at explicit transforms; no kinematic-mate claim", utc = DateTime.UtcNow.ToString("o") });
            }
            catch (Exception err)
            {
                primaryError = err;
                throw;
            }
            finally
            {
                Release(math);
                try { FullCleanupDocuments(c, model, primaryError, "FullBuildNativeAssembly"); }
                finally { foreach (ModelDoc2 part in openedParts) Release(part); }
            }
        }

        static double[] FullWorldBodyBox(Component2 comp)
        {
            ModelDoc2 part = comp.GetModelDoc2() as ModelDoc2;
            Need(part != null, "FULL_COMPONENT_DOC_UNLOADED " + comp.Name2);
            MathTransform compXform = comp.Transform2;
            Need(compXform != null, "FULL_COMPONENT_TRANSFORM_MISSING " + comp.Name2);
            double[] t = Numbers(compXform.ArrayData);
            Release(compXform);
            double[] lb = BodyBounds(part);
            double[] result = { Double.MaxValue, Double.MaxValue, Double.MaxValue, Double.MinValue, Double.MinValue, Double.MinValue };
            for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                    for (int iz = 0; iz < 2; iz++)
                    {
                        double lx = lb[ix == 0 ? 0 : 3] / 1000.0;
                        double ly = lb[iy == 0 ? 1 : 4] / 1000.0;
                        double lz = lb[iz == 0 ? 2 : 5] / 1000.0;
                        double wx = (lx * t[0] + ly * t[3] + lz * t[6] + t[9]) * 1000.0;
                        double wy = (lx * t[1] + ly * t[4] + lz * t[7] + t[10]) * 1000.0;
                        double wz = (lx * t[2] + ly * t[5] + lz * t[8] + t[11]) * 1000.0;
                        result[0] = Math.Min(result[0], wx);
                        result[1] = Math.Min(result[1], wy);
                        result[2] = Math.Min(result[2], wz);
                        result[3] = Math.Max(result[3], wx);
                        result[4] = Math.Max(result[4], wy);
                        result[5] = Math.Max(result[5], wz);
                    }
            return result;
        }

        static Dictionary<string, object> FullClassifyInterference(object interferenceObj, BuildContext c)
        {
            var rawMap = Map(Json.DeserializeObject(Json.Serialize(interferenceObj)));
            object[] cols = Items(rawMap["collisions"]);
            var byName = new Dictionary<string, Placement>(StringComparer.Ordinal);
            foreach (Placement p in c.Placements)
            {
                if (!string.IsNullOrEmpty(p.ReopenedName)) byName[p.ReopenedName] = p;
                if (!string.IsNullOrEmpty(p.ActualName)) byName[p.ActualName] = p;
                byName[p.Role] = p;
            }
            Func<string, string> describe = delegate(string compName)
            {
                Placement p;
                if (byName.TryGetValue(compName, out p)) return p.Kind + "|" + Path.GetFileNameWithoutExtension(p.Path);
                return compName;
            };
            Func<string, bool> isFoot = delegate(string desc) { return desc.Contains("调整脚") || desc.StartsWith("base_foot|", StringComparison.Ordinal); };
            Func<string, bool> isFootMatePart = delegate(string desc) { return desc.Contains("螺母M12") || desc.Contains("底座加强筋") || desc.Contains("底座底板") || desc.Contains("底座外框"); };
            Func<string, bool> isCirclip = delegate(string desc) { return desc.StartsWith("circlip", StringComparison.Ordinal) || desc.Contains("trial_circlip"); };
            Func<string, bool> isHingePin = delegate(string desc) { return desc.StartsWith("hinge_pin", StringComparison.Ordinal) || desc.Contains("trial_door_hinge_pin"); };
            Func<string, bool> isBushingTop = delegate(string compName)
            {
                Placement p;
                return byName.TryGetValue(compName, out p) && p.Kind == "bushing_top";
            };
            Func<string, bool> isDoorPanel = delegate(string compName)
            {
                Placement p;
                return byName.TryGetValue(compName, out p) && p.Kind == "panel";
            };

            int threadCount = 0;
            int structuralCount = 0;
            int approvedCirclipCount = 0;
            int approvedUpperPinPrebiasCount = 0;
            int unapprovedHardwareCount = 0;
            var classified = new List<object>();
            foreach (object raw in cols)
            {
                var item = Map(raw);
                string a = Convert.ToString(item["componentA"]);
                string b = Convert.ToString(item["componentB"]);
                double vol = Number(item, "volumeMm3");
                string da = describe(a), db = describe(b);
                string category;
                bool approved = false;
                string disposition;
                if (((isCirclip(da) && isHingePin(db)) || (isCirclip(db) && isHingePin(da))) && vol <= 1.2)
                {
                    category = "approved_circlip_hinge_pin_groove_fit";
                    approved = true;
                    disposition = "approved_circlip_or_hinge_pin_by_user_20261117";
                    approvedCirclipCount++;
                }
                else if (((isDoorPanel(a) && isBushingTop(b)) || (isDoorPanel(b) && isBushingTop(a))) && vol <= 5.0)
                {
                    category = "approved_upper_pin_prebias_0p5_fit";
                    approved = true;
                    disposition = "approved_production_craft_upper_pin_prebias_0p5mm";
                    approvedUpperPinPrebiasCount++;
                }
                else if ((isFoot(da) && isFootMatePart(db)) || (isFoot(db) && isFootMatePart(da)))
                {
                    category = "fastener_thread_engagement";
                    disposition = "unapproved_fastener_thread_engagement";
                    threadCount++;
                }
                else
                {
                    Placement pa, pb;
                    bool hwA = byName.TryGetValue(a, out pa) && !pa.Path.Contains("\\structure\\");
                    bool hwB = byName.TryGetValue(b, out pb) && !pb.Path.Contains("\\structure\\");
                    if (hwA && hwB)
                    {
                        category = "unapproved_hardware_interference";
                        disposition = "unapproved_hardware_interference";
                        unapprovedHardwareCount++;
                    }
                    else
                    {
                        category = "structural_collision";
                        disposition = "unapproved_structural_collision";
                        structuralCount++;
                    }
                }
                classified.Add(new { componentA = a, componentB = b, volumeMm3 = item["volumeMm3"], intersectionBoundsMm = item["intersectionBoundsMm"], category = category, approved = approved, pass = approved, disposition = disposition });
            }
            int unapprovedCount = threadCount + unapprovedHardwareCount + structuralCount;
            bool interferencePass = unapprovedCount == 0 && structuralCount == 0 && unapprovedHardwareCount == 0;
            return new Dictionary<string, object>
            {
                { "totalInterferenceCount", cols.Length },
                { "fastenerThreadEngagementCount", threadCount },
                { "approvedCirclipHingePinCount", approvedCirclipCount },
                { "approvedUpperPinPrebiasCount", approvedUpperPinPrebiasCount },
                { "unapprovedHardwareInterferenceCount", unapprovedHardwareCount },
                { "hardwareInterferenceCount", approvedCirclipCount + approvedUpperPinPrebiasCount + unapprovedHardwareCount },
                { "structuralCollisionCount", structuralCount },
                { "unapprovedInterferenceCount", unapprovedCount },
                { "interferencePass", interferencePass },
                { "approvalPolicy", "Circlip <-> hinge_pin groove fit (volume <= 1.2 mm^3) approved by user 2026-11-17; door upper pin hole 0.5 mm inward pre-bias overlap with bushing_top (volume <= 5.0 mm^3) approved per production craft; structural, door-latch and unapproved interferences forbidden." },
                { "collisions", classified },
            };
        }

        static void FullCleanupDocuments(BuildContext c, ModelDoc2 model, Exception primaryError, string stage)
        {
            try
            {
                c.Close(model);
                FullCloseAllDocuments(c);
            }
            catch (Exception cleanupError)
            {
                c.Event(new { inspection = "full_cleanup_error", stage = stage, error = cleanupError.ToString(), utc = DateTime.UtcNow.ToString("o") }, true);
                if (primaryError == null) throw;
            }
        }

        static Placement FullMatchReopenedComponent(BuildContext c, Component2 comp, string compName, string compPath, Dictionary<string, Placement> byActualName, HashSet<string> matchedRoles)
        {
            string fullPath = string.IsNullOrEmpty(compPath) ? null : Path.GetFullPath(compPath);
            double[] actual = Transform(comp);
            Placement[] candidates = fullPath == null
                ? new Placement[0]
                : c.Placements.Where(value => Path.GetFullPath(value.Path).Equals(fullPath, StringComparison.OrdinalIgnoreCase) && SameTransform(actual, value.Transform)).ToArray();
            Placement placed = candidates.Length == 1 ? candidates[0] : null;
            if (placed == null && !string.IsNullOrEmpty(compName))
            {
                Placement fallback;
                if (byActualName.TryGetValue(compName, out fallback)) placed = fallback;
            }
            Need(placed != null && matchedRoles.Add(placed.Role), "FULL_REOPEN_COMPONENT_IDENTITY_MISMATCH component=" + compName + " path=" + compPath + " candidateCount=" + candidates.Length);
            placed.ReopenedName = compName;
            return placed;
        }

        static void FullReopenAndMeasure(BuildContext c)
        {
            FullCloseAllDocuments(c);
            Need(VariableTrial.SwGetDocuments(c.Sw, "FullReopenAndMeasure").Length == 0, "FULL_REOPEN_REQUIRES_EMPTY_SESSION");
            ModelDoc2 model = c.Open(c.AssemblyPath, true);
            Exception primaryError = null;
            try
            {
                Need(VariableTrial.ModelForceRebuild3(model, false, "FullReopenAndMeasure", VariableTrial.HeavyAssemblyComTimeoutMs), "FULL_REOPEN_REBUILD_FAILED");
                CheckFeatureHealth(model, c, "full_assembly_reopen", true);
                var assembly = (AssemblyDoc)model;
                object[] rawComps = VariableTrial.AssemblyGetComponents(assembly, false, "FullReopenAndMeasure");
                Need(rawComps.Length == c.Placements.Count, "FULL_REOPEN_COMPONENT_COUNT_MISMATCH actual=" + rawComps.Length + " expected=" + c.Placements.Count);

                var worlds = new List<WorldBody>();
                var checkedDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missingMaterials = new List<string>();
                double[] shellBounds = { Double.MaxValue, Double.MaxValue, Double.MaxValue, Double.MinValue, Double.MinValue, Double.MinValue };
                double[] noFootBounds = { Double.MaxValue, Double.MaxValue, Double.MaxValue, Double.MinValue, Double.MinValue, Double.MinValue };
                double[] totalBounds = { Double.MaxValue, Double.MaxValue, Double.MaxValue, Double.MinValue, Double.MinValue, Double.MinValue };
                var belowFloorNonFoot = new List<object>();
                int unapprovedBelowFloorCount = 0;
                var byActualName = new Dictionary<string, Placement>(StringComparer.Ordinal);
                foreach (Placement p in c.Placements)
                {
                    if (!string.IsNullOrEmpty(p.ActualName)) byActualName[p.ActualName] = p;
                }
                var matchedRoles = new HashSet<string>(StringComparer.Ordinal);

                foreach (object raw in rawComps)
                {
                    var comp = raw as Component2;
                    try
                    {
                        Need(comp != null, "FULL_REOPEN_COMPONENT_NULL");
                        string compPath = comp.GetPathName();
                        string compName = comp.Name2;
                        Need(comp.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed, "FULL_REOPEN_COMPONENT_SUPPRESSED " + compName);
                        ModelDoc2 partDoc = comp.GetModelDoc2() as ModelDoc2;
                        Need(partDoc != null, "FULL_REOPEN_PART_UNLOADED " + compName);
                        if (checkedDocs.Add(compPath))
                        {
                            CheckFeatureHealth(partDoc, c, "full_reopen_part_" + Path.GetFileName(compPath), true);
                            if (partDoc is PartDoc)
                            {
                                string db;
                                string mat = ((PartDoc)partDoc).GetMaterialPropertyName2("", out db);
                                if (string.IsNullOrWhiteSpace(mat))
                                {
                                    string cfg = FullActiveConfigName(partDoc);
                                    if (!string.IsNullOrEmpty(cfg)) mat = ((PartDoc)partDoc).GetMaterialPropertyName2(cfg, out db);
                                }
                                if (string.IsNullOrWhiteSpace(mat)) missingMaterials.Add(compPath);
                            }
                        }
                        double[] wbox = FullWorldBodyBox(comp);
                        for (int a = 0; a < 3; a++)
                        {
                            totalBounds[a] = Math.Min(totalBounds[a], wbox[a]);
                            totalBounds[a + 3] = Math.Max(totalBounds[a + 3], wbox[a + 3]);
                        }
                        Placement placed = FullMatchReopenedComponent(c, comp, compName, compPath, byActualName, matchedRoles);
                        bool isBaseFoot = placed.Kind == "base_foot" || placed.Path.IndexOf("调整脚", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!isBaseFoot)
                        {
                            for (int a = 0; a < 3; a++)
                            {
                                noFootBounds[a] = Math.Min(noFootBounds[a], wbox[a]);
                                noFootBounds[a + 3] = Math.Max(noFootBounds[a + 3], wbox[a + 3]);
                            }
                            if (wbox[1] < -0.01)
                            {
                                bool approvedPin = placed.Kind == "hinge_pin" && wbox[1] >= -25.0;
                                if (!approvedPin) unapprovedBelowFloorCount++;
                                belowFloorNonFoot.Add(new { role = placed.Role, kind = placed.Kind, component = compName, minYmm = wbox[1], maxYmm = wbox[4], approved = approvedPin, disposition = approvedPin ? "approved_above_foot_floor_plane_by_user_20261117" : "unapproved_below_floor_non_foot" });
                            }
                        }
                        bool excludeShell = isBaseFoot || placed.Kind == "hinge_pin";
                        if (!excludeShell)
                        {
                            for (int a = 0; a < 3; a++)
                            {
                                shellBounds[a] = Math.Min(shellBounds[a], wbox[a]);
                                shellBounds[a + 3] = Math.Max(shellBounds[a + 3], wbox[a + 3]);
                            }
                        }
                        worlds.Add(new WorldBody { Component = compName, Kind = placed.Kind, Side = placed.Side, DoorIndex = placed.Index, Path = compPath, Box = wbox });
                    }
                    finally { Release(comp); }
                }
                Need(matchedRoles.Count == c.Placements.Count, "FULL_REOPEN_ROLE_COVERAGE_MISMATCH actual=" + matchedRoles.Count + " expected=" + c.Placements.Count);

                c.Evidence["materialCheck"] = new { uniquePartCount = checkedDocs.Count, missingMaterialCount = missingMaterials.Count, missingMaterialPaths = missingMaterials.ToArray() };
                Need(missingMaterials.Count == 0, "FULL_MISSING_MATERIAL_PARTS " + string.Join("; ", missingMaterials.ToArray()));

                double shellWidthMm = shellBounds[3] - shellBounds[0];
                double shellHeightMm = shellBounds[4] - shellBounds[1];
                double shellDepthMm = -shellBounds[2];
                c.Evidence["worldBoundsMm"] = totalBounds;
                c.Evidence["cabinetWithoutFeetBoundsMm"] = noFootBounds;
                c.Evidence["belowFloorNonFootComponents"] = belowFloorNonFoot;
                c.Evidence["belowFloorNonFootPass"] = unapprovedBelowFloorCount == 0;
                Need(unapprovedBelowFloorCount == 0, "FULL_UNAPPROVED_BELOW_FLOOR_NON_FOOT count=" + unapprovedBelowFloorCount);
                c.Evidence["cabinetShellBoundsMm"] = shellBounds;
                c.Evidence["measuredEnvelopeMm"] = new { widthMm = shellWidthMm, heightMm = shellHeightMm, depthMm = shellDepthMm };
                Need(Math.Abs(shellWidthMm - c.Width) <= 3.0, "FULL_ENVELOPE_WIDTH_READBACK_MISMATCH actual=" + shellWidthMm + " expected=" + c.Width);
                Need(Math.Abs(shellHeightMm - c.Height) <= 3.0, "FULL_ENVELOPE_HEIGHT_READBACK_MISMATCH actual=" + shellHeightMm + " expected=" + c.Height);
                Need(Math.Abs(shellDepthMm - c.Depth) <= 3.0, "FULL_ENVELOPE_DEPTH_READBACK_MISMATCH actual=" + shellDepthMm + " expected=" + c.Depth);

                MassProperty2 mp = model.Extension.CreateMassProperty2() as MassProperty2;
                Need(mp != null, "FULL_MASS_PROPERTY2_UNAVAILABLE");
                double totalMassKg;
                double totalVolumeM3;
                double[] comM;
                try
                {
                    mp.UseSystemUnits = true;
                    mp.IncludeHiddenBodiesOrComponents = true;
                    totalMassKg = mp.Mass;
                    totalVolumeM3 = mp.Volume;
                    comM = Numbers(mp.CenterOfMass);
                }
                finally { Release(mp); }
                Need(!Double.IsNaN(totalMassKg) && totalMassKg > 0.0, "FULL_MASS_PROPERTY2_ZERO_OR_INVALID");
                c.Evidence["massProperty"] = new { totalMassKg = totalMassKg, totalVolumeCm3 = totalVolumeM3 * 1e6, centerOfMassMm = comM.Select(v => v * 1000.0).ToArray() };

                object rawInterference = MeasureInterference(c, model, worlds);
                c.Evidence["interference"] = rawInterference;
                var rawIntMap = Map(Json.DeserializeObject(Json.Serialize(rawInterference)));
                Need(Convert.ToBoolean(rawIntMap["complete"], Invariant), "FULL_INTERFERENCE_DETECTION_INCOMPLETE");
                Dictionary<string, object> classified = FullClassifyInterference(rawInterference, c);
                c.Evidence["interferenceClassification"] = classified;
                int structuralCollisionCount = Convert.ToInt32(classified["structuralCollisionCount"], Invariant);
                int unapprovedInterferenceCount = Convert.ToInt32(classified["unapprovedInterferenceCount"], Invariant);
                c.Evidence["interferencePass"] = Convert.ToBoolean(classified["interferencePass"], Invariant);
                Need(structuralCollisionCount == 0, "FULL_STRUCTURAL_COLLISIONS_DETECTED count=" + structuralCollisionCount);
                Need(unapprovedInterferenceCount == 0, "FULL_UNAPPROVED_INTERFERENCE_DETECTED count=" + unapprovedInterferenceCount);
                c.Write();
            }
            catch (Exception err)
            {
                primaryError = err;
                throw;
            }
            finally
            {
                FullCleanupDocuments(c, model, primaryError, "FullReopenAndMeasure");
            }
        }
    }
}
