using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

internal static class VariableTrial
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 120 };
    const string TrialRoot = @"D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112";
    static ISldWorks Sw;
    static int CadPid;
    static long CadTicks;
    static string CadExe;
    static bool OwnedSession;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetLongPathName(string shortPath,StringBuilder longPath,uint length);
    static string CanonicalExecutable(string path) {var value=new StringBuilder(32768);uint result=GetLongPathName(path,value,(uint)value.Capacity);Need(result>0&&result<value.Capacity,"EXECUTABLE_PATH_UNRESOLVED");return value.ToString();}
    static Dictionary<string, object> Job;
    static List<object> Events = new List<object>();
    static readonly Stopwatch RuntimeClock=Stopwatch.StartNew();
    static readonly List<object> EventTimes=new List<object>();
    static string ResultPath;
    static readonly List<ModelDoc2> OwnedUnsaved = new List<ModelDoc2>();
    internal static void RegisterOwned(ModelDoc2 model) { Need(model!=null,"OWNED_MODEL_REQUIRED"); OwnedUnsaved.Add(model); }
    internal static string OutputRoot { get { return Path.Combine(Text("cadDirectory"),"outputs"); } }
    static void Need(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    static Dictionary<string,object> SharedOwner(string directory)
    {
        using(var stream=new FileStream(Path.Combine(directory,"owner.json"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        using(var reader=new StreamReader(stream,Encoding.UTF8))return Json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
    }
    internal static void RuntimeOwnership()
    {
        if(Job==null)return;
        if(Job.ContainsKey("cancellationPath"))Need(!File.Exists(Text("cancellationPath")),"TRIAL_CANCELLATION_REQUESTED");
        if(Job.ContainsKey("sharedLockDirectory"))
        {
            string expected=@"D:\Winnsen_Structure_Agent_Studio\data\native_model_requests\.native-worker-instance.lock";
            Need(String.Equals(Full(Text("sharedLockDirectory")),expected,StringComparison.OrdinalIgnoreCase),"SHARED_LOCK_PATH_INVALID");
            var owner=SharedOwner(expected);
            Need(Convert.ToString(owner["token"])==Text("sharedToken")&&Convert.ToString(owner["workerId"])==Text("sharedWorkerId"),"NATIVE_SHARED_LOCK_LOST");
        }
    }
    static string Text(string key) { return Convert.ToString(Job[key], CultureInfo.InvariantCulture); }
    static string Full(string path) { return Path.GetFullPath(path); }
    static void Local(string path)
    {
        Need(Full(path).StartsWith(TrialRoot + "\\", StringComparison.OrdinalIgnoreCase), "TRIAL_PATH_REQUIRED " + path);
        for (string p=Full(path); !String.IsNullOrEmpty(p); p=Path.GetDirectoryName(p))
            if(File.Exists(p)||Directory.Exists(p)) Need((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"REPARSE_PATH " + p);
    }
    static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    static void Record(object value) { Events.Add(value);EventTimes.Add(new {index=Events.Count-1,atUtc=DateTime.UtcNow.ToString("o"),elapsedMs=RuntimeClock.ElapsedMilliseconds}); File.WriteAllText(ResultPath, Json.Serialize(new { action=Text("action"),cadPid=CadPid,cadTicks=CadTicks,cadExe=CadExe,production_release=false,events=Events,eventTimes=EventTimes }),new UTF8Encoding(false)); Console.WriteLine(Json.Serialize(value)); }
    static object[] Items(object value) { return value==null ? new object[0] : ((IEnumerable)value).Cast<object>().ToArray(); }
    static int TypeFor(string path) {return Path.GetExtension(path).Equals(".SLDASM",StringComparison.OrdinalIgnoreCase)?2:Path.GetExtension(path).Equals(".SLDDRW",StringComparison.OrdinalIgnoreCase)?3:1;}
    static ModelDoc2 Open(string path, bool readOnly)
    {
        RuntimeOwnership();
        Local(path); int errors=0,warnings=0;
        ModelDoc2 model=Sw.OpenDoc6(path,TypeFor(path),1+(readOnly?2:0),"",ref errors,ref warnings) as ModelDoc2;
        Record(new {inspection="open",path=path,errors=errors,warnings=warnings,actualReadOnly=model==null?(bool?)null:model.IsOpenedReadOnly()});
        Need(model!=null && errors==0,"OPEN_FAILED " + path+" "+errors+"/"+warnings);
        Need(!readOnly || model.IsOpenedReadOnly(),"READONLY_NOT_APPLIED");
        AssertLoadedScope(); return model;
    }
    static void AssertLoadedScope()
    {
        foreach(object value in Items(Sw.GetDocuments())) {ModelDoc2 model=value as ModelDoc2;try {Need(model!=null,"NON_MODEL_DOCUMENT");Need(OwnedDocument(model),"EXTERNAL_LOADED_DOCUMENT "+model.GetPathName());}finally{Release(value);}}
    }
    static bool OwnedDocument(ModelDoc2 model)
    {
        string path=model.GetPathName();
        if(String.IsNullOrEmpty(path))return OwnedUnsaved.Any(owned=>{try{return Sw.IsSame(owned,model)==1;}catch{return false;}});
        try {Local(path);return Full(path).StartsWith(Full(Text("cadDirectory"))+"\\",StringComparison.OrdinalIgnoreCase);} catch{return false;}
    }
    static void Rewire(string folder)
    {
        Local(folder); var files=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(p=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(p).ToUpperInvariant())).ToArray();
        var byName=files.GroupBy(p=>Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);
        foreach(string file in files)
        {
            RuntimeOwnership();
            object[] deps=Items(Sw.GetDocumentDependencies2(file,false,false,false));
            for(int i=1;i<deps.Length;i+=2)
            {
                string old=Convert.ToString(deps[i]),target=null;
                if(Full(old).StartsWith(Full(folder)+"\\",StringComparison.OrdinalIgnoreCase)&&File.Exists(old))target=old;
                if(target==null&&Job.ContainsKey("sourceCadDirectory"))
                {
                    string sourceRoot=Full(Text("sourceCadDirectory"));
                    if(Full(old).StartsWith(sourceRoot+"\\",StringComparison.OrdinalIgnoreCase)) {string mapped=Path.Combine(folder,Full(old).Substring(sourceRoot.Length+1));if(File.Exists(mapped))target=mapped;}
                }
                string[] candidates;
                if(Job.ContainsKey("assemblyResume"))Need(target!=null,"RESUME_EXACT_RELATIVE_DEPENDENCY_REQUIRED "+file+" -> "+old);
                if(target==null&&byName.TryGetValue(Path.GetFileName(old),out candidates)&&candidates.Length==1)target=candidates[0];
                Need(target!=null,"MISSING_OR_AMBIGUOUS_COPIED_DEPENDENCY "+file+" -> "+old);
                if(String.Equals(Full(old),Full(target),StringComparison.OrdinalIgnoreCase))continue;
                bool ok=Sw.ReplaceReferencedDocument(file,old,target);Record(new {inspection="rewrite_reference",file=file,old=old,target=target,success=ok});Need(ok,"REWRITE_REFERENCE_FAILED");
            }
        }
        foreach(string file in files)
        {
            object[] deps=Items(Sw.GetDocumentDependencies2(file,false,false,false));
            for(int i=1;i<deps.Length;i+=2) {string dependency=Convert.ToString(deps[i]);Local(dependency);Need(Full(dependency).StartsWith(Full(folder)+"\\",StringComparison.OrdinalIgnoreCase),"DEPENDENCY_OUTSIDE_CURRENT_COPY "+dependency);}
        }
        Record(new {inspection="dependency_closure",fileCount=files.Length,pass=true});
    }
    static void ReadOnlyDoorDependencies()
    {
        Need(Directory.Exists(OutputRoot),"DOOR_OUTPUTS_REQUIRED");string[] panels=Directory.GetFiles(OutputRoot,"*.SLDPRT",SearchOption.AllDirectories);Need(panels.Length==6,"SIX_DOOR_PARTS_REQUIRED");
        foreach(string panel in panels)
        {
            Local(panel);var dependencies=new List<string>();object[] values=Items(Sw.GetDocumentDependencies2(panel,false,false,false));
            Need(values.Length%2==0,"DOOR_DEPENDENCY_RECORD_INCOMPLETE");
            for(int i=1;i<values.Length;i+=2){string dependency=Full(Convert.ToString(values[i]));Local(dependency);Need(dependency.StartsWith(Full(Text("cadDirectory"))+"\\",StringComparison.OrdinalIgnoreCase)&&File.Exists(dependency),"DOOR_DEPENDENCY_OUTSIDE_COPY "+dependency);dependencies.Add(dependency);}
            Record(new {inspection="door_readonly_dependencies",path=panel,dependencies=dependencies,rewritten=false,pass=true});
        }
    }
    static double[] Bounds(Body2 body)
    {
        double[] box=new double[6];
        for(int a=0;a<3;a++) {double x,y,z;double[] v=new double[3];v[a]=-1;Need(body.GetExtremePoint(v[0],v[1],v[2],out x,out y,out z),"EXTREME_FAILED");box[a]=new[]{x,y,z}[a]*1000;v[a]=1;Need(body.GetExtremePoint(v[0],v[1],v[2],out x,out y,out z),"EXTREME_FAILED");box[a+3]=new[]{x,y,z}[a]*1000;}
        return box;
    }
    static object Geometry(ModelDoc2 model)
    {
        var bodies=new List<object>();PartDoc part=model as PartDoc;
        if(part==null)return new {bodies=bodies};
        foreach(object value in Items(part.GetBodies2(0,false)))
        {
            Body2 body=(Body2)value;
            try
            {
                var circles=new List<object>();foreach(object e in Items(body.GetEdges())) {Edge edge=e as Edge;Curve curve=null;try {curve=edge.GetCurve() as Curve;if(curve!=null&&curve.IsCircle()) circles.Add(curve.CircleParams);}finally{Release(curve);Release(edge);}}
                bodies.Add(new {name=body.Name,boxMm=Bounds(body),sheetMetal=body.IsSheetMetal(),massProperties=body.GetMassProperties(1),circleEdges=circles});
            }finally{Release(body);}
        }
        return new {bodies=bodies};
    }
    static object Properties(ModelDoc2 model)
    {
        string activeBefore=ActiveConfiguration(model);
        string database=null,material=null;var properties=new List<object>();PartDoc part=model as PartDoc;
        if(part!=null)material=part.GetMaterialPropertyName2("Default",out database);
        string[] configurations=Items(model.GetConfigurationNames()).Select(Convert.ToString).OrderBy(v=>v,StringComparer.Ordinal).ToArray();
        var materials=new List<object>();
        try
        {
            foreach(string configuration in configurations)
            {
                string configurationDatabase=null,configurationMaterial=part==null?null:part.GetMaterialPropertyName2(configuration,out configurationDatabase);
                materials.Add(new {configuration=configuration,materialName=configurationMaterial,materialDatabase=configurationDatabase});
            }
            foreach(string configuration in new[]{""}.Concat(configurations))
            {
                CustomPropertyManager manager=model.Extension.CustomPropertyManager[configuration];
                try {foreach(object item in Items(manager.GetNames())) {string name=Convert.ToString(item),raw,resolved;bool wasResolved,linked;int status=manager.Get6(name,false,out raw,out resolved,out wasResolved,out linked);Need(status==0||status==2,"PROPERTY_READBACK_VALUE_MISSING "+configuration+"/"+name);properties.Add(new {configuration=configuration,name=name,value=raw,resolved=resolved,status=status,wasResolved=wasResolved,linked=linked});}}finally{Release(manager);}
            }
        }
        finally
        {
            if(ActiveConfiguration(model)!=activeBefore)Need(model.ShowConfiguration2(activeBefore),"PROPERTY_READ_CONFIGURATION_RESTORE_FAILED");
            Need(ActiveConfiguration(model)==activeBefore,"PROPERTY_READ_CONFIGURATION_RESTORE_READBACK_FAILED");
        }
        return new {materialName=material,materialDatabase=database,configurationMaterials=materials,custom=properties};
    }
    static void ScanFeature(Feature feature,List<object> rows,int depth,HashSet<int> seen,bool top,bool dimensions=true)
    {
        int guard=0;
        while(feature!=null && guard++<10000)
        {
            Feature next=null;
            try
            {
                next=top?feature.GetNextFeature() as Feature:feature.GetNextSubFeature() as Feature;
                if(seen.Add(feature.GetID()))
                {
                    var dims=new List<object>();DisplayDimension display=dimensions?feature.GetFirstDisplayDimension() as DisplayDimension:null;int dg=0;
                    while(display!=null && dg++<500) {DisplayDimension dn=null;Dimension dim=null;try{dn=feature.GetNextDisplayDimension(display) as DisplayDimension;dim=display.GetDimension2(0) as Dimension;if(dim!=null)dims.Add(new{name=dim.FullName,valueM=dim.SystemValue});}finally{Release(dim);Release(display);}display=dn;}
                    bool warning;int error=feature.GetErrorCode2(out warning);
                    rows.Add(new {id=feature.GetID(),name=feature.Name,type=feature.GetTypeName2(),suppressed=feature.IsSuppressed(),error=error,isWarning=warning,depth=depth,dimensions=dims});
                    if(depth<16) ScanFeature(feature.GetFirstSubFeature() as Feature,rows,depth+1,seen,false,dimensions);
                }
            }finally {Release(feature);}feature=next;
        }
    }
    static object Snapshot(ModelDoc2 model)
    {
        var features=new List<object>();ScanFeature(model.FirstFeature() as Feature,features,0,new HashSet<int>(),true);
        EquationMgr eq=model.GetEquationMgr() as EquationMgr;var equations=new List<object>();
        try {if(eq!=null)for(int i=0;i<eq.GetCount();i++)equations.Add(new {index=i,equation=eq.Equation[i],value=eq.Value[i]});}finally{Release(eq);}
        ConfigurationManager cm=model.ConfigurationManager;Configuration config=cm.ActiveConfiguration;string active=config.Name;Release(config);Release(cm);
        return new {path=model.GetPathName(),configurations=model.GetConfigurationNames(),activeConfiguration=active,features=features,equations=equations,properties=Properties(model),geometry=Geometry(model)};
    }
    static void Inspect()
    {
        foreach(object value in Items(Job["targets"]))
        {
            string path=Path.Combine(Text("cadDirectory"),Convert.ToString(value));ModelDoc2 model=Open(path,true);
            try{Record(new {inspection="model_snapshot",snapshot=Snapshot(model)});}finally{Sw.CloseDoc(model.GetTitle());Release(model);}
        }
    }
    static Dictionary<string,string> CadHashes()
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string file in Directory.GetFiles(Text("cadDirectory"),"*",SearchOption.AllDirectories).Where(p=>new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(p).ToUpperInvariant())))
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))result.Add(file,BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""));
        return result;
    }
    static Dimension DimensionFor(ModelDoc2 model,string name)
    {
        Dimension dim=model.Parameter(name) as Dimension;Need(dim!=null,"DIMENSION_MISSING "+name);return dim;
    }
    static void AddEquation(EquationMgr eq,string equation)
    {
        RuntimeOwnership();
        int expected=eq.GetCount();int added=eq.Add3(expected,equation,true,(int)swInConfigurationOpts_e.swAllConfiguration,null);
        Record(new {inspection="equation_added",equation=equation,index=added});Need(added==expected,"EQUATION_ADD_FAILED "+equation);
    }
    static void SetVariable(EquationMgr eq,string name,double value)
    {
        RuntimeOwnership();
        int index=-1;for(int i=0;i<eq.GetCount();i++)if(eq.Equation[i].TrimStart().StartsWith("\""+name+"\"",StringComparison.Ordinal)){index=i;break;}
        Need(index>=0,"VARIABLE_NOT_FOUND "+name);
        int changed=eq.SetEquationAndConfigurationOption(index,"\""+name+"\" = "+value.ToString("R",CultureInfo.InvariantCulture)+"mm",(int)swInConfigurationOpts_e.swAllConfiguration,null);
        Need(changed==index,"EQUATION_UPDATE_FAILED "+name);
    }
    static void CheckEquations(EquationMgr eq)
    {
        var rows=new List<object>();for(int i=0;i<eq.GetCount();i++){double value=eq.Value[i];int status=eq.Status;rows.Add(new {index=i,value=value,status=status,equation=eq.Equation[i]});Need(status==i,"EQUATION_STATUS_FAILED "+i);}
        Record(new {inspection="equations_evaluated",rows=rows});
    }
    static object CheckPanel(ModelDoc2 model,double width,double height,string stage)
    {
        Dimension dw=DimensionFor(model,"D2@草图1"),dh=DimensionFor(model,"D1@草图1");double w,h;
        try{w=dw.SystemValue*1000;h=dh.SystemValue*1000;}finally{Release(dw);Release(dh);}
        Need(Math.Abs(w-width)<.01&&Math.Abs(h-height)<.01,"DIMENSION_READBACK_FAILED "+stage);
        var bodies=Items(((PartDoc)model).GetBodies2(0,false));Need(bodies.Length==1,"ONE_BODY_REQUIRED");Body2 body=(Body2)bodies[0];double[] box;
        try{box=Bounds(body);Need(body.IsSheetMetal(),"SHEET_METAL_REQUIRED");}finally{Release(body);}
        bool pass=Math.Abs(box[3]-box[0]-width)<.01&&Math.Abs(box[4]-box[1]-height)<.01&&Math.Abs(box[5]-box[2]-15)<.01;
        Record(new {inspection="panel_size",stage=stage,widthMm=w,heightMm=h,boxMm=box,geometryPass=pass});Need(pass,"PANEL_GEOMETRY_NOT_FOLLOWING "+stage);
        Feature sheet=((PartDoc)model).FeatureByName("钣金1") as Feature;Need(sheet!=null,"SHEET_FEATURE_MISSING");SheetMetalFeatureData sd=sheet.GetDefinition() as SheetMetalFeatureData;Need(sd!=null,"SHEET_DEFINITION_MISSING");CustomBendAllowance allowance=null;
        try{allowance=sd.GetCustomBendAllowance();Record(new {inspection="sheet_parameters",thicknessMm=sd.Thickness*1000,radiusMm=sd.BendRadius*1000,allowanceType=allowance.Type,bendDeductionMm=allowance.BendDeduction*1000});Need(Math.Abs(sd.Thickness*1000-.8)<.01&&Math.Abs(sd.BendRadius*1000-.2)<.01,"SHEET_PARAMETERS_DRIFT");Need(allowance.Type==(int)swBendAllowanceTypes_e.swBendAllowanceDeduction&&Math.Abs(allowance.BendDeduction*1000-1.4)<.01,"BEND_DEDUCTION_DRIFT");}finally{Release(allowance);Release(sd);Release(sheet);}
        var features=new List<object>();ScanFeature(model.FirstFeature() as Feature,features,0,new HashSet<int>(),true,false);
        Record(new {inspection="panel_health",stage=stage,features=features});
        foreach(object row in features){string serialized=Json.Serialize(row);var data=Json.Deserialize<Dictionary<string,object>>(serialized);Need(Convert.ToBoolean(data["suppressed"])||Convert.ToInt32(data["error"])==0,"FEATURE_ERROR "+data["name"]);}
        return Geometry(model);
    }
    static void SaveModel(ModelDoc2 model,string stage)
    {
        RuntimeOwnership();AssertLoadedScope();
        int errors=0,warnings=0;bool saved=model.Save3(1,ref errors,ref warnings);Record(new {inspection="save",stage=stage,saved=saved,errors=errors,warnings=warnings});Need(saved&&errors==0,"SAVE_FAILED");
    }
    static void DefaultConfiguration(ModelDoc2 model)
    {
        ConfigurationManager manager=model.ConfigurationManager;Configuration config=manager.ActiveConfiguration;string name=config.Name;Release(config);Release(manager);
        if(name!="Default")model.ShowConfiguration2("Default");
        manager=model.ConfigurationManager;config=manager.ActiveConfiguration;
        try{Need(config.Name=="Default","DEFAULT_CONFIGURATION_REQUIRED");}finally{Release(config);Release(manager);}
    }
    static string ActiveConfiguration(ModelDoc2 model)
    {
        ConfigurationManager manager=model.ConfigurationManager;Configuration config=manager.ActiveConfiguration;
        try{return config.Name;}finally{Release(config);Release(manager);}
    }
    sealed class BendReadback
    {
        public int id,treeParentId;public string name,type,error;public int[] parentIds;
        public bool suppressed;public bool? useDefaultAllowance,useDefaultRadius,bendDown;public int? bendDirection;
        public double? angleRadians,thicknessMm;public double radiusMm;
        public Dictionary<string,object> storedAllowance,candidateEffectiveAllowance;public double? candidateRadiusMm;
        public int[] allowanceSourceIds,radiusSourceIds;
    }
    static Dictionary<string,object> ReadAllowance(CustomBendAllowance allowance)
    {
        Need(allowance!=null,"CUSTOM_BEND_ALLOWANCE_READ_REQUIRED");
        return new Dictionary<string,object>{{"type",allowance.Type},{"bendDeductionMm",allowance.BendDeduction*1000},{"bendAllowanceMm",allowance.BendAllowance*1000},{"kFactor",allowance.KFactor},{"bendTableFile",allowance.BendTableFile}};
    }
    static void ScanBendReadback(Feature feature,bool top,int treeParent,List<BendReadback> rows,HashSet<int> seen)
    {
        int guard=0;while(feature!=null&&guard++<10000)
        {
            Feature next=null;int id=0;
            try
            {
                next=top?feature.GetNextFeature() as Feature:feature.GetNextSubFeature() as Feature;id=feature.GetID();
                if(!seen.Add(id))continue;
                string type=feature.GetTypeName2();
                if(new[]{"SheetMetal","FlattenBends","ProcessBends","OneBend"}.Contains(type))
                {
                    var row=new BendReadback{id=id,treeParentId=treeParent,name=feature.Name,type=type,suppressed=feature.IsSuppressed()};rows.Add(row);
                    object definition=null;CustomBendAllowance allowance=null;var parents=new List<int>();
                    try
                    {
                        foreach(object value in Items(feature.GetParents())){try{Feature parent=value as Feature;Need(parent!=null,"BEND_PARENT_FEATURE_REQUIRED");parents.Add(parent.GetID());}finally{Release(value);}}
                        row.parentIds=parents.Distinct().OrderBy(v=>v).ToArray();definition=feature.GetDefinition();
                        if(type=="SheetMetal") {var data=definition as SheetMetalFeatureData;Need(data!=null,"SHEET_PROCESS_DEFINITION_REQUIRED");row.radiusMm=data.BendRadius*1000;row.thicknessMm=data.Thickness*1000;allowance=data.GetCustomBendAllowance();}
                        else if(type=="OneBend") {var data=definition as OneBendFeatureData;Need(data!=null,"ONE_BEND_PROCESS_DEFINITION_REQUIRED");row.radiusMm=data.BendRadius*1000;row.useDefaultAllowance=data.UseDefaultBendAllowance;row.useDefaultRadius=data.UseDefaultBendRadius;row.bendDown=data.BendDown;row.bendDirection=data.BendDirection;row.angleRadians=data.BendAngle;allowance=data.GetCustomBendAllowance();}
                        else {var data=definition as BendsFeatureData;Need(data!=null,"BENDS_PROCESS_DEFINITION_REQUIRED");row.radiusMm=data.BendRadius*1000;row.useDefaultAllowance=data.UseDefaultBendAllowance;row.useDefaultRadius=data.UseDefaultBendRadius;allowance=data.GetCustomBendAllowance();}
                        row.storedAllowance=ReadAllowance(allowance);
                    }
                    catch(Exception error){row.error=error.ToString();}
                    finally{Release(allowance);Release(definition);}
                }
                ScanBendReadback(feature.GetFirstSubFeature() as Feature,false,id,rows,seen);
            }
            finally{Release(feature);feature=next;}
        }
    }
    static void ResolveBendReadback(BendReadback row,Dictionary<int,BendReadback> rows,HashSet<int> resolving)
    {
        if(row.error!=null||row.candidateEffectiveAllowance!=null&&row.candidateRadiusMm.HasValue||!resolving.Add(row.id))return;
        var parents=(row.parentIds??new int[0]).Where(rows.ContainsKey).Select(id=>rows[id]).ToArray();
        foreach(var parent in parents)ResolveBendReadback(parent,rows,resolving);
        if(row.type=="SheetMetal"||row.useDefaultAllowance==false){row.candidateEffectiveAllowance=row.storedAllowance;row.allowanceSourceIds=new[]{row.id};}
        else if(parents.Length>0&&parents.All(p=>p.candidateEffectiveAllowance!=null)&&parents.Select(p=>Json.Serialize(p.candidateEffectiveAllowance)).Distinct().Count()==1){row.candidateEffectiveAllowance=parents[0].candidateEffectiveAllowance;row.allowanceSourceIds=parents.SelectMany(p=>p.allowanceSourceIds).Distinct().OrderBy(v=>v).ToArray();}
        if(row.type=="SheetMetal"||row.useDefaultRadius==false){row.candidateRadiusMm=row.radiusMm;row.radiusSourceIds=new[]{row.id};}
        else if(parents.Length>0&&parents.All(p=>p.candidateRadiusMm.HasValue)&&parents.Max(p=>p.candidateRadiusMm.Value)-parents.Min(p=>p.candidateRadiusMm.Value)<1e-9){row.candidateRadiusMm=parents[0].candidateRadiusMm;row.radiusSourceIds=parents.SelectMany(p=>p.radiusSourceIds).Distinct().OrderBy(v=>v).ToArray();}
        resolving.Remove(row.id);
    }
    static object PanelProcessReadback(ModelDoc2 model)
    {
        Need(ActiveConfiguration(model)=="Default","PROCESS_READBACK_DEFAULT_REQUIRED");var rows=new List<BendReadback>();
        ScanBendReadback(model.FirstFeature() as Feature,true,0,rows,new HashSet<int>());var index=rows.ToDictionary(r=>r.id);
        foreach(var row in rows)ResolveBendReadback(row,index,new HashSet<int>());
        bool complete=rows.Count(r=>r.type=="SheetMetal")==1&&rows.Count(r=>r.type=="FlattenBends")==1&&rows.Count(r=>r.type=="ProcessBends")==1&&rows.Count(r=>r.type=="OneBend")==12&&rows.All(r=>r.error==null&&r.storedAllowance!=null);
        bool topology=false;
        if(complete)
        {
            var sheet=rows.Single(r=>r.type=="SheetMetal");var flatten=rows.Single(r=>r.type=="FlattenBends");var process=rows.Single(r=>r.type=="ProcessBends");
            topology=sheet.parentIds.Length==0&&flatten.parentIds.SequenceEqual(new[]{sheet.id})&&process.parentIds.OrderBy(v=>v).SequenceEqual(new[]{sheet.id,flatten.id}.OrderBy(v=>v))&&rows.Where(r=>r.type=="OneBend").All(r=>r.treeParentId==flatten.id&&r.parentIds.SequenceEqual(new[]{flatten.id}));
        }
        bool explicitMatch=complete&&rows.All(r=>(r.type!="SheetMetal"&&r.useDefaultAllowance!=false||Convert.ToInt32(r.storedAllowance["type"])==(int)swBendAllowanceTypes_e.swBendAllowanceDeduction&&Math.Abs(Convert.ToDouble(r.storedAllowance["bendDeductionMm"])-1.4)<.01)&&(r.type!="SheetMetal"&&r.useDefaultRadius!=false||Math.Abs(r.radiusMm-.2)<.01))&&rows.Where(r=>r.thicknessMm.HasValue).All(r=>Math.Abs(r.thicknessMm.Value-.8)<.01);
        bool candidateMatch=complete&&rows.All(r=>r.candidateEffectiveAllowance!=null&&r.candidateRadiusMm.HasValue&&Convert.ToInt32(r.candidateEffectiveAllowance["type"])==(int)swBendAllowanceTypes_e.swBendAllowanceDeduction&&Math.Abs(Convert.ToDouble(r.candidateEffectiveAllowance["bendDeductionMm"])-1.4)<.01&&Math.Abs(r.candidateRadiusMm.Value-.2)<.01);
        return new {schema="16029-door-process-readback/v1",configuration=ActiveConfiguration(model),features=rows.OrderBy(r=>r.id).ToArray(),rawReadbackComplete=complete,pilotTopologyMatches=topology,fixedExplicitOverridesMatch=explicitMatch,candidateFixedProcessMatches=candidateMatch,
            effectiveInheritanceVerified=false,resolutionBasis="single-sheet fixed pilot; dependency-parent consensus, inheritance semantics not independently verified",
            directionMeaning="native feature enum in model coordinates; DXF manufacturing face and bend direction not confirmed",manufacturingOrientationVerified=false,manufacturingAccepted=false};
    }
    static void ReopenValidation()
    {
        var before=CadHashes();
        foreach(object value in Items(Job["targets"]))
        {
            string path=Path.Combine(Text("cadDirectory"),Convert.ToString(value));ModelDoc2 model=Open(path,true);EquationMgr eq=null;
            try
            {
                ConfigurationManager manager=model.ConfigurationManager;Configuration active=manager.ActiveConfiguration;
                try {Need(active.Name=="Default","SAVED_CONFIGURATION_DRIFT");}finally{Release(active);Release(manager);}
                eq=model.GetEquationMgr() as EquationMgr;Need(eq!=null&&eq.GetCount()==10,"SAVED_EQUATIONS_MISSING");CheckEquations(eq);
                Record(new {inspection="reopen_panel",path=path,geometry=CheckPanel(model,437,471.25,"independent-reopen"),snapshot=Snapshot(model)});
            }finally{Release(eq);Sw.CloseDoc(model.GetTitle());Release(model);}
        }
        var after=CadHashes();Need(before.Count==after.Count&&before.All(p=>after.ContainsKey(p.Key)&&after[p.Key]==p.Value),"READONLY_REOPEN_CHANGED_FILES");
        Record(new {inspection="readonly_hash_comparison",pass=true,files=before});
    }
    static string Digest(string value)
    {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").Substring(0,10);
    }
    static void WriteOutputManifest(string root,List<object> cases,bool complete,bool includeDrawings)
    {
        var manifest=new Dictionary<string,object>{{"schema",includeDrawings?"16029-variable-output-manifest/v1":"16029-variable-output-manifest/v2"},{"complete",complete},{"cases",cases}};
        if(!includeDrawings){manifest["outputScope"]="parts_dxf_only";manifest["drawingStatus"]="deferred_by_user_20261113";manifest["production_release"]=false;}
        File.WriteAllText(Path.Combine(root,"output-manifest.json"),Json.Serialize(manifest),new UTF8Encoding(false));
    }
    static void OutputCases(bool includeDrawings=true)
    {
        Need(includeDrawings||Job.ContainsKey("outputScope")&&Text("outputScope")=="parts_dxf_only","EXPLICIT_DXF_ONLY_SCOPE_REQUIRED");
        if(!includeDrawings)Record(new {inspection="output_scope",outputScope="parts_dxf_only",drawingStatus="deferred_by_user_20261113",fullPlanPass=false,production_release=false});
        string root=OutputRoot;Local(root);Need(!Directory.Exists(root),"OUTPUT_ROOT_EXISTS");Directory.CreateDirectory(root);
        int caseIndex=0;var manifestCases=new List<object>();
        foreach(object value in Items(Job["doors"]))
        {
            var door=(Dictionary<string,object>)value;var variables=(Dictionary<string,object>)door["variables"];var properties=(Dictionary<string,object>)door["properties"];
            string hand=Convert.ToString(door["hand"]);Need(hand=="L"||hand=="R","INVALID_HAND");
            double width=Convert.ToDouble(variables["DOOR_W"]),height=Convert.ToDouble(variables["DOOR_H"]);Need(width>0&&height>0&&width<=537&&height<=1000,"DOOR_TRIAL_SIZE_OUTSIDE_BOUNDS");
            string source=Path.Combine(Text("cadDirectory"),hand=="L"?"door_panel_left_W437_H471p25.SLDPRT":"door_panel_right_W437_H471p25.SLDPRT");
            string folder=Path.Combine(root,hand+(caseIndex++).ToString(CultureInfo.InvariantCulture));Directory.CreateDirectory(folder);
            ModelDoc2 model=Open(source,true);string materialDb,material=((PartDoc)model).GetMaterialPropertyName2("Default",out materialDb);Sw.CloseDoc(model.GetTitle());Release(model);
            string materialIdentity=String.IsNullOrEmpty(material)?"UNSPECIFIED":material;
            string filename="door_"+hand+"_W"+width.ToString("0.####",CultureInfo.InvariantCulture).Replace('.','p')+"_H"+height.ToString("0.####",CultureInfo.InvariantCulture).Replace('.','p')+"_t0p8_mat"+Digest(materialIdentity)+"_pv1.SLDPRT";
            string path=Path.Combine(folder,filename);Need(path.Length<255,"WINDOWS_PATH_TOO_LONG");File.Copy(source,path,false);model=Open(path,false);EquationMgr eq=null;
            try
            {
                DefaultConfiguration(model);eq=model.GetEquationMgr() as EquationMgr;Need(eq!=null&&eq.GetCount()==10,"TEMPLATE_EQUATIONS_MISSING");
                SetVariable(eq,"DOOR_W",width);SetVariable(eq,"DOOR_H",height);eq.EvaluateAll();CheckEquations(eq);Need(model.ForceRebuild3(false),"OUTPUT_SIZE_REBUILD_FAILED");
                object geometry=CheckPanel(model,width,height,"output-"+hand);CheckHandGeometry(geometry,hand,width,height);
                CustomPropertyManager manager=model.Extension.CustomPropertyManager[""];
                try
                {
                    var labels=new Dictionary<string,string>{{"TRIAL_IDENTITY",Convert.ToString(door["identity"])+"_material"+materialIdentity},{"HAND",hand},{"SOURCE_MATERIAL",materialIdentity},{"SOURCE_MATERIAL_DATABASE",materialDb??""},{"PROCESS_VERSION",Convert.ToString(properties["processVersion"])},{"PROCESS_STATUS",Convert.ToString(properties["processStatus"])},{"PRODUCTION_RELEASE","false"}};
                    foreach(var item in labels)
                    {
                        manager.Add3(item.Key,(int)swCustomInfoType_e.swCustomInfoText,item.Value,(int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                        string raw,resolved;bool wasResolved,linked;manager.Get6(item.Key,false,out raw,out resolved,out wasResolved,out linked);Need(raw==item.Value,"OUTPUT_IDENTITY_PROPERTY_WRITE_FAILED "+item.Key);
                    }
                }finally{Release(manager);}
                bool baselineSize=Math.Abs(width-437)<.0001&&Math.Abs(height-471.25)<.0001;
                object flatPreparation=TrialOutput.PrepareFlat(model,baselineSize?473.4:Double.NaN,baselineSize?507.65:Double.NaN,.8);
                Record(new {inspection="output_flat_preparation",path=path,result=flatPreparation});
                CheckPanel(model,width,height,"after-flat-preparation");
                object processReadback=PanelProcessReadback(model);Record(new {inspection="output_process_readback",path=path,result=processReadback});
                var processProof=Map(Json.DeserializeObject(Json.Serialize(processReadback)));Need(Convert.ToBoolean(processProof["rawReadbackComplete"])&&Convert.ToBoolean(processProof["pilotTopologyMatches"])&&Convert.ToBoolean(processProof["fixedExplicitOverridesMatch"])&&Convert.ToBoolean(processProof["candidateFixedProcessMatches"]),"OUTPUT_FIXED_PROCESS_READBACK_INCOMPLETE_OR_CONFLICT");
                SaveModel(model,"output-sized");
                Release(eq);eq=null;
                Record(new {inspection="output_case_model",path=path,hand=hand,widthMm=width,heightMm=height,materialName=material,materialIdentity=materialIdentity,geometry=geometry});
                object dxf=TrialOutput.ExportDxf(Sw,model,Path.Combine(folder,"flat.dxf"));Record(new {inspection="output_dxf",path=path,result=dxf});
                ModelDoc2 refreshed=Sw.ActiveDoc as ModelDoc2;
                Need(refreshed!=null&&refreshed is PartDoc&&String.Equals(Path.GetFullPath(refreshed.GetPathName()),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase),"OUTPUT_ACTIVE_PART_CHANGED");
                Release(model);model=refreshed;DefaultConfiguration(model);
                Need(!model.GetSaveFlag(),"OUTPUT_PART_DIRTY_AFTER_EXPORT_NO_POST_EXPORT_SAVE_ALLOWED");
                Dictionary<string,object> drawingMap=null;
                if(includeDrawings)
                {
                    object drawing=TrialOutput.MakeDrawing(Sw,model,folder,hand=="L"?"left":"right");Record(new {inspection="output_drawing",path=path,result=drawing});
                    drawingMap=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(drawing));
                }
                else Record(new {inspection="output_drawing_deferred",path=path,reason="deferred_by_user_20261113",fullPlanPass=false});
                Need(ActiveConfiguration(model)=="Default","OUTPUT_SAVED_ACTIVE_CONFIGURATION_REQUIRED");
                manifestCases.Add(new {folder=Path.GetFileName(folder),part=Path.GetFileName(path),hand=hand,widthMm=width,heightMm=height,partSha256=FileHash(path),properties=Properties(model),configurations=model.GetConfigurationNames(),activeConfiguration=ActiveConfiguration(model),processReadback=processReadback,flatPreparation=flatPreparation,drawing=drawingMap,dxf=dxf});
                WriteOutputManifest(root,manifestCases,false,includeDrawings);
                AssertLoadedScope();
                foreach(object openValue in Items(Sw.GetDocuments()))
                {
                    ModelDoc2 openModel=openValue as ModelDoc2;
                    try{if(openModel!=null&&openModel is DrawingDoc&&OwnedDocument(openModel))Sw.CloseDoc(openModel.GetTitle());}finally{Release(openValue);}
                }
            }
            catch(Exception error){Record(new {inspection="output_case_failure",path=path,error=error.ToString()});throw;}
            finally{Release(eq);Release(model);CloseOwnedDocuments();}
        }
        WriteOutputManifest(root,manifestCases,true,includeDrawings);
    }
    static string FileHash(string path) {using(var sha=SHA256.Create())using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    // Isolated drawing diagnostics reuse a saved output copy; they do not update a stage-success pointer.
    static void DrawingDiagnostic()
    {
        string[] parts=Directory.GetFiles(OutputRoot,"*.SLDPRT",SearchOption.AllDirectories);Need(parts.Length>0,"PREPARED_OUTPUT_PART_REQUIRED");
        string path=parts.OrderBy(v=>v,StringComparer.Ordinal).First();string folder=Path.GetDirectoryName(path);Need(!Directory.GetFiles(folder,"*.SLDDRW").Any()&&!Directory.GetFiles(folder,"*.pdf").Any(),"NEW_DRAWING_DIAGNOSTIC_COPY_REQUIRED");
        string hand=Path.GetFileName(path).StartsWith("door_L_",StringComparison.Ordinal)?"left":Path.GetFileName(path).StartsWith("door_R_",StringComparison.Ordinal)?"right":null;Need(hand!=null,"DIAGNOSTIC_HAND_REQUIRED");
        var before=CadHashes();ModelDoc2 model=Open(path,false);
        try{DefaultConfiguration(model);object result=TrialOutput.MakeDrawing(Sw,model,folder,hand);Record(new {inspection="diagnostic_drawing",path=path,result=result,stageAcceptance=false});}
        finally{Release(model);CloseOwnedDocuments();}
        var after=CadHashes();Need(before.All(p=>after.ContainsKey(p.Key)&&after[p.Key]==p.Value),"DRAWING_DIAGNOSTIC_CHANGED_INPUT_CAD");
    }
    static void PdfDiagnostic(bool chineseFontOnly=false,bool chineseNoteIsolation=false,bool localizedFontProbe=false)
    {
        string[] drawings=Directory.GetFiles(OutputRoot,"*.SLDDRW",SearchOption.AllDirectories);Need(drawings.Length==1,"ONE_SAVED_DRAWING_REQUIRED_FOR_PDF_DIAGNOSTIC");
        var before=CadHashes();string output=Path.Combine(Text("cadDirectory"),"pdf-probe");Local(output);Need(!Directory.Exists(output),"NEW_PDF_DIAGNOSTIC_DIRECTORY_REQUIRED");
        try {object result=TrialOutput.PdfProbe(Sw,drawings[0],output,chineseFontOnly,chineseNoteIsolation,localizedFontProbe);Record(new {inspection="diagnostic_pdf",path=drawings[0],result=result,stageAcceptance=false});Need(Convert.ToBoolean(Map(Json.DeserializeObject(Json.Serialize(result)))["completed"]),"PDF_DIAGNOSTIC_SEQUENCE_INCOMPLETE");}
        finally {CloseOwnedDocuments();}
        var after=CadHashes();Need(before.All(p=>after.ContainsKey(p.Key)&&after[p.Key]==p.Value),"PDF_DIAGNOSTIC_CHANGED_INPUT_CAD");
    }
    static Dictionary<string,object> Map(object value) {return (Dictionary<string,object>)value;}
    static double Number(Dictionary<string,object> value,string key) {return Convert.ToDouble(value[key],CultureInfo.InvariantCulture);}
    static string PropertyContent(object value)
    {
        var source=Map(Json.DeserializeObject(Json.Serialize(value)));var rows=Items(source["custom"]).Select(Map).OrderBy(v=>Convert.ToString(v["configuration"]),StringComparer.Ordinal).ThenBy(v=>Convert.ToString(v["name"]),StringComparer.Ordinal).Select(v=>new {configuration=v["configuration"],name=v["name"],value=v["value"],resolved=v["resolved"],linked=v["linked"]}).ToArray();
        var materials=Items(source["configurationMaterials"]).Select(Map).OrderBy(v=>Convert.ToString(v["configuration"]),StringComparer.Ordinal).Select(v=>new {configuration=v["configuration"],materialName=v["materialName"],materialDatabase=v["materialDatabase"]}).ToArray();
        return Json.Serialize(new {materialName=source["materialName"],materialDatabase=source["materialDatabase"],configurationMaterials=materials,custom=rows});
    }
    static void CheckHandGeometry(object geometry,string hand,double width,double height)
    {
        var map=Map(Json.DeserializeObject(Json.Serialize(geometry)));var body=Map(Items(map["bodies"]).Single());
        var circles=Items(body["circleEdges"]).Select(v=>Items(v).Select(x=>Convert.ToDouble(x,CultureInfo.InvariantCulture)).ToArray()).ToArray();
        double sign=hand=="L"?1:-1;var hinge=circles.Where(v=>Math.Abs(v[6]*1000-5.1)<.01).ToArray();var locks=circles.Where(v=>Math.Abs(v[6]*1000-2)<.01).ToArray();
        Need(hinge.Length==4&&locks.Length==2,"RELOCATED_HAND_HOLE_COUNT_FAILED");
        Need(hinge.All(v=>Math.Abs(v[0]*1000+(width/2-10)*sign)<.01&&Math.Abs(v[2]*1000+7)<.01),"RELOCATED_HINGE_HAND_GEOMETRY_FAILED");
        double[] ys={-height/2,-height/2+.8,height/2-.8,height/2};
        Need(ys.All(y=>hinge.Count(v=>Math.Abs(v[1]*1000-y)<.01)==1),"RELOCATED_HINGE_HEIGHT_GEOMETRY_FAILED");
        Need(locks.All(v=>Math.Abs(v[0]*1000-(width/2-6)*sign)<.01&&Math.Abs(v[1]*1000)<.01)&&new[]{-15.0,-14.2}.All(z=>locks.Count(v=>Math.Abs(v[2]*1000-z)<.01)==1),"RELOCATED_LOCK_HAND_GEOMETRY_FAILED");
    }
    static readonly Dictionary<string,string> ResumeRewiredHashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    static void AssemblyResumeCopies(bool beforeRewire)
    {
        var resume=Map(Job["assemblyResume"]);string cad=Full(Text("cadDirectory")),source=Full(Convert.ToString(resume["sourceRun"])),oldCad=Path.Combine(source,"cad");Local(source);
        Need(Convert.ToString(resume["schema"])=="16029-variable-assembly-resume/v1"&&Full(Text("sourceCadDirectory")).Equals(oldCad,StringComparison.OrdinalIgnoreCase),"RESUME_SOURCE_BINDING_REQUIRED");
        Need(Items(resume["generateCaseIndices"]).Select(Convert.ToInt32).SequenceEqual(new[]{3})&&Items(resume["reuseCaseIndices"]).Select(Convert.ToInt32).SequenceEqual(new[]{1,2})&&Items(Job["layouts"]).Length==3,"EXACT_RESUME_CASES_REQUIRED");
        Need(Convert.ToBoolean(resume["oldFailurePreserved"])&&!Convert.ToBoolean(resume["acceptancePass"])&&!Convert.ToBoolean(resume["production_release"])&&!Convert.ToBoolean(resume["fullPlanPass"]),"RESUME_TECHNICAL_SCOPE_REQUIRED");
        string root=Path.Combine(cad,"assemblies");Need(Directory.GetDirectories(root).Select(Path.GetFileName).OrderBy(v=>v).SequenceEqual(new[]{"case1","case2"}),"RESUME_ONLY_TWO_EXISTING_CASES_REQUIRED");
        foreach(object raw in Items(resume["sourceEvidence"])) {var item=Map(raw);string path=Full(Convert.ToString(item["path"]));Local(path);Need(path.StartsWith(source+"\\",StringComparison.OrdinalIgnoreCase)&&FileHash(path)==Convert.ToString(item["sha256"]),"RESUME_ORIGINAL_EVIDENCE_CHANGED");}
        object[] cases=Items(resume["cases"]);Need(cases.Length==2,"RESUME_TWO_CASE_RECORDS_REQUIRED");var receipts=new List<object>();
        foreach(object raw in cases)
        {
            var item=Map(raw);int index=Convert.ToInt32(item["index"]);Need(index==Array.IndexOf(cases,raw)+1,"RESUME_CASE_ORDER_REQUIRED");
            Need(Json.Serialize(item["layout"])==Json.Serialize(Items(Job["layouts"])[index-1]),"RESUME_LAYOUT_BINDING_CHANGED");
            string prefix="assemblies/case"+index+"/",evidenceRelative=Convert.ToString(item["evidenceRelativePath"]);
            Need(evidenceRelative==prefix+"assembly-evidence.json","RESUME_EVIDENCE_PATH_REQUIRED");
            string evidencePath=Path.Combine(cad,evidenceRelative.Replace('/',Path.DirectorySeparatorChar));Need(FileHash(evidencePath)==Convert.ToString(item["evidenceSha256"]),"RESUME_CASE_EVIDENCE_CHANGED");
            var prior=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(evidencePath,Encoding.UTF8));
            Need(Convert.ToBoolean(prior["success"])&&Convert.ToBoolean(prior["nativeReadbackPass"])&&Convert.ToBoolean(prior["componentIdentityReadbackPass"])&&Convert.ToBoolean(prior["documentMetadataReadbackPass"])&&Convert.ToBoolean(prior["inputFilesUnchanged"])&&!Convert.ToBoolean(prior["acceptancePass"])&&!Convert.ToBoolean(prior["production_release"]),"RESUME_COMPLETE_TECHNICAL_CASE_REQUIRED");
            var declared=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(object entry in Items(item["inventory"]))
            {
                var file=Map(entry);string relative=Convert.ToString(file["relativePath"]);Need(relative.StartsWith(prefix,StringComparison.Ordinal)&&!relative.Split('/').Any(v=>v==".."||v=="."||v.Length==0)&&!relative.Contains("\\"),"RESUME_FILE_PATH_INVALID");
                string copied=Full(Path.Combine(cad,relative.Replace('/',Path.DirectorySeparatorChar))),original=Full(Path.Combine(oldCad,relative.Replace('/',Path.DirectorySeparatorChar)));Local(copied);Local(original);
                Need(copied.StartsWith(Path.Combine(root,"case"+index)+"\\",StringComparison.OrdinalIgnoreCase)&&declared.Add(copied),"RESUME_DUPLICATE_OR_ESCAPED_FILE");
                string sha=Convert.ToString(file["sha256"]);long size=Convert.ToInt64(file["size"]);Need(FileHash(original)==sha&&new FileInfo(original).Length==size,"RESUME_SOURCE_FILE_CHANGED");
                if(beforeRewire||!new[]{".SLDPRT",".SLDASM",".SLDDRW"}.Contains(Path.GetExtension(copied).ToUpperInvariant()))Need(FileHash(copied)==sha&&new FileInfo(copied).Length==size,"RESUME_COPIED_FILE_CHANGED");
                if(!beforeRewire){string rewired=FileHash(copied);ResumeRewiredHashes.Add(copied,rewired);receipts.Add(new {index=index,path=copied,sourcePath=original,sourceSha256=sha,copiedSha256=sha,rewiredSha256=rewired});}
            }
            Need(declared.SetEquals(Directory.GetFiles(Path.Combine(root,"case"+index),"*",SearchOption.AllDirectories)),"RESUME_CASE_MEMBERSHIP_CHANGED");
            if(beforeRewire)Need(FileHash(Path.Combine(root,"case"+index,"16029_variable_minimum.SLDASM"))==Convert.ToString(item["assemblySha256"]),"RESUME_ASSEMBLY_BYTES_CHANGED");
        }
        if(!beforeRewire)File.WriteAllText(Path.Combine(cad,"assembly-resume-receipt.json"),Json.Serialize(new {schema="16029-variable-assembly-resume-receipt/v1",sourceRun=source,mixedProvenance=true,technicalRecoveryOnly=true,files=receipts,production_release=false,acceptancePass=false}),new UTF8Encoding(false));
        Record(new {inspection=beforeRewire?"resume_copied_bytes_verified":"resume_rewired_copies_bound",sourceRun=source,reusedCaseCount=2,production_release=false});
    }
    static void AssemblyCases()
    {
        string root=Path.Combine(Text("cadDirectory"),"assemblies");Local(root);bool resume=Job.ContainsKey("assemblyResume");if(!resume){Need(!Directory.Exists(root),"ASSEMBLY_ROOT_EXISTS");Directory.CreateDirectory(root);}int index=0;
        foreach(object value in Items(Job["layouts"]))
        {
            string folder=Path.Combine(root,"case"+(++index));
            if(resume&&index<3){Record(new {inspection="minimum_assembly_reused",index=index,evidencePath=Path.Combine(folder,"assembly-evidence.json"),evidenceSha256=FileHash(Path.Combine(folder,"assembly-evidence.json")),oldFailurePreserved=true,acceptancePass=false,production_release=false});continue;}
            object result=TrialAssembly.Build(Sw,Text("cadDirectory"),folder,(Dictionary<string,object>)value);
            Record(new {inspection="minimum_assembly",index=index,result=result});AssertLoadedScope();
            var resultMap=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(result));
            Need(resultMap.ContainsKey("success")&&Convert.ToBoolean(resultMap["success"]),"MINIMUM_ASSEMBLY_GENERATION_FAILED case"+index);
        }
        if(resume)Need(ResumeRewiredHashes.All(item=>FileHash(item.Key)==item.Value),"RESUME_REUSED_CAD_CHANGED_DURING_CASE3");
    }
    static void AssemblyMeasureDiagnostic()
    {
        string root=Path.Combine(Text("cadDirectory"),"assemblies");Local(root);
        string[] folders=Directory.GetDirectories(root);Need(folders.Length==1&&Path.GetFileName(folders[0])=="case1","ONE_BUILT_CASE1_REQUIRED_FOR_MEASUREMENT_DIAGNOSTIC");
        var before=CadHashes();
        try {object result=TrialAssembly.MeasureBuiltCase(Sw,Text("cadDirectory"),folders[0]);Record(new {inspection="diagnostic_assembly_measurement",path=folders[0],result=result,stageAcceptance=false});Need(Convert.ToBoolean(Map(Json.DeserializeObject(Json.Serialize(result)))["success"]),"ASSEMBLY_DIAGNOSTIC_MEASUREMENT_INCOMPLETE");}
        finally {CloseOwnedDocuments();}
        var after=CadHashes();Need(before.Count==after.Count&&before.All(p=>after.ContainsKey(p.Key)&&after[p.Key]==p.Value),"ASSEMBLY_DIAGNOSTIC_CHANGED_INPUT_CAD");
    }
    static void CloseOwnedDocuments()
    {
        AssertLoadedScope();var titles=new List<string>();
        foreach(object raw in Items(Sw.GetDocuments())) {var model=raw as ModelDoc2;try{Need(model!=null&&OwnedDocument(model),"FOREIGN_DOCUMENT_PRESERVED");titles.Add(model.GetTitle());}finally{Release(raw);}}
        foreach(string title in titles)Sw.CloseDoc(title);
    }
    static void RelocatedValidation(bool doorsOnly=false)
    {
        var before=CadHashes();string output=OutputRoot;Need(Directory.Exists(output),"SIX_DOOR_OUTPUTS_REQUIRED");
        var manifest=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(output,"output-manifest.json"),Encoding.UTF8));
        bool dxfOnly=Convert.ToString(manifest["schema"])=="16029-variable-output-manifest/v2";
        Need(!doorsOnly||dxfOnly&&Text("outputScope")=="parts_dxf_only","DOOR_REOPEN_DXF_ONLY_REQUIRED");
        Need(Convert.ToBoolean(manifest["complete"])&&(dxfOnly||Convert.ToString(manifest["schema"])=="16029-variable-output-manifest/v1"),"COMPLETE_OUTPUT_MANIFEST_REQUIRED");
        if(dxfOnly)Need(manifest.ContainsKey("outputScope")&&Convert.ToString(manifest["outputScope"])=="parts_dxf_only"&&manifest.ContainsKey("drawingStatus")&&Convert.ToString(manifest["drawingStatus"])=="deferred_by_user_20261113"&&manifest.ContainsKey("production_release")&&manifest["production_release"] is bool&&!Convert.ToBoolean(manifest["production_release"])&&Job.ContainsKey("outputScope")&&Text("outputScope")=="parts_dxf_only","EXPLICIT_RELOCATION_DXF_ONLY_SCOPE_REQUIRED");
        else Need(!Job.ContainsKey("outputScope")||Text("outputScope")=="parts_dxf_pdf","RELOCATION_FULL_OUTPUT_SCOPE_MISMATCH");
        var cases=Items(manifest["cases"]).Select(Map).ToArray();var expectedDoors=Items(Job["doors"]).Select(Map).ToArray();Need(cases.Length==6&&expectedDoors.Length==6,"SIX_APPROVED_DOOR_CASES_REQUIRED");
        var panelCases=new Dictionary<string,Dictionary<string,object>>(StringComparer.OrdinalIgnoreCase);var identities=new HashSet<string>();
        foreach(var item in cases)
        {
            string hand=Convert.ToString(item["hand"]);double width=Number(item,"widthMm"),height=Number(item,"heightMm");
            Need(expectedDoors.Count(d=>Convert.ToString(d["hand"])==hand&&Math.Abs(Number(Map(d["variables"]),"DOOR_W")-width)<.0001&&Math.Abs(Number(Map(d["variables"]),"DOOR_H")-height)<.0001)==1,"RELOCATED_UNAPPROVED_DOOR_CASE");
            Need(identities.Add(hand+"/"+width.ToString("R",CultureInfo.InvariantCulture)+"/"+height.ToString("R",CultureInfo.InvariantCulture)),"RELOCATED_DUPLICATE_DOOR_CASE");
            string panel=Full(Path.Combine(output,Convert.ToString(item["folder"]),Convert.ToString(item["part"])));Local(panel);Need(panel.StartsWith(Full(output)+"\\",StringComparison.OrdinalIgnoreCase)&&FileHash(panel)==Convert.ToString(item["partSha256"]),"RELOCATED_PANEL_MANIFEST_HASH_FAILED");panelCases.Add(panel,item);
            if(dxfOnly)Need(item.ContainsKey("drawing")&&item["drawing"]==null&&item.ContainsKey("flatPreparation"),"RELOCATED_DXF_ONLY_DRAWING_OR_FLAT_SCOPE_CONFLICT");
            else
            {
                var proof=Map(item["drawing"]);foreach(string key in new[]{"pdfPath","path"})
                {
                    string relocated=Path.Combine(Path.GetDirectoryName(panel),Path.GetFileName(Convert.ToString(proof[key])));Need(File.Exists(relocated),"RELOCATED_OUTPUT_FILE_MISSING");
                    if(key=="pdfPath")Need(FileHash(relocated)==Convert.ToString(proof["pdfSha256"]),"RELOCATED_PDF_HASH_CHANGED");
                }
            }
            var dxf=Map(item["dxf"]);Need(FileHash(Path.Combine(Path.GetDirectoryName(panel),Path.GetFileName(Convert.ToString(dxf["path"]))))==Convert.ToString(dxf["sha256"]),"RELOCATED_DXF_HASH_CHANGED");
        }
        string[] panels=Directory.GetFiles(output,"*.SLDPRT",SearchOption.AllDirectories);Need(panels.Length==6,"SIX_DOOR_PARTS_REQUIRED");
        foreach(string path in panels)
        {
            Match match=Regex.Match(Path.GetFileName(path),@"^door_([LR])_W([0-9p]+)_H([0-9p]+)_");Need(match.Success,"DOOR_FILE_IDENTITY_UNRECOGNIZED");
            double width=Double.Parse(match.Groups[2].Value.Replace('p','.'),CultureInfo.InvariantCulture),height=Double.Parse(match.Groups[3].Value.Replace('p','.'),CultureInfo.InvariantCulture);
            ModelDoc2 model=Open(path,true);EquationMgr eq=null;
            try
            {
                Need(panelCases.ContainsKey(Full(path)),"RELOCATED_UNEXPECTED_PANEL");var declared=panelCases[Full(path)];
                Need(Convert.ToString(declared["hand"])==match.Groups[1].Value&&Math.Abs(Number(declared,"widthMm")-width)<.0001&&Math.Abs(Number(declared,"heightMm")-height)<.0001,"RELOCATED_FILENAME_IDENTITY_FAILED");
                string savedActive=ActiveConfiguration(model);
                Need(declared.ContainsKey("activeConfiguration")&&savedActive==Convert.ToString(declared["activeConfiguration"])&&savedActive=="Default","RELOCATED_SAVED_ACTIVE_CONFIGURATION_CHANGED");
                Need(Items(model.GetConfigurationNames()).Select(Convert.ToString).OrderBy(v=>v,StringComparer.Ordinal).SequenceEqual(Items(declared["configurations"]).Select(Convert.ToString).OrderBy(v=>v,StringComparer.Ordinal)),"RELOCATED_CONFIGURATIONS_CHANGED");
                eq=model.GetEquationMgr() as EquationMgr;Need(eq!=null&&eq.GetCount()==10&&!eq.LinkToFile,"RELOCATED_EQUATIONS_MISSING");CheckEquations(eq);
                object geometry=CheckPanel(model,width,height,"relocated"),properties=Properties(model);CheckHandGeometry(geometry,match.Groups[1].Value,width,height);
                Need(PropertyContent(properties)==PropertyContent(declared["properties"]),"RELOCATED_PANEL_PROPERTIES_CHANGED");
                Record(new {inspection="relocated_panel",path=path,hand=match.Groups[1].Value,widthMm=width,heightMm=height,savedActiveConfiguration=savedActive,geometry=geometry,properties=properties});
                if(doorsOnly)
                {
                    Need(declared.ContainsKey("processReadback"),"DOOR_PROCESS_READBACK_SOURCE_REQUIRED");object processReadback=PanelProcessReadback(model);
                    Record(new {inspection="relocated_process_readback",path=path,result=processReadback});
                    var processProof=Map(Json.DeserializeObject(Json.Serialize(processReadback)));Need(Convert.ToBoolean(processProof["rawReadbackComplete"])&&Convert.ToBoolean(processProof["pilotTopologyMatches"])&&Convert.ToBoolean(processProof["fixedExplicitOverridesMatch"])&&Convert.ToBoolean(processProof["candidateFixedProcessMatches"]),"RELOCATED_FIXED_PROCESS_READBACK_INCOMPLETE_OR_CONFLICT");
                    Need(Json.Serialize(processReadback)==Json.Serialize(declared["processReadback"]),"RELOCATED_EFFECTIVE_BEND_PROCESS_CHANGED");
                }
                if(dxfOnly){object flatReadback=TrialOutput.ReadReopenedFlat(model,Map(declared["flatPreparation"]));Record(new {inspection="relocated_flat",path=path,result=flatReadback});}
            }
            finally{Release(eq);Release(model);CloseOwnedDocuments();}
        }
        string[] drawings=Directory.GetFiles(output,"*.SLDDRW",SearchOption.AllDirectories);
        if(dxfOnly)
        {
            Need(drawings.Length==0&&Directory.GetFiles(output,"*.pdf",SearchOption.AllDirectories).Length==0,"DXF_ONLY_RELOCATION_MUST_NOT_CONTAIN_UNVERIFIED_DRAWINGS");
            Record(new {inspection="relocated_drawing_deferred",outputScope="parts_dxf_only",drawingStatus="deferred_by_user_20261113",fullPlanPass=false,production_release=false});
        }
        else Need(drawings.Length==6,"SIX_NATIVE_DRAWINGS_REQUIRED");
        foreach(string path in drawings)
        {
            string panel=panelCases.Keys.Single(p=>String.Equals(Path.GetDirectoryName(p),Path.GetDirectoryName(path),StringComparison.OrdinalIgnoreCase));
            var drawingProof=Map(panelCases[panel]["drawing"]);Need(Path.GetFileName(path)==Path.GetFileName(Convert.ToString(drawingProof["path"])),"RELOCATED_DRAWING_FILENAME_FAILED");
            var expectedViews=Items(drawingProof["views"]).Select(Map).ToArray();var seenViews=new HashSet<string>();int flatCount=0;
            ModelDoc2 model=Open(path,true);var views=new List<object>();
            try
            {
                model.Visible=true;Need(model.Visible,"RELOCATED_DRAWING_VISIBILITY_REQUIRED");
                object outputReadback=TrialOutput.ReadReopenedDrawing(Sw,model,drawingProof);
                Need(Convert.ToBoolean(Map(Json.DeserializeObject(Json.Serialize(outputReadback)))["pass"]),"RELOCATED_DRAWING_OUTPUT_READBACK_FAILED");
                var drawing=(DrawingDoc)model;Need(drawing.GetSheetCount()==1,"RELOCATED_SINGLE_DRAWING_SHEET_REQUIRED");SolidWorks.Interop.sldworks.View view=drawing.GetFirstView() as SolidWorks.Interop.sldworks.View;
                while(view!=null)
                {
                    SolidWorks.Interop.sldworks.View next=view.GetNextView() as SolidWorks.Interop.sldworks.View;
                    try
                    {
                        string referenced=view.GetReferencedModelName();
                        if(!String.IsNullOrEmpty(referenced))
                        {
                            Need(String.Equals(Full(referenced),panel,StringComparison.OrdinalIgnoreCase)&&File.Exists(referenced),"RELOCATED_DRAWING_WRONG_PART");
                            string viewName=view.GetName2();var prior=expectedViews.Single(v=>Convert.ToString(v["name"])==viewName);Need(seenViews.Add(viewName)&&view.IsFlatPatternView()==Convert.ToBoolean(prior["flatPattern"])&&view.ReferencedConfiguration==Convert.ToString(prior["configuration"]),"RELOCATED_DRAWING_VIEW_CONFIGURATION_FAILED");if(view.IsFlatPatternView())flatCount++;
                            var expectedDimensions=Items(prior["dimensions"]).Select(Map).ToArray();var seenDimensions=new HashSet<string>();
                            var dimensions=new List<object>();
                            foreach(object raw in Items(view.GetDisplayDimensions()))
                            {
                                DisplayDimension display=raw as DisplayDimension;Annotation annotation=null;Dimension dimension=null;
                                try{Need(display!=null,"RELOCATED_DIMENSION_MISSING");annotation=display.GetAnnotation() as Annotation;dimension=display.GetDimension2(0) as Dimension;Need(annotation!=null&&dimension!=null&&!annotation.IsDangling()&&!display.GetOverride()&&display.ShowDimensionValue&&String.IsNullOrEmpty(display.GetLinkedText()),"RELOCATED_DRAWING_DIMENSION_FAILED");string name=annotation.GetName();var expected=expectedDimensions.Single(d=>Convert.ToString(d["name"])==name);Need(seenDimensions.Add(name)&&Math.Abs(dimension.SystemValue*1000-Number(expected,"actualMm"))<.01,"RELOCATED_DRAWING_DIMENSION_VALUE_CHANGED");dimensions.Add(new {name=dimension.FullName,annotationName=name,valueM=dimension.SystemValue,dangling=false,overridden=false});}
                                finally{Release(dimension);Release(annotation);Release(display);}
                            }
                            Need(dimensions.Count>=2&&dimensions.Count==expectedDimensions.Length,"RELOCATED_DRAWING_DIMENSIONS_INCOMPLETE");views.Add(new {name=view.GetName2(),reference=referenced,configuration=view.ReferencedConfiguration,flatPattern=view.IsFlatPatternView(),dimensions=dimensions});
                        }
                    }finally{Release(view);}view=next;
                }
                Need(views.Count==2&&flatCount==1,"RELOCATED_FOLDED_AND_FLAT_VIEWS_REQUIRED");Record(new {inspection="relocated_drawing",path=path,views=views,outputReadback=outputReadback,pass=true});
            }finally{Release(model);CloseOwnedDocuments();}
        }
        if(doorsOnly)
        {
            Need(!Directory.Exists(Path.Combine(Text("cadDirectory"),"assemblies")),"DOOR_ONLY_REOPEN_ASSEMBLY_SCOPE_CONFLICT");
            var doorAfter=CadHashes();Need(before.Count==doorAfter.Count&&before.All(p=>doorAfter.ContainsKey(p.Key)&&doorAfter[p.Key]==p.Value),"RELOCATED_READONLY_CHANGED_CAD");
            Record(new {inspection="relocated_hash_comparison",scope="doors_only",pass=true,files=before});return;
        }
        string assemblyRoot=Path.Combine(Text("cadDirectory"),"assemblies");Need(Directory.Exists(assemblyRoot),"THREE_MINIMUM_ASSEMBLIES_REQUIRED");
        string[] assemblyCases=Directory.GetDirectories(assemblyRoot);Need(assemblyCases.Length==3,"THREE_MINIMUM_ASSEMBLY_CASES_REQUIRED");var expectedLayouts=Items(Job["layouts"]).Select(Json.Serialize).ToList();
        foreach(string folder in assemblyCases)
        {
            var prior=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"assembly-evidence.json"),Encoding.UTF8));string layout=Json.Serialize(prior["layout"]);Need(expectedLayouts.Remove(layout),"RELOCATED_UNAPPROVED_OR_DUPLICATE_ASSEMBLY_LAYOUT");
            object result=TrialAssembly.ReopenExisting(Sw,Text("cadDirectory"),folder);Record(new {inspection="relocated_assembly",path=folder,result=result});
            var map=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(result));Need(Convert.ToBoolean(map["success"]),"RELOCATED_ASSEMBLY_FAILED");
        }
        Need(expectedLayouts.Count==0,"RELOCATED_ASSEMBLY_LAYOUT_MISSING");
        var after=CadHashes();Need(before.Count==after.Count&&before.All(p=>after.ContainsKey(p.Key)&&after[p.Key]==p.Value),"RELOCATED_READONLY_CHANGED_CAD");Record(new {inspection="relocated_hash_comparison",pass=true,files=before});
    }
    static void Variables()
    {
        foreach(object value in Items(Job["targets"]))
        {
            string name=Convert.ToString(value),path=Path.Combine(Text("cadDirectory"),name);ModelDoc2 model=Open(path,false);EquationMgr eq=null;
            try
            {
                Need(!model.IsOpenedReadOnly(),"WRITABLE_COPY_REQUIRED");DefaultConfiguration(model);
                Record(new {inspection="panel_before_binding",path=path,geometry=Geometry(model)});
                eq=model.GetEquationMgr() as EquationMgr;Need(eq!=null&&eq.GetCount()==0,"CLEAN_EQUATION_SET_REQUIRED");
                foreach(string dimension in new[]{"D1@草图1","D2@草图1","D1@抽壳1","D1@凸台-拉伸2","厚度@钣金1","D1@钣金1"}){Dimension dim=DimensionFor(model,dimension);try{Record(new {inspection="dimension_binding_candidate",name=dimension,drivenState=dim.DrivenState,valueMm=dim.SystemValue*1000});}finally{Release(dim);}}
                AddEquation(eq,"\"DOOR_W\" = 437mm");AddEquation(eq,"\"DOOR_H\" = 471.25mm");AddEquation(eq,"\"T\" = 0.8mm");AddEquation(eq,"\"BEND_R\" = 0.2mm");AddEquation(eq,"\"BEND_DED\" = 1.4mm");
                AddEquation(eq,"\"D2@草图1\" = \"DOOR_W\"");AddEquation(eq,"\"D1@草图1\" = \"DOOR_H\"");
                AddEquation(eq,"\"D1@抽壳1\" = \"T\"");AddEquation(eq,"\"D1@凸台-拉伸2\" = \"T\"");
                AddEquation(eq,"\"D1@钣金1\" = \"BEND_R\"");
                eq.EvaluateAll();CheckEquations(eq);Need(model.ForceRebuild3(false),"BIND_REBUILD_FAILED");
                Record(new {inspection="panel_after_binding",path=path,geometry=CheckPanel(model,437,471.25,"bound-baseline")});
                SaveModel(model,"bound-baseline");
                if(Text("action")=="variables-matrix")
                {
                    double[][] sizes={new[]{437.0,471.25},new[]{287.0,200.0},new[]{437.0,471.25},new[]{537.0,1000.0},new[]{437.0,471.25}};
                    for(int i=0;i<sizes.Length;i++)
                    {
                        DefaultConfiguration(model);SetVariable(eq,"DOOR_W",sizes[i][0]);SetVariable(eq,"DOOR_H",sizes[i][1]);eq.EvaluateAll();CheckEquations(eq);
                        bool rebuilt=model.ForceRebuild3(false);Record(new {inspection="size_rebuild",path=path,index=i,rebuilt=rebuilt});Need(rebuilt,"SIZE_REBUILD_FAILED");
                        Record(new {inspection="size_geometry",path=path,index=i,geometry=CheckPanel(model,sizes[i][0],sizes[i][1],"matrix-"+i)});SaveModel(model,"matrix-"+i);
                    }
                }
            }
            finally{Release(eq);Sw.CloseDoc(model.GetTitle());Release(model);}
        }
    }
    static double DoorInputNumber(Dictionary<string,object> input,string key)
    {
        Need(input.ContainsKey(key)&&(input[key] is int||input[key] is long||input[key] is decimal||input[key] is double),"DOOR_NUMERIC_INPUT_REQUIRED "+key);
        double value=Number(input,key);Need(!Double.IsNaN(value)&&!Double.IsInfinity(value),"DOOR_FINITE_INPUT_REQUIRED "+key);return value;
    }
    static void ValidateDoorJob()
    {
        if(!new[]{"outputs","outputs-dxf","doors-reopen"}.Contains(Text("action")))return;
        Need(Job.ContainsKey("production_release")&&Job["production_release"] is bool&&!Convert.ToBoolean(Job["production_release"]),"DOOR_PRODUCTION_RELEASE_FORBIDDEN");
        var process=Map(Job["process"]);string version=Convert.ToString(process["version"]);
        Need(!String.IsNullOrWhiteSpace(version)&&version.Trim()==version&&Convert.ToString(process["status"])=="draft_pending_factory_confirmation","DOOR_FIXED_PROCESS_VERSION_REQUIRED");
        Need(DoorInputNumber(process,"thicknessMm")==.8&&DoorInputNumber(process,"innerRadiusMm")==.2&&DoorInputNumber(process,"bendDeductionMm")==1.4,"DOOR_FIXED_PROCESS_INPUT_REQUIRED");
        var declared=Items(Map(Job["specification"])["doorCases"]).Select(Map).ToArray();var doors=Items(Job["doors"]).Select(Map).ToArray();
        Need(doors.Length==6&&declared.Length==6,"SIX_DOOR_INPUT_CASES_REQUIRED");var identities=new HashSet<string>();
        for(int i=0;i<doors.Length;i++)
        {
            var door=doors[i];var variables=Map(door["variables"]);var properties=Map(door["properties"]);var source=declared[i];
            string hand=Convert.ToString(door["hand"]);double width=DoorInputNumber(variables,"DOOR_W"),height=DoorInputNumber(variables,"DOOR_H");
            Need((hand=="L"||hand=="R")&&width>=287&&width<=537&&height>=200&&height<=1000,"DOOR_INPUT_OUTSIDE_PILOT");
            Need(DoorInputNumber(variables,"T")==.8&&DoorInputNumber(variables,"BEND_R")==.2&&DoorInputNumber(variables,"BEND_DED")==1.4,"DOOR_VARIABLE_PROCESS_CONFLICT");
            Need(DoorInputNumber(source,"widthMm")==width&&DoorInputNumber(source,"heightMm")==height&&Convert.ToString(source["hand"])==hand,"DOOR_SPECIFICATION_CONFLICT");
            string identity="16029-door_W"+width.ToString("R",CultureInfo.InvariantCulture)+"_H"+height.ToString("R",CultureInfo.InvariantCulture)+"_hand"+hand+"_t0.8_process"+Uri.EscapeDataString(version)+"_leaf";
            Need(Convert.ToString(door["identity"])==identity&&identities.Add(identity),"DOOR_IDENTITY_CONFLICT");
            Need(!source.ContainsKey("identity")||Convert.ToString(source["identity"])==identity,"DOOR_DECLARED_IDENTITY_CONFLICT");
            Need(!source.ContainsKey("properties")||!Map(source["properties"]).ContainsKey("hand")||Convert.ToString(Map(source["properties"])["hand"])==hand,"DOOR_DECLARED_HAND_CONFLICT");
            Need(Convert.ToString(properties["hand"])==hand&&DoorInputNumber(properties,"widthMm")==width&&DoorInputNumber(properties,"heightMm")==height&&DoorInputNumber(properties,"thicknessMm")==.8&&DoorInputNumber(properties,"innerRadiusMm")==.2&&DoorInputNumber(properties,"bendDeductionMm")==1.4&&Convert.ToString(properties["processVersion"])==version&&Convert.ToString(properties["processStatus"])=="draft_pending_factory_confirmation","DOOR_COMPILED_PROPERTIES_CONFLICT");
            var doorProcess=Map(door["process"]);Need(doorProcess.Count==process.Count&&process.All(pair=>doorProcess.ContainsKey(pair.Key)&&Json.Serialize(doorProcess[pair.Key])==Json.Serialize(pair.Value)),"DOOR_COMPILED_PROCESS_CONFLICT");
        }
        var inputs=Items(Job["inputVersions"]).Select(Map).ToArray();Need(inputs.Length==2,"DOOR_INPUT_VERSION_RECORDS_REQUIRED");
        for(int i=0;i<inputs.Length;i++)
        {
            string path=Full(Convert.ToString(inputs[i]["path"]));Need(FileHash(path)==Convert.ToString(inputs[i]["sha256"]),"DOOR_INPUT_FILE_HASH_CHANGED");
            object actual=Json.DeserializeObject(File.ReadAllText(path,Encoding.UTF8));Need(Json.Serialize(actual)==Json.Serialize(Job[i==0?"specification":"process"]),"DOOR_JOB_INPUT_CONTENT_CONFLICT");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        bool success=false;string failure=null;
        try
        {
            Need(args.Length==1,"JOB_ARGUMENT_REQUIRED");Local(args[0]);Job=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[0],Encoding.UTF8));ResultPath=Text("resultPath");Local(ResultPath);
            ValidateDoorJob();RuntimeOwnership();
            Need(Process.GetProcessesByName("SLDWORKS").Length==0,"EXISTING_CAD_FORBIDDEN");
            DateTime activationStarted=DateTime.UtcNow;
            Sw=(ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
            CadPid=Sw.GetProcessID();string startedAt;using(Process p=Process.GetProcessById(CadPid)){CadTicks=p.StartTime.ToUniversalTime().Ticks;startedAt=p.StartTime.ToUniversalTime().ToString("o");CadExe=CanonicalExecutable(p.MainModule.FileName);Need(CadTicks>=activationStarted.Ticks,"CAD_PREEXISTS_ACTIVATION_WINDOW");Need(CadExe.Equals(@"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe",StringComparison.OrdinalIgnoreCase)&&p.MainModule.FileVersionInfo.FileMajorPart==28,"SW2020_PATH_REQUIRED");OwnedSession=true;}
            Sw.Visible=false;Sw.UserControl=false;Record(new {inspection="cad_owned",pid=CadPid,startTicks=CadTicks,startedAtUtc=startedAt,executable=CadExe,revision=Sw.RevisionNumber()});
            if(Text("action")=="rewire-inspect") {Rewire(Text("cadDirectory"));Inspect();}
            else if(Text("action")=="inspect")Inspect();
            else if(Text("action")=="reopen-validate") {Rewire(Text("cadDirectory"));ReopenValidation();}
            else if(Text("action")=="outputs") {Rewire(Text("cadDirectory"));OutputCases();}
            else if(Text("action")=="outputs-dxf") {Rewire(Text("cadDirectory"));OutputCases(false);}
            else if(Text("action")=="assembly") {if(Job.ContainsKey("assemblyResume"))AssemblyResumeCopies(true);Rewire(Text("cadDirectory"));if(Job.ContainsKey("assemblyResume"))AssemblyResumeCopies(false);AssemblyCases();}
            else if(Text("action")=="assembly-measure-diagnostic") {Rewire(Text("cadDirectory"));AssemblyMeasureDiagnostic();}
            else if(Text("action")=="structure-refresh-diagnostic") {Rewire(Text("cadDirectory"));object probe=TrialAssembly.ProbeBottomFrame(Sw,Text("cadDirectory"),Convert.ToBoolean(Job["sourceFirst"]));Record(new {inspection="structure_refresh_probe",result=probe,stageAcceptance=false});Need(Convert.ToBoolean(Map(Json.DeserializeObject(Json.Serialize(probe)))["success"]),"STRUCTURE_REFRESH_PROBE_FAILED");}
            else if(Text("action")=="relocate") {Rewire(Text("cadDirectory"));RelocatedValidation();}
            else if(Text("action")=="doors-reopen") {var before=CadHashes();ReadOnlyDoorDependencies();RelocatedValidation(true);var after=CadHashes();Need(before.Count==after.Count&&before.All(p=>after.ContainsKey(p.Key)&&after[p.Key]==p.Value),"DOOR_REOPEN_FULL_ACTION_CHANGED_CAD");Record(new {inspection="door_reopen_full_action_hashes",pass=true,files=before});}
            else if(Text("action")=="drawing-diagnostic") {Rewire(Text("cadDirectory"));DrawingDiagnostic();}
            else if(Text("action")=="pdf-diagnostic") {Rewire(Text("cadDirectory"));PdfDiagnostic();}
            else if(Text("action")=="pdf-cjk-diagnostic") {Rewire(Text("cadDirectory"));PdfDiagnostic(true);}
            else if(Text("action")=="pdf-note-diagnostic") {Rewire(Text("cadDirectory"));PdfDiagnostic(false,true);}
            else if(Text("action")=="pdf-local-font-diagnostic") {Rewire(Text("cadDirectory"));PdfDiagnostic(false,false,true);}
            else if(Text("action")=="variables"||Text("action")=="variables-matrix") {Rewire(Text("cadDirectory"));Variables();}
            else throw new InvalidOperationException("UNKNOWN_ACTION");
            success=true;
        }
        catch(Exception e){failure=e.ToString();Console.Error.WriteLine(failure);if(Job!=null)Record(new {inspection="failure",error=failure});}
        finally
        {
            if(Sw!=null)
            {
                try
                {
                    Need(OwnedSession&&Sw.GetProcessID()==CadPid,"OWNERSHIP_LOST");
                    using(Process process=Process.GetProcessById(CadPid))Need(process.StartTime.ToUniversalTime().Ticks==CadTicks&&CanonicalExecutable(process.MainModule.FileName).Equals(CadExe,StringComparison.OrdinalIgnoreCase),"CAD_IDENTITY_LOST");
                    var ownedTitles=new List<string>();var foreignPaths=new List<string>();
                    foreach(object value in Items(Sw.GetDocuments())) {ModelDoc2 doc=value as ModelDoc2;try{if(doc!=null&&OwnedDocument(doc))ownedTitles.Add(doc.GetTitle());else foreignPaths.Add(doc==null?"NON_MODEL":doc.GetPathName());}finally{Release(value);}}
                    foreach(string title in ownedTitles)Sw.CloseDoc(title);
                    Record(new {inspection="owned_document_cleanup",closed=ownedTitles,foreignDocuments=foreignPaths});
                    Need(foreignPaths.Count==0,"EXTERNAL_DOCUMENT_SESSION_PRESERVED");
                    Need(Items(Sw.GetDocuments()).Length==0,"DOCUMENTS_REMAIN_SESSION_PRESERVED");Sw.ExitApp();
                }catch(Exception e){success=false;failure=(failure==null?"":failure+"\n")+e.ToString();}
                Release(Sw);Sw=null;GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            }
            bool exited=CadPid==0;
            if(CadPid!=0)try {using(Process p=Process.GetProcessById(CadPid)){Need(p.StartTime.ToUniversalTime().Ticks==CadTicks,"PID_REUSED");exited=p.WaitForExit(120000);}}catch(ArgumentException){exited=true;}
            if(!exited)
            {
                bool cadOwnsLease=false;
                if(Job!=null&&Job.ContainsKey("sharedLockDirectory"))try
                {
                    var owner=SharedOwner(Text("sharedLockDirectory"));
                    cadOwnsLease=Convert.ToInt32(owner["pid"])==CadPid&&Convert.ToString(owner["token"])==Text("sharedToken");
                }catch{}
                if(!cadOwnsLease)
                {
                    Record(new {inspection="native_guard_retained",reason="CAD alive and lock PID handoff not proven",cadPid=CadPid});
                    for(;;)
                    {
                        try {using(Process p=Process.GetProcessById(CadPid)){if(p.StartTime.ToUniversalTime().Ticks!=CadTicks||p.WaitForExit(1000)){exited=true;break;}}}
                        catch(ArgumentException){exited=true;break;}
                    }
                }
            }
            if(Job!=null) Record(new {inspection="finished",success=success&&exited,cadExited=exited,error=failure});
            success=success&&exited;
        }
        return success?0:1;
    }
}
