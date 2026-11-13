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
internal static class TrialAssembly
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
        internal string Role, Path, Side, Kind, ActualName, OriginalName;
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
        internal bool Disabled;
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
        internal object Evidence() { return new {elapsedMilliseconds=Now,phaseMilliseconds=PhaseMilliseconds,nativeSurfaceScans=NativeSurfaceScans,topologyCacheHits=TopologyCacheHits,topologyCacheEntries=TopologyCacheEntries,normalBodiesConfirmed=NormalBodies,cacheFallbackReasons=FallbackReasons,nativeBoundsCounterexamples=BoundsCounterexamples,cacheScope="one ReopenAndMeasure invocation only",cachePolicy="same actual file SHA256, referenced configuration, bit-identical rotation and scale, confirmed normal body, ISldWorks.IsSame and retained IUnknown identity; native six-direction body bounds corroborate translation only",timingsCoverCompletedOperationsOnly=true}; }
        internal void Write(BuildContext c) { c.Evidence["measurementPerformance"]=Evidence(); double started=Now; c.Write(); Add("evidence_write",started); }
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

        internal void Write()
        {
            if (EvidencePath != null) File.WriteAllText(EvidencePath, Json.Serialize(Evidence), new UTF8Encoding(false));
        }
        internal void Event(object value) { Events.Add(value); Write(); }
        internal void Scope()
        {
            foreach (object value in Items(Sw.GetDocuments()))
            {
                var model=value as ModelDoc2;
                try { Need(model != null && !String.IsNullOrEmpty(model.GetPathName()), "UNSAVED_OR_UNKNOWN_DOCUMENT"); Within(model.GetPathName(),Cad); }
                finally { Release(value); }
            }
        }
        internal ModelDoc2 Open(string path, bool readOnly)
        {
            VariableTrial.RuntimeOwnership(); Scope();
            path=Within(path,Cad); bool previouslyOpened=OpenedPaths.Contains(path); int errors=0,warnings=0;
            var model=Sw.OpenDoc6(path,Path.GetExtension(path).Equals(".SLDASM",StringComparison.OrdinalIgnoreCase)?2:1,1|(readOnly?2:0),"",ref errors,ref warnings) as ModelDoc2;
            bool alreadyOpen=warnings==(int)swFileLoadWarning_e.swFileLoadWarning_AlreadyOpen;
            Event(new { inspection="assembly_document_open",path=path,readOnly=readOnly,errors=errors,warnings=warnings,previouslyOpenedInContext=previouslyOpened,reusedAlreadyOpen=alreadyOpen,actualReadOnly=model==null?(bool?)null:model.IsOpenedReadOnly() });
            Need(model!=null && errors==0 && (warnings==0 || warnings==(int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen || alreadyOpen&&previouslyOpened&&readOnly), "ASSEMBLY_OPEN_FAILED " + path + " " + errors + "/" + warnings);
            Need(String.Equals(Path.GetFullPath(model.GetPathName()),path,StringComparison.OrdinalIgnoreCase),"ASSEMBLY_OPEN_PATH_MISMATCH");
            Need(model.IsOpenedReadOnly()==readOnly,"ASSEMBLY_OPEN_MODE_MISMATCH " + path); Scope(); OpenedPaths.Add(path); if(alreadyOpen)ReusedOpenPaths.Add(path); return model;
        }
        internal void Close(ModelDoc2 model)
        {
            if(model==null)return; string path=Within(model.GetPathName(),Cad); bool reused=ReusedOpenPaths.Remove(path);
            try { Event(new {inspection="assembly_document_close",path=path,reusedAlreadyOpen=reused,closeDocument=!reused,dirty=model.GetSaveFlag()}); if(!reused)Sw.CloseDoc(model.GetTitle()); }
            finally { Release(model); }
        }
        internal void CloseOutputDocuments()
        {
            int guard=0;
            while(true)
            {
                object[] documents=Items(Sw.GetDocuments()); if(documents.Length==0)return;
                Need(guard++<200,"ASSEMBLY_DOCUMENT_CLOSE_INCOMPLETE"); string title=null;
                try
                {
                    foreach(object value in documents) { var model=value as ModelDoc2; Need(model!=null,"UNKNOWN_LOADED_DOCUMENT"); Within(model.GetPathName(),Output); if(title==null)title=model.GetTitle(); }
                    Sw.CloseDoc(title);
                }
                finally { foreach(object value in documents)Release(value); }
            }
        }
        internal void Save(ModelDoc2 model)
        {
            VariableTrial.RuntimeOwnership(); Scope();
            Within(model.GetPathName(),Output); Need(!model.IsOpenedReadOnly(),"ASSEMBLY_SAVE_READONLY");
            int errors=0,warnings=0; bool saved=model.Save3(1,ref errors,ref warnings);
            Event(new { inspection="assembly_document_save",path=model.GetPathName(),saved=saved,errors=errors,warnings=warnings });
            Need(saved&&errors==0&&warnings==0&&!model.GetSaveFlag(),"ASSEMBLY_SAVE_FAILED " + model.GetPathName());
        }
        internal void SaveNew(ModelDoc2 model,string path,bool copy)
        {
            VariableTrial.RuntimeOwnership();
            Within(path,Output); Need(!File.Exists(path),"PRESERVE_ASSEMBLY_OUTPUT " + path); model.ClearSelection2(true);
            int errors=0,warnings=0;
            bool saved=model.Extension.SaveAs(path,0,1|(copy?(int)swSaveAsOptions_e.swSaveAsOptions_Copy:0),null,ref errors,ref warnings);
            Event(new { inspection="assembly_document_save_as",source=model.GetPathName(),path=path,copy=copy,saved=saved,errors=errors,warnings=warnings });
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
                VariableTrial.RuntimeOwnership(); Need(Sw.ReplaceReferencedDocument(target,item.Key,item.Value),"ASSEMBLY_COPY_REWIRE_FAILED " + target + " -> " + item.Key);
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
        var c=new BuildContext { Sw=sw,Cad=Path.GetFullPath(cadDirectory),Output=Path.GetFullPath(outputDirectory),Layout=layout };
        c.Evidence=new Dictionary<string,object> {
            {"schema","16029-independent-variable-minimum-assembly/v1"},{"production_release",false},{"success",false},{"acceptancePass",false},
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
        try
        {
            VariableTrial.RuntimeOwnership();
            Need(sw!=null && layout!=null,"ASSEMBLY_ARGUMENT_REQUIRED"); Within(c.Cad,TrialRoot);
            Within(c.Output,Path.Combine(c.Cad,"assemblies"));
            Need(Path.GetDirectoryName(c.Output).Equals(Path.Combine(c.Cad,"assemblies"),StringComparison.OrdinalIgnoreCase),"ASSEMBLY_CASE_DIRECTORY_REQUIRED");
            Need(!Directory.Exists(c.Output)&&!File.Exists(c.Output),"PRESERVE_EXISTING_ASSEMBLY_CASE " + c.Output);
            Need(Items(sw.GetDocuments()).Length==0,"ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
            Directory.CreateDirectory(c.Output); c.EvidencePath=Path.Combine(c.Output,"assembly-evidence.json");
            c.AssemblyPath=Path.Combine(c.Output,"16029_variable_minimum.SLDASM");
            c.Evidence["assemblyPath"]=c.AssemblyPath; c.Evidence["outputDirectory"]=c.Output;
            var cabinet=Map(layout["cabinet"]); c.Width=Number(cabinet,"widthMm"); c.Height=Number(cabinet,"heightMm"); c.Depth=Number(cabinet,"depthMm"); c.DoorWidth=Number(layout,"doorLeafWidthMm");
            Need((Near(c.Width,1000)||Near(c.Width,900))&&Near(c.Height,2000)&&Near(c.Depth,500)&&Near(c.DoorWidth,(c.Width-126)/2),"ASSEMBLY_TRIAL_MATRIX_RANGE_REQUIRED");
            ValidateLayout(c);
            c.InputHashes=Directory.GetFiles(c.Cad).Where(p=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(p).ToUpperInvariant())).ToDictionary(p=>p,Hash,StringComparer.OrdinalIgnoreCase);
            Need(c.InputHashes.Count==60,"ASSEMBLY_EXPECTS_COMPLETE_60_FILE_INPUT"); c.Write();
            var structure=PrepareStructure(c);
            PrepareHardware(c);
            PreparePlacements(c,structure);
            BuildNativeAssembly(c);
            ReopenAndMeasure(c);
            c.Evidence["success"]=true;
        }
        catch(Exception error) { c.Evidence["error"]=error.ToString(); }
        finally
        {
            try { c.CloseOutputDocuments(); } catch(Exception error) { c.Evidence["cleanupError"]=error.ToString(); c.Evidence["success"]=false; }
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
            Need(Items(sw.GetDocuments()).Length==0,"ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
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
            Need(Items(sw.GetDocuments()).Length==0,"ASSEMBLY_REQUIRES_EMPTY_DOCUMENT_SESSION");
            string oldRoot=Within(Convert.ToString(prior["outputDirectory"]),TrialRoot),oldAssembly=Path.GetFullPath(Convert.ToString(prior["assemblyPath"]));
            Need(!oldRoot.Equals(folder,StringComparison.OrdinalIgnoreCase),"NEW_ASSEMBLY_MEASUREMENT_COPY_REQUIRED");
            Need(oldAssembly.Equals(Path.Combine(oldRoot,Path.GetFileName(c.AssemblyPath)),StringComparison.OrdinalIgnoreCase),"MEASUREMENT_SOURCE_ASSEMBLY_PATH_MISMATCH");
            Need(prior.ContainsKey("inputFilesUnchanged")&&Convert.ToBoolean(prior["inputFilesUnchanged"])&&Items(prior["changedInputFiles"]).Length==0,"MEASUREMENT_SOURCE_INPUTS_CHANGED");
            var cabinet=Map(c.Layout["cabinet"]);c.Width=Number(cabinet,"widthMm");c.Height=Number(cabinet,"heightMm");c.Depth=Number(cabinet,"depthMm");c.DoorWidth=Number(c.Layout,"doorLeafWidthMm");
            Need((Near(c.Width,1000)||Near(c.Width,900))&&Near(c.Height,2000)&&Near(c.Depth,500)&&Near(c.DoorWidth,(c.Width-126)/2),"ASSEMBLY_TRIAL_MATRIX_RANGE_REQUIRED"); ValidateLayout(c);
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
        object[] values=Items(c.Sw.GetDocumentDependencies2(path,false,false,false)); Need(values.Length%2==0,"ASSEMBLY_DEPENDENCY_ARRAY_INVALID " + path);
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
                Need(c.Sw.ReplaceReferencedDocument(item.Value,old,copies[old]),"ASSEMBLY_STRUCTURE_REWIRE_FAILED " + item.Value + " -> " + old);
            }
        }
        foreach(var item in copies)
        {
            string[] actual=Dependencies(c,item.Value); foreach(string dependency in actual)Within(dependency,folder);
            Need(new HashSet<string>(actual,StringComparer.OrdinalIgnoreCase).SetEquals(dependencies[item.Key].Select(path=>copies[path])),"ASSEMBLY_STRUCTURE_REWIRED_DEPENDENCY_SET_MISMATCH " + item.Value);
        }
        c.Event(new { inspection="minimum_structure_dependency_closure",copyMethod="verified byte copy of complete part/assembly context before native ReplaceReferencedDocument; retains native SplitBody/MirrorStock context",files=copies.Select(item=>new {source=item.Key,path=item.Value,sourceSha256=c.InputHashes[item.Key],byteCopySha256=c.InputHashes[item.Key],rewiredSha256=Hash(item.Value),preparedAsStructuralPart=refreshVisited.Contains(item.Key)}).ToArray(),dependencyEdges=dependencies.SelectMany(item=>item.Value.Select(path=>new {source=item.Key,dependency=path,copiedSource=copies[item.Key],copiedDependency=copies[path]})).ToArray(),assemblyContextCycles=contextCycles.ToArray(),partRefreshOrder=refreshOrder.Select(path=>copies[path]).ToArray(),copiedContextAssemblyCount=ordered.Count(path=>Path.GetExtension(path).Equals(".SLDASM",StringComparison.OrdinalIgnoreCase)),insertedTargetPartCount=targets.Length,referenceModelsInserted=false,contextAssembliesExplicitlyRebuilt=false });
        string master=copies[c.Source("标准寄存柜 模型.SLDPRT")];
        foreach(string source in refreshOrder)
        {
            string path=copies[source]; ModelDoc2 model=null,loadedSource=null;string loadedSourceHash=null;
            try
            {
                if(Path.GetFileName(path).Equals("门框 下.sldprt",StringComparison.OrdinalIgnoreCase))
                {
                    loadedSourceHash=Hash(master);loadedSource=c.Open(master,true);
                    c.Event(new {inspection="locked_bottom_frame_exact_source_loaded",path=master,sourceSha256=loadedSourceHash,actualReadOnly=loadedSource.IsOpenedReadOnly(),geometry=PartGeometry(loadedSource),sourceRule="NativeParametricWidth.RunDerivedEdit; verified isolated A/B probe 20261113"});
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
                Need(model.ForceRebuild3(false),"ASSEMBLY_STRUCTURE_REBUILD_FAILED " + path); CheckFeatureHealth(model);
                double[] afterBox=BodyBounds(model);
                c.Event(new {inspection="structural_part_after_refresh_before_validation",path=path,source=source,beforeBoxMm=beforeBox,afterBoxMm=afterBox,deltaBoxMm=afterBox.Select((value,axis)=>value-beforeBox[axis]).ToArray(),geometry=PartGeometry(model),stageAcceptance=false});
                if(path.Equals(master,StringComparison.OrdinalIgnoreCase))
                {
                    Need(Near(DimensionValue(model,"D6@草图115"),c.DoorWidth)&&Near(DimensionValue(model,"D8@草图76"),c.DoorWidth),"ASSEMBLY_MASTER_WIDTH_READBACK_FAILED");
                    Need(Near(afterBox[3]-afterBox[0],c.Width),"ASSEMBLY_MASTER_ACTUAL_WIDTH_FAILED");
                }
                else
                {
                    double[] expected=ExpectedStructureX(Path.GetFileName(path),c.Width);
                    if(expected!=null)Need(Near(afterBox[0],expected[0])&&Near(afterBox[3],expected[1]),"ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED " + path);
                    Need(new[]{1,2,4,5}.All(axis=>Near(beforeBox[axis],afterBox[axis])),"ASSEMBLY_DERIVED_YZ_DRIFT " + path);
                }
                c.Save(model);
                c.Parts.Add(new { role=targets.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase)?"structural_component":"structural_native_reference",path=path,source=source,sha256=Hash(path),widthInterfaces=changes,nativeLinks=links,before=before,after=PartGeometry(model),geometryPass=true }); c.Write();
            }
            finally { c.Close(model);if(loadedSource!=null)c.Close(loadedSource);c.CloseOutputDocuments();if(loadedSourceHash!=null)Need(Hash(master)==loadedSourceHash,"ASSEMBLY_READONLY_DERIVED_SOURCE_CHANGED"); }
        }
        return targets.ToDictionary(name=>name,name=>copies[c.Source(name)],StringComparer.OrdinalIgnoreCase);
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
            Need(Items(sw.GetDocuments()).Length==0,"PROBE_EMPTY_SESSION_REQUIRED");
            if(sourceFirst) {sourceModel=c.Open(source,true);c.Event(new {inspection="probe_source_loaded",geometry=PartGeometry(sourceModel)});}
            model=c.Open(path,false); feature=model.FirstFeature() as Feature;
            while(feature!=null&&feature.GetTypeName2()!="SplitBody") {Feature next=feature.GetNextFeature() as Feature;Release(feature);feature=next;}
            Need(feature!=null,"PROBE_SPLIT_BODY_REQUIRED");
            Action<string> capture=stage=>{
                object files,parts,features,types,statuses,entities,components; int option=0;string config="";
                feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out option,out config);
                string[] paths=Items(files).Select(v=>Path.GetFullPath(Convert.ToString(v,Invariant))).ToArray();int[] states=Items(statuses).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                var loaded=new List<object>();foreach(object raw in Items(sw.GetDocuments())){var doc=(ModelDoc2)raw;try{loaded.Add(new {path=doc.GetPathName(),readOnly=doc.IsOpenedReadOnly(),dirty=doc.GetSaveFlag(),configuration=doc.ConfigurationManager.ActiveConfiguration.Name});}finally{Release(raw);}}
                c.Event(new {inspection="probe_stage",stage=stage,feature=feature.Name,sourcePaths=paths,statuses=states,configOption=option,configName=config,geometry=PartGeometry(model),loadedDocuments=loaded});
                Need(paths.Length>0&&states.Length==paths.Length&&paths.All(p=>p.Equals(source,StringComparison.OrdinalIgnoreCase))&&!states.Any(v=>v==0||v==5),"PROBE_EXACT_LOCAL_LINK_REQUIRED "+stage);
                if(stage=="opened"||stage=="relocked"||stage=="relock_rebuild"||stage=="cold_reopen")Need(states.All(v=>v==1),"PROBE_LOCK_STATE_REQUIRED "+stage);
                if(stage=="unlocked"||stage=="updated"||stage=="rebuilt")Need(states.All(v=>v!=1),"PROBE_UNLOCK_FAILED "+stage);
            };
            capture("opened");double[] before=BodyBounds(model);
            object f,linkedParts,fs,t,s,e,co;int opt=0;string cfg="";feature.ListExternalFileReferences2(out f,out linkedParts,out fs,out t,out s,out e,out co,out opt,out cfg);
            feature.UpdateExternalFileReferences(opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesunlockAll);capture("unlocked");
            feature.UpdateExternalFileReferences(opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone);capture("updated");
            Need(model.ForceRebuild3(false),"PROBE_REBUILD_FAILED");capture("rebuilt");
            feature.UpdateExternalFileReferences(opt,cfg??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll);capture("relocked");
            Need(model.ForceRebuild3(false),"PROBE_RELOCK_REBUILD_FAILED");capture("relock_rebuild");
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
                    object files,parts,features,types,statuses,entities,components; int configOption=0; string configName="";
                    feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out configOption,out configName);
                    object[] fileRows=Items(files); int[] before=Items(statuses).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                    if(fileRows.Length>0)
                    {
                        foreach(object file in fileRows)Within(Convert.ToString(file,Invariant),c.Output);
                        Need(!before.Any(v=>v==0||v==5),"ASSEMBLY_BROKEN_DERIVED_REFERENCE " + model.GetPathName());
                        bool locked=before.Any(v=>v==1);
                        Need(!locked||before.All(v=>v==1),"ASSEMBLY_MIXED_DERIVED_LOCK_STATE");
                        var stages=new List<object>();
                        Action<string,bool> capture=(stage,expectLocked)=>{
                            object sf,sp,sft,st,ss,se,sc;int so=0;string sn="";feature.ListExternalFileReferences2(out sf,out sp,out sft,out st,out ss,out se,out sc,out so,out sn);
                            string[] paths=Items(sf).Select(v=>Path.GetFullPath(Convert.ToString(v,Invariant))).ToArray();int[] states=Items(ss).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                            var row=new {stage=stage,sourcePaths=paths,statuses=states,geometry=PartGeometry(model)};stages.Add(row);c.Event(new {inspection="derived_link_refresh_stage",path=model.GetPathName(),feature=feature.Name,state=row});
                            Need(states.Length==before.Length&&paths.Length==fileRows.Length&&!states.Any(v=>v==0||v==5)&&new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase).SetEquals(fileRows.Select(v=>Path.GetFullPath(Convert.ToString(v,Invariant)))),"ASSEMBLY_DERIVED_INTERMEDIATE_REFERENCE_FAILED "+stage);
                            if(locked)Need(expectLocked?states.All(v=>v==1):states.All(v=>v!=1),"ASSEMBLY_DERIVED_INTERMEDIATE_LOCK_FAILED "+stage);
                        };
                        if(locked){feature.UpdateExternalFileReferences(configOption,configName??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesunlockAll);capture("unlocked",false);}
                        feature.UpdateExternalFileReferences(configOption,configName??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone);
                        capture("updated",false);
                        Need(model.ForceRebuild3(false),"ASSEMBLY_DERIVED_REFRESH_FAILED " + model.GetPathName());
                        capture("rebuilt",false);
                        if(locked){feature.UpdateExternalFileReferences(configOption,configName??"",(int)swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll);capture("relocked",true);Need(model.ForceRebuild3(false),"ASSEMBLY_DERIVED_RELOCK_REBUILD_FAILED");capture("relock_rebuilt",true);}
                        feature.ListExternalFileReferences2(out files,out parts,out features,out types,out statuses,out entities,out components,out configOption,out configName);
                        int[] after=Items(statuses).Select(v=>Convert.ToInt32(v,Invariant)).ToArray();
                        Need(after.Length==before.Length&&!after.Any(v=>v==0||v==5)&&(!locked||after.All(v=>v==1)),"ASSEMBLY_DERIVED_REFERENCE_STATUS_FAILED");
                        rows.Add(new {feature=feature.Name,type=type,sourcePaths=files,statusesBefore=before,statusesAfter=after,wasLocked=locked,lockStateRestored=!locked||after.All(v=>v==1),stages=stages,refreshed=true});
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
        Need(model.ForceRebuild3(false),"ASSEMBLY_DOOR_VARIABLE_REBUILD_FAILED");
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
                bool rebuilt=model.ForceRebuild3(false); c.Event(new {inspection="assembly_hardware_rebuild",path=path,source=source,rebuilt=rebuilt});
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
                            else { var changes=new List<object>(); SetDimension(model,"D2@草图1",height-10.5,changes); Need(model.ForceRebuild3(false),"ASSEMBLY_STIFFENER_REBUILD_FAILED"); double[] box=BodyBounds(model); Need(Near(box[4]-box[1],height-10.5),"ASSEMBLY_STIFFENER_ACTUAL_HEIGHT_FAILED"); edits=changes; }
                            CheckFeatureHealth(model); c.Save(model); c.Parts.Add(new {role=partIndex==0?"door_panel":"door_stiffener",hand=side,widthMm=c.DoorWidth,heightMm=height,path=paths[partIndex],sha256=Hash(paths[partIndex]),edits=edits,geometry=PartGeometry(model)}); c.Write();
                        }
                        finally { c.Close(model); }
                    }
                    variants[key]=paths;
                }
                double globalX=-hand*(c.Width/4+8.5),hingeX=-(c.DoorWidth/2-10)*hand,lockX=(c.DoorWidth/2-15)*hand,half=height/2,latchY=half-28.8;
                string prefix="door_"+side+"_"+index.ToString("D2",Invariant)+"_";
                Action<string,string,double[],double,double,double> add=(kind,path,rotation,x,y,z)=>c.Placements.Add(new Placement(prefix+kind,path,side,index,kind,rotation,globalX+x,center+y,z));
                add("panel",paths[0],Identity,0,0,0);
                add("stiffener",paths[1],FlipXZ,0,0,-.8);
                add("hinge_latch_bottom",c.Hardware["hinge_latch_plate"],BottomLatch,hingeX,-latchY,-.8);
                add("hinge_latch_top",c.Hardware[side=="L"?"top_latch_left":"top_latch_right"],Identity,(side=="L"?-1:1)*(c.DoorWidth-307)/2,(height-298)/2,0);
                add("hook_pad",c.Hardware["u_hook_pad"],side=="L"?FlipXZ:FlipYZ,lockX,0,-10.5);
                add("bushing_top",c.Hardware["plastic_bushing"],FlipYZ,hingeX,half+1.5,-7);
                add("bushing_bottom",c.Hardware["plastic_bushing"],Identity,hingeX,-half-1.5,-7);
                add("hinge_pin",c.Hardware["door_hinge_pin"],Identity,hingeX,-half-9.7,-7);
                add("circlip_top",c.Hardware["circlip"],Identity,hingeX,half-29.7,-7.7236);
                add("circlip_bottom",c.Hardware["circlip"],Identity,hingeX,-half+29.2,-7.7236);
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

    static void BuildNativeAssembly(BuildContext c)
    {
        c.CloseOutputDocuments(); string template=c.Sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
        Need(!String.IsNullOrWhiteSpace(template)&&File.Exists(template),"ASSEMBLY_NATIVE_TEMPLATE_MISSING");
        VariableTrial.RuntimeOwnership();
        ModelDoc2 model=c.Sw.NewDocument(template,0,0,0) as ModelDoc2;
        Need(model!=null,"ASSEMBLY_NEW_DOCUMENT_FAILED"); VariableTrial.RegisterOwned(model);
        MathUtility math=null;
        try
        {
            Need(Items(((AssemblyDoc)model).GetComponents(true)).Length==0,"ASSEMBLY_TEMPLATE_CONTAINS_COMPONENTS"); c.SaveNew(model,c.AssemblyPath,false); math=c.Sw.GetMathUtility() as MathUtility; Need(math!=null,"ASSEMBLY_MATH_UTILITY_MISSING");
            var assembly=(AssemblyDoc)model;
            foreach(Placement placement in c.Placements)
            {
                VariableTrial.RuntimeOwnership();
                ModelDoc2 part=c.Open(placement.Path,true); Component2 component=null; MathTransform transform=null;
                try
                {
                    int errors=0; c.Sw.ActivateDoc3(model.GetTitle(),false,0,ref errors); Need(errors==0,"ASSEMBLY_ACTIVATE_FAILED");
                    component=assembly.AddComponent5(placement.Path,(int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,"",false,"",placement.Transform[9],placement.Transform[10],placement.Transform[11]) as Component2;
                    Need(component!=null,"ASSEMBLY_COMPONENT_ADD_FAILED " + placement.Role); model.ClearSelection2(true); Need(component.Select4(false,null,false),"ASSEMBLY_COMPONENT_SELECT_FAILED");
                    if(component.IsFixed())assembly.UnfixComponent();
                    string beforeName=component.Name2;placement.OriginalName=beforeName; bool prior=c.Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames);
                    ModelDoc2 active=null; SelectionMgr selection=null; object selected=null;
                    try
                    {
                        c.Sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames,false);
                        errors=0; c.Sw.ActivateDoc3(model.GetTitle(),false,0,ref errors); active=c.Sw.ActiveDoc as ModelDoc2;
                        bool activeAssembly=active!=null&&SameNativeObject(active,model); model.ClearSelection2(true); bool selectReturned=component.Select4(false,null,false);
                        selection=model.SelectionManager as SelectionMgr; int selectedCount=selection==null?-1:selection.GetSelectedObjectCount2(-1);
                        if(selectedCount==1)selected=selection.GetSelectedObject6(1,-1);
                        bool selectedIdentity=selected!=null&&SameNativeObject(selected,component),updateNames=c.Sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames);
                        c.Event(new {inspection="assembly_component_rename_selection",requested=placement.Role,before=beforeName,path=placement.Path,activationErrors=errors,activeDocument=active==null?null:active.GetPathName(),activeAssemblyIdentity=activeAssembly,selectReturned=selectReturned,selectedCount=selectedCount,selectedComponentIdentity=selectedIdentity,updateComponentNames=updateNames});
                        Need(errors==0&&activeAssembly&&selectReturned&&selectedCount==1&&selectedIdentity&&!updateNames,"ASSEMBLY_COMPONENT_RENAME_CONTEXT_FAILED " + placement.Role);
                        component.Name2=placement.Role; string actualName=component.Name2;
                        bool nameMatches=actualName==placement.Role||actualName.StartsWith(placement.Role+"-",StringComparison.Ordinal);
                        bool unique=c.Placements.All(item=>item.ActualName==null||item.ActualName!=actualName);
                        c.Event(new {inspection="assembly_component_rename_readback",requested=placement.Role,before=beforeName,actual=actualName,path=component.GetPathName(),nameMatches=nameMatches,identityUnique=unique,selectedComponentIdentity=selectedIdentity});
                        Need(nameMatches&&unique&&component.GetPathName().Equals(placement.Path,StringComparison.OrdinalIgnoreCase),"ASSEMBLY_COMPONENT_RENAME_FAILED " + placement.Role);
                        placement.ActualName=actualName;
                    }
                    finally { Release(selected); Release(selection); Release(active); c.Sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames,prior); }
                    transform=math.CreateTransform(placement.Transform) as MathTransform; Need(transform!=null&&component.SetTransformAndSolve2(transform),"ASSEMBLY_TRANSFORM_FAILED " + placement.Role);
                    double[] actual=Transform(component); Need(SameTransform(actual,placement.Transform),"ASSEMBLY_TRANSFORM_READBACK_FAILED " + placement.Role);
                    model.ClearSelection2(true); Need(component.Select4(false,null,false),"ASSEMBLY_FIX_SELECTION_FAILED"); assembly.FixComponent(); Need(component.IsFixed(),"ASSEMBLY_FIXED_PLACEMENT_FAILED"); model.ClearSelection2(true);
                }
                finally { Release(transform); Release(component); c.Close(part); }
            }
            Need(model.ForceRebuild3(false),"ASSEMBLY_NATIVE_REBUILD_FAILED"); CheckFeatureHealth(model); c.Save(model);
            c.Evidence["assemblySha256"]=Hash(c.AssemblyPath); c.Event(new {inspection="minimum_assembly_built",path=c.AssemblyPath,componentCount=c.Placements.Count,placementMode="fixed native components at explicit transforms; no kinematic-mate claim"});
        }
        finally { Release(math); c.Close(model); c.CloseOutputDocuments(); }
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

    static object ReadDocumentMetadata(BuildContext c,Component2 component,Placement placement)
    {
        ModelDoc2 model=component.GetModelDoc2() as ModelDoc2;ModelDocExtension extension=null;string activeBefore=null,activeAfterRead=null,activeAfterRestore=null,referenced=component.ReferencedConfiguration;
        var result=new Dictionary<string,object>();
        try
        {
            Need(model!=null&&model is PartDoc,"ASSEMBLY_METADATA_PART_DOCUMENT_REQUIRED "+placement.Role);
            string path=Within(model.GetPathName(),c.Output);Need(path.Equals(Path.GetFullPath(placement.Path),StringComparison.OrdinalIgnoreCase),"ASSEMBLY_METADATA_DOCUMENT_PATH_MISMATCH "+placement.Role);
            activeBefore=MetadataActiveConfiguration(model);
            string[] configurations=Items(model.GetConfigurationNames()).Select(Convert.ToString).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            Need(configurations.Length>0&&configurations.All(value=>!String.IsNullOrEmpty(value))&&configurations.Distinct(StringComparer.Ordinal).Count()==configurations.Length&&configurations.Contains(referenced),"ASSEMBLY_METADATA_CONFIGURATION_MISSING "+placement.Role);
            string database,material=((PartDoc)model).GetMaterialPropertyName2(referenced,out database);var materials=new List<object>();var scopes=new List<object>();extension=model.Extension;
            foreach(string configuration in configurations)
            {
                string configurationDatabase,configurationMaterial=((PartDoc)model).GetMaterialPropertyName2(configuration,out configurationDatabase);
                materials.Add(new {configuration=configuration,materialName=configurationMaterial,materialDatabase=configurationDatabase});
            }
            foreach(string configuration in new[]{""}.Concat(configurations))
            {
                CustomPropertyManager manager=extension.CustomPropertyManager[configuration];
                try
                {
                    Need(manager!=null,"ASSEMBLY_METADATA_PROPERTY_MANAGER_MISSING "+placement.Role+"/"+configuration);
                    string[] names=Items(manager.GetNames()).Select(Convert.ToString).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
                    Need(names.All(value=>!String.IsNullOrEmpty(value))&&names.Distinct(StringComparer.Ordinal).Count()==names.Length,"ASSEMBLY_METADATA_PROPERTY_NAMES_INVALID "+placement.Role+"/"+configuration);
                    var properties=new List<object>();
                    foreach(string name in names)
                    {
                        string raw,resolved;bool wasResolved,linked;int status=manager.Get6(name,false,out raw,out resolved,out wasResolved,out linked);
                        Need(status==0||status==2,"ASSEMBLY_METADATA_NAMED_PROPERTY_NOT_PRESENT "+placement.Role+"/"+configuration+"/"+name);
                        properties.Add(new {name=name,value=raw,resolved=resolved,status=status,wasResolved=wasResolved,linked=linked});
                    }
                    scopes.Add(new {configuration=configuration,propertyCount=properties.Count,properties=properties});
                }
                finally {Release(manager);}
            }
            result["identity"]=placement.Role;result["path"]=path;result["configurations"]=configurations;result["referencedConfiguration"]=referenced;
            result["materialName"]=material;result["materialDatabase"]=database;result["configurationMaterials"]=materials;result["customPropertyScopes"]=scopes;
        }
        finally
        {
            try
            {
                if(model!=null&&activeBefore!=null)
                {
                    activeAfterRead=MetadataActiveConfiguration(model);
                    if(activeAfterRead!=activeBefore)Need(model.ShowConfiguration2(activeBefore),"ASSEMBLY_METADATA_CONFIGURATION_RESTORE_FAILED "+placement.Role);
                    activeAfterRestore=MetadataActiveConfiguration(model);Need(activeAfterRestore==activeBefore,"ASSEMBLY_METADATA_CONFIGURATION_RESTORE_READBACK_FAILED "+placement.Role);
                    Need(component.ReferencedConfiguration==referenced,"ASSEMBLY_METADATA_REFERENCED_CONFIGURATION_CHANGED "+placement.Role);
                }
            }
            finally {Release(extension);Release(model);}
        }
        result["activeConfigurationBefore"]=activeBefore;result["activeConfigurationAfterPropertyRead"]=activeAfterRead;result["activeConfigurationAfterRestore"]=activeAfterRestore;
        result["configurationRestored"]=activeBefore==activeAfterRestore;result["referencedConfigurationUnchanged"]=component.ReferencedConfiguration==referenced;result["readbackComplete"]=true;
        return result;
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
        var performance=new MeasurementPerformance(); var topologyTemplates=new List<TopologyTemplate>(); double phase=performance.Now;
        string hash=Hash(c.AssemblyPath); var worlds=new List<WorldBody>(); ModelDoc2 model=c.Open(c.AssemblyPath,true);
        try
        {
            var assembly=(AssemblyDoc)model; assembly.ResolveAllLightWeightComponents(false); Need(model.ForceRebuild3(false),"ASSEMBLY_REOPEN_REBUILD_FAILED"); CheckFeatureHealth(model); performance.Add("assembly_reopen_rebuild",phase);
            object[] components=Items(assembly.GetComponents(true)); Need(components.Length==c.Placements.Count,"ASSEMBLY_REOPEN_COMPONENT_COUNT_FAILED actual="+components.Length+" expected="+c.Placements.Count); var instances=new List<object>();c.Evidence["instances"]=instances;c.Evidence["nativeReadbackPass"]=false;
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
                    identities.Add(identity);c.Event(identity);
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
                    foreach(object sourceRaw in sources)
                    {
                        var source=(Body2)sourceRaw; Body2 body=null; MathTransform native=null;
                        try
                        {
                            phase=performance.Now;native=component.GetTotalTransform(true);double[] totalTransform=null;string nativeTransformError=null;
                            try {if(native!=null)totalTransform=Numbers(native.ArrayData);}catch(Exception error){nativeTransformError=error.Message;}
                            body=source.Copy() as Body2; Need(body!=null&&native!=null&&body.ApplyTransform(native),"ASSEMBLY_WORLD_BODY_TRANSFORM_FAILED " + placement.Role);
                            var world=new WorldBody {Body=body,Component=component.Name2,Kind=placement.Kind,Side=placement.Side,DoorIndex=placement.Index,Name=source.Name,Path=path,Box=Bounds(body)};performance.Add("native_world_copy_transform_and_six_direction_bounds",phase);
                            bool normalBody=bodyInfo==(int)swBodyInfo_e.swNormalBody_e,eligible=normalBody&&!String.IsNullOrEmpty(configuration)&&CacheTransformAvailable(totalTransform)&&SameTransform(totalTransform,actual),reused=false;
                            if(normalBody)performance.NormalBodies++;string cacheReason=eligible?"no_same_key_template":!normalBody?"normal_body_not_confirmed":String.IsNullOrEmpty(configuration)?"configuration_unavailable":"native_total_transform_unverified",cacheError=null,cacheStoreError=null;var identityChecks=new List<object>();TopologyTemplate template=null;double[] deltaMm=null;object boundsCheck=null;
                            phase=performance.Now;
                            if(eligible)foreach(TopologyTemplate candidate in topologyTemplates.Where(v=>String.Equals(v.Path,path,StringComparison.OrdinalIgnoreCase)&&v.Sha256==sourceHash&&v.Configuration==configuration&&SameRotationScale(v.Transform,totalTransform)))
                            {
                                try
                                {
                                    bool sameIUnknown=SameNativeObject(candidate.Source,source);int equality=c.Sw.IsSame(candidate.Source,source);
                                    identityChecks.Add(new {sourceIdentity=candidate.Role,sourceComponent=candidate.World.Component,sameIUnknown=sameIUnknown,swObjectEquality=equality,disabled=candidate.Disabled});
                                    if(sameIUnknown&&equality==(int)swObjectEquality.swObjectSame){template=candidate;break;}cacheReason="native_source_identity_different_or_unverified";
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
                                        retained=RetainTopologySource(source);Need(retained!=null&&!Object.ReferenceEquals(retained,source)&&SameNativeObject(retained,source)&&c.Sw.IsSame(retained,source)==(int)swObjectEquality.swObjectSame,"ASSEMBLY_CACHE_RETAINED_SOURCE_IDENTITY_UNVERIFIED");
                                        topologyTemplates.Add(new TopologyTemplate {Source=retained,World=world,Role=placement.Role,Path=path,Sha256=sourceHash,Configuration=configuration,Transform=(double[])totalTransform.Clone()});retained=null;performance.TopologyCacheEntries=topologyTemplates.Count;
                                    }
                                    catch(Exception error){cacheStoreError=error.Message;}
                                    finally{Release(retained);}performance.Add("topology_cache_source_retention",phase);
                                }
                            }
                            phase=performance.Now;
                            bodyRows.Add(new {bodyName=world.Name,boxMm=world.Box,topologyReadback=new {mode=reused?"same_native_source_translation_reuse":"native_transformed_body_scan",independentNativeTopologyReadbackForThisInstance=!reused,normalBodyInfo=bodyInfo,normalBodyConfirmed=normalBody,bodyInfoError=bodyInfoError,cacheEligible=eligible,cacheFallbackReason=cacheReason,cacheError=cacheError,cacheStoreError=cacheStoreError,nativeTotalTransform=totalTransform,nativeTransformError=nativeTransformError,sourceIdentity=reused?template.Role:null,sourceComponent=reused?template.World.Component:null,sourceActualSha256=reused?template.Sha256:null,sourceReferencedConfiguration=reused?template.Configuration:null,sourceNativeTotalTransform=reused?template.Transform:null,sourceNativeObjectIdentityChecks=identityChecks,translationMm=reused?deltaMm:null,nativeSixDirectionBoundsCheck=boundsCheck,nativeFaceAndEdgeIdsReferToSourceReadback=reused},cylinders=world.Cylinders.Select(v=>v.Evidence()).ToArray(),planarFaces=world.Planes.Select(v=>v.Evidence()).ToArray(),zNormalPlanarFaceBoxesMm=world.Planes.Where(v=>Math.Abs(v.Normal[2])>.999999).Select(v=>v.Box).ToArray(),faceBoxesAreApproximateCandidatesOnly=true}); worlds.Add(world); body=null;performance.Add("instance_evidence_materialization",phase);
                            if(placement.Kind=="panel")ValidateWorldPanel(c,placement,world.Box);
                            if(placement.Kind=="shelf"||placement.Kind=="crossbar")ValidateWorldBoundary(c,placement,world.Box);
                        }
                        finally { Release(native); Release(body); Release(source); }
                    }
                    instances.Add(new {identity=placement.Role,component=component.Name2,componentNameBeforeRename=placement.OriginalName,componentNameAfterRename=placement.ActualName,componentNameAfterReopen=component.Name2,path=path,sha256=sourceHash,side=placement.Side,index=placement.Index,kind=placement.Kind,configuration=configuration,fixedState=component.IsFixed(),transform=actual,transformPass=true,bodies=bodyRows});performance.Write(c);
                }
                c.Evidence["instances"]=instances;c.Evidence["nativeReadbackPass"]=true;c.Evidence["nativeTopologyIndependentlyScannedForEveryInstance"]=performance.TopologyCacheHits==0;phase=performance.Now;c.Evidence["hingeInterfaceMeasurements"]=MeasureHingeInterfaces(c,worlds);performance.Add("hinge_interface_measurements",phase);performance.Write(c);
                phase=performance.Now;c.Evidence["interference"]=MeasureInterference(c,model,worlds);performance.Add("full_native_interference",phase);performance.Write(c);RequireCompleteInterference(c.Evidence["interference"],components.Length);
                phase=performance.Now;var metadata=new List<object>();c.Evidence["documentMetadata"]=metadata;c.Evidence["documentMetadataReadbackPass"]=false;
                for(int i=0;i<components.Length;i++)metadata.Add(ReadDocumentMetadata(c,(Component2)components[i],matched[i]));
                c.Evidence["documentMetadataReadbackPass"]=metadata.Count==components.Length;performance.Add("document_metadata_readback_and_configuration_restore",phase);
                c.Evidence["reopen"]=new {pass=true,coldSession=c.ColdSession,assemblySha256=hash,componentCount=components.Length};performance.Write(c);
            }
            finally {foreach(object component in components)Release(component);}
        }
        finally
        {
            phase=performance.Now;try {foreach(TopologyTemplate template in topologyTemplates)Release(template.Source);foreach(WorldBody body in worlds)Release(body.Body);c.Close(model);c.CloseOutputDocuments();}
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
            model.ClearSelection2(true); manager=((AssemblyDoc)model).InterferenceDetectionManager; Need(manager!=null,"ASSEMBLY_INTERFERENCE_MANAGER_MISSING");
            manager.TreatCoincidenceAsInterference=false; manager.TreatSubAssembliesAsComponents=false; manager.IncludeMultibodyPartInterferences=true; manager.IgnoreHiddenBodies=false; manager.ShowIgnoredInterferences=true; manager.UseTransform=false; manager.CreateFastenersFolder=false;
            object[] results=Items(manager.GetInterferences()); declared=manager.GetInterferenceCount(); Need(declared==results.Length,"ASSEMBLY_INTERFERENCE_RESULT_COUNT_MISMATCH");
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
        finally { if(manager!=null) { try { manager.Done(); } catch(Exception error) { failures.Add(error.ToString()); complete=false; } Release(manager); } }
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
    static void CheckFeatureHealth(ModelDoc2 model,BuildContext context=null,string phase=null,bool requireHealthy=true)
    {
        var seen=new HashSet<int>(); int visited=0; var rows=new List<object>(); var failures=new List<string>(); Action<Feature,int,bool> scan=null;
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
        try
        {
            scan(model.FirstFeature() as Feature,0,true);
            if(context!=null)context.Event(new {inspection="assembly_native_feature_health",path=model.GetPathName(),phase=phase,traversalComplete=true,featureCount=rows.Count,healthy=failures.Count==0,features=rows.ToArray(),errors=failures.ToArray(),requiredForAcceptance=requireHealthy});
        }
        catch(Exception error)
        {
            if(context!=null)context.Event(new {inspection="assembly_native_feature_health",path=model.GetPathName(),phase=phase,traversalComplete=false,featureCount=rows.Count,healthy=false,features=rows.ToArray(),errors=failures.ToArray(),error=error.ToString(),requiredForAcceptance=requireHealthy});
            throw;
        }
        Need(!requireHealthy||failures.Count==0,"ASSEMBLY_NATIVE_FEATURE_ERROR " + model.GetPathName()+" "+String.Join("; ",failures.ToArray()));
    }
}
