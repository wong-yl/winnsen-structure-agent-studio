using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static object InstanceBodyCounts(ModelDoc2 top){return ((Array)((AssemblyDoc)top).GetComponents(false)).Cast<Component2>().Where(c=>c.GetPathName().EndsWith(".SLDPRT",StringComparison.OrdinalIgnoreCase)).Select(c=>{object info;var bodies=c.IsSuppressed()?null:c.GetBodies3(0,out info) as Array;return new{name=c.Name2,document=Path.GetFileName(c.GetPathName()),suppressed=c.IsSuppressed(),hidden=c.IsHidden(true),bodies=bodies==null?0:bodies.Length};}).ToArray();}
}
