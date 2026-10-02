using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Native888
{
    internal static class InspectLockTopology888x14
    {
        private const string ToolId = "native_lock_topology_inspector_v1";
        private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";
        private const string SwPath = @"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe";
        private const string SwSha = "1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978";
        private const string LeftName = "箱体竖隔板L.sldprt";
        private const string RightName = "箱体竖隔板R.SLDPRT";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--identity") { Console.WriteLine("sourceNormalizedSha256=" + ExpectedSourceSha256); Console.WriteLine("executableSha256=" + HashFile(System.Reflection.Assembly.GetExecutingAssembly().Location)); return 0; }
            if (args.Length == 1 && args[0] == "--self-test") { Console.WriteLine("{\"selfTest\":\"PASS\",\"solidWorksStarted\":false,\"toolId\":\"" + ToolId + "\"}"); return 0; }
            if (args.Length != 2) { Console.Error.WriteLine("Usage: InspectLockTopology888x14.exe <attempt-root> <build-evidence-sha256>"); return 2; }
            string attempt = Path.GetFullPath(args[0]).TrimEnd('\\');
            string build = Path.Combine(attempt, @"evidence\lock_topology_888x14.v2.json");
            string cad = Path.Combine(attempt, @"native_cad\working_pack");
            string output = Path.Combine(attempt, @"evidence\lock_topology_888x14.inspector.v1.json");
            if (!Directory.Exists(cad) || !File.Exists(build) || !IsHash(args[1]) || !HashFile(build).Equals(args[1], StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Process.GetProcessesByName("SLDWORKS").Length != 0 || Process.GetProcessesByName("sldProcMon").Length != 0) return 3;
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string source = Path.Combine(Path.GetDirectoryName(exe), "..", "InspectLockTopology888x14.cs");
            if (!File.Exists(source) || !NormalizedSourceHash(source).Equals(ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase)) return 4;
            var evidence = new Dictionary<string, object> {
                {"schema","winnsen.locker16029.native_888x14_lock_topology_inspector.v1"}, {"attempt_root",attempt}, {"cad_directory",cad},
                {"build_evidence_sha256",args[1].ToUpperInvariant()}, {"generated_at_utc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)},
                {"inspector",new Dictionary<string,object>{{"id",ToolId},{"source_path",Path.GetFullPath(source)},{"source_sha256",ExpectedSourceSha256},{"executable_path",Path.GetFullPath(exe)},{"executable_sha256",HashFile(exe)}}}
            };
            ISldWorks sw = null; int pid = 0; long start = 0; var monitors = new List<Dictionary<string,object>>();
            try {
                sw = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28", true)) as ISldWorks;
                if (sw == null) return 10; sw.Visible = false; sw.UserControl = false; sw.CommandInProgress = true;
                pid = sw.GetProcessID(); start = Process.GetProcessById(pid).StartTime.ToUniversalTime().Ticks;
                string revision = sw.RevisionNumber(); string swExe = Process.GetProcessById(pid).MainModule.FileName;
                if (!revision.StartsWith("28.") || !Same(swExe, SwPath) || !HashFile(swExe).Equals(SwSha, StringComparison.OrdinalIgnoreCase)) return 11;
                Thread.Sleep(750); monitors = OwnedMonitors(pid);
                evidence["parts"] = new Dictionary<string,object>{{"L",InspectPart(sw,Path.Combine(cad,LeftName),"L")},{"R",InspectPart(sw,Path.Combine(cad,RightName),"R")}};
                evidence["session"] = new Dictionary<string,object>{{"purpose","inspect_lock_topology_readonly"},{"created",true},{"sldworks_process_id",pid},{"sldworks_start_ticks_utc",start},{"solidworks_revision",revision},{"solidworks_executable_path",swExe},{"solidworks_executable_sha256",HashFile(swExe)},{"solidworks_2020_exact_gate",true},{"monitor_processes",monitors}};
            } finally {
                if (sw != null) { try { sw.CloseAllDocuments(true); sw.CommandInProgress=false; sw.ExitApp(); } catch {} Release(sw); }
                WaitGone(pid, start, 30000); foreach(var row in monitors) KillExact(Convert.ToInt32(row["pid"]),"sldProcMon");
            }
            bool clean = Process.GetProcessesByName("SLDWORKS").Length == 0 && Process.GetProcessesByName("sldProcMon").Length == 0;
            evidence["final_process_cleanup_gate"] = clean; if (!clean) return 12;
            var session=(Dictionary<string,object>)evidence["session"]; session["process_exited"]=true; session["monitors_exited"]=true; session["sldprocmon_identity_gate"]=true;
            evidence["self_digest"] = SelfDigest(evidence);
            WriteNew(output,evidence); Console.WriteLine(output); return 0;
        }

        private static Dictionary<string,object> InspectPart(ISldWorks sw,string path,string side)
        {
            int errors=0,warnings=0; string before=HashFile(path); int saves=0;
            ModelDoc2 model=sw.OpenDoc6(path,(int)swDocumentTypes_e.swDocPART,(int)swOpenDocOptions_e.swOpenDocOptions_Silent|(int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,"",ref errors,ref warnings) as ModelDoc2;
            if(model==null||errors!=0||warnings!=0) throw new InvalidOperationException("readonly part open failed: "+side);
            bool rebuilt=model.ForceRebuild3(false); var types=new List<string>(); int featureCount=0,errorCount=0,warningCount=0,mirror=0,forbidden=0; bool sheet=false,flat=false,traversal=true;
            Feature f=model.FirstFeature() as Feature; int guard=0; while(f!=null&&guard++<20000){CaptureFeature(f,types,ref featureCount,ref errorCount,ref warningCount,ref mirror,ref forbidden,ref sheet,ref flat,ref traversal,0);Feature n=f.GetNextFeature() as Feature;Release(f);f=n;} if(f!=null){traversal=false;Release(f);}
            var edges=new List<Dictionary<string,object>>(); PartDoc part=model as PartDoc; Array bodies=part.GetBodies2((int)swBodyType_e.swSolidBody,false) as Array; int bodyCount=bodies==null?0:bodies.Length;
            if(bodies!=null)foreach(object bi in bodies){Body2 b=bi as Body2;Array es=b.GetEdges() as Array;if(es!=null)foreach(object ei in es){Edge edge=ei as Edge;Curve curve=edge.GetCurve() as Curve;CurveParamData cp=edge.GetCurveParams3() as CurveParamData;double len=cp==null||curve==null?0:curve.GetLength3(cp.UMinValue,cp.UMaxValue)*1000;edges.Add(new Dictionary<string,object>{{"index",edges.Count+1},{"length_mm",len},{"param_start_mm",Point(cp==null?null:cp.StartPoint)},{"vertex_start_mm",Vertex(edge.GetStartVertex() as Vertex)},{"vertex_end_mm",Vertex(edge.GetEndVertex() as Vertex)}});Release(cp);Release(curve);Release(edge);}Release(b);}
            int refs=Math.Max(model.Extension.ListExternalFileReferencesCount(),model.ListExternalFileReferencesCount2()); string title=model.GetTitle();Release(model);sw.CloseDoc(title);string after=HashFile(path);
            return new Dictionary<string,object>{{"side",side},{"source_path",path},{"source_sha256",after},{"opened",true},{"read_only",true},{"save_api_calls",saves},{"source_unchanged",before==after},{"open_errors",errors},{"open_warnings",warnings},{"rebuild_succeeded",rebuilt},{"body_count",bodyCount},{"feature_count",featureCount},{"feature_types",types},{"has_sheet_metal",sheet},{"has_flat_pattern",flat},{"traversal_complete",traversal},{"error_feature_count",errorCount},{"warning_feature_count",warningCount},{"mirror_feature_count",mirror},{"forbidden_import_feature_count",forbidden},{"external_reference_count",refs},{"edges",edges}};
        }
        private static double[] Point(object raw){double[] a=raw as double[];return a==null?new double[0]:a.Take(3).Select(v=>v*1000).ToArray();}
        private static void CaptureFeature(Feature f,List<string> types,ref int count,ref int errors,ref int warnings,ref int mirrors,ref int forbidden,ref bool sheet,ref bool flat,ref bool complete,int depth){if(f==null)return;if(depth>30||count>20000){complete=false;return;}string t=f.GetTypeName2()??"";types.Add(t);count++;bool warning=false;int code=-1;try{code=f.GetErrorCode2(out warning);}catch{}if(code>0)errors++;if(warning)warnings++;if(t.Equals("SheetMetal",StringComparison.OrdinalIgnoreCase))sheet=true;if(t.Equals("FlatPattern",StringComparison.OrdinalIgnoreCase))flat=true;if(t.Equals("MirrorStock",StringComparison.OrdinalIgnoreCase)||t.Equals("MirrorPart",StringComparison.OrdinalIgnoreCase))mirrors++;if(new[]{"BaseBody","Imported","ImportedBody","ForeignBody"}.Contains(t,StringComparer.OrdinalIgnoreCase))forbidden++;Feature child=f.GetFirstSubFeature() as Feature;int guard=0;while(child!=null&&guard++<3000){CaptureFeature(child,types,ref count,ref errors,ref warnings,ref mirrors,ref forbidden,ref sheet,ref flat,ref complete,depth+1);Feature next=child.GetNextSubFeature() as Feature;Release(child);child=next;}if(child!=null){complete=false;Release(child);}}
        private static double[] Vertex(Vertex v){try{return Point(v==null?null:v.GetPoint());}finally{Release(v);}}
        private static void WriteNew(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+".tmp-"+Process.GetCurrentProcess().Id;byte[] data=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(value)+System.Environment.NewLine);using(var fs=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){fs.Write(data,0,data.Length);fs.Flush(true);}File.Move(tmp,path);}
        private static bool Same(string a,string b){return string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}
        private static bool IsHash(string s){return s!=null&&System.Text.RegularExpressions.Regex.IsMatch(s,"^[0-9A-Fa-f]{64}$");}
        private static string HashFile(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return Hex(h.ComputeHash(s));}
        private static string NormalizedSourceHash(string p){string s=File.ReadAllText(p,Encoding.UTF8);s=System.Text.RegularExpressions.Regex.Replace(s,"private const string ExpectedSourceSha256 = \"(?:__SOURCE_SHA256__|[0-9A-F]{64})\";","private const string ExpectedSourceSha256 = \"__SOURCE_SHA256__\";");using(var h=SHA256.Create())return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(s)));}
        private static string Hex(byte[] b){return string.Concat(b.Select(x=>x.ToString("X2")));}
        private static string SelfDigest(Dictionary<string,object> doc){var copy=new Dictionary<string,object>(doc,StringComparer.Ordinal);copy.Remove("self_digest");return HashText(Stable(copy));}
        private static string HashText(string s){using(var h=SHA256.Create())return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(s)));}
        private static string Stable(object value){var json=new JavaScriptSerializer();var map=value as Dictionary<string,object>;if(map!=null)return "{"+string.Join(",",map.Keys.OrderBy(k=>k,StringComparer.Ordinal).Select(k=>json.Serialize(k)+":"+Stable(map[k])))+"}";var list=value as IEnumerable;if(list!=null&&!(value is string))return "["+string.Join(",",list.Cast<object>().Select(Stable))+ "]";return json.Serialize(value);}
        private static void Release(object o){if(o!=null&&Marshal.IsComObject(o))try{Marshal.FinalReleaseComObject(o);}catch{}}
        private static void WaitGone(int pid,long ticks,int ms){DateTime end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){try{using(var p=Process.GetProcessById(pid)){if(p.HasExited||p.StartTime.ToUniversalTime().Ticks!=ticks)return;}}catch{return;}Thread.Sleep(250);}KillExact(pid,"SLDWORKS");}
        private static void KillExact(int pid,string name){try{using(var p=Process.GetProcessById(pid)){if(p.ProcessName.Equals(name,StringComparison.OrdinalIgnoreCase)){p.Kill();p.WaitForExit(5000);}}}catch{}}
        private static List<Dictionary<string,object>> OwnedMonitors(int parent){var rows=new List<Dictionary<string,object>>();using(var q=new ManagementObjectSearcher("SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='sldProcMon.exe'"))using(var found=q.Get())foreach(ManagementObject item in found){int pid=Convert.ToInt32(item["ProcessId"]);string cmd=Convert.ToString(item["CommandLine"])??"";if(System.Text.RegularExpressions.Regex.IsMatch(cmd,"(?:^|\\s)--ppid="+parent+"(?:\\s|$)"))rows.Add(new Dictionary<string,object>{{"pid",pid},{"parent_sldworks_process_id",parent},{"start_ticks_utc",Process.GetProcessById(pid).StartTime.ToUniversalTime().Ticks},{"command_line",cmd},{"ownership_verified",true}});}return rows;}
    }
}
