using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static void RestartSavedReloadCad(Dictionary<string,object> row) {
        var previous=Process.GetProcessById(Sw.GetProcessID());
        Need(previous.Id==Convert.ToInt32(E["process_id"]),"Saved reload process ownership changed");
        string exitName=Path.GetFileNameWithoutExtension(EvidencePath)+"_saved_reload_exit.json";
        string exitPath=Path.Combine(Path.GetDirectoryName(EvidencePath),exitName);
        Need(!File.Exists(exitPath),"Preserve saved reload exit evidence");
        var receipt=new Dictionary<string,object>{{"process_id",previous.Id},{"process_start_utc",previous.StartTime.ToUniversalTime().ToString("o")},{"exit_app_called",false}};
        row["generation_process_id"]=previous.Id;row["saved_reload_exit_receipt"]=exitPath;
        Sw.CommandInProgress=false;Sw.ExitApp();receipt["exit_app_called"]=true;
        File.WriteAllText(exitPath,Json.Serialize(receipt),new UTF8Encoding(false));
        var oldApplication=Sw;Sw=null;
        row["owned_application_reference_release_count"]=System.Runtime.InteropServices.Marshal.ReleaseComObject(oldApplication);oldApplication=null;
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        row["old_application_reference_released_before_wait"]=true;Flush("saved_reload_old_application_reference_released");
        var cleanup=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(Stage,"scripts","cleanup_owned_residual_poll_r4.ps1")+"\" -EvidenceName \"evidence/"+exitName+"\""){UseShellExecute=false,CreateNoWindow=true};
        using(var child=Process.Start(cleanup)){child.WaitForExit();Need(child.ExitCode==0,"Owned saved reload session cleanup failed");}
        Need(Process.GetProcessesByName("SLDWORKS").Length==0,"Saved reload requires no existing CAD process");
        DateTime activation=DateTime.UtcNow;
        var next=Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.28",true)) as ISldWorks;
        var process=Process.GetProcessById(next.GetProcessID());var path=new StringBuilder(4096);
        Need(GetLongPathName(process.MainModule.FileName,path,(uint)path.Capacity)>0&&path.ToString().Equals(@"D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe",StringComparison.OrdinalIgnoreCase)&&process.StartTime.ToUniversalTime()>=activation.AddSeconds(-2)&&process.Id!=previous.Id&&Process.GetProcessesByName("SLDWORKS").Length==1&&next.GetFirstDocument()==null,"Fresh saved reload CAD identity required");
        Sw=next;E["process_id"]=process.Id;E["process_start_utc"]=process.StartTime.ToUniversalTime().ToString("o");
        Sw.Visible=false;Sw.DocumentVisible(false,1);Sw.DocumentVisible(false,2);Sw.CommandInProgress=true;
        row["saved_reload_fresh_process_id"]=process.Id;row["saved_reload_zero_loaded_documents"]=true;Flush("saved_reload_fresh_native_session");
    }
}
